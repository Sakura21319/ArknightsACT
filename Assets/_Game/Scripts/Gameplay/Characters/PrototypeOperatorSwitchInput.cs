using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Lightweight runtime selector for operators and skins.
    /// Every registered operator/skin instance is a switch target; state transfer is delegated to
    /// PlayableOperatorSwitchController/PlayerRuntimeContext.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeOperatorSwitchInput : MonoBehaviour
    {
        private const float CardWidth = 210f;
        private const float CardHeight = 245f;
        private const float CardGap = 14f;
        private const float HeaderHeight = 58f;
        private const float FooterHeight = 54f;
        private const float PanelPadding = 22f;
        private const float MaxPanelWidth = 980f;
        private const int MaxColumns = 4;

        [SerializeField] private PlayableOperatorSwitchController switchController;

        private bool _isOpen;
        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;
        private Vector2 _selectorScroll;

        public void Configure(PlayableOperatorSwitchController controller)
        {
            switchController = controller;
        }

        private void Awake()
        {
            switchController ??= GetComponent<PlayableOperatorSwitchController>();
        }

        private void OnDisable()
        {
            CloseSelector();
        }

        private void Update()
        {
            if (switchController == null)
            {
                CloseSelector();
                return;
            }

            var flow = RogueliteGameFlowController.Instance;
            if (flow != null && !flow.IsRunning)
            {
                CloseSelector();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    CloseSelector();
                }
                else if (!GameplayInputBlocker.IsBlocked && HasMultipleTargets())
                {
                    OpenSelector();
                }

                return;
            }

            if (!_isOpen)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseSelector();
                return;
            }

            var players = switchController.Context?.RegisteredPlayers;
            if (players == null)
                return;

            var shortcutIndex = 0;
            for (var i = 0; i < players.Count && shortcutIndex < 9; i++)
            {
                var player = players[i];
                if (player == null)
                    continue;

                if (IsShortcutPressed(keyboard, shortcutIndex))
                {
                    TrySwitch(player);
                    return;
                }

                shortcutIndex++;
            }
        }

        private static bool IsShortcutPressed(Keyboard keyboard, int index)
        {
            return index switch
            {
                0 => keyboard.digit1Key.wasPressedThisFrame,
                1 => keyboard.digit2Key.wasPressedThisFrame,
                2 => keyboard.digit3Key.wasPressedThisFrame,
                3 => keyboard.digit4Key.wasPressedThisFrame,
                4 => keyboard.digit5Key.wasPressedThisFrame,
                5 => keyboard.digit6Key.wasPressedThisFrame,
                6 => keyboard.digit7Key.wasPressedThisFrame,
                7 => keyboard.digit8Key.wasPressedThisFrame,
                8 => keyboard.digit9Key.wasPressedThisFrame,
                _ => false
            };
        }

        private bool HasMultipleTargets()
        {
            var players = switchController.Context?.RegisteredPlayers;
            if (players == null)
                return false;

            var count = 0;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i] != null && ++count > 1)
                    return true;
            }

            return false;
        }

        private void OpenSelector()
        {
            if (_isOpen)
                return;

            _isOpen = true;
            _selectorScroll = Vector2.zero;
            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameplayInputBlocker.SetBlocked(this, true);
        }

        private void CloseSelector()
        {
            if (!_isOpen)
            {
                GameplayInputBlocker.Release(this);
                return;
            }

            _isOpen = false;
            GameplayInputBlocker.Release(this);
            Cursor.lockState = _previousCursorLockMode;
            Cursor.visible = _previousCursorVisible;
        }

        private void OnGUI()
        {
            if (!_isOpen || switchController == null)
                return;

            var context = switchController.Context;
            var players = context?.RegisteredPlayers;
            if (players == null)
                return;

            var validCount = CountValid(players);
            if (validCount <= 1)
            {
                CloseSelector();
                return;
            }

            var maxUsableWidth = Mathf.Max(
                CardWidth + PanelPadding * 2f,
                Mathf.Min(MaxPanelWidth, Screen.width - 60f));
            var columns = Mathf.Clamp(
                Mathf.FloorToInt(
                    (maxUsableWidth - PanelPadding * 2f + CardGap) /
                    (CardWidth + CardGap)),
                1,
                Mathf.Min(MaxColumns, validCount));
            var rows = Mathf.CeilToInt(validCount / (float)columns);

            var cardsWidth = columns * CardWidth + (columns - 1) * CardGap;
            var panelWidth = cardsWidth + PanelPadding * 2f;
            var contentHeight = rows * CardHeight + Mathf.Max(0, rows - 1) * CardGap;
            var desiredHeight = HeaderHeight + contentHeight + FooterHeight + PanelPadding;
            var panelHeight = Mathf.Min(desiredHeight, Screen.height - 50f);

            var panelRect = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            GUI.Box(panelRect, GUIContent.none);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(
                new Rect(
                    panelRect.x + PanelPadding,
                    panelRect.y + 10f,
                    panelRect.width - PanelPadding * 2f,
                    36f),
                "切换干员 / 皮肤",
                titleStyle);

            var bodyRect = new Rect(
                panelRect.x + PanelPadding,
                panelRect.y + HeaderHeight,
                cardsWidth,
                panelHeight - HeaderHeight - FooterHeight);
            var contentRect = new Rect(0f, 0f, cardsWidth - 1f, contentHeight);
            _selectorScroll = GUI.BeginScrollView(bodyRect, _selectorScroll, contentRect);

            var targetIndex = 0;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                    continue;

                var row = targetIndex / columns;
                var column = targetIndex % columns;
                var cardRect = new Rect(
                    column * (CardWidth + CardGap),
                    row * (CardHeight + CardGap),
                    CardWidth,
                    CardHeight);

                DrawTargetCard(
                    cardRect,
                    player,
                    targetIndex + 1,
                    player == context.ActivePlayer);
                targetIndex++;
            }

            GUI.EndScrollView();

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
            GUI.Label(
                new Rect(
                    panelRect.x + PanelPadding,
                    panelRect.yMax - FooterHeight + 10f,
                    panelRect.width - PanelPadding * 2f,
                    28f),
                "Tab / Esc 关闭    ·    点击目标或按 1-9 切换",
                hintStyle);
        }

        private static int CountValid(System.Collections.Generic.IReadOnlyList<Transform> players)
        {
            var count = 0;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i] != null)
                    count++;
            }

            return count;
        }

        private void DrawTargetCard(
            Rect rect,
            Transform player,
            int shortcut,
            bool isActive)
        {
            GUI.Box(rect, GUIContent.none);

            var identity = player.GetComponent<PlayableOperatorIdentity>();
            var displayName = identity != null &&
                              !string.IsNullOrWhiteSpace(identity.DisplayName)
                ? identity.DisplayName
                : player.name;
            var skinName = identity != null &&
                           !string.IsNullOrWhiteSpace(identity.SkinDisplayName)
                ? identity.SkinDisplayName
                : identity?.SkinId ?? "default";

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 28f),
                displayName,
                nameStyle);

            var skinStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 34f, rect.width - 20f, 22f),
                skinName,
                skinStyle);

            Texture2D avatar = null;
            if (identity != null && !string.IsNullOrWhiteSpace(identity.AvatarResourceKey))
                avatar = Resources.Load<Texture2D>(identity.AvatarResourceKey);

            var avatarRect = new Rect(
                rect.x + 38f,
                rect.y + 58f,
                rect.width - 76f,
                120f);
            if (avatar != null)
                GUI.DrawTexture(avatarRect, avatar, ScaleMode.ScaleToFit, true);
            else
                GUI.Box(avatarRect, "NO AVATAR");

            if (shortcut <= 9)
            {
                GUI.Label(
                    new Rect(rect.x + 10f, rect.y + 180f, rect.width - 20f, 22f),
                    $"快捷键 [{shortcut}]",
                    new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11
                    });
            }

            var buttonRect = new Rect(
                rect.x + 22f,
                rect.yMax - 38f,
                rect.width - 44f,
                28f);
            using (new GUIEnabledScope(!isActive && switchController.CanSwitchNow()))
            {
                if (GUI.Button(buttonRect, isActive ? "当前使用" : "切换"))
                    TrySwitch(player);
            }
        }

        private void TrySwitch(Transform player)
        {
            if (player == null || player == switchController.ActivePlayer)
                return;

            if (switchController.TrySwitchTo(player))
                CloseSelector();
        }

        private readonly struct GUIEnabledScope : System.IDisposable
        {
            private readonly bool _previous;

            public GUIEnabledScope(bool enabled)
            {
                _previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _previous;
            }
        }
    }
}
