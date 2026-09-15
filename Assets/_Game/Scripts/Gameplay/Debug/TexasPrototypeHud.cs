using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters.Texas;
using ArknightsACT.Gameplay.Rooms;
using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    public sealed class TexasPrototypeHud : MonoBehaviour
    {
        private Health _health;
        private PlayerSkillController _skillController;
        private TexasBuildLab _buildLab;
        private PrototypeRoomLoopController _roomLoop;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _skillController = GetComponent<PlayerSkillController>();
            _buildLab = GetComponent<TexasBuildLab>();
            _roomLoop = FindFirstObjectByType<PrototypeRoomLoopController>();
        }

        private void OnGUI()
        {
            if (_roomLoop == null)
                _roomLoop = FindFirstObjectByType<PrototypeRoomLoopController>();

            var currentHealth = _health != null ? _health.CurrentHealth : 0f;
            var maxHealth = _health != null ? _health.MaxHealth : 0f;
            var skill = _skillController != null ? _skillController.Skill : null;
            var cooldown = skill != null ? skill.CooldownRemaining : 0f;
            var room = _roomLoop != null ? _roomLoop.CurrentRoom : 0;
            var enemies = _roomLoop != null ? _roomLoop.LivingEnemies : 0;

            GUI.Box(new Rect(12, 12, 470, 258), "Texas Combat Prototype");
            GUI.Label(new Rect(24, 38, 430, 22), $"HP: {currentHealth:0}/{maxHealth:0}");
            GUI.Label(new Rect(24, 60, 430, 22), $"L / RMB  Sword Rain   CD: {cooldown:0.0}s");
            GUI.Label(new Rect(24, 82, 430, 22), $"Room: {room}   Living Enemies: {enemies}");
            GUI.Label(new Rect(24, 104, 430, 22), "Ground J: 1 -> 2 -> HEAVY 3");
            GUI.Label(new Rect(24, 126, 430, 22), "Dash -> J within 0.35s: Dash Slash");
            GUI.Label(new Rect(24, 148, 430, 22), "Air J: Air Slash");
            GUI.Label(new Rect(24, 170, 430, 22), "Air Down + J: Plunge (damage on landing)");
            GUI.Label(new Rect(24, 196, 430, 22), $"Swift Blade Lv.{Level(_buildLab?.SwiftBladeLevel ?? 0)}");
            GUI.Label(new Rect(24, 216, 430, 22), $"Residual Thunder Lv.{Level(_buildLab?.ResidualThunderLevel ?? 0)}");
            GUI.Label(new Rect(24, 236, 430, 22), $"Conductive Lv.{Level(_buildLab?.ConductiveLevel ?? 0)}");
        }

        private static string Level(int value) => $"{value}/{TexasBuildLab.MaxUpgradeLevel}";
    }
}
