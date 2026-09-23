namespace ArknightsACT.Gameplay.Combat
{
    /// <summary>
    /// Optional per-character presentation lock for basic-attack movement.
    /// This lets ranged characters release movement as soon as the authored firing
    /// animation is complete while the projectile/damage timeline can continue independently.
    /// </summary>
    public interface IPlayerBasicAttackMovementLockProvider
    {
        bool IsBasicAttackMovementLocked { get; }
    }
}
