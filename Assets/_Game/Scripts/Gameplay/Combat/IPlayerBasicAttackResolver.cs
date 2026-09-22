using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Combat
{
    /// <summary>
    /// Optional override for how a character resolves a basic attack hit.
    /// The generic attack controller still owns timing/combo/input and feedback; a resolver only
    /// chooses and damages the target. This keeps ranged operators out of the melee box logic.
    /// </summary>
    public interface IPlayerBasicAttackResolver
    {
        bool TryResolveBasicAttack(
            CombatEntity attacker,
            AttackDefinition definition,
            IPlayerLocomotion locomotion,
            float finalDamage,
            float rangeMultiplier,
            out CombatEntity hitTarget);
    }
}
