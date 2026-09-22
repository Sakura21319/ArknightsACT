namespace ArknightsACT.Gameplay.Combat
{
    /// <summary>
    /// Optional character/buff contribution to generic player basic attacks.
    /// Values are multiplicative and are queried at hit time so temporary buffs stay decoupled
    /// from PlayerAttackController.
    /// </summary>
    public interface IPlayerBasicAttackModifier
    {
        float BasicAttackDamageMultiplier { get; }
        float BasicAttackRangeMultiplier { get; }
        float BasicAttackTimingMultiplier { get; }
    }
}
