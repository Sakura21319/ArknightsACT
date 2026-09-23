using UnityEngine;

namespace ArknightsACT.Combat
{
    public readonly struct DamageContext
    {
        public CombatEntity Source { get; }
        public CombatEntity Owner { get; }
        public CombatEntity Target { get; }
        public float BaseDamage { get; }
        public DamageType DamageType { get; }
        public Vector2 Knockback { get; }
        public int ProcGeneration { get; }
        public string SourceId { get; }
        public DamagePenetration Penetration { get; }
        public DamageTags Tags { get; }

        public DamageContext(
            CombatEntity source,
            CombatEntity owner,
            CombatEntity target,
            float baseDamage,
            DamageType damageType,
            Vector2 knockback,
            int procGeneration = 0,
            string sourceId = "",
            DamagePenetration penetration = default,
            DamageTags tags = DamageTags.None)
        {
            Source = source;
            Owner = owner;
            Target = target;
            BaseDamage = Mathf.Max(0f, baseDamage);
            DamageType = damageType;
            Knockback = knockback;
            ProcGeneration = Mathf.Max(0, procGeneration);
            SourceId = sourceId ?? string.Empty;
            Penetration = penetration;
            Tags = tags;
        }
    }
}
