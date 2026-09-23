using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.SkillUpgrades
{
    [DisallowMultipleComponent]
    public sealed class CharacterSkillUpgradeRewardController : MonoBehaviour
    {
        private sealed class RewardRequest
        {
            public string Title;
            public int ChoiceCount;
            public Action Completed;
        }

        [SerializeField] private CharacterSkillUpgradeInventory inventory;
        [SerializeField] private CharacterSkillUpgradeDefinition[] pool;

        private readonly Queue<RewardRequest> _pendingRequests = new();
        private readonly List<CharacterSkillUpgradeDefinition> _choices = new();
        private GameplayPauseService _pause;
        private RewardSelectionCoordinator _coordinator;
        private Action _completed;
        private bool _isOpen;
        private string _title = "技能特化";
        private int _inputUnlockFrame;

        public bool IsOpen => _isOpen;

        public void Configure(CharacterSkillUpgradeInventory targetInventory, CharacterSkillUpgradeDefinition[] rewardPool)
        {
            inventory = targetInventory;
            pool = rewardPool;
        }

        private void OnEnable()
        {
            _pause = GameplayPauseService.Instance;
            _coordinator = RewardSelectionCoordinator.Instance ?? FindFirstObjectByType<RewardSelectionCoordinator>();
            BindActivePlayer();
        }

        private void OnDisable()
        {
            CloseWithoutCallback();
            _coordinator?.Release(this);
            _completed = null;
            _pendingRequests.Clear();
        }

        public bool OpenReward(string title, int requestedChoiceCount, Action completed)
        {
            BindActivePlayer();
            if (inventory == null)
                return false;

            _pendingRequests.Enqueue(new RewardRequest
            {
                Title = string.IsNullOrWhiteSpace(title) ? "技能特化" : title,
                ChoiceCount = Mathf.Clamp(requestedChoiceCount, 1, 3),
                Completed = completed
            });
            TryOpenNext();
            return true;
        }

        private void TryOpenNext()
        {
            if (_isOpen || _pendingRequests.Count == 0)
                return;

            _coordinator ??= RewardSelectionCoordinator.Instance ?? FindFirstObjectByType<RewardSelectionCoordinator>();
            if (_coordinator != null && !_coordinator.TryAcquire(this))
                return;

            var request = _pendingRequests.Dequeue();
            _title = request.Title;
            _completed = request.Completed;
            BuildChoices(request.ChoiceCount);
            if (_choices.Count == 0)
            {
                var callback = _completed;
                _completed = null;
                _coordinator?.Release(this);
                callback?.Invoke();
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
            if (ArknightsACT.Gameplay.Input.GameplayInputBlocker.IsBlocked) return;
            if (!_isOpen)
            {
                TryOpenNext();
                return;
            }
            if (Time.frameCount < _inputUnlockFrame || Keyboard.current == null)
                return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                Choose(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                Choose(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                Choose(2);
        }

        private void BuildChoices(int wantedCount)
        {
            BindActivePlayer();
            _choices.Clear();
            if (pool == null)
                return;

            var candidates = new List<CharacterSkillUpgradeDefinition>();
            for (var i = 0; i < pool.Length; i++)
            {
                var definition = pool[i];
                if (definition != null && inventory.CanAcquire(definition))
                    candidates.Add(definition);
            }

            while (_choices.Count < wantedCount && candidates.Count > 0)
            {
                var total = 0f;
                for (var i = 0; i < candidates.Count; i++)
                    total += candidates[i].RewardWeight;
                var roll = UnityEngine.Random.value * Mathf.Max(0.01f, total);
                var pickIndex = candidates.Count - 1;
                for (var i = 0; i < candidates.Count; i++)
                {
                    roll -= candidates[i].RewardWeight;
                    if (roll <= 0f)
                    {
                        pickIndex = i;
                        break;
                    }
                }
                _choices.Add(candidates[pickIndex]);
                candidates.RemoveAt(pickIndex);
            }
        }

        private void BindActivePlayer()
        {
            var activePlayer = PlayerRuntimeContext.Resolve();
            if (activePlayer == null)
                return;
            var nextInventory = activePlayer.GetComponent<CharacterSkillUpgradeInventory>();
            if (nextInventory != null)
                inventory = nextInventory;
        }

        private void Choose(int index)
        {
            if (!_isOpen || index < 0 || index >= _choices.Count)
                return;
            if (!inventory.Acquire(_choices[index]))
                return;

            var callback = _completed;
            _completed = null;
            CloseWithoutCallback();
            callback?.Invoke();
            TryOpenNext();
        }

        private void CloseWithoutCallback()
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
                fontSize = Mathf.Clamp(width / 45, 22, 38),
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(0f, height * 0.10f, width, 60f), _title, titleStyle);

            var cardWidth = Mathf.Min(340f, width * 0.30f);
            var cardHeight = Mathf.Min(330f, height * 0.50f);
            var gap = 30f;
            var totalWidth = _choices.Count * cardWidth + Mathf.Max(0, _choices.Count - 1) * gap;
            var startX = (width - totalWidth) * 0.5f;
            var startY = height * 0.25f;
            var cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = Mathf.Clamp(width / 85, 14, 22),
                padding = new RectOffset(18, 18, 18, 18)
            };

            for (var i = 0; i < _choices.Count; i++)
            {
                var definition = _choices[i];
                var stack = inventory.GetStackCount(definition);
                var text = $"[{i + 1}] {definition.DisplayName}\n\n{definition.Description}\n\n层数：{stack + 1}/{definition.MaxStacks}";
                if (GUI.Button(new Rect(startX + i * (cardWidth + gap), startY, cardWidth, cardHeight), text, cardStyle))
                    Choose(i);
            }
        }
    }
}
