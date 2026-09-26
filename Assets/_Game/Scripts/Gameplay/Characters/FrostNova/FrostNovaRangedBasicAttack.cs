using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    [DisallowMultipleComponent]
    public sealed class FrostNovaRangedBasicAttack :
        MonoBehaviour,
        IPlayerBasicAttackResolver,
        IPlayerBasicAttackTargetProvider,
        IPlayerBasicAttackImpactTimingProvider
    {
        [SerializeField, Min(1f)] private float baseRange = 10.5f;
        [SerializeField, Range(-1f, 1f)] private float minimumForwardDot = 0.05f;
        [SerializeField, Min(0.1f)] private float maxHeightDifference = 2.2f;
        [SerializeField, Min(0.01f)] private float lineRadius = 0.10f;

        private CombatEntity _entity;
        private IPlayerLocomotion _locomotion;
        private FrostNovaTuningProfile _tuning;
        private CombatEntity _pendingTarget;
        private bool _hasPendingTarget;

        public float CurrentVisualRange => baseRange;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _locomotion = FindLocomotion();
            RefreshTuning();
        }

        public void RefreshTuning()
        {
            _tuning = Resources.Load<FrostNovaTuningProfile>(
                FrostNovaTuningProfile.ResourcePath);
        }

        public float GetBasicAttackImpactSeconds(AttackDefinition definition)
        {
            if (_tuning == null)
                RefreshTuning();

            return _tuning != null
                ? _tuning.BasicAttackImpactSeconds
                : -1f;
        }

        public bool TryGetAimTarget(out CombatEntity target)
        {
            if (_hasPendingTarget && IsPendingTargetValid(_entity, _pendingTarget, 1f))
            {
                target = _pendingTarget;
                return true;
            }

            return TryAcquireBasicAttackTarget(_entity, _locomotion, 1f, out target);
        }

        public bool TryAcquireBasicAttackTarget(
            CombatEntity attacker,
            IPlayerLocomotion locomotion,
            float rangeMultiplier,
            out CombatEntity target)
        {
            target = FindBestTarget(attacker, locomotion, Mathf.Max(0.1f, rangeMultiplier));
            _pendingTarget = target;
            _hasPendingTarget = true;
            return target != null;
        }

        public Vector3 GetMissDestination()
        {
            var forward = ResolveForward(_locomotion);
            return transform.position + Vector3.up * 0.82f + forward * Mathf.Max(1f, baseRange);
        }

        public bool TryResolveBasicAttack(
            CombatEntity attacker,
            AttackDefinition definition,
            IPlayerLocomotion locomotion,
            float finalDamage,
            float rangeMultiplier,
            out CombatEntity hitTarget)
        {
            if (_hasPendingTarget)
            {
                hitTarget = IsPendingTargetValid(attacker, _pendingTarget, Mathf.Max(0.1f, rangeMultiplier))
                    ? _pendingTarget
                    : null;
                _pendingTarget = null;
                _hasPendingTarget = false;
            }
            else
            {
                hitTarget = FindBestTarget(attacker, locomotion, Mathf.Max(0.1f, rangeMultiplier));
            }

            if (hitTarget == null)
                return false;

            var context = new DamageContext(
                attacker,
                attacker,
                hitTarget,
                Mathf.Max(0f, finalDamage),
                DamageType.Arts,
                Vector2.zero,
                sourceId: definition != null ? definition.name : "FrostNova_BasicShot",
                tags: DamageTags.BasicAttack);

            if (!DamageSystem.Apply(context).Applied)
            {
                hitTarget = null;
                return false;
            }

            return true;
        }

        private CombatEntity FindBestTarget(
            CombatEntity attacker,
            IPlayerLocomotion locomotion,
            float rangeMultiplier = 1f)
        {
            var forward = ResolveForward(locomotion);
            var origin = transform.position + Vector3.up * 0.82f;
            var range = Mathf.Max(1f, baseRange * rangeMultiplier);

            return RangedBasicAttackTargeting.TryFindBestTarget(
                attacker,
                transform.position,
                range,
                maxHeightDifference,
                forward,
                minimumForwardDot,
                forwardPenaltyWeight: 1f,
                extraFilter: candidate => HasClearLine(attacker, candidate, origin),
                out var target)
                ? target
                : null;
        }

        private bool IsPendingTargetValid(
            CombatEntity attacker,
            CombatEntity target,
            float rangeMultiplier)
        {
            if (!RangedBasicAttackTargeting.IsValidEnemy(attacker, target))
                return false;

            var delta = target.transform.position - transform.position;
            var height = Mathf.Abs(delta.y);
            delta.y = 0f;
            var range = Mathf.Max(1f, baseRange * rangeMultiplier);
            if (height > maxHeightDifference || delta.sqrMagnitude < 0.001f || delta.sqrMagnitude > range * range)
                return false;

            var forward = ResolveForward(_locomotion);
            var dot = Vector3.Dot(forward, delta.normalized);
            return dot >= minimumForwardDot &&
                   HasClearLine(attacker, target, transform.position + Vector3.up * 0.82f);
        }

        private static Vector3 ResolveForward(IPlayerLocomotion locomotion)
        {
            var forward = locomotion != null ? locomotion.PlanarForward : Vector3.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;
            return forward.normalized;
        }

        private bool HasClearLine(CombatEntity attacker, CombatEntity target, Vector3 origin)
        {
            var destination = target.transform.position + Vector3.up * 0.76f;
            var cast = destination - origin;
            var distance = cast.magnitude;
            if (distance < 0.001f)
                return true;

            var hits = Physics.SphereCastAll(
                origin,
                lineRadius,
                cast / distance,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(attacker.transform))
                    continue;

                var entity = collider.GetComponentInParent<CombatEntity>();
                if (entity != null)
                    return entity == target;

                return false;
            }

            return true;
        }

        private IPlayerLocomotion FindLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            return null;
        }

        private static bool CanTarget(CombatEntity attacker, CombatEntity target)
        {
            return target != null &&
                   target != attacker &&
                   target.Team != attacker.Team &&
                   target.Health != null &&
                   !target.Health.IsDead;
        }
    }
}
