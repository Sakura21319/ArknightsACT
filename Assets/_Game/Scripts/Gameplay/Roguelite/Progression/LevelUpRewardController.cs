using System.Collections.Generic;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Progression
{
    [DisallowMultipleComponent]
    public sealed class LevelUpRewardController : MonoBehaviour
    {
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private LevelUpgradeInventory inventory;
        [SerializeField] private PlayerCombatProfile combatProfile;
        [SerializeField] private LevelUpgradeDefinition[] upgradePool;
        [SerializeField, Range(1, 3)] private int choiceCount = 3;

        private readonly Queue<int> _pendingLevels = new();
        private readonly List<LevelUpgradeDefinition> _choices = new();
        private GameplayPauseService _pause;
        private RewardSelectionCoordinator _coordinator;
        private bool _isOpen;
        private int _displayLevel;
        private int _inputUnlockFrame;

        public bool IsOpen => _isOpen;

        public void Configure(
            RogueliteRunState state,
            LevelUpgradeInventory targetInventory,
            PlayerCombatProfile profile,
            LevelUpgradeDefinition[] pool)
        {
            runState = state;
            inventory = targetInventory;
            combatProfile = profile;
            upgradePool = pool;
        }

        private void OnEnable()
        {
            _pause = GameplayPauseService.Instance;
            _coordinator = RewardSelectionCoordinator.Instance ?? FindFirstObjectByType<RewardSelectionCoordinator>();
            if (runState != null)
                runState.LevelIncreased += OnLevelIncreased;
        }

        private void OnDisable()
        {
            if (runState != null)
                runState.LevelIncreased -= OnLevelIncreased;
            Close();
            _coordinator?.Release(this);
            _pendingLevels.Clear();
        }

        private void OnLevelIncreased(int level)
        {
            _pendingLevels.Enqueue(level);
            TryOpenNext();
        }

        private void TryOpenNext()
        {
            if (_isOpen || _pendingLevels.Count == 0)
                return;

            _coordinator ??= RewardSelectionCoordinator.Instance ?? FindFirstObjectByType<RewardSelectionCoordinator>();
            if (_coordinator != null && !_coordinator.TryAcquire(this))
                return;

            _displayLevel = _pendingLevels.Dequeue();
            BuildChoices();
            if (_choices.Count == 0)
            {
                Debug.LogWarning("[ArknightsACT/Progression] No valid level-up choices remain.", this);
                _coordinator?.Release(this);
                TryOpenNext();
                return;
            }

            _pause ??= GameplayPauseService.Instance;
            if (_pause != null)
                _pause.Pause(this);
            else
                Time.timeScale = 0f;

            _isOpen = true;
            _inputUnlockFrame = Time.frameCount + 1;
        }

        private void Update()
        {
            if (!_isOpen)
            {
                TryOpenNext();
                return;
            }
            if (Time.frameCount < _inputUnlockFrame)
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

        private void BuildChoices()
        {
            _choices.Clear();
            if (upgradePool == null || inventory == null)
                return;

            var candidates = new List<LevelUpgradeDefinition>();
            for (var i = 0; i < upgradePool.Length; i++)
            {
                var definition = upgradePool[i];
                if (definition == null || !inventory.CanAcquire(definition))
                    continue;
                if (combatProfile != null && !combatProfile.Supports(definition.RequiredFeatures))
                    continue;
                candidates.Add(definition);
            }

            var wanted = Mathf.Min(Mathf.Clamp(choiceCount, 1, 3), candidates.Count);
            for (var i = 0; i < wanted; i++)
            {
                var picked = WeightedPick(candidates);
                if (picked == null)
                    break;
                _choices.Add(picked);
                candidates.Remove(picked);
            }
        }

        private static LevelUpgradeDefinition WeightedPick(List<LevelUpgradeDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            var totalWeight = 0f;
            for (var i = 0; i < candidates.Count; i++)
                totalWeight += candidates[i].RewardWeight;

            var roll = UnityEngine.Random.value * totalWeight;
            for (var i = 0; i < candidates.Count; i++)
            {
                roll -= candidates[i].RewardWeight;
                if (roll <= 0f)
                    return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private void Choose(int index)
        {
            if (!_isOpen || inventory == null || index < 0 || index >= _choices.Count)
                return;

            if (!inventory.Acquire(_choices[index]))
                return;

            Close();
            TryOpenNext();
        }

        private void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            _choices.Clear();
            if (_pause != null)
                _pause.Resume(this);
            else
                Time.timeScale = 1f;
            _coordinator?.Release(this);
        }

        private void OnGUI()
        {
            if (!_isOpen)
                return;

            var width = Screen.width;
            var height = Screen.height;
            GUI.Box(new Rect(0f, 0f, width, height), GUIContent.none);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 44, 24, 40),
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(0f, height * 0.09f, width, 62f), $"等级提升 · Lv.{_displayLevel}", titleStyle);

            var subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 90, 14, 20)
            };
            GUI.Label(new Rect(0f, height * 0.16f, width, 36f), "选择一项本局战斗强化", subtitleStyle);

            var cardWidth = Mathf.Min(310f, width * 0.27f);
            var cardHeight = Mathf.Min(320f, height * 0.50f);
            var gap = Mathf.Min(30f, width * 0.024f);
            var totalWidth = _choices.Count * cardWidth + Mathf.Max(0, _choices.Count - 1) * gap;
            var startX = (width - totalWidth) * 0.5f;
            var startY = height * 0.25f;

            var cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 88, 14, 21),
                wordWrap = true,
                padding = new RectOffset(18, 18, 18, 18)
            };

            for (var i = 0; i < _choices.Count; i++)
            {
                var definition = _choices[i];
                var current = inventory.GetStackCount(definition);
                var text = $"[{i + 1}]  {definition.DisplayName}\n\n{definition.Description}\n\n等级：{current + 1}/{definition.MaxStacks}";
                var rect = new Rect(startX + i * (cardWidth + gap), startY, cardWidth, cardHeight);
                if (GUI.Button(rect, text, cardStyle))
                    Choose(i);
            }

            GUI.Label(
                new Rect(0f, startY + cardHeight + 24f, width, 32f),
                "点击卡片，或按 1 / 2 / 3 选择",
                subtitleStyle);
        }
    }
}
