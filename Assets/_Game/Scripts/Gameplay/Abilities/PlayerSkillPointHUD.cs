using UnityEngine;

namespace ArknightsACT.Gameplay.Abilities
{
    /// <summary>
    /// Legacy scene-compatibility shell. The IMGUI implementation has been retired.
    /// GameplayHUDController is now the only player HP / skill HUD.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSkillController))]
    public sealed class PlayerSkillPointHUD : MonoBehaviour
    {
    }
}
