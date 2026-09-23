namespace ArknightsACT.Combat
{
    public readonly struct DamageResult
    {
        public bool Applied { get; }
        public float RawDamage { get; }
        public float PreMitigationDamage { get; }
        public float MitigatedDamage { get; }
        public float FinalDamage { get; }
        public float Damage => FinalDamage; // Legacy alias.
        public float EffectiveDefense { get; }
        public float EffectiveResistance { get; }
        public bool Killed { get; }

        public DamageResult(bool applied, float damage, bool killed)
            : this(applied, damage, damage, damage, damage, 0f, 0f, killed)
        {
        }

        public DamageResult(
            bool applied,
            float rawDamage,
            float preMitigationDamage,
            float mitigatedDamage,
            float finalDamage,
            float effectiveDefense,
            float effectiveResistance,
            bool killed)
        {
            Applied = applied;
            RawDamage = rawDamage;
            PreMitigationDamage = preMitigationDamage;
            MitigatedDamage = mitigatedDamage;
            FinalDamage = finalDamage;
            EffectiveDefense = effectiveDefense;
            EffectiveResistance = effectiveResistance;
            Killed = killed;
        }

        public static DamageResult Rejected => new(false, 0f, 0f, 0f, 0f, 0f, 0f, false);
    }
}
