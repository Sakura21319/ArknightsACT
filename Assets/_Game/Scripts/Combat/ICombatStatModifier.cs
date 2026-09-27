namespace ArknightsACT.Combat
{
    /// <summary>
    /// Ordered stages applied after the authored/progression base value.
    /// Values intentionally leave gaps so future layers can be inserted without changing serialized meaning.
    /// </summary>
    public enum CombatStatModifierLayer
    {
        MetaProgression = 100,
        RunPermanent = 200,
        CollectibleEquipment = 300,
        Temporary = 400
    }

    /// <summary>
    /// Generic stat extension point. Implementations contribute flat and additive-percent
    /// modifiers without making CombatStats depend on a status, relic, equipment or character type.
    /// Legacy implementations that do not expose a layer are treated as Temporary.
    /// </summary>
    public interface ICombatStatModifier
    {
        void AccumulateStatModifiers(CombatStatType stat, ref float flat, ref float additivePercent);
    }

    public interface ILayeredCombatStatModifier : ICombatStatModifier
    {
        CombatStatModifierLayer ModifierLayer { get; }
    }
}
