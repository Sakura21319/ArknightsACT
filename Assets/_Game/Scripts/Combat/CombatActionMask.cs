using System;

namespace ArknightsACT.Combat
{
    [Flags]
    public enum CombatActionMask
    {
        None = 0,
        Movement = 1 << 0,
        Dash = 1 << 1,
        BasicAttack = 1 << 2,
        Skill = 1 << 3,
        Interaction = 1 << 4,
        Targeting = 1 << 5,
        All = Movement | Dash | BasicAttack | Skill | Interaction | Targeting
    }
}
