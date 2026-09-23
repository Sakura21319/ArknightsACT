using System;

namespace ArknightsACT.Combat
{
    [Flags]
    public enum DamageTags
    {
        None = 0,
        BasicAttack = 1 << 0,
        Skill = 1 << 1,
        DamageOverTime = 1 << 2,
        Environment = 1 << 3,
        SecondaryProc = 1 << 4
    }
}
