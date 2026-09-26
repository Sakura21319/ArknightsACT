using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    /// <summary>
    /// Small runtime IMGUI panel for combat iteration.
    /// F8 toggles panel visibility. Cheats always apply only to the current active operator.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class PlayerCombatDebugPanel : MonoBehaviour
    {
        private const int WindowId = 0x41524354;

        private static PlayerCombatDebugPanel _instance;

        private Rect _windowRect = new(18f, 240f, 270f, 154f);
        private Transform _boundPlayer;
        private PlayerDebugCheatState _boundState;
        private bool _visible = true;
        private bool _invincible;
        private bool _infiniteSkillPoints;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
                return;

            var existing = FindFirstObjectByType<PlayerCombatDebugPanel>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject("[PlayerCombatDebugPanel]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlayerCombatDebugPanel>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ClearBoundPlayer();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            var context = PlayerRuntimeContext.Instance ?? FindFirstObjectByType<PlayerRuntimeContext>();
            var activePlayer = context != null ? context.ActivePlayer : null;
            if (activePlayer != _boundPlayer)
                Bind(activePlayer);

            if (_boundState != null)
                _boundState.Configure(_invincible, _infiniteSkillPoints);
        }

        private void OnGUI()
        {
            var currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.F8)
            {
                _visible = !_visible;
                currentEvent.Use();
            }

            if (!_visible)
                return;

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "Combat Debug  [F8]");
        }

        private void DrawWindow(int id)
        {
            var identity = _boundPlayer != null
                ? _boundPlayer.GetComponent<PlayableOperatorIdentity>()
                : null;
            var displayName = identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName)
                ? identity.DisplayName
                : _boundPlayer != null
                    ? _boundPlayer.name
                    : "<no active player>";

            GUILayout.Label("Current: " + displayName);

            var nextInvincible = GUILayout.Toggle(_invincible, "Invincible / 无敌");
            if (nextInvincible != _invincible)
            {
                _invincible = nextInvincible;
                Apply();
            }

            var nextInfinite = GUILayout.Toggle(_infiniteSkillPoints, "Infinite CD / 无限技能SP");
            if (nextInfinite != _infiniteSkillPoints)
            {
                _infiniteSkillPoints = nextInfinite;
                Apply();
            }

            GUILayout.Space(4f);
            GUILayout.Label("Only affects the current operator.");
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void Bind(Transform player)
        {
            ClearBoundPlayer();
            _boundPlayer = player;
            if (_boundPlayer == null)
                return;

            _boundState = _boundPlayer.GetComponent<PlayerDebugCheatState>();
            if (_boundState == null)
                _boundState = _boundPlayer.gameObject.AddComponent<PlayerDebugCheatState>();

            var gate = _boundPlayer.GetComponent<PlayerDamageGate>();
            if (gate == null)
                gate = _boundPlayer.gameObject.AddComponent<PlayerDamageGate>();
            gate.RefreshSources();

            Apply();
        }

        private void Apply()
        {
            _boundState?.Configure(_invincible, _infiniteSkillPoints);
        }

        private void ClearBoundPlayer()
        {
            if (_boundState != null)
                _boundState.Clear();

            _boundState = null;
            _boundPlayer = null;
        }
    }
}
