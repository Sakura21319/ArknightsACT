namespace ArknightsACT.Combat.Status
{
    public sealed class ActiveStatusInstance
    {
        public CombatStatusDefinition Definition { get; internal set; }
        public CombatEntity Source { get; internal set; }
        public CombatEntity Owner { get; internal set; }
        public int Stacks { get; internal set; }
        public float Magnitude { get; internal set; }
        public float ExpiresAt { get; internal set; }
        public float NextTickAt { get; internal set; }
        public DamageType PeriodicDamageType { get; internal set; }
        public int ProcGeneration { get; internal set; }
        public string SourceId { get; internal set; }

        public string Id => Definition?.Id ?? string.Empty;
    }
}
