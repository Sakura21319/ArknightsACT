namespace ArknightsACT.Gameplay.Abilities
{
    public interface IPlayerSkill
    {
        string DisplayName { get; }
        float CooldownRemaining { get; }
        bool TryCast();
        void ReduceCooldown(float seconds);
    }
}
