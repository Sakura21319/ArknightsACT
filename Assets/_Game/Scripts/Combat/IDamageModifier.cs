namespace ArknightsACT.Combat
{
    /// <summary>
    /// Generic damage extension point. Combat owns the pipeline while higher-level systems
    /// (stats, equipment, roguelite collectibles) may contribute modifiers through components.
    /// </summary>
    public interface IDamageModifier
    {
        float ModifyOutgoingDamage(in DamageContext context, float currentDamage);
        float ModifyIncomingDamage(in DamageContext context, float currentDamage);
    }
}
