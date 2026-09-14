namespace ArknightsACT.Gameplay.Combat
{
    /// <summary>
    /// Optional runtime timing source for a basic attack.
    /// Gameplay remains authoritative, but presentation can provide the real visual cycle
    /// length so one animation swing maps to exactly one damage pulse.
    /// </summary>
    public interface IAttackTimingProvider
    {
        bool TryGetBasicAttackTiming(out float impactSeconds, out float cycleSeconds);
    }
}
