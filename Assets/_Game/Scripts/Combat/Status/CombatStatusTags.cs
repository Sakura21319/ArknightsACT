using System;

namespace ArknightsACT.Combat.Status
{
    [Flags]
    public enum CombatStatusTags
    {
        None = 0,
        Buff = 1 << 0,
        Debuff = 1 << 1,
        CrowdControl = 1 << 2,
        HardCrowdControl = 1 << 3,
        Elemental = 1 << 4,
        MovementImpair = 1 << 5,
        AttackImpair = 1 << 6,
        SkillImpair = 1 << 7,
        DamageOverTime = 1 << 8,
        Dispellable = 1 << 9,
        Undispellable = 1 << 10
    }
}
