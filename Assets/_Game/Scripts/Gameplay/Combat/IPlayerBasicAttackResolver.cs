using System;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Combat
{
    /// <summary>
    /// Optional override for how a character resolves a basic attack hit.
    /// The generic attack controller still owns timing/combo/input and feedback; a resolver only
    /// chooses and damages the target. This keeps ranged operators out of the melee box logic.
    /// </summary>
    /// <summary>
    /// Optional pre-attack target acquisition hook for ranged/basic-attack characters.
    /// PlayerAttackController calls this before it starts an attack cycle. Returning false means
    /// the attack should not start (and therefore must not consume ammo or play attack FX).
    /// The provider may cache the selected target for its later hit resolver.
    /// </summary>
    /// <summary>
    /// Shared target selection for ranged operators. CombatEntity registration is authoritative:
    /// runtime-created targets (including the public training dummy) participate exactly like
    /// spawned enemies and no character has to rediscover them through collider topology.
    /// </summary>
    public static class RangedBasicAttackTargeting
    {
        public static bool TryFindBestTarget(
            CombatEntity attacker,
            Vector3 origin,
            float range,
            float maximumHeightDifference,
            Vector3 forward,
            float minimumForwardDot,
            float forwardPenaltyWeight,
            Func<CombatEntity, bool> extraFilter,
            out CombatEntity target)
        {
            target = null;
            if (attacker == null || attacker.Health == null || attacker.Health.IsDead)
                return false;

            var resolvedRange = Mathf.Max(0.1f, range);
            var rangeSq = resolvedRange * resolvedRange;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;
            forward.Normalize();

            var candidates = new HashSet<CombatEntity>();
            foreach (var entity in CombatEntity.ActiveEntities)
                if (entity != null)
                    candidates.Add(entity);

            var colliders = Physics.OverlapSphere(
                origin,
                resolvedRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                    continue;
                var entity = collider.GetComponentInParent<CombatEntity>();
                if (entity != null)
                    candidates.Add(entity);
            }

            var bestScore = float.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (!IsValidEnemy(attacker, candidate))
                    continue;

                var delta = candidate.transform.position - origin;
                if (Mathf.Abs(delta.y) > Mathf.Max(0.1f, maximumHeightDifference))
                    continue;

                delta.y = 0f;
                var distanceSq = delta.sqrMagnitude;
                if (distanceSq < 0.000001f || distanceSq > rangeSq)
                    continue;

                var distance = Mathf.Sqrt(distanceSq);
                var dot = Vector3.Dot(forward, delta / distance);
                if (dot < minimumForwardDot)
                    continue;
                if (extraFilter != null && !extraFilter(candidate))
                    continue;

                var score = distance +
                            Mathf.Max(0f, forwardPenaltyWeight) *
                            Mathf.Max(0f, 1f - dot) *
                            resolvedRange;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                target = candidate;
            }

            return target != null;
        }

        public static bool IsValidEnemy(CombatEntity attacker, CombatEntity candidate)
        {
            return candidate != null &&
                   candidate.isActiveAndEnabled &&
                   candidate.gameObject.activeInHierarchy &&
                   candidate != attacker &&
                   candidate.Team != Team.Neutral &&
                   candidate.Team != attacker.Team &&
                   candidate.Health != null &&
                   !candidate.Health.IsDead;
        }
    }

    public interface IPlayerBasicAttackTargetProvider
    {
        bool TryAcquireBasicAttackTarget(
            CombatEntity attacker,
            IPlayerLocomotion locomotion,
            float rangeMultiplier,
            out CombatEntity target);
    }

    public interface IPlayerBasicAttackResolver
    {
        bool TryResolveBasicAttack(
            CombatEntity attacker,
            AttackDefinition definition,
            IPlayerLocomotion locomotion,
            float finalDamage,
            float rangeMultiplier,
            out CombatEntity hitTarget);
    }
}
