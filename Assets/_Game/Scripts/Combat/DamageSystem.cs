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

            var dealt = context.Target.Health.TakeDamage(context.BaseDamage);
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
    }
}
