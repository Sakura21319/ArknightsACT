using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.TopDown
{
    [RequireComponent(typeof(TopDownTexasBuildLab))]
    public sealed class TopDownTexasUpgradePanel : MonoBehaviour
    {
        private TopDownTexasBuildLab _build;
        private bool _open;
        private int _room;
        private float _previousTimeScale = 1f;
        private Font _font;

        public bool IsOpen => _open;
        public bool HasAvailableUpgrade => _build != null && !_build.HasAllUpgrades;
        public event Action<int> UpgradeChosen;

        private void Awake()
        {
            _build = GetComponent<TopDownTexasBuildLab>();
            try
            {
                _font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC" }, 24);
            }
            catch { _font = null; }
        }

        public bool OpenForRoom(int room)
        {
            if (_open || !HasAvailableUpgrade)
                return false;
            _room = Mathf.Max(1, room);
            _previousTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _open = true;
            return true;
        }

        private void Update()
        {
            if (!_open || Keyboard.current == null)
                return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) Choose(2);
        }

        private void OnGUI()
        {
            if (!_open)
                return;

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            var width = Mathf.Min(1120f, Screen.width - 70f);
            var height = Mathf.Min(410f, Screen.height - 50f);
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, string.Empty);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.038f), 24, 42),
                fontStyle = FontStyle.Bold,
                font = _font != null ? _font : GUI.skin.font
            };
            GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 52f), $"房间 {_room} 清理完成 · 选择强化", titleStyle);

            var gap = 16f;
            var cardWidth = (panel.width - 48f - gap * 2f) / 3f;
            var y = panel.y + 92f;
            var cardHeight = panel.height - 118f;
            DrawCard(new Rect(panel.x + 24f, y, cardWidth, cardHeight), 0, "迅刃", SwiftText());
            DrawCard(new Rect(panel.x + 24f + cardWidth + gap, y, cardWidth, cardHeight), 1, "余雷", ThunderText());
            DrawCard(new Rect(panel.x + 24f + (cardWidth + gap) * 2f, y, cardWidth, cardHeight), 2, "导电", ConductiveText());
        }

        private void DrawCard(Rect rect, int index, string title, string description)
        {
            var level = _build.GetLevel(index);
            var enabledBefore = GUI.enabled;
            GUI.enabled = _build.CanUpgrade(index);
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.021f), 15, 23),
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(16, 16, 18, 14),
                font = _font != null ? _font : GUI.skin.font
            };
            if (GUI.Button(rect, $"{index + 1}  {title}  Lv.{level}/3\n\n{description}", style))
                Choose(index);
            GUI.enabled = enabledBefore;
        }

        private string SwiftText() => _build.SwiftBladeLevel switch
        {
            0 => "每 4 次近战挥砍向瞄准方向释放剑气。",
            1 => "改为每 3 次挥砍触发，剑气伤害提高。",
            2 => "每 3 次挥砍连续释放两道剑气。",
            _ => "双剑气持续扫场。"
        };

        private string ThunderText() => _build.ResidualThunderLevel switch
        {
            0 => "剑雨结束后留下持续雷场。",
            1 => "雷场持续更久、攻击更频繁。",
            2 => "雷场扩大并产生高频雷击。",
            _ => "高频大范围雷场。"
        };

        private string ConductiveText() => _build.ConductiveLevel switch
        {
            0 => "近战命中震击目标时连锁 1 次，并缩短剑雨冷却。",
            1 => "连锁 2 次，冷却返还提高。",
            2 => "连锁 4 次并传播震击。",
            _ => "四段连锁雷。"
        };

        private void Choose(int index)
        {
            if (!_open || !_build.Upgrade(index))
                return;
            _open = false;
            Time.timeScale = _previousTimeScale > 0.001f ? _previousTimeScale : 1f;
            UpgradeChosen?.Invoke(index);
        }

        private void OnDisable()
        {
            if (_open)
                Time.timeScale = _previousTimeScale > 0.001f ? _previousTimeScale : 1f;
            _open = false;
        }

        private void OnDestroy()
        {
            if (_font != null)
                Destroy(_font);
        }
    }
}
