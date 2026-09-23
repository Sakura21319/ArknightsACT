namespace ArknightsACT.Combat
{
    /// <summary>
    /// Generic stat extension point. Implementations contribute flat and additive-percent
    /// modifiers without making CombatStats depend on a status, relic, equipment or character type.
    /// </summary>
    public interface ICombatStatModifier
    {
        void AccumulateStatModifiers(CombatStatType stat, ref float flat, ref float additivePercent);
    }
}
