namespace ArknightsACT.Gameplay.Abilities
{
    public interface IPlayerSkill
    {
        int Slot { get; }
        string DisplayName { get; }
        float CooldownRemaining { get; }
        float SkillPoints { get; }
        float SkillPointCost { get; }
        float SkillPointRatio { get; }
        float NaturalSkillPointPerSecond { get; }
        bool IsCasting { get; }
        bool TryCast();
        void TickSkillPoints(float deltaTime, float recoveryMultiplier, float flatRecoveryPerSecond);
        void GainSkillPoints(float amount);
        void ReduceCooldown(float seconds);
    }
}
