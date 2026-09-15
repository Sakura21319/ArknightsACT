namespace ArknightsACT.Gameplay.Abilities
{
    public interface IPlayerSkill
    {
        int Slot { get; }
        string DisplayName { get; }
        float CooldownRemaining { get; }
        bool IsCasting { get; }
        bool TryCast();
        void ReduceCooldown(float seconds);
    }
}
