using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Rewards
{
    /// <summary>
    /// Reusable collectible reward screen. Route / node orchestration decides when it opens,
    /// which rarity floor applies, and what happens after selection.
    /// </summary>
    public sealed class RogueliteRewardController : MonoBehaviour
    {
        [SerializeField] private CollectibleInventory inventory;
        [SerializeField] private PlayerCombatProfile combatProfile;
        [SerializeField] private CollectibleDefinition[] rewardPool;
        [SerializeField, Range(1, 3)] private int defaultChoiceCount = 3;

        private readonly List<CollectibleDefinition> _choices = new();
        private bool _isOpen;
        private GameplayPauseService _pause;
        private CollectibleRarity _minimumRarity;
        private int _activeChoiceCount;
        private string _title = "选择一件收藏品";
        private Action _completed;

        public bool IsOpen => _isOpen;

        public void Configure(
            CollectibleInventory targetInventory,
            PlayerCombatProfile profile,
            CollectibleDefinition[] pool)
        {
            inventory = targetInventory;
            combatProfile = profile;
            rewardPool = pool;
        }

        private void OnEnable()
        {
            _pause = GameplayPauseService.Instance;
        }

        private void OnDisable()
        {
            CloseWithoutCallback();
            _completed = null;
        }

        public bool OpenReward(
            string title,
            CollectibleRarity minimumRarity,
            int requestedChoiceCount,
            Action completed)
        {
            if (_isOpen)
                return false;

            _title = string.IsNullOrWhiteSpace(title) ? "选择一件收藏品" : title;
            _minimumRarity = minimumRarity;
            _activeChoiceCount = Mathf.Clamp(requestedChoiceCount > 0 ? requestedChoiceCount : defaultChoiceCount, 1, 3);
            _completed = completed;
            BuildChoices();

            if (_choices.Count == 0)
            {
                Debug.LogWarning(
                    $"[ArknightsACT/Roguelite] No compatible rewards remain for rarity >= {_minimumRarity}.",
                    this);
                var callback = _completed;
                _completed = null;
                callback?.Invoke();
                return false;
            }

            _pause ??= GameplayPauseService.Instance;
            if (_pause != null)
                _pause.Pause(this);
            else
                Time.timeScale = 0f;
            _isOpen = true;
            return true;
        }

        private void Update()
        {
            if (!_isOpen)
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
            if (rewardPool == null || inventory == null)
                return;

            var candidates = new List<CollectibleDefinition>();
            for (var i = 0; i < rewardPool.Length; i++)
            {
                var definition = rewardPool[i];
                if (definition == null || !inventory.CanAcquire(definition))
                    continue;
                if ((int)definition.Rarity < (int)_minimumRarity)
                    continue;
                if (combatProfile != null && !combatProfile.Supports(definition.RequiredFeatures))
                    continue;
                candidates.Add(definition);
            }

            var wanted = Mathf.Min(_activeChoiceCount, candidates.Count);
            for (var i = 0; i < wanted; i++)
            {
                var picked = WeightedPick(candidates);
                if (picked == null)
                    break;
                _choices.Add(picked);
                candidates.Remove(picked);
            }
        }

        private static CollectibleDefinition WeightedPick(List<CollectibleDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            var totalWeight = 0f;
            for (var i = 0; i < candidates.Count; i++)
                totalWeight += Mathf.Max(0.01f, candidates[i].RewardWeight);

            var roll = UnityEngine.Random.value * totalWeight;
            for (var i = 0; i < candidates.Count; i++)
            {
                roll -= Mathf.Max(0.01f, candidates[i].RewardWeight);
                if (roll <= 0f)
                    return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private void Choose(int index)
        {
            if (!_isOpen || index < 0 || index >= _choices.Count || inventory == null)
                return;

            var selected = _choices[index];
            if (!inventory.Acquire(selected))
                return;

            var callback = _completed;
            _completed = null;
            CloseWithoutCallback();
            callback?.Invoke();
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

            var cardWidth = Mathf.Min(310f, width * 0.27f);
            var cardHeight = Mathf.Min(330f, height * 0.52f);
            var gap = Mathf.Min(32f, width * 0.025f);
            var totalWidth = _choices.Count * cardWidth + Mathf.Max(0, _choices.Count - 1) * gap;
            var startX = (width - totalWidth) * 0.5f;
            var startY = height * 0.25f;

            var cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 85, 14, 22),
                wordWrap = true,
                padding = new RectOffset(18, 18, 18, 18)
            };

            for (var i = 0; i < _choices.Count; i++)
            {
                var definition = _choices[i];
                var currentStacks = inventory != null ? inventory.GetStackCount(definition) : 0;
                var rarity = definition.Rarity switch
                {
                    CollectibleRarity.Epic => "高级",
                    CollectibleRarity.Rare => "稀有",
                    _ => "普通"
                };
                var text =
                    $"[{i + 1}]  {rarity}\n\n{definition.DisplayName}\n\n{definition.Description}\n\n" +
                    $"层数：{currentStacks + 1}/{definition.MaxStacks}";
                var rect = new Rect(startX + i * (cardWidth + gap), startY, cardWidth, cardHeight);
                if (GUI.Button(rect, text, cardStyle))
                    Choose(i);
            }

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16
            };
            GUI.Label(new Rect(0f, startY + cardHeight + 25f, width, 35f), "点击卡片，或按 1 / 2 / 3 选择", hintStyle);
        }
    }
}
