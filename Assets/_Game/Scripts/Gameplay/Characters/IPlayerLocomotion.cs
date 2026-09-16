using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Dimension-agnostic player locomotion contract used by combat, skills and presentation.
    /// 2D side-view and 2.5D XZ motors expose the same gameplay-facing state.
    /// </summary>
    public interface IPlayerLocomotion
    {
        bool IsGrounded { get; }
        bool IsMoving { get; }
        int FacingSign { get; }
        Vector3 PlanarForward { get; }
        float PlanarSpeed { get; }
    }
}
