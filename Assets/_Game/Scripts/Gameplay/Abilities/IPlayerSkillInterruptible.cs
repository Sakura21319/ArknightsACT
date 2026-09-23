namespace ArknightsACT.Gameplay.Abilities
{
    /// <summary>
    /// Optional contract for skills whose current cast can be interrupted by a combat status.
    /// Persistent buff windows may remain active; implementations decide what "current cast" means.
    /// </summary>
    public interface IPlayerSkillInterruptible
    {
        void InterruptCast();
    }
}
