namespace ArknightsACT.Combat
{
    /// <summary>
    /// Optional target-side rule used by DamageSystem before health is modified.
    /// Gameplay modules can implement invulnerability, shields or parry without
    /// making the Combat assembly depend on player-specific code.
    /// </summary>
    public interface IDamageGate
    {
        bool CanReceiveDamage(in DamageContext context);
    }
}
