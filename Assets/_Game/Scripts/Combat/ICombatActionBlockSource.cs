namespace ArknightsACT.Combat
{
    public interface ICombatActionBlockSource
    {
        CombatActionMask BlockedActions { get; }
    }
}
