using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Combat
{
    [RequireComponent(typeof(CombatEntity), typeof(PlayerMotor2D), typeof(Rigidbody2D))]
    public sealed class PlayerAttackController : MonoBehaviour
    {
        [SerializeField] private AttackDefinition[] combo;
        [SerializeField] private float baseAttack = 10f;
        [SerializeField] private float comboResetSeconds = 0.55f;

        [Header("Action feel")]
        [SerializeField, Min(0f)] private float movementUnlockAfterImpactSeconds = 0.04f;

        [Header("Post-dash slash")]
        [SerializeField, Min(0f)] private float dashSlashWindowSeconds = 0.35f;
        [SerializeField, Min(0f)] private float dashSlashDamageMultiplier = 1.45f;
        [SerializeField] private Vector2 dashSlashHitboxOffset = new(1.30f, 0f);
        [SerializeField] private Vector2 dashSlashHitboxSize = new(2.25f, 1.20f);
        [SerializeField] private Vector2 dashSlashKnockback = new(4.2f, 0.75f);

        [Header("Air slash")]
        [SerializeField, Min(0f)] private float airSlashDamageMultiplier = 1.15f;
        [SerializeField] private Vector2 airSlashHitboxOffset = new(0.95f, 0.02f);
        [SerializeField] private Vector2 airSlashHitboxSize = new(1.75f, 1.35f);
        [SerializeField] private Vector2 airSlashKnockback = new(2.8f, 0.55f);
        [SerializeField] private float airSlashHangVelocity = 0.85f;
        [SerializeField, Range(0.15f, 1f)] private float airSlashCycleScale = 0.86f;

        [Header("Plunge")]
        [SerializeField, Range(-1f, -0.1f)] private float plungeInputThreshold = -0.45f;
        [SerializeField, Min(1f)] private float plungeSpeed = 14f;
        [SerializeField, Min(0f)] private float plungeDamageMultiplier = 1.80f;
        [SerializeField] private Vector2 plungeHitboxOffset = new(0f, -0.42f);
        [SerializeField] private Vector2 plungeHitboxSize = new(2.20f, 1.05f);
        [SerializeField] private Vector2 plungeKnockback = new(2.8f, 1.75f);
        [SerializeField, Min(0.02f)] private float plungeWindupSeconds = 0.07f;
        [SerializeField, Min(0.02f)] private float plungeLandingRecoverySeconds = 0.12f;
        [SerializeField, Min(0.2f)] private float plungeTimeoutSeconds = 1.4f;

        private CombatEntity _entity;
        private PlayerMotor2D _motor;
        private PlayerDashController _dash;
        private Rigidbody2D _body;
        private IPlayerInputSource _input;
        private IAttackTimingProvider _timingProvider;
        private AttackSlashPresentation2D _presentation;
        private Coroutine _attackRoutine;
        private int _comboIndex;
        private float _lastAttackFinishedAt = -999f;
        private float _attackStartedAt;
        private float _currentImpactSeconds;
        private float _currentCycleSeconds;
        private AttackDefinition _currentDefinition;
        private bool _attackQueued;

        public bool IsAttacking => _attackRoutine != null;
        public PlayerAttackActionType CurrentActionType { get; private set; } = PlayerAttackActionType.GroundLight1;
        public bool IsPlunging => IsAttacking && CurrentActionType == PlayerAttackActionType.Plunge;
        public bool IsAirSlashing => IsAttacking && CurrentActionType == PlayerAttackActionType.AirSlash;

        public bool IsMovementLocked
        {
            get
            {
                if (!IsAttacking)
                    return false;

                if (CurrentActionType == PlayerAttackActionType.Plunge)
                    return true;

                var unlockAt = Mathf.Min(
                    Mathf.Max(0f, _currentCycleSeconds),
                    Mathf.Max(0f, _currentImpactSeconds) + movementUnlockAfterImpactSeconds);
                return Time.time - _attackStartedAt < unlockAt;
            }
        }

        public event Action<int> AttackStarted;
        public event Action<PlayerAttackActionType> AttackActionStarted;
        public event Action<CombatEntity> AttackHit;

        public bool CanDashCancel
        {
            get
            {
                if (!IsAttacking || _currentDefinition == null)
                    return true;

                if (CurrentActionType == PlayerAttackActionType.Plunge)
                    return false;

                var elapsed = Time.time - _attackStartedAt;
                var duration = _currentCycleSeconds > 0f ? _currentCycleSeconds : _currentDefinition.TotalDuration;
                var normalized = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                return normalized >= _currentDefinition.dashCancelNormalizedTime;
            }
        }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _motor = GetComponent<PlayerMotor2D>();
            _dash = GetComponent<PlayerDashController>();
            _body = GetComponent<Rigidbody2D>();
            _input = GetComponent<IPlayerInputSource>();
            _presentation = GetComponentInChildren<AttackSlashPresentation2D>();

            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IAttackTimingProvider provider)
                {
                    _timingProvider = provider;
                    break;
                }
            }
        }

        private void OnEnable()
        {
            if (_entity != null && _entity.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_entity != null && _entity.Health != null)
                _entity.Health.Died -= OnDied;
        }

        private void Update()
        {
            if (_entity == null || _entity.Health == null || _entity.Health.IsDead || _input == null)
                return;

            if (!_input.AttackPressedThisFrame)
                return;

            if (IsAttacking)
            {
                _attackQueued = true;
                return;
            }

            TryBeginAttack();
        }

        public void Configure(AttackDefinition[] definitions, float attackValue = 10f)
        {
            combo = definitions;
            baseAttack = Mathf.Max(0f, attackValue);
        }

        public void CancelCurrentAttack()
        {
            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);
            _attackRoutine = null;
            _currentDefinition = null;
            _currentImpactSeconds = 0f;
            _currentCycleSeconds = 0f;
            _attackQueued = false;
        }

        private void OnDied()
        {
            CancelCurrentAttack();
        }

        private void TryBeginAttack()
        {
            if (combo == null || combo.Length == 0 ||
                _entity == null || _entity.Health == null || _entity.Health.IsDead)
                return;

            // Air actions take priority. The same J input becomes a different move according to
            // body state, so the moveset grows without adding more buttons.
            if (_motor != null && !_motor.IsGrounded)
            {
                if (_input != null && _input.Move.y <= plungeInputThreshold)
                {
                    _attackRoutine = StartCoroutine(PlungeRoutine(combo[Mathf.Min(2, combo.Length - 1)]));
                    return;
                }

                _attackRoutine = StartCoroutine(AirSlashRoutine(combo[0]));
                return;
            }

            // A completed dash opens one contextual attack window.
            if (_dash != null && _dash.TryConsumePostDashAttack(dashSlashWindowSeconds))
            {
                _attackRoutine = StartCoroutine(TimedAttackRoutine(
                    combo[0],
                    PlayerAttackActionType.DashSlash,
                    presentationIndex: 3,
                    cycleScale: 0.92f,
                    impactNormalized: 0.50f));
                return;
            }

            if (Time.time - _lastAttackFinishedAt > comboResetSeconds)
                _comboIndex = 0;

            var index = Mathf.Clamp(_comboIndex, 0, combo.Length - 1);
            var actionType = index switch
            {
                0 => PlayerAttackActionType.GroundLight1,
                1 => PlayerAttackActionType.GroundLight2,
                _ => PlayerAttackActionType.GroundHeavy3
            };

            _attackRoutine = StartCoroutine(TimedAttackRoutine(
                combo[index],
                actionType,
                presentationIndex: index,
                cycleScale: 1f,
                impactNormalized: -1f));
        }

        private IEnumerator AirSlashRoutine(AttackDefinition definition)
        {
            if (_body != null)
            {
                var velocity = _body.linearVelocity;
                velocity.x *= 0.72f;
                velocity.y = Mathf.Max(velocity.y, airSlashHangVelocity);
                _body.linearVelocity = velocity;
            }

            yield return TimedAttackRoutine(
                definition,
                PlayerAttackActionType.AirSlash,
                presentationIndex: 4,
                cycleScale: airSlashCycleScale,
                impactNormalized: 0.44f,
                allowGroundComboAdvance: false);
        }

        private IEnumerator PlungeRoutine(AttackDefinition definition)
        {
            BeginAction(definition, PlayerAttackActionType.Plunge, presentationIndex: 5);
            _currentImpactSeconds = plungeWindupSeconds;
            _currentCycleSeconds = plungeTimeoutSeconds + plungeLandingRecoverySeconds;

            if (_body != null)
            {
                var windupVelocity = _body.linearVelocity;
                windupVelocity.x *= 0.35f;
                windupVelocity.y = Mathf.Min(windupVelocity.y, 0.35f);
                _body.linearVelocity = windupVelocity;
            }

            yield return WaitScaled(plungeWindupSeconds);

            var elapsed = 0f;
            var leftGroundOnce = _motor == null || !_motor.IsGrounded;
            while (elapsed < plungeTimeoutSeconds &&
                   _entity != null &&
                   _entity.Health != null &&
                   !_entity.Health.IsDead)
            {
                if (_motor != null && !_motor.IsGrounded)
                    leftGroundOnce = true;

                if (leftGroundOnce && _motor != null && _motor.IsGrounded)
                    break;

                if (_body != null)
                {
                    var velocity = _body.linearVelocity;
                    velocity.x *= 0.82f;
                    velocity.y = -plungeSpeed;
                    _body.linearVelocity = velocity;
                }

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (_entity != null && _entity.Health != null && !_entity.Health.IsDead)
            {
                _presentation?.PlayBasic(5, _motor != null ? _motor.FacingSign : 1);
                PerformHit(definition, PlayerAttackActionType.Plunge);
            }

            yield return WaitScaled(plungeLandingRecoverySeconds);
            FinishAction(advanceGroundCombo: false);
        }

        private IEnumerator TimedAttackRoutine(
            AttackDefinition definition,
            PlayerAttackActionType actionType,
            int presentationIndex,
            float cycleScale,
            float impactNormalized,
            bool allowGroundComboAdvance = true)
        {
            BeginAction(definition, actionType, presentationIndex);

            ResolveTiming(definition, out var impactSeconds, out var cycleSeconds);
            cycleSeconds = Mathf.Max(0.14f, cycleSeconds * Mathf.Max(0.1f, cycleScale));
            if (impactNormalized >= 0f)
                impactSeconds = Mathf.Clamp(cycleSeconds * impactNormalized, 0.01f, cycleSeconds - 0.01f);
            else
                impactSeconds = Mathf.Clamp(impactSeconds * Mathf.Max(0.1f, cycleScale), 0.01f, cycleSeconds - 0.01f);

            _currentImpactSeconds = impactSeconds;
            _currentCycleSeconds = cycleSeconds;

            yield return WaitScaled(impactSeconds);

            if (_entity != null && _entity.Health != null && !_entity.Health.IsDead)
            {
                _presentation?.PlayBasic(presentationIndex, _motor != null ? _motor.FacingSign : 1);
                PerformHit(definition, actionType);
            }

            yield return WaitScaled(Mathf.Max(0f, cycleSeconds - impactSeconds));
            FinishAction(allowGroundComboAdvance && IsGroundComboAction(actionType));
        }

        private void BeginAction(AttackDefinition definition, PlayerAttackActionType actionType, int presentationIndex)
        {
            _currentDefinition = definition;
            CurrentActionType = actionType;
            _attackStartedAt = Time.time;
            _attackQueued = false;
            AttackStarted?.Invoke(presentationIndex);
            AttackActionStarted?.Invoke(actionType);
        }

        private void FinishAction(bool advanceGroundCombo)
        {
            _lastAttackFinishedAt = Time.time;

            if (advanceGroundCombo && combo != null && combo.Length > 0)
                _comboIndex = (_comboIndex + 1) % combo.Length;
            else
                _comboIndex = 0;

            _attackRoutine = null;
            _currentDefinition = null;
            _currentImpactSeconds = 0f;
            _currentCycleSeconds = 0f;

            var continueChain = _attackQueued &&
                                _entity != null &&
                                _entity.Health != null &&
                                !_entity.Health.IsDead;
            _attackQueued = false;
            if (continueChain)
                TryBeginAttack();
        }

        private void ResolveTiming(AttackDefinition definition, out float impactSeconds, out float cycleSeconds)
        {
            impactSeconds = Mathf.Max(0f, definition.startup);
            cycleSeconds = Mathf.Max(0.01f, definition.TotalDuration);

            if (_timingProvider != null &&
                _timingProvider.TryGetBasicAttackTiming(out var providedImpact, out var providedCycle) &&
                providedCycle > 0.01f)
            {
                cycleSeconds = providedCycle;
                impactSeconds = Mathf.Clamp(providedImpact, 0f, cycleSeconds);
            }
        }

        private void PerformHit(AttackDefinition definition, PlayerAttackActionType actionType)
        {
            var facing = _motor != null ? _motor.FacingSign : 1;
            var localOffset = definition.hitboxOffset;
            var hitboxSize = definition.hitboxSize;
            var knockback = definition.knockback;
            var damageMultiplier = definition.damageMultiplier;
            var sourceId = definition.name;
            var hitStop = definition.hitStopSeconds;
            var shake = definition.cameraShakeAmplitude;

            switch (actionType)
            {
                case PlayerAttackActionType.DashSlash:
                    localOffset = dashSlashHitboxOffset;
                    hitboxSize = dashSlashHitboxSize;
                    knockback = dashSlashKnockback;
                    damageMultiplier *= dashSlashDamageMultiplier;
                    sourceId = "Texas_DashSlash";
                    hitStop = Mathf.Max(0.045f, hitStop);
                    shake = Mathf.Max(0.11f, shake);
                    break;

                case PlayerAttackActionType.AirSlash:
                    localOffset = airSlashHitboxOffset;
                    hitboxSize = airSlashHitboxSize;
                    knockback = airSlashKnockback;
                    damageMultiplier *= airSlashDamageMultiplier;
                    sourceId = "Texas_AirSlash";
                    hitStop = Mathf.Max(0.030f, hitStop);
                    shake = Mathf.Max(0.075f, shake);
                    break;

                case PlayerAttackActionType.Plunge:
                    localOffset = plungeHitboxOffset;
                    hitboxSize = plungeHitboxSize;
                    knockback = plungeKnockback;
                    damageMultiplier *= plungeDamageMultiplier;
                    sourceId = "Texas_Plunge";
                    hitStop = Mathf.Max(0.060f, hitStop);
                    shake = Mathf.Max(0.13f, shake);
                    break;
            }

            localOffset.x *= facing;
            knockback.x *= facing;
            var center = (Vector2)transform.position + localOffset;
            var hits = Physics2D.OverlapBoxAll(center, hitboxSize, 0f);
            var processed = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;

                var target = hit.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team || !processed.Add(target))
                    continue;

                var context = new DamageContext(
                    _entity,
                    _entity,
                    target,
                    baseAttack * damageMultiplier,
                    DamageType.Physical,
                    knockback,
                    sourceId: sourceId);
                var result = DamageSystem.Apply(context);
                if (!result.Applied)
                    continue;

                hitAny = true;
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
                AttackHit?.Invoke(target);
            }

            if (!hitAny)
                return;

            HitStopService.Instance?.Request(hitStop);
            CameraShake2D.Instance?.Shake(shake, actionType == PlayerAttackActionType.Plunge ? 0.13f : 0.09f);
        }

        private static bool IsGroundComboAction(PlayerAttackActionType actionType)
        {
            return actionType == PlayerAttackActionType.GroundLight1 ||
                   actionType == PlayerAttackActionType.GroundLight2 ||
                   actionType == PlayerAttackActionType.GroundHeavy3;
        }

        private static IEnumerator WaitScaled(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_currentDefinition == null)
                return;

            var facing = Application.isPlaying && _motor != null ? _motor.FacingSign : 1;
            var offset = _currentDefinition.hitboxOffset;
            var size = _currentDefinition.hitboxSize;

            switch (CurrentActionType)
            {
                case PlayerAttackActionType.DashSlash:
                    offset = dashSlashHitboxOffset;
                    size = dashSlashHitboxSize;
                    break;
                case PlayerAttackActionType.AirSlash:
                    offset = airSlashHitboxOffset;
                    size = airSlashHitboxSize;
                    break;
                case PlayerAttackActionType.Plunge:
                    offset = plungeHitboxOffset;
                    size = plungeHitboxSize;
                    break;
            }

            offset.x *= facing;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube((Vector2)transform.position + offset, size);
        }
#endif
    }
}
