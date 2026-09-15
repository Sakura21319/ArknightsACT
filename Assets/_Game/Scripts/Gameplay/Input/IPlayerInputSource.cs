using UnityEngine;

namespace ArknightsACT.Gameplay.Input
{
    public interface IPlayerInputSource
    {
        Vector2 Move { get; }
        bool JumpPressedThisFrame { get; }
        bool AttackPressedThisFrame { get; }
        bool DashPressedThisFrame { get; }
        bool Skill1PressedThisFrame { get; }
        bool Skill2PressedThisFrame { get; }
    }
}
