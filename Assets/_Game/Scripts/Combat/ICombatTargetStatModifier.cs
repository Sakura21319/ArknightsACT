namespace ArknightsACT.Combat
{
    /// <summary>
    /// Attacker/owner-side aura that changes the target stat used by the current damage
    /// calculation. This is distinct from a status on the target and from per-hit penetration.
    /// </summary>
    public interface ICombatTargetStatModifier
    {
        float ModifyTargetStat(in DamageContext context, CombatStatType stat, float currentValue);
    }
}
