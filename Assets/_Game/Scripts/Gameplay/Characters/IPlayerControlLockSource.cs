namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Optional character state that temporarily owns player controls.
    /// Generic movement/dash/attack systems query this contract without knowing the operator.
    /// </summary>
    public interface IPlayerControlLockSource
    {
        bool BlocksMovement { get; }
        bool BlocksDash { get; }
        bool BlocksBasicAttack { get; }
    }
}
