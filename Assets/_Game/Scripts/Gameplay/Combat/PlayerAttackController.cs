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
    [RequireComponent(typeof(CombatEntity), typeof(PlayerMotor2D))]
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

        private CombatEntity _entity;
        private PlayerMotor2D _motor;
        private PlayerDashController _dash;
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
        private bool _currentDashSlash;
        private bool _attackQueued;

        public bool IsAttacking => _attackRoutine != null;

        public bool IsMovementLocked
        {
            get
            {
                if (!IsAttacking)
                    return false;

                var unlockAt = Mathf.Min(
                    Mathf.Max(0f, _currentCycleSeconds),
                    Mathf.Max(0f, _currentImpactSeconds) + movementUnlockAfterImpactSeconds);
                return Time.time - _attackStartedAt < unlockAt;
            }
        }

        public event Action<int> AttackStarted;
        public event Action<CombatEntity> AttackHit;

        public bool CanDashCancel
        {
            get
            {
                if (!IsAttacking || _currentDefinition == null)
                    return true;
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
            _currentDashSlash = false;
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

            // A completed dash opens one contextual attack window. It never adds another button:
            // the same Attack input becomes a distinct forward cut once, then normal combo resumes.
            if (_dash != null && _dash.TryConsumePostDashAttack(dashSlashWindowSeconds))
            {
                _attackRoutine = StartCoroutine(AttackRoutine(combo[0], 3, true));
                return;
            }

            if (Time.time - _lastAttackFinishedAt > comboResetSeconds)
                _comboIndex = 0;

            var index = _comboIndex;
            _attackRoutine = StartCoroutine(AttackRoutine(combo[index], index, false));
        }

        private IEnumerator AttackRoutine(AttackDefinition definition, int presentationIndex, bool dashSlash)
        {
            _currentDefinition = definition;
            _currentDashSlash = dashSlash;
            _attackStartedAt = Time.time;
            _attackQueued = false;

            ResolveTiming(definition, out var impactSeconds, out var cycleSeconds);
            if (dashSlash)
            {
                cycleSeconds = Mathf.Max(0.16f, cycleSeconds * 0.92f);
                impactSeconds = Mathf.Clamp(cycleSeconds * 0.50f, 0.01f, cycleSeconds - 0.01f);
            }

            _currentImpactSeconds = impactSeconds;
            _currentCycleSeconds = cycleSeconds;

            AttackStarted?.Invoke(presentationIndex);

            yield return WaitScaled(impactSeconds);

            if (_entity != null && _entity.Health != null && !_entity.Health.IsDead)
            {
                _presentation?.PlayBasic(presentationIndex, _motor.FacingSign);
                PerformHit(definition, dashSlash);
            }

            yield return WaitScaled(Mathf.Max(0f, cycleSeconds - impactSeconds));

            _lastAttackFinishedAt = Time.time;
            if (dashSlash)
                _comboIndex = 0;
            else
                _comboIndex = (_comboIndex + 1) % combo.Length;

            _attackRoutine = null;
            _currentDefinition = null;
            _currentImpactSeconds = 0f;
            _currentCycleSeconds = 0f;
            _currentDashSlash = false;

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

        private void PerformHit(AttackDefinition definition, bool dashSlash)
        {
            var facing = _motor.FacingSign;
            var localOffset = dashSlash ? dashSlashHitboxOffset : definition.hitboxOffset;
            localOffset.x *= facing;
            var center = (Vector2)transform.position + localOffset;
            var hitboxSize = dashSlash ? dashSlashHitboxSize : definition.hitboxSize;
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

                var knockback = dashSlash ? dashSlashKnockback : definition.knockback;
                knockback.x *= facing;
                var damage = baseAttack * definition.damageMultiplier * (dashSlash ? dashSlashDamageMultiplier : 1f);
                var context = new DamageContext(
                    _entity,
                    _entity,
                    target,
                    damage,
                    DamageType.Physical,
                    knockback,
                    sourceId: dashSlash ? "Texas_DashSlash" : definition.name);
                var result = DamageSystem.Apply(context);
                if (!result.Applied)
                    continue;

                hitAny = true;
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
                AttackHit?.Invoke(target);
            }

            if (!hitAny)
                return;

            var hitStop = dashSlash ? Mathf.Max(0.045f, definition.hitStopSeconds) : definition.hitStopSeconds;
            var shake = dashSlash ? Mathf.Max(0.11f, definition.cameraShakeAmplitude) : definition.cameraShakeAmplitude;
            HitStopService.Instance?.Request(hitStop);
            CameraShake2D.Instance?.Shake(shake, dashSlash ? 0.11f : 0.08f);
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
            var offset = _currentDashSlash ? dashSlashHitboxOffset : _currentDefinition.hitboxOffset;
            offset.x *= facing;
            var size = _currentDashSlash ? dashSlashHitboxSize : _currentDefinition.hitboxSize;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube((Vector2)transform.position + offset, size);
        }
#endif
    }
}
