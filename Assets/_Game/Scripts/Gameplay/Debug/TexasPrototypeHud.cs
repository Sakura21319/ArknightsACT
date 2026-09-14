using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters.Texas;
using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    public sealed class TexasPrototypeHud : MonoBehaviour
    {
        private Health _health;
        private PlayerSkillController _skillController;
        private TexasBuildLab _buildLab;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _skillController = GetComponent<PlayerSkillController>();
            _buildLab = GetComponent<TexasBuildLab>();
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 350, 132), "Texas Combat Lab");
            GUI.Label(new Rect(24, 38, 320, 22), $"HP: {_health?.CurrentHealth:0}/{_health?.MaxHealth:0}");
            var skill = _skillController?.Skill;
            GUI.Label(new Rect(24, 60, 320, 22), $"L / RMB  Sword Rain   CD: {skill?.CooldownRemaining ?? 0f:0.0}s");
            GUI.Label(new Rect(24, 82, 320, 22), $"[1] Swift Blade: {OnOff(_buildLab?.SwiftBlade ?? false)}");
            GUI.Label(new Rect(24, 102, 320, 22), $"[2] Residual Thunder: {OnOff(_buildLab?.ResidualThunder ?? false)}");
            GUI.Label(new Rect(24, 122, 320, 22), $"[3] Conductive: {OnOff(_buildLab?.Conductive ?? false)}");
        }

        private static string OnOff(bool value) => value ? "ON" : "OFF";
    }
}
