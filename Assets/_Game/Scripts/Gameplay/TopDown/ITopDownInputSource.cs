using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Input contract for the isolated top-down prototype. Gameplay reads intent only;
    /// desktop/gamepad/mobile adapters can be swapped without changing combat code.
    /// </summary>
    public interface ITopDownInputSource
    {
        Vector2 Move { get; }
        Vector2 AimStick { get; }
        Vector2 PointerScreenPosition { get; }
        bool HasPointer { get; }
        bool AttackPressedThisFrame { get; }
        bool AttackHeld { get; }
        bool DashPressedThisFrame { get; }
        bool SkillPressedThisFrame { get; }
    }
}
