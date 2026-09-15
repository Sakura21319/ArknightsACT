using System;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite
{
    [Flags]
    public enum CombatFeature
    {
        None = 0,
        BasicAttack = 1 << 0,
        ActiveSkills = 1 << 1,
        Dash = 1 << 2,
        PhysicalDamage = 1 << 3,
        ArtsDamage = 1 << 4,
        TrueDamage = 1 << 5
    }

    /// <summary>
    /// Declares what a playable character can actually use. Reward generation uses this profile
    /// to avoid dead choices without ever checking a concrete operator type or name.
    /// </summary>
    public sealed class PlayerCombatProfile : MonoBehaviour
    {
        [SerializeField] private CombatFeature features = CombatFeature.BasicAttack | CombatFeature.ActiveSkills;

        public CombatFeature Features => features;

        public void Configure(CombatFeature value) => features = value;

        public bool Supports(CombatFeature required) =>
            required == CombatFeature.None || (features & required) == required;
    }
}
