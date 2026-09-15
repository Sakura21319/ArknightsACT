using System.Collections;
using ArknightsACT.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    /// <summary>
    /// Prototype roguelite reward: when the current room has no living enemies, pause the game
    /// and offer three existing Texas build effects. This deliberately sits above TexasBuildLab;
    /// the build component contains no UI/input policy of its own.
    /// </summary>
    [RequireComponent(typeof(TexasBuildLab))]
    public sealed class TexasUpgradeChoicePanel : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float roomClearDelay = 0.45f;

        private TexasBuildLab _buildLab;
        private bool _sawEnemy;
        private bool _rewardScheduled;
        private bool _rewardShown;
        private bool _open;
        private float _previousTimeScale = 1f;

        public bool IsOpen => _open;

        private void Awake()
        {
            _buildLab = GetComponent<TexasBuildLab>();
        }

        private void Update()
        {
            if (_rewardShown || _rewardScheduled)
                return;

            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            var livingEnemies = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == null || entity.Team != Team.Enemy || entity.Health == null)
                    continue;

                _sawEnemy = true;
                if (!entity.Health.IsDead)
                    livingEnemies++;
            }

            if (_sawEnemy && livingEnemies == 0)
            {
                _rewardScheduled = true;
                StartCoroutine(OpenAfterDelay());
            }
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

        private IEnumerator OpenAfterDelay()
        {
            if (roomClearDelay > 0f)
                yield return new WaitForSecondsRealtime(roomClearDelay);

            if (_rewardShown)
                yield break;

            _rewardShown = true;
            _rewardScheduled = false;
            _open = true;
            _previousTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }

        private void OnGUI()
        {
            if (!_open)
                return;

            var overlay = new Color(0f, 0f, 0f, 0.72f);
            var previous = GUI.color;
            GUI.color = overlay;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

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
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(panel.x, panel.y + 22f, panel.width, 56f), "ROOM CLEAR  -  CHOOSE 1 UPGRADE", titleStyle);

            var subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.022f), 15, 24)
            };
            GUI.Label(new Rect(panel.x, panel.y + 72f, panel.width, 34f), "Press 1 / 2 / 3 or click a card", subtitleStyle);

            var gap = 18f;
            var cardY = panel.y + 122f;
            var cardHeight = panel.height - 150f;
            var cardWidth = (panel.width - 48f - gap * 2f) / 3f;
            DrawChoice(new Rect(panel.x + 24f, cardY, cardWidth, cardHeight), 0,
                "1  SWIFT BLADE",
                "Basic attacks build a rapid-hit loop.\nRewards staying aggressive and chaining swings.");
            DrawChoice(new Rect(panel.x + 24f + cardWidth + gap, cardY, cardWidth, cardHeight), 1,
                "2  RESIDUAL THUNDER",
                "Sword Rain leaves a damaging thunder field.\nArea control + repeated Arts damage.");
            DrawChoice(new Rect(panel.x + 24f + (cardWidth + gap) * 2f, cardY, cardWidth, cardHeight), 2,
                "3  CONDUCTIVE",
                "Shocked enemies amplify your follow-up loop.\nTurns status into combo payoff.");
        }

        private void DrawChoice(Rect rect, int index, string title, string description)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.022f), 16, 24),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(18, 18, 22, 16)
            };

            if (GUI.Button(rect, title + "\n\n" + description, style))
                Choose(index);
        }

        private void Choose(int index)
        {
            if (!_open || _buildLab == null)
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
        }

        private void OnDisable()
        {
            if (_open)
                Time.timeScale = _previousTimeScale > 0.001f ? _previousTimeScale : 1f;
            _open = false;
        }
    }
}
