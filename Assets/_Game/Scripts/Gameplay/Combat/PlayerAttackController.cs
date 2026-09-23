using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEngine;

namespace ArknightsACT.Gameplay.Combat
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class PlayerAttackController : MonoBehaviour, ICombatActionInterruptHandler
    {
        [SerializeField] private AttackDefinition[] combo;
        [SerializeField] private float baseAttack = 10f;
        [SerializeField] private float comboResetSeconds = 0.60f;
        [SerializeField, Min(0f)] private float movementUnlockAfterImpactSeconds = 0.04f;
        [SerializeField, Min(0.1f)] private float max25DHeightDifference = 1.15f;

        private CombatEntity _entity;
        private IPlayerLocomotion _motor;
        private PlayerMotor25D _motor25D;
        private PlayerDashController _dash;
        private IPlayerInputSource _input;
        private PlayerSkillController _skills;
        private CollectibleInventory _collectibles;
        private IPlayerBasicAttackMovementLockProvider _movementLockProvider;
        private Coroutine _attackRoutine;
        private AttackDefinition _currentDefinition;
        private int _comboIndex;
        private bool _attackQueued;
        private float _lastAttackFinishedAt = -999f;
        private float _attackStartedAt;
        private float _currentImpactSeconds;
        private float _currentCycleSeconds;

        public bool IsAttacking => _attackRoutine != null;
        public int CurrentComboIndex { get; private set; }
        public string CurrentAttackSourceId { get; private set; } = string.Empty;
        public string LastHitSourceId { get; private set; } = string.Empty;

        public bool IsMovementLocked
        {
            get
            {
                if (!IsAttacking)
                    return false;

                if (_movementLockProvider != null)
                    return _movementLockProvider.IsBasicAttackMovementLocked;

                var unlockAt = Mathf.Min(
                    Mathf.Max(0f, _currentCycleSeconds),
                    Mathf.Max(0f, _currentImpactSeconds) + movementUnlockAfterImpactSeconds);
                return Time.time - _attackStartedAt < unlockAt;
            }
        }

        public bool CanDashCancel
        {
            get
            {
                if (!IsAttacking || _currentDefinition == null)
                    return true;
                var elapsed = Time.time - _attackStartedAt;
                var duration = Mathf.Max(0.01f, _currentCycleSeconds);
                return Mathf.Clamp01(elapsed / duration) >= _currentDefinition.dashCancelNormalizedTime;
            }
        }

        public event Action<int> AttackStarted;
        public event Action<CombatEntity> AttackHit;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _motor = FindLocomotion();
            _motor25D = GetComponent<PlayerMotor25D>();
            _dash = GetComponent<PlayerDashController>();
            _input = GetComponent<IPlayerInputSource>();
            _skills = GetComponent<PlayerSkillController>();
            _collectibles = GetComponent<CollectibleInventory>();
            _movementLockProvider = GetComponent<IPlayerBasicAttackMovementLockProvider>();
        }

        private void OnEnable()
        {
            if (_entity?.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            CancelCurrentAttack();
            if (_entity?.Health != null)
                _entity.Health.Died -= OnDied;
        }

        private void Update()
        {
            if (_entity?.Health == null || _entity.Health.IsDead || _input == null)
                return;
            if (IsExternallyBasicAttackBlocked())
                return;
            if (!_input.AttackPressedThisFrame)
                return;
            if (_dash != null && _dash.IsDashing)
                return;
            if (_skills != null && _skills.IsCasting)
                return;
            if (_motor != null && !_motor.IsGrounded)
                return;

            if (IsAttacking)
            {
                _attackQueued = true;
                return;
            }

            BeginNextComboAttack();
        }

        public void Configure(AttackDefinition[] definitions, float attackValue = 10f)
        {
            combo = definitions;
            baseAttack = Mathf.Max(0f, attackValue);
        }

        public bool TryManualTargetAttack(
            CombatEntity target,
            string sourceId,
            float damageMultiplier = 1f,
            float impactDelay = 0f)
        {
            if (CombatActionUtility.IsBlocked(_entity, CombatActionMask.BasicAttack))
                return false;
            if (target == null || _entity?.Health == null || _entity.Health.IsDead ||
                target.Health == null || target.Health.IsDead || target.Team == _entity.Team)
                return false;
            if (_dash != null && _dash.IsDashing)
                return false;
            if (_skills != null && _skills.IsCasting)
                return false;

            var definition = combo != null && combo.Length > 0 ? combo[0] : null;
            var finalDamage = baseAttack * Mathf.Max(0f, damageMultiplier) * GetBasicAttackDamageMultiplier();
            var resolvedSourceId = string.IsNullOrWhiteSpace(sourceId) ? "ManualBasicAttack" : sourceId;
            CurrentAttackSourceId = resolvedSourceId;
            AttackStarted?.Invoke(0);
            CurrentAttackSourceId = string.Empty;
            StartCoroutine(ManualTargetImpactRoutine(
                target,
                resolvedSourceId,
                finalDamage,
                Mathf.Max(0f, impactDelay),
                definition));
            return true;
        }

        private IEnumerator ManualTargetImpactRoutine(
            CombatEntity target,
            string sourceId,
            float finalDamage,
            float impactDelay,
            AttackDefinition definition)
        {
            if (impactDelay > 0f)
                yield return new WaitForSeconds(impactDelay);

            if (target == null || target.Health == null || target.Health.IsDead ||
                _entity?.Health == null || _entity.Health.IsDead)
                yield break;

            var context = new DamageContext(
                _entity,
                _entity,
                target,
                finalDamage,
                DamageType.Physical,
                Vector2.zero,
                sourceId: sourceId, tags: DamageTags.BasicAttack);
            var result = DamageSystem.Apply(context);
            if (!result.Applied)
                yield break;

            target.GetComponentInChildren<HitFlash2D>()?.Flash();
            RaiseAttackHit(target, sourceId);
            if (definition != null)
                ApplyImpactFeedback(definition, true);
        }

        public void InterruptCombatActions(CombatActionMask actions)
        {
            if ((actions & CombatActionMask.BasicAttack) != 0)
                CancelCurrentAttack();
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
            _comboIndex = 0;
            CurrentComboIndex = 0;
            _lastAttackFinishedAt = -999f;
            CurrentAttackSourceId = string.Empty;
            LastHitSourceId = string.Empty;
        }

        private void OnDied() => CancelCurrentAttack();

        private void BeginNextComboAttack()
        {
            if (combo == null || combo.Length == 0 || _entity?.Health == null || _entity.Health.IsDead)
                return;
            if (_dash != null && _dash.IsDashing)
                return;
            if (_motor != null && !_motor.IsGrounded)
                return;

            if (Time.time - _lastAttackFinishedAt > comboResetSeconds)
                _comboIndex = 0;

            CurrentComboIndex = Mathf.Clamp(_comboIndex, 0, combo.Length - 1);
            _attackRoutine = StartCoroutine(AttackRoutine(combo[CurrentComboIndex], CurrentComboIndex));
        }

        private IEnumerator AttackRoutine(AttackDefinition definition, int presentationIndex)
        {
            _currentDefinition = definition;
            _attackStartedAt = Time.time;
            _attackQueued = false;
            var attackSpeedBonus = _collectibles != null
                ? _collectibles.GetEffectTotal(CollectibleEffectType.AttackSpeedPercent)
                : 0f;
            var statAttackSpeed = _entity?.Stats != null ? _entity.Stats.AttackSpeedMultiplier : 1f;
            var timingMultiplier = GetBasicAttackTimingMultiplier() /
                                   Mathf.Max(0.1f, (1f + attackSpeedBonus) * statAttackSpeed);
            var customImpactSeconds = GetBasicAttackImpactSecondsOverride(definition);
            _currentImpactSeconds = customImpactSeconds >= 0f
                ? customImpactSeconds
                : Mathf.Max(0f, definition.startup * timingMultiplier);
            _currentCycleSeconds = Mathf.Max(
                Mathf.Max(0.01f, definition.TotalDuration * timingMultiplier),
                _currentImpactSeconds);
            CurrentAttackSourceId = string.Empty;
            AttackStarted?.Invoke(presentationIndex);

            if (_currentImpactSeconds > 0f)
                yield return new WaitForSeconds(_currentImpactSeconds);

            if (_entity?.Health != null && !_entity.Health.IsDead)
                PerformHit(definition);

            var remainder = Mathf.Max(0f, _currentCycleSeconds - _currentImpactSeconds);
            if (remainder > 0f)
                yield return new WaitForSeconds(remainder);

            _lastAttackFinishedAt = Time.time;
            _comboIndex = (_comboIndex + 1) % combo.Length;
            _attackRoutine = null;
            _currentDefinition = null;
            _currentImpactSeconds = 0f;
            _currentCycleSeconds = 0f;

            var continueChain = _attackQueued &&
                                _entity?.Health != null &&
                                !_entity.Health.IsDead &&
                                (_dash == null || !_dash.IsDashing) &&
                                (_motor == null || _motor.IsGrounded) &&
                                (_skills == null || !_skills.IsCasting);
            _attackQueued = false;
            if (continueChain)
                BeginNextComboAttack();
        }

        private void PerformHit(AttackDefinition definition)
        {
            if (TryResolveCustomBasicAttack(definition))
                return;

            if (_motor25D != null)
                PerformHit25D(definition);
            else
                PerformHit2D(definition);
        }

        private bool TryResolveCustomBasicAttack(AttackDefinition definition)
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IPlayerBasicAttackResolver resolver)
                    continue;

                var damage = baseAttack * definition.damageMultiplier * GetBasicAttackDamageMultiplier();
                var hitAny = resolver.TryResolveBasicAttack(
                    _entity,
                    definition,
                    _motor,
                    damage,
                    GetBasicAttackRangeMultiplier(),
                    out var target);

                if (hitAny && target != null)
                {
                    target.GetComponentInChildren<HitFlash2D>()?.Flash();
                    RaiseAttackHit(target, string.Empty);
                }

                ApplyImpactFeedback(definition, hitAny);
                return true;
            }

            return false;
        }

        private void PerformHit2D(AttackDefinition definition)
        {
            var facing = _motor != null ? _motor.FacingSign : 1;
            var rangeMultiplier = GetBasicAttackRangeMultiplier();
            var offset = definition.hitboxOffset;
            offset.x *= facing * rangeMultiplier;
            var knockback = definition.knockback;
            knockback.x *= facing;
            var hitboxSize = definition.hitboxSize;
            hitboxSize.x *= rangeMultiplier;

            var center = (Vector2)transform.position + offset;
            var hits = Physics2D.OverlapBoxAll(center, hitboxSize, 0f);
            var processed = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;
                var target = hit.GetComponentInParent<CombatEntity>();
                if (!CanHit(target, processed))
                    continue;

                if (ApplyHit(target, definition, knockback))
                    hitAny = true;
            }

            ApplyImpactFeedback(definition, hitAny);
        }

        private void PerformHit25D(AttackDefinition definition)
        {
            var forward = _motor != null ? _motor.PlanarForward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            // AttackDefinition was authored for a side-view XY box where hitboxSize.x is the
            // sword's forward reach. In XZ that authored X dimension should therefore map to
            // local Z (forward), not local X (sideways). Keeping that semantic makes Ch'en's
            // attacks longer and slimmer instead of short and excessively wide.
            const float forwardCenterScale = 1.10f;
            const float forwardHalfExtentScale = 0.48f;
            const float lateralHalfExtentScale = 0.38f;
            const float closeRangeSafetyRadius = 0.92f;
            const float closeRangeForwardDot = -0.05f;

            var rangeMultiplier = GetBasicAttackRangeMultiplier();
            var center = transform.position +
                         forward * Mathf.Max(0.55f, Mathf.Abs(definition.hitboxOffset.x) * forwardCenterScale) * rangeMultiplier +
                         Vector3.up * 0.78f;
            var halfExtents = new Vector3(
                Mathf.Max(0.48f, definition.hitboxSize.y * lateralHalfExtentScale),
                0.72f,
                Mathf.Max(0.65f, definition.hitboxSize.x * forwardHalfExtentScale) * rangeMultiplier);
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            // The long slim box preserves the authored sword reach. A small point-blank sphere is
            // merged in because two CharacterControllers can touch diagonally while the enemy
            // centre sits just outside that slim box. The frontal-dot guard keeps this from
            // becoming a 360-degree attack.
            var candidates = new List<Collider>();
            candidates.AddRange(Physics.OverlapBox(center, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore));
            candidates.AddRange(Physics.OverlapSphere(
                transform.position + Vector3.up * 0.78f,
                closeRangeSafetyRadius * Mathf.Min(1.35f, rangeMultiplier),
                ~0,
                QueryTriggerInteraction.Ignore));

            var processed = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var hit in candidates)
            {
                if (hit == null)
                    continue;
                var target = hit.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team || target.Health == null || target.Health.IsDead)
                    continue;

                var planarToTarget = target.transform.position - transform.position;
                planarToTarget.y = 0f;
                if (planarToTarget.sqrMagnitude > 0.001f &&
                    Vector3.Dot(forward, planarToTarget.normalized) < closeRangeForwardDot)
                    continue;

                if (!CanHit(target, processed))
                    continue;
                if (Mathf.Abs(target.transform.position.y - transform.position.y) > max25DHeightDifference)
                    continue;
                if (!HasClear25DHitPath(target))
                    continue;

                if (ApplyHit(target, definition, Vector2.zero))
                    hitAny = true;
            }

            ApplyImpactFeedback(definition, hitAny);
        }

        private bool CanHit(CombatEntity target, HashSet<CombatEntity> processed)
        {
            return target != null &&
                   target != _entity &&
                   target.Team != _entity.Team &&
                   target.Health != null &&
                   !target.Health.IsDead &&
                   processed.Add(target);
        }

        private bool HasClear25DHitPath(CombatEntity target)
        {
            var origin = transform.position + Vector3.up * 0.68f;
            var destination = target.transform.position + Vector3.up * 0.68f;
            var cast = destination - origin;
            var distance = cast.magnitude;
            if (distance < 0.001f)
                return true;

            var hits = Physics.SphereCastAll(origin, 0.10f, cast / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;
                var entity = collider.GetComponentInParent<CombatEntity>();
                if (entity != null)
                {
                    if (entity == target)
                        return true;
                    continue;
                }
                return false;
            }
            return true;
        }

        private bool ApplyHit(CombatEntity target, AttackDefinition definition, Vector2 knockback)
        {
            var context = new DamageContext(
                _entity,
                _entity,
                target,
                baseAttack * definition.damageMultiplier * GetBasicAttackDamageMultiplier(),
                DamageType.Physical,
                knockback,
                sourceId: definition.name, tags: DamageTags.BasicAttack);
            var result = DamageSystem.Apply(context);
            if (!result.Applied)
                return false;

            target.GetComponentInChildren<HitFlash2D>()?.Flash();
            RaiseAttackHit(target, string.Empty);
            return true;
        }

        private void RaiseAttackHit(CombatEntity target, string sourceId)
        {
            LastHitSourceId = sourceId ?? string.Empty;
            AttackHit?.Invoke(target);
        }

        private static void ApplyImpactFeedback(AttackDefinition definition, bool hitAny)
        {
            if (!hitAny)
                return;
            if (definition.hitStopSeconds > 0f)
                HitStopService.Instance?.Request(definition.hitStopSeconds);
            if (definition.cameraShakeAmplitude > 0f)
                CameraShake2D.Instance?.Shake(definition.cameraShakeAmplitude, 0.06f);
        }

        private bool IsExternallyBasicAttackBlocked()
        {
            if (CombatActionUtility.IsBlocked(_entity, CombatActionMask.BasicAttack))
                return true;
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerControlLockSource source && source.BlocksBasicAttack)
                    return true;
            return false;
        }

        private float GetBasicAttackDamageMultiplier()
        {
            var multiplier = 1f;
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerBasicAttackModifier modifier)
                    multiplier *= Mathf.Max(0f, modifier.BasicAttackDamageMultiplier);
            return Mathf.Clamp(multiplier, 0f, 12f);
        }

        private float GetBasicAttackRangeMultiplier()
        {
            var multiplier = 1f;
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerBasicAttackModifier modifier)
                    multiplier *= Mathf.Max(0.1f, modifier.BasicAttackRangeMultiplier);
            return Mathf.Clamp(multiplier, 0.25f, 4f);
        }

        private float GetBasicAttackImpactSecondsOverride(AttackDefinition definition)
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IPlayerBasicAttackImpactTimingProvider provider)
                    continue;

                var seconds = provider.GetBasicAttackImpactSeconds(definition);
                if (seconds >= 0f)
                    return Mathf.Max(0f, seconds);
            }

            return -1f;
        }

        private float GetBasicAttackTimingMultiplier()
        {
            var multiplier = 1f;
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerBasicAttackModifier modifier)
                    multiplier *= Mathf.Max(0.1f, modifier.BasicAttackTimingMultiplier);
            return Mathf.Clamp(multiplier, 0.25f, 4f);
        }

        private IPlayerLocomotion FindLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            }
            return null;
        }
    }
}
