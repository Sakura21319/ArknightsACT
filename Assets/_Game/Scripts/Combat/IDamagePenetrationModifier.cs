namespace ArknightsACT.Combat
{
    /// <summary>
    /// Adds per-hit penetration without making DamageSystem depend on a concrete operator,
    /// status, relic or equipment implementation.
    /// </summary>
    public interface IDamagePenetrationModifier
    {
        DamagePenetration ModifyPenetration(in DamageContext context, DamagePenetration current);
    }
}
