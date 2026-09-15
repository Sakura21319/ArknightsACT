namespace ArknightsACT.Gameplay.Combat
{
    /// <summary>
    /// Readable attack-action states. Gameplay branches on movement state first; presentation
    /// can then animate the same authoritative action without guessing from VFX or combo index.
    /// </summary>
    public enum PlayerAttackActionType
    {
        GroundLight1,
        GroundLight2,
        GroundHeavy3,
        DashSlash,
        AirSlash,
        Plunge
    }
}
