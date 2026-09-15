namespace ArknightsACT.Gameplay.Rooms
{
    public readonly struct CombatRoomTuning
    {
        public CombatRoomTuning(
            int enemyCountBonus,
            int enemyCountOverride,
            float healthMultiplier,
            bool enableRangedEarly,
            int forcedTemplateIndex,
            string label)
        {
            EnemyCountBonus = enemyCountBonus;
            EnemyCountOverride = enemyCountOverride;
            HealthMultiplier = healthMultiplier <= 0f ? 1f : healthMultiplier;
            EnableRangedEarly = enableRangedEarly;
            ForcedTemplateIndex = forcedTemplateIndex;
            Label = string.IsNullOrWhiteSpace(label) ? "Normal" : label;
        }

        public int EnemyCountBonus { get; }
        public int EnemyCountOverride { get; }
        public float HealthMultiplier { get; }
        public bool EnableRangedEarly { get; }
        public int ForcedTemplateIndex { get; }
        public string Label { get; }

        public static CombatRoomTuning Default => new(0, 0, 1f, false, -1, "Normal");
    }
}
