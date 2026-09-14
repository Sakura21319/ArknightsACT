using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    public sealed class PrototypeDebugHud : MonoBehaviour
    {
        private CombatEntity _player;
        private PlayerMotor2D _motor;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public void BindPlayer(GameObject player)
        {
            _player = player != null ? player.GetComponent<CombatEntity>() : null;
            _motor = player != null ? player.GetComponent<PlayerMotor2D>() : null;
        }

        private void OnGUI()
        {
            var scale = Mathf.Clamp(Screen.height / 720f, 1f, 2.5f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.92f, 0.95f, 1f) }
            };

            GUI.Box(new Rect(16f, 16f, 370f, 152f), GUIContent.none);
            GUI.Label(new Rect(30f, 26f, 320f, 30f), "ArknightsACT · Phase 1 Graybox", _titleStyle);
            GUI.Label(new Rect(30f, 60f, 330f, 24f), "A/D 移动   Space 跳跃   J 攻击   K/Shift 闪避", _bodyStyle);

            if (_player != null && _player.Health != null)
            {
                var hp = $"德克萨斯 HP: {_player.Health.CurrentHealth:0}/{_player.Health.MaxHealth:0}";
                GUI.Label(new Rect(30f, 88f, 330f, 24f), hp, _bodyStyle);
            }
            else
            {
                GUI.Label(new Rect(30f, 88f, 330f, 24f), "Player: NOT FOUND", _bodyStyle);
            }

            if (_motor != null)
            {
                var status = $"Pos: {_motor.transform.position.x:0.0}, {_motor.transform.position.y:0.0}   Grounded: {_motor.IsGrounded}";
                GUI.Label(new Rect(30f, 116f, 330f, 24f), status, _bodyStyle);
            }

            GUI.Label(new Rect(30f, 140f, 330f, 24f), "蓝色 = 玩家    红色 = 训练敌人", _bodyStyle);
            GUI.matrix = previous;
        }
    }
}
