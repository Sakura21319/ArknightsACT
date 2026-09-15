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

            GUI.Box(new Rect(12, 12, 410, 188), "Texas Combat Prototype");
            GUI.Label(new Rect(24, 38, 370, 22), $"HP: {currentHealth:0}/{maxHealth:0}");
            GUI.Label(new Rect(24, 60, 370, 22), $"L / RMB  Sword Rain   CD: {cooldown:0.0}s");
            GUI.Label(new Rect(24, 82, 370, 22), $"Room: {room}   Living Enemies: {enemies}");
            GUI.Label(new Rect(24, 104, 370, 22), "Clear room -> choose upgrade -> next room");
            GUI.Label(new Rect(24, 126, 370, 22), $"Swift Blade: {OnOff(_buildLab != null && _buildLab.SwiftBlade)}");
            GUI.Label(new Rect(24, 146, 370, 22), $"Residual Thunder: {OnOff(_buildLab != null && _buildLab.ResidualThunder)}");
            GUI.Label(new Rect(24, 166, 370, 22), $"Conductive: {OnOff(_buildLab != null && _buildLab.Conductive)}");
        }

        private static string OnOff(bool value) => value ? "ON" : "OFF";
    }
}
