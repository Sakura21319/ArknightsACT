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
            var currentHealth = _health != null ? _health.CurrentHealth : 0f;
            var maxHealth = _health != null ? _health.MaxHealth : 0f;
            var skill = _skillController != null ? _skillController.Skill : null;
            var cooldown = skill != null ? skill.CooldownRemaining : 0f;

            GUI.Box(new Rect(12, 12, 390, 150), "Texas Combat Prototype");
            GUI.Label(new Rect(24, 38, 350, 22), $"HP: {currentHealth:0}/{maxHealth:0}");
            GUI.Label(new Rect(24, 60, 350, 22), $"L / RMB  Sword Rain   CD: {cooldown:0.0}s");
            GUI.Label(new Rect(24, 82, 350, 22), "Clear all enemies to choose one upgrade.");
            GUI.Label(new Rect(24, 104, 350, 22), $"Swift Blade: {OnOff(_buildLab != null && _buildLab.SwiftBlade)}");
            GUI.Label(new Rect(24, 124, 350, 22), $"Residual Thunder: {OnOff(_buildLab != null && _buildLab.ResidualThunder)}");
            GUI.Label(new Rect(24, 144, 350, 22), $"Conductive: {OnOff(_buildLab != null && _buildLab.Conductive)}");
        }

        private static string OnOff(bool value) => value ? "ON" : "OFF";
    }
}
