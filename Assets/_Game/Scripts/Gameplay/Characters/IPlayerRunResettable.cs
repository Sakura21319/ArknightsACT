namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Character-specific runtime state that must be restored when a fresh run begins.
    /// </summary>
    public interface IPlayerRunResettable
    {
        void ResetForNewRun();
    }
}
