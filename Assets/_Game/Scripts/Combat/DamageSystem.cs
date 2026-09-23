using System;
using UnityEngine;

namespace ArknightsACT.Combat
{
    public static class DamageSystem
    {
        public const int MaxProcGeneration = 4;
        public const float PhysicalMinimumDamageRatio = 0.05f;
        public const float MinArtsResistance = -100f;
        public const float MaxArtsResistance = 95f;

        public static event Action<DamageContext, DamageResult> DamageApplied;

        public static DamageResult Apply(in DamageContext context)
        {
            if (context.Target == null || context.Target.Health == null || context.Target.Health.IsDead)
                return DamageResult.Rejected;
            if (context.ProcGeneration > MaxProcGeneration)
                return DamageResult.Rejected;
            if (context.Source != null && context.Source.Team != Team.Neutral && context.Source.Team == context.Target.Team)
                return DamageResult.Rejected;

            var behaviours = context.Target.GetComponents<MonoBehaviour>();
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IDamageGate gate && !gate.CanReceiveDamage(context))
                    return DamageResult.Rejected;
            }

            var rawDamage = context.BaseDamage;
            var preMitigationDamage = ApplyOutgoingModifiers(context.Source, context, rawDamage);
            if (context.Owner != null && context.Owner != context.Source)
                preMitigationDamage = ApplyOutgoingModifiers(context.Owner, context, preMitigationDamage);
            preMitigationDamage = Mathf.Max(0f, preMitigationDamage);

            if (preMitigationDamage <= 0f)
                return DamageResult.Rejected;

            var penetration = ResolvePenetration(context);
            var mitigatedDamage = ApplyMitigation(
                context,
                penetration,
                preMitigationDamage,
                out var effectiveDefense,
                out var effectiveResistance);

            var finalDamage = ApplyIncomingModifiers(context.Target, context, mitigatedDamage);
            finalDamage = Mathf.Max(0f, finalDamage);
            if (finalDamage <= 0f)
                return DamageResult.Rejected;

            var dealt = context.Target.Health.TakeDamage(finalDamage);
            if (dealt <= 0f)
                return DamageResult.Rejected;

            var result = new DamageResult(
                true,
                rawDamage,
                preMitigationDamage,
                mitigatedDamage,
                dealt,
                effectiveDefense,
                effectiveResistance,
                context.Target.Health.IsDead);

            context.Target.NotifyDamaged(context, result);
            DamageApplied?.Invoke(context, result);

            if (!result.Killed)
            {
                var body = context.Target.GetComponent<Rigidbody2D>();
                if (body != null && body.bodyType == RigidbodyType2D.Dynamic && context.Knockback.sqrMagnitude > 0.001f)
                    body.AddForce(context.Knockback, ForceMode2D.Impulse);
            }

            return result;
        }

        private static float ApplyMitigation(
            in DamageContext context,
            in DamagePenetration penetration,
            float damage,
            out float effectiveDefense,
            out float effectiveResistance)
        {
            effectiveDefense = 0f;
            effectiveResistance = 0f;
            var stats = context.Target != null ? context.Target.Stats : null;

            switch (context.DamageType)
            {
                case DamageType.Physical:
                {
                    var defense = stats != null ? stats.PhysicalDefense : 0f;
                    defense = ApplyTargetStatModifiers(
                        context.Source,
                        context,
                        CombatStatType.PhysicalDefense,
                        defense);
                    if (context.Owner != null && context.Owner != context.Source)
                    {
                        defense = ApplyTargetStatModifiers(
                            context.Owner,
                            context,
                            CombatStatType.PhysicalDefense,
                            defense);
                    }

                    effectiveDefense = Mathf.Max(
                        0f,
                        defense * (1f - penetration.PhysicalDefenseIgnorePercent) -
                        penetration.PhysicalDefenseIgnoreFlat);

                    var minimum = damage * PhysicalMinimumDamageRatio;
                    return Mathf.Max(minimum, damage - effectiveDefense);
                }

                case DamageType.Arts:
                {
                    var resistance = stats != null ? stats.ArtsResistance : 0f;
                    resistance = ApplyTargetStatModifiers(
                        context.Source,
                        context,
                        CombatStatType.ArtsResistance,
                        resistance);
                    if (context.Owner != null && context.Owner != context.Source)
                    {
                        resistance = ApplyTargetStatModifiers(
                            context.Owner,
                            context,
                            CombatStatType.ArtsResistance,
                            resistance);
                    }

                    effectiveResistance = Mathf.Clamp(
                        resistance * (1f - penetration.ArtsResistanceIgnorePercent) -
                        penetration.ArtsResistanceIgnoreFlat,
                        MinArtsResistance,
                        MaxArtsResistance);

                    return damage * (1f - effectiveResistance / 100f);
                }

                case DamageType.True:
                default:
                    return damage;
            }
        }

        private static float ApplyTargetStatModifiers(
            CombatEntity entity,
            in DamageContext context,
            CombatStatType stat,
            float currentValue)
        {
            if (entity == null)
                return currentValue;

            var behaviours = entity.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatTargetStatModifier modifier)
                    currentValue = modifier.ModifyTargetStat(context, stat, currentValue);
            }

            return currentValue;
        }

        private static DamagePenetration ResolvePenetration(in DamageContext context)
        {
            var result = context.Penetration;
            result = ApplyPenetrationModifiers(context.Source, context, result);
            if (context.Owner != null && context.Owner != context.Source)
                result = ApplyPenetrationModifiers(context.Owner, context, result);
            return result;
        }

        private static DamagePenetration ApplyPenetrationModifiers(
            CombatEntity entity,
            in DamageContext context,
            DamagePenetration penetration)
        {
            if (entity == null)
                return penetration;

            var behaviours = entity.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamagePenetrationModifier modifier)
                    penetration = modifier.ModifyPenetration(context, penetration);
            }
            return penetration;
        }

        private static float ApplyOutgoingModifiers(CombatEntity entity, in DamageContext context, float damage)
        {
            if (entity == null)
                return damage;

            var behaviours = entity.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageModifier modifier)
                    damage = modifier.ModifyOutgoingDamage(context, damage);
            }
            return damage;
        }

        private static float ApplyIncomingModifiers(CombatEntity entity, in DamageContext context, float damage)
        {
            if (entity == null)
                return damage;

            var behaviours = entity.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageModifier modifier)
                    damage = modifier.ModifyIncomingDamage(context, damage);
            }
            return damage;
        }
    }
}
