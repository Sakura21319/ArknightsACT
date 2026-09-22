namespace ArknightsACT.Gameplay.Abilities
{
    /// <summary>
    /// Optional state contract for duration-based active skills.
    /// Used by presentation/HUD without coupling generic systems to a concrete operator.
    /// </summary>
    public interface IPlayerSkillActiveState
    {
        bool IsActive { get; }
    }
}
