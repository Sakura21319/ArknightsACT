using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    internal static class FrostNovaCombatUtility
    {
        public static int DamageEnemiesInRadius(
            CombatEntity source,
            Vector3 center,
            float radius,
            float damage,
            string sourceId,
            string statusId = null,
            float statusDuration = 0f)
        {
            if (source == null || radius <= 0f || damage <= 0f)
                return 0;

            var hits = Physics.OverlapSphere(
                center,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);
            var visited = new HashSet<CombatEntity>();
            var applied = 0;

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i];
                if (collider == null)
                    continue;

                var target = collider.GetComponentInParent<CombatEntity>();
                if (target == null ||
                    target == source ||
                    target.Team == source.Team ||
                    target.Health == null ||
                    target.Health.IsDead ||
                    !visited.Add(target))
                    continue;

                var context = new DamageContext(
                    source,
                    source,
                    target,
                    damage,
                    DamageType.Arts,
                    Vector2.zero,
                    sourceId: sourceId,
                    tags: DamageTags.Skill);

                if (DamageSystem.Apply(context).Applied)
                {
                    applied++;
                    if (!string.IsNullOrWhiteSpace(statusId) && target.Status != null)
                    {
                        target.Status.Apply(
                            statusId,
                            duration: statusDuration,
                            source: source,
                            owner: target,
                            sourceId: sourceId);
                    }
                }
            }

            return applied;
        }

        public static bool TryFindNearestEnemy(
            CombatEntity source,
            Vector3 origin,
            float radius,
            out CombatEntity target)
        {
            target = null;
            if (source == null || radius <= 0f)
                return false;

            var hits = Physics.OverlapSphere(
                origin,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);
            var bestDistanceSq = float.PositiveInfinity;
            var visited = new HashSet<CombatEntity>();

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i];
                if (collider == null)
                    continue;

                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (candidate == null ||
                    candidate == source ||
                    candidate.Team == source.Team ||
                    candidate.Health == null ||
                    candidate.Health.IsDead ||
                    !visited.Add(candidate))
                    continue;

                var distanceSq = (candidate.transform.position - origin).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        public static Vector3 ResolveForward(IPlayerLocomotion locomotion)
        {
            var forward = locomotion != null ? locomotion.PlanarForward : Vector3.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;
            return forward.normalized;
        }
    }
}
