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
                "按 1 / 2 / 3，或点击卡片",
                subtitleStyle);

            var gap = 18f;
            var cardY = panel.y + 122f;
            var cardHeight = panel.height - 150f;
            var cardWidth = (panel.width - 48f - gap * 2f) / 3f;

            DrawChoice(
                new Rect(panel.x + 24f, cardY, cardWidth, cardHeight),
                0,
                "1　迅刃",
                "每完成 8 次普攻，向前释放一道剑气，造成 6 点法术伤害。\n适合持续贴身连击。");
            DrawChoice(
                new Rect(panel.x + 24f + cardWidth + gap, cardY, cardWidth, cardHeight),
                1,
                "2　余雷",
                "剑雨结束后留下持续 3 秒的雷场，每 0.5 秒造成 3 点法术伤害并施加震击。\n强化范围压制能力。");
            DrawChoice(
                new Rect(panel.x + 24f + (cardWidth + gap) * 2f, cardY, cardWidth, cardHeight),
                2,
                "3　导电",
                "普攻命中处于【震击】状态的敌人时，剑雨冷却减少 0.15 秒。\n让普攻与技能形成循环。 ");
        }

        private void DrawChoice(Rect rect, int index, string title, string description)
        {
            var acquired = _buildLab != null && _buildLab.HasUpgrade(index);
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.022f), 16, 24),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(18, 18, 22, 16),
                font = _chineseFont != null ? _chineseFont : GUI.skin.font
            };

            var previousEnabled = GUI.enabled;
            GUI.enabled = !acquired;
            var text = acquired
                ? title + "\n\n已获得\n\n" + description
                : title + "\n\n" + description;
            var clicked = GUI.Button(rect, text, style);
            GUI.enabled = previousEnabled;

            if (clicked)
                Choose(index);
        }

        private void Choose(int index)
        {
            if (!_open || _buildLab == null || _buildLab.HasUpgrade(index))
                return;

            switch (index)
            {
                case 0:
                    _buildLab.SetSwiftBlade(true);
                    break;
                case 1:
                    _buildLab.SetResidualThunder(true);
                    break;
                case 2:
                    _buildLab.SetConductive(true);
                    break;
                default:
                    return;
            }

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
