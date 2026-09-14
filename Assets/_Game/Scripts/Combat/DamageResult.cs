namespace ArknightsACT.Combat
{
    public readonly struct DamageResult
    {
        public bool Applied { get; }
        public float Damage { get; }
        public bool Killed { get; }

        public DamageResult(bool applied, float damage, bool killed)
        {
            Applied = applied;
            Damage = damage;
            Killed = killed;
        }

        public static DamageResult Rejected => new(false, 0f, false);
    }
}
