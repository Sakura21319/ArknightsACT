using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>
    /// R3 route layer. Reuses the existing combat arena while varying node rules and rewards.
    /// Non-combat nodes are intentionally lightweight prototype interactions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteRouteController : MonoBehaviour
    {
        private enum OverlayMode
        {
            None,
            Route,
            Encounter,
            SafeHouse,
            Trader
        }

        [SerializeField] private PrototypeRoomLoopController roomLoop;
        [SerializeField] private RogueliteRewardController rewards;
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private Transform player;
        [SerializeField, Min(1)] private int combatsBeforeBoss = 4;
        [SerializeField, Range(0f, 1f)] private float normalCollectibleChance = 0.20f;

        private readonly List<RogueliteRouteNodeChoice> _routeChoices = new();
        private OverlayMode _mode;
        private RogueliteRouteNodeType _currentCombatNode = RogueliteRouteNodeType.Combat;
        private GameplayPauseService _pause;
        private string _statusMessage = string.Empty;
        private int _inputUnlockFrame;

        public bool IsOpen => _mode != OverlayMode.None;
        public RogueliteRouteNodeType CurrentCombatNode => _currentCombatNode;

        public void Configure(
            PrototypeRoomLoopController loop,
            RogueliteRewardController rewardController,
            RogueliteRunState state,
            Transform playerTransform)
        {
            roomLoop = loop;
            rewards = rewardController;
            runState = state;
            player = playerTransform;
        }

        private void OnEnable()
        {
            _pause = GameplayPauseService.Instance;
            if (roomLoop != null)
                roomLoop.RoomCleared += OnRoomCleared;
        }

        private void OnDisable()
        {
            if (roomLoop != null)
                roomLoop.RoomCleared -= OnRoomCleared;
            _pause?.Resume(this);
            _mode = OverlayMode.None;
        }

        private void Update()
        {
            if (_mode == OverlayMode.None || Time.frameCount < _inputUnlockFrame)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                ActivateOption(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                ActivateOption(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                ActivateOption(2);
        }

        private void OnRoomCleared(int roomIndex)
        {
            var isBoss = _currentCombatNode == RogueliteRouteNodeType.Boss;
            var isEmergency = _currentCombatNode == RogueliteRouteNodeType.EmergencyCombat;
            runState?.RecordCombatClear(isBoss, isEmergency);
            runState?.AddIngots(_currentCombatNode switch
            {
                RogueliteRouteNodeType.EmergencyCombat => 4,
                RogueliteRouteNodeType.Boss => 8,
                _ => 2
            });

            if (rewards == null)
            {
                OpenRouteSelection();
                return;
            }

            switch (_currentCombatNode)
            {
                case RogueliteRouteNodeType.EmergencyCombat:
                    rewards.OpenReward(
                        $"紧急作战 {roomIndex} 完成 · 选择战利品",
                        CollectibleRarity.Rare,
                        2,
                        OpenRouteSelection);
                    break;
                case RogueliteRouteNodeType.Boss:
                    rewards.OpenReward(
                        $"险路恶敌 {roomIndex} 击破 · 选择战利品",
                        CollectibleRarity.Rare,
                        3,
                        OpenRouteSelection);
                    break;
                default:
                    if (UnityEngine.Random.value < normalCollectibleChance)
                    {
                        rewards.OpenReward(
                            $"普通作战 {roomIndex} · 意外战利品",
                            CollectibleRarity.Common,
                            2,
                            OpenRouteSelection);
                    }
                    else
                    {
                        OpenRouteSelection();
                    }
                    break;
            }
        }

        private void OpenRouteSelection()
        {
            BuildRouteChoices();
            _statusMessage = string.Empty;
            _mode = OverlayMode.Route;
            ArmInputDelay();
            _pause ??= GameplayPauseService.Instance;
            if (_pause != null)
                _pause.Pause(this);
            else
                Time.timeScale = 0f;
        }

        private void BuildRouteChoices()
        {
            _routeChoices.Clear();
            if (runState != null && runState.CombatWinsSinceBoss >= Mathf.Max(1, combatsBeforeBoss))
            {
                _routeChoices.Add(CreateChoice(RogueliteRouteNodeType.Boss));
                return;
            }

            var firstType = UnityEngine.Random.value < 0.25f
                ? RogueliteRouteNodeType.EmergencyCombat
                : RogueliteRouteNodeType.Combat;
            _routeChoices.Add(CreateChoice(firstType));

            var candidates = new List<RogueliteRouteNodeType>
            {
                RogueliteRouteNodeType.Combat,
                RogueliteRouteNodeType.EmergencyCombat,
                RogueliteRouteNodeType.Encounter,
                RogueliteRouteNodeType.SafeHouse,
                RogueliteRouteNodeType.Trader
            };
            candidates.Remove(firstType);

            while (_routeChoices.Count < 3 && candidates.Count > 0)
            {
                var picked = PickWeighted(candidates);
                _routeChoices.Add(CreateChoice(picked));
                candidates.Remove(picked);
            }
        }

        private static RogueliteRouteNodeType PickWeighted(List<RogueliteRouteNodeType> candidates)
        {
            var total = 0f;
            for (var i = 0; i < candidates.Count; i++)
                total += Weight(candidates[i]);

            var roll = UnityEngine.Random.value * total;
            for (var i = 0; i < candidates.Count; i++)
            {
                roll -= Weight(candidates[i]);
                if (roll <= 0f)
                    return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private static float Weight(RogueliteRouteNodeType type) => type switch
        {
            RogueliteRouteNodeType.Combat => 40f,
            RogueliteRouteNodeType.EmergencyCombat => 18f,
            RogueliteRouteNodeType.Encounter => 20f,
            RogueliteRouteNodeType.SafeHouse => 12f,
            RogueliteRouteNodeType.Trader => 10f,
            _ => 1f
        };

        private static RogueliteRouteNodeChoice CreateChoice(RogueliteRouteNodeType type) => type switch
        {
            RogueliteRouteNodeType.EmergencyCombat => new RogueliteRouteNodeChoice(
                type,
                "紧急作战",
                "敌人 +1，生命 +35%，远程敌人提前加入；敌人经验 1.5 倍。通关 +4 源石锭，并必定获得 Rare+ 收藏品二选一。"),
            RogueliteRouteNodeType.Encounter => new RogueliteRouteNodeChoice(
                type,
                "不期而遇",
                "进入随机事件原型：在资源与恢复之间做一次选择。"),
            RogueliteRouteNodeType.SafeHouse => new RogueliteRouteNodeChoice(
                type,
                "安全屋",
                "无战斗。选择充分休整，或整理补给后继续前进。"),
            RogueliteRouteNodeType.Trader => new RogueliteRouteNodeChoice(
                type,
                "诡意行商",
                "使用源石锭购买治疗或一次收藏品选择，也可以直接离开。"),
            RogueliteRouteNodeType.Boss => new RogueliteRouteNodeChoice(
                type,
                "险路恶敌",
                "路线汇聚。迎战高生命重装防御者；Boss 经验 2 倍，通关 +8 源石锭并获得 Rare+ 收藏品三选一。"),
            _ => new RogueliteRouteNodeChoice(
                type,
                "普通作战",
                "标准敌群。击杀获得经验，通关 +2 源石锭，并有 20% 概率获得 Common+ 收藏品二选一。")
        };

        private void ActivateOption(int index)
        {
            if (Time.frameCount < _inputUnlockFrame)
                return;

            switch (_mode)
            {
                case OverlayMode.Route:
                    ChooseRoute(index);
                    break;
                case OverlayMode.Encounter:
                    ChooseEncounter(index);
                    break;
                case OverlayMode.SafeHouse:
                    ChooseSafeHouse(index);
                    break;
                case OverlayMode.Trader:
                    ChooseTrader(index);
                    break;
            }
        }

        private void ChooseRoute(int index)
        {
            if (index < 0 || index >= _routeChoices.Count)
                return;

            var selected = _routeChoices[index];
            runState?.AdvanceRouteDepth();
            _statusMessage = string.Empty;

            switch (selected.Type)
            {
                case RogueliteRouteNodeType.Encounter:
                    _mode = OverlayMode.Encounter;
                    ArmInputDelay();
                    return;
                case RogueliteRouteNodeType.SafeHouse:
                    _mode = OverlayMode.SafeHouse;
                    ArmInputDelay();
                    return;
                case RogueliteRouteNodeType.Trader:
                    _mode = OverlayMode.Trader;
                    ArmInputDelay();
                    return;
            }

            _currentCombatNode = selected.Type;
            _mode = OverlayMode.None;
            ResumeOwnPause();

            var tuning = selected.Type switch
            {
                RogueliteRouteNodeType.EmergencyCombat => new CombatRoomTuning(
                    enemyCountBonus: 1,
                    enemyCountOverride: 0,
                    healthMultiplier: 1.35f,
                    enableRangedEarly: true,
                    forcedTemplateIndex: -1,
                    label: "Emergency"),
                RogueliteRouteNodeType.Boss => new CombatRoomTuning(
                    enemyCountBonus: 0,
                    enemyCountOverride: 1,
                    healthMultiplier: 4f,
                    enableRangedEarly: true,
                    forcedTemplateIndex: 3,
                    label: "Boss"),
                _ => new CombatRoomTuning(0, 0, 1f, false, -1, "Combat")
            };

            if (roomLoop == null || !roomLoop.ContinueToNextRoom(tuning))
            {
                Debug.LogWarning("[ArknightsACT/Roguelite] Failed to continue into selected combat node.", this);
                OpenRouteSelection();
            }
        }

        private void ChooseEncounter(int index)
        {
            switch (index)
            {
                case 0:
                    runState?.AddIngots(5);
                    _statusMessage = "你搜出了还能流通的物资：获得 5 源石锭。";
                    CompleteNonCombatNode();
                    break;
                case 1:
                    var healed = HealPlayer(0.20f);
                    _statusMessage = $"简单整备完成：恢复 {healed:0} 生命。";
                    CompleteNonCombatNode();
                    break;
            }
        }

        private void ChooseSafeHouse(int index)
        {
            switch (index)
            {
                case 0:
                    var fullRest = HealPlayer(0.40f);
                    _statusMessage = $"充分休整：恢复 {fullRest:0} 生命。";
                    CompleteNonCombatNode();
                    break;
                case 1:
                    var lightRest = HealPlayer(0.15f);
                    runState?.AddIngots(4);
                    _statusMessage = $"整理补给：恢复 {lightRest:0} 生命，并获得 4 源石锭。";
                    CompleteNonCombatNode();
                    break;
            }
        }

        private void ChooseTrader(int index)
        {
            switch (index)
            {
                case 0:
                    if (runState != null && !runState.TrySpendIngots(3))
                    {
                        _statusMessage = "源石锭不足，需要 3。";
                        return;
                    }
                    var healed = HealPlayer(0.30f);
                    _statusMessage = $"购入战地医疗包：恢复 {healed:0} 生命。";
                    CompleteNonCombatNode();
                    break;
                case 1:
                    if (runState != null && !runState.TrySpendIngots(6))
                    {
                        _statusMessage = "源石锭不足，需要 6。";
                        return;
                    }

                    _mode = OverlayMode.None;
                    ResumeOwnPause();
                    if (rewards != null)
                    {
                        rewards.OpenReward(
                            "诡意行商 · 收藏品货箱",
                            CollectibleRarity.Common,
                            3,
                            OpenRouteSelection);
                    }
                    else
                    {
                        OpenRouteSelection();
                    }
                    break;
                case 2:
                    _statusMessage = "你没有消费，离开了行商。";
                    CompleteNonCombatNode();
                    break;
            }
        }

        private void CompleteNonCombatNode()
        {
            BuildRouteChoices();
            _mode = OverlayMode.Route;
            ArmInputDelay();
        }

        private void ArmInputDelay()
        {
            _inputUnlockFrame = Time.frameCount + 1;
        }

        private float HealPlayer(float fraction)
        {
            if (player == null || fraction <= 0f)
                return 0f;

            var entity = player.GetComponent<CombatEntity>();
            var health = entity != null ? entity.Health : player.GetComponent<Health>();
            if (health == null || health.IsDead)
                return 0f;
            return health.Heal(health.MaxHealth * fraction);
        }

        private void ResumeOwnPause()
        {
            if (_pause != null)
                _pause.Resume(this);
            else
                Time.timeScale = 1f;
        }

        private void OnGUI()
        {
            if (_mode == OverlayMode.None)
                return;

            var width = Screen.width;
            var height = Screen.height;
            GUI.Box(new Rect(0f, 0f, width, height), GUIContent.none);

            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 48, 20, 34),
                fontStyle = FontStyle.Bold
            };
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                wordWrap = true
            };

            var ingots = runState != null ? runState.Ingots : 0;
            var depth = runState != null ? runState.RouteDepth : 1;
            GUI.Label(new Rect(0f, height * 0.07f, width, 50f), HeaderForMode(), headerStyle);
            GUI.Label(
                new Rect(0f, height * 0.14f, width, 30f),
                $"路线深度 {depth}    ·    源石锭 {ingots}",
                subStyle);

            if (_mode == OverlayMode.Route)
                DrawRouteCards(width, height);
            else
                DrawNodeOptions(width, height);

            if (!string.IsNullOrWhiteSpace(_statusMessage))
                GUI.Label(new Rect(width * 0.18f, height * 0.82f, width * 0.64f, 60f), _statusMessage, subStyle);
        }

        private string HeaderForMode() => _mode switch
        {
            OverlayMode.Encounter => "不期而遇",
            OverlayMode.SafeHouse => "安全屋",
            OverlayMode.Trader => "诡意行商",
            _ => _routeChoices.Count == 1 && _routeChoices[0].Type == RogueliteRouteNodeType.Boss
                ? "前方只有险路"
                : "选择下一节点"
        };

        private void DrawRouteCards(float width, float height)
        {
            var count = _routeChoices.Count;
            if (count <= 0)
                return;

            var cardWidth = Mathf.Min(330f, width * 0.28f);
            var cardHeight = Mathf.Min(300f, height * 0.48f);
            var gap = Mathf.Min(30f, width * 0.025f);
            var totalWidth = count * cardWidth + Mathf.Max(0, count - 1) * gap;
            var startX = (width - totalWidth) * 0.5f;
            var startY = height * 0.25f;
            var cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(width / 90f), 14, 21),
                padding = new RectOffset(18, 18, 18, 18)
            };

            for (var i = 0; i < count; i++)
            {
                var choice = _routeChoices[i];
                var text = $"[{i + 1}]  {choice.Title}\n\n{choice.Description}";
                if (GUI.Button(new Rect(startX + i * (cardWidth + gap), startY, cardWidth, cardHeight), text, cardStyle))
                    ActivateOption(i);
            }
        }

        private void DrawNodeOptions(float width, float height)
        {
            var options = _mode switch
            {
                OverlayMode.Encounter => new[]
                {
                    "[1] 搜刮运输残骸\n获得 5 源石锭",
                    "[2] 就地整备\n恢复 20% 最大生命"
                },
                OverlayMode.SafeHouse => new[]
                {
                    "[1] 充分休整\n恢复 40% 最大生命",
                    "[2] 整理补给\n恢复 15% 最大生命，并获得 4 源石锭"
                },
                _ => new[]
                {
                    "[1] 战地医疗包 · 3 锭\n恢复 30% 最大生命",
                    "[2] 收藏品货箱 · 6 锭\n从 3 件收藏品中选择 1 件",
                    "[3] 离开\n不进行消费"
                }
            };

            var buttonWidth = Mathf.Min(390f, width * 0.34f);
            var buttonHeight = 92f;
            var gap = 18f;
            var startY = height * 0.28f;
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(width / 92f), 14, 20)
            };

            for (var i = 0; i < options.Length; i++)
            {
                var rect = new Rect((width - buttonWidth) * 0.5f, startY + i * (buttonHeight + gap), buttonWidth, buttonHeight);
                if (GUI.Button(rect, options[i], style))
                    ActivateOption(i);
            }
        }
    }
}