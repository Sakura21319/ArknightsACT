using System;
using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Feedback;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class ChenSkill2 : MonoBehaviour, IPlayerSkill, IPlayerInvulnerabilitySource, IPlayerSkillInterruptible, IOperatorSkillMasteryTarget
    {
        [Header("Skill points")]
        [SerializeField, Min(0f)] private float skillPointCost;
        [SerializeField, Min(0f)] private float initialSkillPoints;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [SerializeField, Min(0f)] private float startupSeconds = 0.28f;
        [SerializeField, Min(1)] private int strikeCount = 1;
        [SerializeField, Min(0.02f)] private float strikeInterval = 0.12f;
        [SerializeField, Min(0f)] private float masteryAttackScale;
        [SerializeField] private string masteryRangeId = string.Empty;
        [SerializeField, Min(0f)] private float finalStrikeStunSeconds;
        [SerializeField, Min(0.5f)] private float targetingRadius = 5.5f;
        [SerializeField, Min(0.1f)] private float castLockSeconds = 2.1f;
        [SerializeField, Min(0.1f)] private float max25DHeightDifference = 1.35f;

        private CombatEntity _entity;
        private OperatorRuntimeStats _operatorStats;
        private PlayerMotor25D _motor25D;
        private float _skillPoints;
        private int _bonusStrikeCount;
        private float _runtimeFinalDamageMultiplier = 1f;
        private float _runtimeRadiusMultiplier = 1f;
        private int _killRefreshStrikeCap;
        private bool _officialSkillDataApplied;

        public int Slot => 2;
        public int MasterySlot => 2;
        public string DisplayName => "赤霄·绝影";
        public float SkillPointCost => Mathf.Max(1f, skillPointCost);
        public float SkillPoints => Mathf.Clamp(_skillPoints, 0f, SkillPointCost);
        public float SkillPointRatio => Mathf.Clamp01(SkillPoints / SkillPointCost);
        public float NaturalSkillPointPerSecond => Mathf.Max(0f, naturalSkillPointPerSecond);
        public float CooldownRemaining => NaturalSkillPointPerSecond <= 0f
            ? (SkillPointRatio >= 1f ? 0f : float.PositiveInfinity)
            : Mathf.Max(0f, SkillPointCost - SkillPoints) / NaturalSkillPointPerSecond;
        public bool IsCasting { get; private set; }
        public bool IsInvulnerable => IsCasting;
        public int StrikeCount => Mathf.Max(1, strikeCount);
        public float FinalStrikeStunSeconds => Mathf.Max(0f, finalStrikeStunSeconds);

        /// <summary>
        /// Raised when the gameplay portion of Jueying has released its cast lock. Presentation
        /// effects may still be finishing after this event; UI systems use their own presentation
        /// gate for that separate lifetime.
        /// </summary>
        public event Action CastEnded;

        /// <summary>
        /// Fired after a Jueying strike resolves a valid target. strikeIndex is zero-based and
        /// is intentionally exposed so the original chen_skill_03_hit_01..10 assets stay in order.
        /// </summary>
        public event Action<int, Transform, bool> StrikeResolved;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _operatorStats = GetComponent<OperatorRuntimeStats>();
            _motor25D = GetComponent<PlayerMotor25D>();
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public bool TryCast()
        {
            if (!_officialSkillDataApplied || IsCasting || SkillPoints + 0.0001f < SkillPointCost || _entity?.Health == null || _entity.Health.IsDead)
                return false;
            if (FindNearestTarget() == null)
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

        public void AddStrikeCount(int value) =>
            _bonusStrikeCount = Mathf.Clamp(_bonusStrikeCount + Mathf.Max(0, value), 0, 12);

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
            var hasTimes = snapshot.TryGetBlackboard("times", out var times);
            var hasStun = snapshot.TryGetBlackboard("stun", out var stun);
            masteryAttackScale = hasAttackScale ? Mathf.Max(0f, attackScale) : 0f;
            strikeCount = hasTimes ? Mathf.Max(1, Mathf.RoundToInt(times)) : 1;
            finalStrikeStunSeconds = hasStun ? Mathf.Max(0f, stun) : 0f;

            _officialSkillDataApplied =
                skillPointCost > 0f &&
                masteryAttackScale > 0f &&
                hasTimes &&
                hasStun &&
                !string.IsNullOrWhiteSpace(masteryRangeId);
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public void AddFinalDamagePercent(float value) =>
            _runtimeFinalDamageMultiplier = Mathf.Clamp(_runtimeFinalDamageMultiplier + Mathf.Max(0f, value), 1f, 4f);

        public void AddTargetingRadiusPercent(float value) =>
            _runtimeRadiusMultiplier = Mathf.Clamp(_runtimeRadiusMultiplier + Mathf.Max(0f, value), 1f, 2.5f);

        public void EnableKillRefresh(int maxExtraStrikes)
        {
            _killRefreshStrikeCap = Mathf.Max(
                _killRefreshStrikeCap,
                Mathf.Clamp(maxExtraStrikes, 1, 8));
        }

        public void ResetRunModifiers()
        {
            StopAllCoroutines();
            IsCasting = false;
            _bonusStrikeCount = 0;
            _runtimeFinalDamageMultiplier = 1f;
            _runtimeRadiusMultiplier = 1f;
            _killRefreshStrikeCap = 0;
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
            CastEnded?.Invoke();
        }

        public void InterruptCast()
        {
            if (!IsCasting)
                return;
            StopAllCoroutines();
            IsCasting = false;
            CastEnded?.Invoke();
        }

        private IEnumerator CastRoutine()
        {
            IsCasting = true;
            var startedAt = Time.time;

            if (startupSeconds > 0f)
                yield return new WaitForSeconds(startupSeconds);

            var totalStrikes = Mathf.Max(1, strikeCount + _bonusStrikeCount);
            var refreshedStrikes = 0;
            for (var i = 0;
                 i < totalStrikes + refreshedStrikes && _entity?.Health != null && !_entity.Health.IsDead;
                 i++)
            {
                var target = FindNearestTarget();
                // Jueying is a target-driven skill. Do not keep the cast lock or spawn empty
                // strike timings after the room has been cleared or the target leaves range/LOS.
                if (target == null)
                    break;

                var isFinal = i == totalStrikes + refreshedStrikes - 1;
                if (!_officialSkillDataApplied || masteryAttackScale <= 0f || _operatorStats == null)
                    break;
                var officialDamage = _operatorStats.Attack * masteryAttackScale;
                var damage = isFinal
                    ? officialDamage * _runtimeFinalDamageMultiplier
                    : officialDamage;
                var knockback = Vector2.zero;
                if (_motor25D == null)
                {
                    var direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                    knockback = isFinal ? direction * 3.5f : Vector2.zero;
                }

                var context = new DamageContext(
                    _entity,
                    _entity,
                    target,
                    damage,
                    DamageType.Physical,
                    knockback,
                    sourceId: isFinal ? "Chen_Skill2_Final" : "Chen_Skill2_Strike",
                    tags: DamageTags.Skill);
                var result = DamageSystem.Apply(context);
                if (result.Applied)
                {
                    if (result.Killed && refreshedStrikes < _killRefreshStrikeCap)
                        refreshedStrikes++;

                    target.GetComponentInChildren<HitFlash2D>()?.Flash();
                    if (isFinal &&
                        finalStrikeStunSeconds > 0f &&
                        target.Health != null &&
                        !target.Health.IsDead)
                    {
                        target.Status?.Apply(
                            CombatStatusIds.Stun,
                            finalStrikeStunSeconds,
                            _entity,
                            _entity,
                            sourceId: "Chen_Skill2_FinalStun");
                    }
                    HitStopService.Instance?.Request(isFinal ? 0.040f : 0.012f);
                    CameraShake2D.Instance?.Shake(isFinal ? 0.10f : 0.025f, 0.04f);
                }

                // The strike presentation is target-driven, but it must not disappear just
                // because combat rules rejected the damage application.
                StrikeResolved?.Invoke(i, target.transform, isFinal);

                if (i < totalStrikes + refreshedStrikes - 1)
                {
                    // Do not spend another strike interval in an already-cleared room. The last
                    // hit can kill the final target, so check before yielding instead of waiting
                    // once more and only discovering the empty target set on the next iteration.
                    if (FindNearestTarget() == null)
                        break;
                    yield return new WaitForSeconds(strikeInterval);
                }
            }

            var targetWasLost = FindNearestTarget() == null;
            var remaining = castLockSeconds - (Time.time - startedAt);
            if (!targetWasLost && remaining > 0f)
                yield return new WaitForSeconds(remaining);
            EndCast();
        }

        private void EndCast()
        {
            if (!IsCasting)
                return;

            IsCasting = false;
            CastEnded?.Invoke();
        }

        private CombatEntity FindNearestTarget()
        {
            return _motor25D != null ? FindNearestTarget25D() : FindNearestTarget2D();
        }

        private CombatEntity FindNearestTarget2D()
        {
            var radius = targetingRadius * ResolveSkillRangeMultiplier();
            var colliders = Physics2D.OverlapCircleAll(transform.position, radius);
            CombatEntity best = null;
            var bestSqr = float.PositiveInfinity;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (!CanTarget(candidate))
                    continue;

                var sqr = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = candidate;
            }

            return best;
        }

        private CombatEntity FindNearestTarget25D()
        {
            var radius = targetingRadius * ResolveSkillRangeMultiplier();
            var colliders = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            CombatEntity best = null;
            var bestSqr = float.PositiveInfinity;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (!CanTarget(candidate))
                    continue;
                if (Mathf.Abs(candidate.transform.position.y - transform.position.y) > max25DHeightDifference)
                    continue;
                if (!HasClear25DLine(candidate))
                    continue;

                var delta = candidate.transform.position - transform.position;
                delta.y = 0f;
                var sqr = delta.sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = candidate;
            }

            return best;
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
                var hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;
                var entity = hitCollider.GetComponentInParent<CombatEntity>();
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

        private float ResolveSkillRangeMultiplier()
        {
            var pipeline = _operatorStats != null
                ? _operatorStats.GetSkillRangeMultiplier(MasterySlot)
                : 1f;
            return Mathf.Clamp(pipeline * _runtimeRadiusMultiplier, 0.25f, 4f);
        }

        private bool CanTarget(CombatEntity candidate)
        {
            if (candidate == null ||
                candidate == _entity ||
                candidate.Team == _entity.Team ||
                candidate.Health == null ||
                candidate.Health.IsDead)
                return false;

            if (_motor25D == null || string.IsNullOrWhiteSpace(masteryRangeId))
                return true;

            var forward = _motor25D.PlanarForward;
            var worldReach = targetingRadius * ResolveSkillRangeMultiplier();
            return OperatorRangeUtility.Contains(
                masteryRangeId,
                transform.position,
                forward,
                candidate.transform.position,
                worldReach);
        }
    }
}
