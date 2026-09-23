using System;
using System.Collections.Generic;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Combat
{
    public static class AreaDamageResolver
    {
        public static int ApplyUnique(
            Collider2D[] colliders,
            CombatEntity source,
            CombatEntity owner,
            float damage,
            DamageType damageType,
            Vector2 knockback,
            string sourceId,
            Action<CombatEntity, DamageResult> onApplied = null,
            DamageTags tags = DamageTags.None,
            DamagePenetration penetration = default)
        {
            if (colliders == null || source == null)
                return 0;

            var processed = new HashSet<CombatEntity>();
            var appliedCount = 0;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;

                var target = collider.GetComponentInParent<CombatEntity>();
                if (target == null || target == source || target.Team == source.Team || !processed.Add(target))
                    continue;

                var context = new DamageContext(
                    source,
                    owner != null ? owner : source,
                    target,
                    damage,
                    damageType,
                    knockback,
                    sourceId: sourceId,
                    penetration: penetration,
                    tags: tags);

                var result = DamageSystem.Apply(context);
                if (!result.Applied)
                    continue;

                appliedCount++;
                onApplied?.Invoke(target, result);
            }

            return appliedCount;
        }
    }
}
