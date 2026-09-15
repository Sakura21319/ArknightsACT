using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    /// <summary>
    /// Presentation/input for the prototype room reward. Room progression decides when to open it;
    /// this component only pauses, presents the available Texas upgrades and reports the choice.
    /// </summary>
    [RequireComponent(typeof(TexasBuildLab))]
    public sealed class TexasUpgradeChoicePanel : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float openDelay = 0.45f;

        private TexasBuildLab _buildLab;
        private Coroutine _openRoutine;
        private bool _open;
        private int _roomIndex;
        private float _previousTimeScale = 1f;
        private Font _chineseFont;

        public bool IsOpen => _open;
        public bool HasAvailableUpgrade => _buildLab != null && !_buildLab.HasAllUpgrades;
        public event Action<int> UpgradeChosen;

        private void Awake()
        {
            _buildLab = GetComponent<TexasBuildLab>();
            TryCreateChineseFont();
        }

        private void LateUpdate()
        {
            if (!_open)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                Choose(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                Choose(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                Choose(2);
        }

        public bool OpenForRoom(int roomIndex)
        {
            if (_open || _openRoutine != null || !HasAvailableUpgrade)
                return false;

            _roomIndex = Mathf.Max(1, roomIndex);
            _openRoutine = StartCoroutine(OpenAfterDelay());
            return true;
        }

        private IEnumerator OpenAfterDelay()
        {
            if (openDelay > 0f)
                yield return new WaitForSecondsRealtime(openDelay);

            _openRoutine = null;
            if (!HasAvailableUpgrade)
                yield break;

            _open = true;
            _previousTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }

        private void OnGUI()
        {
            if (!_open)
                return;

            var overlay = new Color(0f, 0f, 0f, 0.72f);
            var previousColor = GUI.color;
            GUI.color = overlay;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            var panelWidth = Mathf.Min(1180f, Screen.width - 80f);
            var panelHeight = Mathf.Min(430f, Screen.height - 60f);
            var panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUI.Box(panel, string.Empty);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.042f), 24, 46),
                fontStyle = FontStyle.Bold,
                font = _chineseFont != null ? _chineseFont : GUI.skin.font
            };
            GUI.Label(
                new Rect(panel.x, panel.y + 22f, panel.width, 56f),
                $"第 {_roomIndex} 房间已清理　选择 1 项升级",
                titleStyle);

            var subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.022f), 15, 24),
                font = _chineseFont != null ? _chineseFont : GUI.skin.font
            };
            GUI.Label(
                new Rect(panel.x, panel.y + 72f, panel.width, 34f),
                "同一流派可连续强化至 Lv.3；Lv.3 会发生质变",
                subtitleStyle);

            var gap = 18f;
            var cardY = panel.y + 122f;
            var cardHeight = panel.height - 150f;
            var cardWidth = (panel.width - 48f - gap * 2f) / 3f;

            DrawChoice(
                new Rect(panel.x + 24f, cardY, cardWidth, cardHeight),
                0,
                "迅刃",
                GetSwiftBladeDescription());
            DrawChoice(
                new Rect(panel.x + 24f + cardWidth + gap, cardY, cardWidth, cardHeight),
                1,
                "余雷",
                GetResidualThunderDescription());
            DrawChoice(
                new Rect(panel.x + 24f + (cardWidth + gap) * 2f, cardY, cardWidth, cardHeight),
                2,
                "导电",
                GetConductiveDescription());
        }

        private void DrawChoice(Rect rect, int index, string title, string description)
        {
            var level = _buildLab != null ? _buildLab.GetLevel(index) : 0;
            var maxed = _buildLab == null || !_buildLab.CanUpgrade(index);
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.021f), 15, 23),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(18, 18, 20, 16),
                font = _chineseFont != null ? _chineseFont : GUI.skin.font
            };

            var previousEnabled = GUI.enabled;
            GUI.enabled = !maxed;
            var header = $"{index + 1}　{title}　Lv.{level}/{TexasBuildLab.MaxUpgradeLevel}";
            var text = maxed
                ? header + "\n\n已满级\n\n" + description
                : header + "\n\n" + description;
            var clicked = GUI.Button(rect, text, style);
            GUI.enabled = previousEnabled;

            if (clicked)
                Choose(index);
        }

        private string GetSwiftBladeDescription()
        {
            var level = _buildLab != null ? _buildLab.SwiftBladeLevel : 0;
            return level switch
            {
                0 => "获得：每 4 次普攻释放剑气，造成 7 点法术伤害。\n让平A开始产生额外攻击。",
                1 => "强化：改为每 3 次普攻释放剑气，伤害提升至 10。\n触发频率明显提高。",
                2 => "进化：每 3 次普攻连续释放两道剑气，每道 12 点法术伤害。\n形成持续扫场。",
                _ => "每 3 次普攻连续释放两道剑气，每道 12 点法术伤害。"
            };
        }

        private string GetResidualThunderDescription()
        {
            var level = _buildLab != null ? _buildLab.ResidualThunderLevel : 0;
            return level switch
            {
                0 => "获得：剑雨后留下 3 秒雷场，每 0.5 秒造成 3 点法术伤害并施加震击。",
                1 => "强化：雷场延长至 4 秒，每 0.4 秒造成 4 点法术伤害。\n覆盖时间和频率同时提升。",
                2 => "进化：雷场延长至 5 秒，每 0.3 秒造成 5 点伤害，范围扩大 25%，周期性雷爆。",
                _ => "5 秒高频大范围雷场，持续施加震击并周期性雷爆。"
            };
        }

        private string GetConductiveDescription()
        {
            var level = _buildLab != null ? _buildLab.ConductiveLevel : 0;
            return level switch
            {
                0 => "获得：普攻击中震击敌人时，向附近 1 个目标连锁闪电，造成 5 点法术伤害；剑雨冷却 -0.25 秒。",
                1 => "强化：连锁 2 个目标，每跳 7 点法术伤害；剑雨冷却 -0.40 秒。",
                2 => "进化：最多连锁 4 个目标，每跳 10 点法术伤害并传播震击；剑雨冷却 -0.60 秒。",
                _ => "最多连锁 4 个目标，每跳 10 点法术伤害并传播震击。"
            };
        }

        private void Choose(int index)
        {
            if (!_open || _buildLab == null || !_buildLab.CanUpgrade(index))
                return;

            if (!_buildLab.Upgrade(index))
                return;

            _open = false;
            Time.timeScale = _previousTimeScale > 0.001f ? _previousTimeScale : 1f;
            UpgradeChosen?.Invoke(index);
        }

        private void OnDisable()
        {
            if (_openRoutine != null)
            {
                StopCoroutine(_openRoutine);
                _openRoutine = null;
            }

            if (_open)
                Time.timeScale = _previousTimeScale > 0.001f ? _previousTimeScale : 1f;
            _open = false;
        }

        private void OnDestroy()
        {
            if (_chineseFont != null)
                Destroy(_chineseFont);
        }

        private void TryCreateChineseFont()
        {
            try
            {
                _chineseFont = Font.CreateDynamicFontFromOSFont(
                    new[]
                    {
                        "Microsoft YaHei UI",
                        "Microsoft YaHei",
                        "SimHei",
                        "PingFang SC",
                        "Noto Sans CJK SC"
                    },
                    24);
            }
            catch
            {
                _chineseFont = null;
            }
        }
    }
}
