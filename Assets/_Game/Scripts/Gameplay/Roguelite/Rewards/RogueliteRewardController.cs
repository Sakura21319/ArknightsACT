using System.Collections.Generic;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Rewards
{
    /// <summary>
    /// R1 prototype reward gate: clear a combat room -> pause -> choose one of three
    /// compatible collectibles -> continue the existing room loop.
    /// Uses immediate-mode GUI intentionally so the prototype has no prefab/UI asset dependency.
    /// </summary>
    public sealed class RogueliteRewardController : MonoBehaviour
    {
        [SerializeField] private PrototypeRoomLoopController roomLoop;
        [SerializeField] private CollectibleInventory inventory;
        [SerializeField] private PlayerCombatProfile combatProfile;
        [SerializeField] private CollectibleDefinition[] rewardPool;
        [SerializeField, Range(1, 3)] private int choiceCount = 3;

        private readonly List<CollectibleDefinition> _choices = new();
        private bool _isOpen;
        private float _previousTimeScale = 1f;
        private int _clearedRoom;

        public bool IsOpen => _isOpen;

        public void Configure(
            PrototypeRoomLoopController loop,
            CollectibleInventory targetInventory,
            PlayerCombatProfile profile,
            CollectibleDefinition[] pool)
        {
            roomLoop = loop;
            inventory = targetInventory;
            combatProfile = profile;
            rewardPool = pool;
        }

        private void OnEnable()
        {
            if (roomLoop != null)
                roomLoop.RoomCleared += OnRoomCleared;
        }

        private void OnDisable()
        {
            if (roomLoop != null)
                roomLoop.RoomCleared -= OnRoomCleared;
            CloseWithoutContinuing();
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

        private void OnRoomCleared(int roomIndex)
        {
            if (_isOpen)
                return;

            _clearedRoom = roomIndex;
            BuildChoices();
            if (_choices.Count == 0)
            {
                Debug.LogWarning(
                    "[ArknightsACT/Roguelite] No compatible collectible rewards remain; continuing room loop.",
                    this);
                roomLoop?.ContinueToNextRoom();
                return;
            }

            // The killing blow may still be inside HitStop (Time.timeScale == 0).
            // Cancel that transient pause first so the reward screen never records 0 as the
            // value it should restore when continuing to the next room.
            HitStopService.Instance?.Cancel();
            _previousTimeScale = Time.timeScale > 0.001f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _isOpen = true;
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
                if (combatProfile != null && !combatProfile.Supports(definition.RequiredFeatures))
                    continue;
                candidates.Add(definition);
            }

            var wanted = Mathf.Min(choiceCount, candidates.Count);
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

            var roll = Random.value * totalWeight;
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

            CloseWithoutContinuing();
            roomLoop?.ContinueToNextRoom();
        }

        private void CloseWithoutContinuing()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            _choices.Clear();
            Time.timeScale = _previousTimeScale > 0.001f ? _previousTimeScale : 1f;
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
            GUI.Label(
                new Rect(0f, height * 0.10f, width, 60f),
                $"作战 {_clearedRoom} 完成  ·  选择一件收藏品",
                titleStyle);

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
