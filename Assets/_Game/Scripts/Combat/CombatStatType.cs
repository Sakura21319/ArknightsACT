namespace ArknightsACT.Combat
{
    public enum CombatStatType
    {
        // Keep the first four numeric values stable because existing assets may serialize them.
        PhysicalDefense = 0,
        ArtsResistance = 1,
        MoveSpeedMultiplier = 2,
        AttackSpeedMultiplier = 3,

        // Playable-operator runtime stats. These are resolved through the same modifier pipeline
        // so future meta progression / in-run upgrades do not mutate authored PRTS base values.
        MaxHealth = 4,
        Attack = 5,
        AttackInterval = 6,
        BasicAttackRange = 7,
        SkillRange = 8,

        // Slot-specific runtime range multipliers used by roguelite permanent upgrades.
        // Keep appended numeric values stable for any future serialized stat-modifier assets.
        Skill1RangeMultiplier = 9,
        Skill2RangeMultiplier = 10
    }
}
