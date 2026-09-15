using UnityEngine;

namespace ArknightsACT.Combat
{
    public static class DamageSystem
    {
        public const int MaxProcGeneration = 4;

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

            var damage = context.BaseDamage;
            damage = ApplyOutgoingModifiers(context.Source, context, damage);
            if (context.Owner != null && context.Owner != context.Source)
                damage = ApplyOutgoingModifiers(context.Owner, context, damage);
            damage = ApplyIncomingModifiers(context.Target, context, damage);
            damage = Mathf.Max(0f, damage);

            if (damage <= 0f)
                return DamageResult.Rejected;

            var dealt = context.Target.Health.TakeDamage(damage);
            if (dealt <= 0f)
                return DamageResult.Rejected;

            var result = new DamageResult(true, dealt, context.Target.Health.IsDead);
            context.Target.NotifyDamaged(context, result);

            // A lethal hit enters the target's death state during TakeDamage. Do not apply a
            // later physics impulse that can make the corpse slide while its Die clip is playing.
            if (!result.Killed)
            {
                var body = context.Target.GetComponent<Rigidbody2D>();
                if (body != null && body.bodyType == RigidbodyType2D.Dynamic && context.Knockback.sqrMagnitude > 0.001f)
                    body.AddForce(context.Knockback, ForceMode2D.Impulse);
            }

            return result;
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
