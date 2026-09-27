using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Feedback;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class ChenSkill1 : MonoBehaviour, IPlayerSkill, IPlayerSkillInterruptible, IOperatorSkillMasteryTarget
    {
        [Header("Skill points")]
        [SerializeField, Min(0f)] private float skillPointCost;
        [SerializeField, Min(0f)] private float initialSkillPoints;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [Tooltip("横向刀光在起手第 1 帧出现，因此伤害与起手特效同帧结算。")]
        [SerializeField, Min(0f)] private float impactDelay = 0f;
        [SerializeField, Min(0.1f)] private float castLockSeconds = 1.15f;
        [SerializeField, Min(0f)] private float masteryAttackScale;
        [SerializeField] private string masteryRangeId = string.Empty;
        [SerializeField, Min(1)] private int masteryMaxTargets = 1;
        [SerializeField] private Vector2 hitboxOffset = new(1.25f, 0.08f);
        [SerializeField] private Vector2 hitboxSize = new(3.4f, 1.8f);
        [SerializeField, Min(0.1f)] private float max25DHeightDifference = 1.20f;

        private CombatEntity _entity;
        private OperatorRuntimeStats _operatorStats;
        private IPlayerLocomotion _motor;
        private PlayerMotor25D _motor25D;
        private float _skillPoints;
        private float _runtimeRangeMultiplier = 1f;
        private float _runtimeDamageMultiplier = 1f;
        private float _runtimeSkillPointCostMultiplier = 1f;
        private float _echoSlashDamageMultiplier;
        private bool _officialSkillDataApplied;

        public int Slot => 1;
        public int MasterySlot => 1;
        public string DisplayName => "赤霄·拔刀";
        public float SkillPointCost => Mathf.Max(1f, skillPointCost * _runtimeSkillPointCostMultiplier);
        public float SkillPoints => Mathf.Clamp(_skillPoints, 0f, SkillPointCost);
        public float SkillPointRatio => Mathf.Clamp01(SkillPoints / SkillPointCost);
        public float NaturalSkillPointPerSecond => Mathf.Max(0f, naturalSkillPointPerSecond);
        public float CooldownRemaining => NaturalSkillPointPerSecond <= 0f
            ? (SkillPointRatio >= 1f ? 0f : float.PositiveInfinity)
            : Mathf.Max(0f, SkillPointCost - SkillPoints) / NaturalSkillPointPerSecond;
        public bool IsCasting { get; private set; }

        /// <summary>
        /// Fired once for every valid enemy selected by 赤霄·拔刀. The combat result is kept
        /// separate so presentation FX still play when damage is blocked.
        /// </summary>
        public event Action<Transform> HitResolved;

        /// <summary>
        /// Fired as soon as 赤霄·拔刀 is accepted by the skill controller. This is intentionally
        /// separate from HitResolved because the cast presentation must start even before the
        /// delayed hitbox resolves.
        /// </summary>
        public event Action CastStarted;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _operatorStats = GetComponent<OperatorRuntimeStats>();
            _motor = FindLocomotion();
            _motor25D = GetComponent<PlayerMotor25D>();
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public bool TryCast()
        {
            if (!_officialSkillDataApplied || IsCasting || SkillPoints + 0.0001f < SkillPointCost || _entity?.Health == null || _entity.Health.IsDead)
                return false;

            _skillPoints = Mathf.Max(0f, SkillPoints - SkillPointCost);
            StartCoroutine(CastRoutine());
            return true;
        }

        public void TickSkillPoints(float deltaTime, float recoveryMultiplier, float flatRecoveryPerSecond)
        {
            if (IsCasting || deltaTime <= 0f || SkillPointRatio >= 1f) return;
            var perSecond = NaturalSkillPointPerSecond * Mathf.Max(0f, recoveryMultiplier) + Mathf.Max(0f, flatRecoveryPerSecond);
            GainSkillPoints(perSecond * deltaTime);
        }

        public void GainSkillPoints(float amount)
        {
            if (amount <= 0f) return;
            _skillPoints = Mathf.Clamp(SkillPoints + amount, 0f, SkillPointCost);
        }

        public void SetSkillPoints(float amount)
        {
            _skillPoints = Mathf.Clamp(amount, 0f, SkillPointCost);
        }

        public void ReduceCooldown(float seconds)
        {
            if (seconds > 0f)
                GainSkillPoints(seconds * Mathf.Max(0.01f, NaturalSkillPointPerSecond));
        }

        public void ApplyMasterySnapshot(OperatorSkillMasterySnapshot snapshot)
        {
            if (snapshot == null)
            {
                _officialSkillDataApplied = false;
                return;
            }

            skillPointCost = snapshot.SkillPointCost;
            initialSkillPoints = Mathf.Clamp(
                snapshot.InitialSkillPoints,
                0f,
                Mathf.Max(1f, skillPointCost));
            masteryRangeId = snapshot.RangeId ?? string.Empty;

            var hasAttackScale = snapshot.TryGetBlackboard("atk_scale", out var attackScale);
            var hasMaxTargets = snapshot.TryGetBlackboard("max_target", out var maxTargets);
            masteryAttackScale = hasAttackScale ? Mathf.Max(0f, attackScale) : 0f;
            masteryMaxTargets = hasMaxTargets ? Mathf.Max(1, Mathf.RoundToInt(maxTargets)) : 1;

            _officialSkillDataApplied =
                skillPointCost > 0f &&
                masteryAttackScale > 0f &&
                hasMaxTargets &&
                !string.IsNullOrWhiteSpace(masteryRangeId);
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public void AddRangePercent(float value) =>
            _runtimeRangeMultiplier = Mathf.Clamp(_runtimeRangeMultiplier + Mathf.Max(0f, value), 1f, 2.5f);

        public void AddDamagePercent(float value) =>
            _runtimeDamageMultiplier = Mathf.Clamp(_runtimeDamageMultiplier + Mathf.Max(0f, value), 1f, 3f);

        public void AddCooldownReductionPercent(float value) =>
            _runtimeSkillPointCostMultiplier = Mathf.Clamp(
                _runtimeSkillPointCostMultiplier * (1f - Mathf.Max(0f, value)), 0.45f, 1f);

        public void EnableEchoSlash(float damageFraction)
        {
            _echoSlashDamageMultiplier = Mathf.Max(
                _echoSlashDamageMultiplier,
                Mathf.Clamp(damageFraction, 0.10f, 1.50f));
        }

        public void ResetRunModifiers()
        {
            StopAllCoroutines();
            IsCasting = false;
            _runtimeRangeMultiplier = 1f;
            _runtimeDamageMultiplier = 1f;
            _runtimeSkillPointCostMultiplier = 1f;
            _echoSlashDamageMultiplier = 0f;
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public void InterruptCast()
        {
            if (!IsCasting)
                return;
            StopAllCoroutines();
            IsCasting = false;
        }

        private IEnumerator CastRoutine()
        {
            IsCasting = true;
            // The extracted skill_02_start horizontal slash is already visible on frame 1.
            // Fire the presentation event before a zero-delay hit resolves so both happen in
            // the same simulation step instead of waiting for the slash animation to finish.
            CastStarted?.Invoke();
            if (impactDelay > 0f)
                yield return new WaitForSeconds(impactDelay);

            ResolveHit(1f);

            var elapsed = impactDelay;
            if (_echoSlashDamageMultiplier > 0f)
            {
                const float echoDelay = 0.18f;
                yield return new WaitForSeconds(echoDelay);
                elapsed += echoDelay;
                ResolveHit(_echoSlashDamageMultiplier);
            }

            var remaining = Mathf.Max(0f, castLockSeconds - elapsed);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
            IsCasting = false;
        }

        private void ResolveHit(float damageScale)
        {
            if (_entity?.Health == null || _entity.Health.IsDead)
                return;

            if (_motor25D != null)
                ResolveHit25D(damageScale);
            else
                ResolveHit2D(damageScale);
        }

        private void ResolveHit2D(float damageScale)
        {
            var facing = _motor != null ? _motor.FacingSign : 1;
            var offset = hitboxOffset;
            var rangeMultiplier = ResolveSkillRangeMultiplier();
            offset.x *= facing * rangeMultiplier;
            var size = hitboxSize;
            size.x *= rangeMultiplier;
            var center = (Vector2)transform.position + offset;
            var colliders = Physics2D.OverlapBoxAll(center, size, 0f);
            var seen = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var target = collider.GetComponentInParent<CombatEntity>();
                if (!CanHit(target, seen))
                    continue;
                seen.Add(target);

                if (ApplyDamagePair(target, new Vector2(4.2f * facing, 0.9f), damageScale))
                    hitAny = true;
                // Presentation FX should still play when a valid target is found but the
                // damage is blocked by invulnerability/other combat rules.
                HitResolved?.Invoke(target.transform);
            }

            ApplyFeedback(hitAny);
        }

        private void ResolveHit25D(float damageScale)
        {
            var forward = _motor != null ? _motor.PlanarForward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            var rangeMultiplier = ResolveSkillRangeMultiplier();
            var forwardCenterDistance =
                Mathf.Max(0.95f, Mathf.Abs(hitboxOffset.x) * 1.08f * rangeMultiplier);
            var forwardHalfExtent =
                Mathf.Max(1.25f, hitboxSize.x * 0.50f * rangeMultiplier);
            var center = transform.position +
                         forward * forwardCenterDistance +
                         Vector3.up * 0.78f;
            var halfExtents = new Vector3(
                Mathf.Max(0.66f, hitboxSize.y * 0.40f),
                0.72f,
                forwardHalfExtent);
            var officialForwardReach = forwardCenterDistance + forwardHalfExtent;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var colliders = Physics.OverlapBox(center, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var target = collider.GetComponentInParent<CombatEntity>();
                if (!CanHit(target, seen))
                    continue;
                if (!OperatorRangeUtility.Contains(
                        masteryRangeId,
                        transform.position,
                        forward,
                        target.transform.position,
                        officialForwardReach))
                    continue;
                if (Mathf.Abs(target.transform.position.y - transform.position.y) > max25DHeightDifference)
                    continue;
                if (!HasClear25DLine(target))
                    continue;
                seen.Add(target);

                if (ApplyDamagePair(target, Vector2.zero, damageScale))
                    hitAny = true;
                HitResolved?.Invoke(target.transform);
            }

            ApplyFeedback(hitAny);
        }

        private float ResolveSkillRangeMultiplier()
        {
            var pipeline = _operatorStats != null
                ? _operatorStats.GetSkillRangeMultiplier(MasterySlot)
                : 1f;
            return Mathf.Clamp(pipeline * _runtimeRangeMultiplier, 0.25f, 4f);
        }

        private bool CanHit(CombatEntity target, HashSet<CombatEntity> seen)
        {
            return seen.Count < Mathf.Max(1, masteryMaxTargets) &&
                   target != null &&
                   target != _entity &&
                   target.Team != _entity.Team &&
                   target.Health != null &&
                   !target.Health.IsDead &&
                   !seen.Contains(target);
        }

        private bool HasClear25DLine(CombatEntity target)
        {
            var origin = transform.position + Vector3.up * 0.70f;
            var destination = target.transform.position + Vector3.up * 0.70f;
            var cast = destination - origin;
            var distance = cast.magnitude;
            if (distance < 0.001f)
                return true;

            var hits = Physics.SphereCastAll(origin, 0.12f, cast / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
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

        private bool ApplyDamagePair(CombatEntity target, Vector2 knockback, float damageScale)
        {
            if (!_officialSkillDataApplied || masteryAttackScale <= 0f || _operatorStats == null)
                return false;

            var hitAny = false;
            var officialDamage = _operatorStats.Attack * masteryAttackScale;
            var physical = new DamageContext(
                _entity, _entity, target, officialDamage * _runtimeDamageMultiplier * damageScale, DamageType.Physical, knockback,
                sourceId: "Chen_Skill1_Physical", tags: DamageTags.Skill);
            var physicalResult = DamageSystem.Apply(physical);
            if (physicalResult.Applied)
                hitAny = true;

            var artsBaseDamage = officialDamage;
            if (!target.Health.IsDead && artsBaseDamage > 0f)
            {
                var arts = new DamageContext(
                    _entity, _entity, target, artsBaseDamage * _runtimeDamageMultiplier * damageScale, DamageType.Arts, Vector2.zero,
                    sourceId: "Chen_Skill1_Arts", tags: DamageTags.Skill);
                if (DamageSystem.Apply(arts).Applied)
                    hitAny = true;
            }

            if (physicalResult.Applied)
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
            return hitAny;
        }

        private static void ApplyFeedback(bool hitAny)
        {
            if (!hitAny)
                return;
            HitStopService.Instance?.Request(0.045f);
            CameraShake2D.Instance?.Shake(0.11f, 0.08f);
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
