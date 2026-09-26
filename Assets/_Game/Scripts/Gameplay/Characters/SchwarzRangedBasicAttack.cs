using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    /// <summary>
    /// True ranged basic attack for Schwarz. Selection is exposed to the FX layer at attack start,
    /// then reused by the damage resolver at impact time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SchwarzRangedBasicAttack :
        MonoBehaviour,
        IPlayerBasicAttackResolver,
        IPlayerBasicAttackTargetProvider
    {
        [SerializeField, Min(1f)] private float baseRange = 12f;
        [SerializeField, Range(-1f, 1f)] private float minimumForwardDot = 0.18f;
        [SerializeField, Min(0.1f)] private float maxHeightDifference = 1.6f;
        [SerializeField, Min(0.01f)] private float lineRadius = 0.08f;

        private CombatEntity _entity;
        private IPlayerLocomotion _locomotion;
        private CombatEntity _pendingTarget;
        private bool _hasPendingTarget;

        public float CurrentVisualRange => baseRange * GetCurrentRangeMultiplier();

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _locomotion = FindLocomotion();
        }

        public bool TryGetAimTarget(out CombatEntity target)
        {
            if (_hasPendingTarget && IsPendingTargetValid(_entity, _pendingTarget, GetCurrentRangeMultiplier()))
            {
                target = _pendingTarget;
                return true;
            }

            return TryAcquireBasicAttackTarget(
                _entity,
                _locomotion,
                GetCurrentRangeMultiplier(),
                out target);
        }

        public bool TryAcquireBasicAttackTarget(
            CombatEntity attacker,
            IPlayerLocomotion locomotion,
            float rangeMultiplier,
            out CombatEntity target)
        {
            target = FindBestTarget(attacker, locomotion, rangeMultiplier);
            _pendingTarget = target;
            _hasPendingTarget = true;
            return target != null;
        }

        public Vector3 GetMissDestination()
        {
            var forward = ResolveForward(_locomotion);
            return transform.position + Vector3.up * 0.76f + forward * Mathf.Max(1f, CurrentVisualRange);
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
                hitTarget = IsPendingTargetValid(attacker, _pendingTarget, rangeMultiplier)
                    ? _pendingTarget
                    : null;
                _pendingTarget = null;
                _hasPendingTarget = false;
            }
            else
            {
                hitTarget = FindBestTarget(attacker, locomotion, rangeMultiplier);
            }

            if (hitTarget == null)
                return false;

            var context = new DamageContext(
                attacker,
                attacker,
                hitTarget,
                Mathf.Max(0f, finalDamage),
                DamageType.Physical,
                Vector2.zero,
                sourceId: definition != null ? definition.name : "Schwarz_BasicShot",
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
            float rangeMultiplier)
        {
            var forward = ResolveForward(locomotion);
            var origin = transform.position + Vector3.up * 0.76f;
            var range = Mathf.Max(1f, baseRange * Mathf.Max(0.1f, rangeMultiplier));

            return RangedBasicAttackTargeting.TryFindBestTarget(
                attacker,
                transform.position,
                range,
                maxHeightDifference,
                forward,
                minimumForwardDot,
                forwardPenaltyWeight: 2f,
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
            var range = Mathf.Max(1f, baseRange * Mathf.Max(0.1f, rangeMultiplier));
            if (height > maxHeightDifference || delta.sqrMagnitude < 0.001f || delta.sqrMagnitude > range * range)
                return false;

            var forward = ResolveForward(_locomotion);
            var dot = Vector3.Dot(forward, delta.normalized);
            return dot >= minimumForwardDot &&
                   HasClearLine(attacker, target, transform.position + Vector3.up * 0.76f);
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

        private float GetCurrentRangeMultiplier()
        {
            var multiplier = 1f;
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerBasicAttackModifier modifier)
                    multiplier *= Mathf.Max(0.1f, modifier.BasicAttackRangeMultiplier);
            return Mathf.Clamp(multiplier, 0.25f, 4f);
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
