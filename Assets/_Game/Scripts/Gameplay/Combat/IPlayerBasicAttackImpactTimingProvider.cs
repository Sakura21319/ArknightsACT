namespace ArknightsACT.Gameplay.Combat
{
    /// <summary>
    /// Optional per-character override for the moment a basic attack resolves damage.
    /// The generic attack cycle remains owned by PlayerAttackController; only the impact
    /// point is overridden so projectile characters can align damage with authored visuals.
    /// Return a negative value to use AttackDefinition.startup.
    /// </summary>
    public interface IPlayerBasicAttackImpactTimingProvider
    {
        float GetBasicAttackImpactSeconds(AttackDefinition definition);
    }
}
