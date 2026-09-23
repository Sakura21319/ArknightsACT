namespace ArknightsACT.Combat
{
    public interface ICombatActionInterruptHandler
    {
        void InterruptCombatActions(CombatActionMask actions);
    }
}
