using System;
using System.Collections.Generic;
using System.Linq;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Progression;
using ArknightsACT.Gameplay.Roguelite.SkillUpgrades;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    public enum RogueliteShellState
    {
        Home,
        OperationPreparation,
        Running,
        ExtractionDecision,
        Settlement,
        WarehouseTrade
    }

    /// <summary>
    /// Scheme-B shell: home -> operation -> physical extraction -> settlement -> home,
    /// with a persistent warehouse/trade surface. Squad, Missions and Commander Level are
    /// intentionally presentation-only placeholders for now.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteGameFlowController : MonoBehaviour
    {
        private sealed class SettlementEntry
        {
            public CollectibleDefinition Item;
            public int Count;
        }

        private const float RefWidth = 1600f;
        private const float RefHeight = 900f;

        [SerializeField] private Transform player;
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private RogueliteMetaState metaState;

        private RogueliteStageRuntimeController _stageRuntime;
        private ScavengingInventory25D _scavenging;
        private CollectibleInventory _collectibles;
        private Health _health;
        private GameplayPauseService _pause;
        private bool _healthSubscribed;
        private PlayerRuntimeContext _subscribedPlayerRuntime;
        private float _runStartedAt;
        private bool _settlementSuccess;
        private int _settlementStage;
        private int _settlementValue;
        private int _settlementItemCount;
        private int _settlementCombatWins;
        private int _settlementExplored;
        private float _settlementDuration;
        private readonly List<SettlementEntry> _settlementItems = new();
        private readonly List<CollectibleDefinition> _marketCatalog = new();
        private CollectibleDefinition _selectedMarketItem;
        private bool _marketSellMode = true;
        private int _marketCategory;
        private Vector2 _marketScroll;
        private string _placeholderToast;
        private float _placeholderToastUntil;
        private int _selectedOperationArea;
        private int _selectedRiskLevel = 2;

        private static readonly string[] OperationAreaNames =
        {
            "切城外围", "切城核心区", "切城工业区", "切城南部废墟"
        };

        private Font _font;
        private GUIStyle _title;
        private GUIStyle _heading;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _tiny;
        private GUIStyle _center;
        private GUIStyle _button;
        private GUIStyle _buttonMuted;

        public static RogueliteGameFlowController Instance { get; private set; }
        public RogueliteShellState State { get; private set; } = RogueliteShellState.Home;
        public bool IsRunning => State == RogueliteShellState.Running;
        public RogueliteMetaState MetaState => metaState;
        public RogueliteRunState RunState => runState;
        public ScavengingInventory25D Scavenging => _scavenging;
        public IReadOnlyList<CollectibleDefinition> MarketCatalog => _marketCatalog;
        public bool SettlementSuccess => _settlementSuccess;
        public int SettlementStage => _settlementStage;
        public int SettlementValue => _settlementValue;
        public int SettlementItemCount => _settlementItemCount;
        public int SettlementCombatWins => _settlementCombatWins;
        public int SettlementExplored => _settlementExplored;
        public float SettlementDuration => _settlementDuration;
        public int SettlementEntryCount => _settlementItems.Count;
        public int SelectedOperationArea => _selectedOperationArea;
        public int SelectedRiskLevel => _selectedRiskLevel;
        public string SelectedOperationAreaName => OperationAreaNames[Mathf.Clamp(_selectedOperationArea, 0, OperationAreaNames.Length - 1)];

        public event Action<RogueliteShellState> StateChanged;

        public void Configure(Transform playerTransform, RogueliteRunState state, RogueliteStageMapController map, RogueliteMetaState meta)
        {
            player = playerTransform;
            runState = state;
            stageMap = map;
            metaState = meta;
            EnsurePlayerRuntime();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            EnsurePlayerRuntime();
            SubscribePlayerRuntime();
            ResolveReferences();
            var shellUi = GetComponent<RogueliteShellUI>() ?? gameObject.AddComponent<RogueliteShellUI>();
            shellUi.Configure(this);
            SetState(RogueliteShellState.Home);
        }

        private void OnDestroy()
        {
            if (_subscribedPlayerRuntime != null)
                _subscribedPlayerRuntime.ActivePlayerChanged -= OnActivePlayerChanged;
            _subscribedPlayerRuntime = null;
            if (_healthSubscribed && _health != null)
                _health.Died -= OnPlayerDied;
            GameplayInputBlocker.Release(this);
            _pause?.Resume(this);
            if (_font != null) Destroy(_font);
            if (Instance == this) Instance = null;
        }

        private void EnsurePlayerRuntime()
        {
            var runtime = PlayerRuntimeContext.Instance ?? GetComponent<PlayerRuntimeContext>();
            if (runtime == null)
                runtime = gameObject.AddComponent<PlayerRuntimeContext>();

            if (runtime.ActivePlayer == null && player != null)
                runtime.Configure(player);

            var switcher = runtime.GetComponent<PlayableOperatorSwitchController>() ??
                           runtime.gameObject.AddComponent<PlayableOperatorSwitchController>();
            switcher.Configure(runtime);
        }

        private void SubscribePlayerRuntime()
        {
            var runtime = PlayerRuntimeContext.Instance;
            if (runtime == null || runtime == _subscribedPlayerRuntime)
                return;

            if (_subscribedPlayerRuntime != null)
                _subscribedPlayerRuntime.ActivePlayerChanged -= OnActivePlayerChanged;

            _subscribedPlayerRuntime = runtime;
            _subscribedPlayerRuntime.ActivePlayerChanged += OnActivePlayerChanged;
        }

        private void ResolveReferences()
        {
            EnsurePlayerRuntime();
            SubscribePlayerRuntime();
            metaState ??= RogueliteMetaState.Instance ?? FindFirstObjectByType<RogueliteMetaState>();
            runState ??= RogueliteRunState.Instance ?? FindFirstObjectByType<RogueliteRunState>();
            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            _stageRuntime ??= FindFirstObjectByType<RogueliteStageRuntimeController>();
            _pause ??= GameplayPauseService.Instance ?? FindFirstObjectByType<GameplayPauseService>();

            var resolvedPlayer = PlayerRuntimeContext.Resolve(player);
            if (resolvedPlayer == null)
            {
                var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
                for (var i = 0; i < entities.Length; i++)
                {
                    if (entities[i] == null || entities[i].Team != Team.Player)
                        continue;
                    resolvedPlayer = entities[i].transform;
                    break;
                }
            }
            BindPlayer(resolvedPlayer);

            if (_marketCatalog.Count == 0 && _scavenging != null)
            {
                metaState?.PurgeRunOnlyItems(_scavenging.Catalog);
                _marketCatalog.AddRange(_scavenging.Catalog.Where(x => x != null && x.IsSalvageCommodity));
                _marketCatalog.Sort((a, b) =>
                {
                    var kind = a.IsSalvageCommodity.CompareTo(b.IsSalvageCommodity);
                    if (kind != 0) return -kind;
                    var rarity = b.SalvageRarity.CompareTo(a.SalvageRarity);
                    return rarity != 0 ? rarity : b.CollectionValue.CompareTo(a.CollectionValue);
                });
            }
        }

        private void Update()
        {
            ResolveReferences();
            if (Keyboard.current == null)
                return;

            if ((State == RogueliteShellState.WarehouseTrade || State == RogueliteShellState.OperationPreparation) && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetState(RogueliteShellState.Home);
            else if (State == RogueliteShellState.ExtractionDecision && Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelExtraction();
        }

        private void OnActivePlayerChanged(Transform previousPlayer, Transform nextPlayer)
        {
            BindPlayer(nextPlayer);
        }

        private void BindPlayer(Transform nextPlayer)
        {
            if (nextPlayer == null)
                return;

            if (player != nextPlayer)
            {
                if (_healthSubscribed && _health != null)
                    _health.Died -= OnPlayerDied;
                _healthSubscribed = false;
                player = nextPlayer;
                _scavenging = null;
                _collectibles = null;
                _health = null;
            }

            _scavenging ??= player.GetComponent<ScavengingInventory25D>();
            _collectibles ??= player.GetComponent<CollectibleInventory>();
            _health ??= player.GetComponent<Health>();
            if (!_healthSubscribed && _health != null)
            {
                _health.Died += OnPlayerDied;
                _healthSubscribed = true;
            }
        }

        public void OpenOperationPreparation()
        {
            ResolveReferences();
            SetState(RogueliteShellState.OperationPreparation);
        }

        public void SetOperationArea(int index)
        {
            _selectedOperationArea = Mathf.Clamp(index, 0, OperationAreaNames.Length - 1);
            StateChanged?.Invoke(State);
        }

        public void SetRiskLevel(int level)
        {
            _selectedRiskLevel = Mathf.Clamp(level, 1, 5);
            StateChanged?.Invoke(State);
        }

        public void BeginOperation()
        {
            ResolveReferences();
            if (runState == null || stageMap == null || _stageRuntime == null || _scavenging == null)
            {
                Debug.LogError("[ArknightsACT/Shell] Cannot begin operation: runtime references are incomplete.", this);
                return;
            }

            ResetRegisteredOperatorsForNewRun();
            runState.ResetRun();
            stageMap.GenerateStage(1);
            _stageRuntime.RestartRun();
            _runStartedAt = Time.unscaledTime;
            _settlementItems.Clear();
            SetState(RogueliteShellState.Running);
        }

        private void ResetRegisteredOperatorsForNewRun()
        {
            var runtime = PlayerRuntimeContext.Instance;
            if (runtime == null)
            {
                ResetOperatorForNewRun(player);
                return;
            }

            var operators = runtime.RegisteredPlayers;
            for (var i = 0; i < operators.Count; i++)
                ResetOperatorForNewRun(operators[i]);

            // Ensure the currently resolved player participates even if a legacy scene registered late.
            if (player != null)
            {
                var registered = false;
                for (var i = 0; i < operators.Count; i++)
                {
                    if (operators[i] == player)
                    {
                        registered = true;
                        break;
                    }
                }
                if (!registered)
                {
                    runtime.RegisterPlayer(player);
                    ResetOperatorForNewRun(player);
                }
            }

            ResolveReferences();
        }

        private static void ResetOperatorForNewRun(Transform operatorRoot)
        {
            if (operatorRoot == null)
                return;

            operatorRoot.GetComponent<CollectibleInventory>()?.ClearRunCollectibles();
            operatorRoot.GetComponent<LevelUpgradeInventory>()?.ResetRun();
            operatorRoot.GetComponent<CharacterSkillUpgradeInventory>()?.ResetRun();
            operatorRoot.GetComponent<ScavengingInventory25D>()?.BeginNewRun();

            var health = operatorRoot.GetComponent<Health>();
            if (health != null)
                health.SetMaxHealth(health.MaxHealth, refill: true);

            var behaviours = operatorRoot.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerRunResettable resettable)
                    resettable.ResetForNewRun();
            }
        }

        public void OpenExtractionDecision()
        {
            if (State != RogueliteShellState.Running)
                return;
            ResolveReferences();
            SetState(RogueliteShellState.ExtractionDecision);
        }

        public void CancelExtraction()
        {
            if (State == RogueliteShellState.ExtractionDecision)
                SetState(RogueliteShellState.Running);
        }

        public void OpenWarehouseTrade()
        {
            ResolveReferences();
            _marketSellMode = true;
            _selectedMarketItem = null;
            SetState(RogueliteShellState.WarehouseTrade);
        }

        public void ReturnHome() => SetState(RogueliteShellState.Home);

        public CollectibleDefinition GetSettlementItemDefinition(int index) =>
            index >= 0 && index < _settlementItems.Count ? _settlementItems[index].Item : null;

        public int GetSettlementItemStack(int index) =>
            index >= 0 && index < _settlementItems.Count ? _settlementItems[index].Count : 0;

        public void ConfirmExtraction()
        {
            if (State != RogueliteShellState.ExtractionDecision || _scavenging == null)
                return;

            BuildSettlementSnapshot(true);
            metaState?.Deposit(_scavenging.Pending);
            _scavenging.SecureHaul();
            _stageRuntime?.MarkRunEnded();
            _collectibles?.ClearRunCollectibles();
            SetState(RogueliteShellState.Settlement);
        }

        private void OnPlayerDied()
        {
            if (State != RogueliteShellState.Running && State != RogueliteShellState.ExtractionDecision)
                return;

            BuildSettlementSnapshot(false);
            _stageRuntime?.MarkRunEnded();
            _collectibles?.ClearRunCollectibles();
            SetState(RogueliteShellState.Settlement);
        }

        private void BuildSettlementSnapshot(bool success)
        {
            ResolveReferences();
            _settlementSuccess = success;
            _settlementStage = runState != null ? runState.StageIndex : 1;
            _settlementCombatWins = runState != null ? runState.CombatWinsSinceBoss : 0;
            _settlementExplored = runState != null ? runState.ExploredBlocks : 0;
            _settlementDuration = Mathf.Max(0f, Time.unscaledTime - _runStartedAt);
            _settlementValue = 0;
            _settlementItemCount = 0;
            _settlementItems.Clear();

            if (!success || _scavenging == null)
                return;

            var map = new Dictionary<string, SettlementEntry>();
            for (var i = 0; i < _scavenging.Pending.Count; i++)
            {
                var item = _scavenging.Pending[i];
                if (item == null || !item.IsSalvageCommodity) continue;
                _settlementValue += item.CollectionValue;
                _settlementItemCount++;
                if (!map.TryGetValue(item.Id, out var entry))
                {
                    entry = new SettlementEntry { Item = item, Count = 0 };
                    map.Add(item.Id, entry);
                    _settlementItems.Add(entry);
                }
                entry.Count++;
            }
        }

        private void SetState(RogueliteShellState next)
        {
            State = next;
            var modal = next != RogueliteShellState.Running;
            GameplayInputBlocker.SetBlocked(this, modal);

            _pause ??= GameplayPauseService.Instance ?? FindFirstObjectByType<GameplayPauseService>();
            if (_pause != null)
            {
                if (modal) _pause.Pause(this);
                else _pause.Resume(this);
            }
            else
            {
                Time.timeScale = modal ? 0f : 1f;
            }

            StateChanged?.Invoke(next);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 16);
            _title = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 34, fontStyle = FontStyle.Bold };
            _heading = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 22, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 16 };
            _small = new GUIStyle(_label) { fontSize = 13 };
            _tiny = new GUIStyle(_label) { fontSize = 11 };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { font = _font, fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(22, 18, 10, 10) };
            _buttonMuted = new GUIStyle(_button);
        }

        private void OnGUI()
        {
            var shellUi = GetComponent<RogueliteShellUI>();
            if (shellUi != null && shellUi.IsReady)
                return;
            if (State == RogueliteShellState.Running)
                return;

            ResolveReferences();
            EnsureStyles();

            var saved = GUI.matrix;
            var scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);

            Fill(new Rect(0f, 0f, RefWidth, RefHeight), new Color(0.91f, 0.93f, 0.94f, 1f));
            switch (State)
            {
                case RogueliteShellState.Home:
                    DrawHome();
                    break;
                case RogueliteShellState.OperationPreparation:
                    DrawOperationPreparation();
                    break;
                case RogueliteShellState.ExtractionDecision:
                    DrawExtraction();
                    break;
                case RogueliteShellState.Settlement:
                    DrawSettlement();
                    break;
                case RogueliteShellState.WarehouseTrade:
                    DrawWarehouseTrade();
                    break;
            }

            GUI.matrix = saved;
        }

        private void DrawHome()
        {
            DrawTopBar("主页", false);

            var left = new Rect(26f, 92f, 550f, 720f);
            Fill(left, new Color(0.08f, 0.105f, 0.12f, 1f));
            Fill(new Rect(left.x, left.y, 5f, left.height), new Color(0.20f, 0.55f, 0.85f));
            GUI.Label(new Rect(left.x + 34f, left.y + 42f, 420f, 50f), "ARKNIGHTS ACT", _title);
            GUI.Label(new Rect(left.x + 36f, left.y + 100f, 300f, 28f), "TACTICAL RECOVERY", _small);

            var commander = new Rect(left.x + 28f, left.yMax - 92f, 250f, 60f);
            Fill(commander, new Color(0.14f, 0.17f, 0.19f, 1f));
            GUI.Label(new Rect(commander.x + 16f, commander.y + 8f, 180f, 20f), "指挥官等级", _tiny);
            GUI.Label(new Rect(commander.x + 16f, commander.y + 27f, 180f, 28f), $"Lv.{metaState?.CommanderLevel ?? 1}", _heading);

            var x = 620f;
            if (GUI.Button(new Rect(x, 145f, 900f, 180f), "开始行动   >", _button))
                OpenOperationPreparation();

            GUI.enabled = false;
            GUI.Button(new Rect(x, 350f, 435f, 135f), "编队\n敬请期待", _buttonMuted);
            GUI.Button(new Rect(x + 465f, 350f, 435f, 135f), "任务\n敬请期待", _buttonMuted);
            GUI.enabled = true;

            if (GUI.Button(new Rect(x, 515f, 900f, 160f), "仓库 / 交易   >", _button))
            {
                _marketSellMode = false;
                _selectedMarketItem = null;
                SetState(RogueliteShellState.WarehouseTrade);
            }

            if (!string.IsNullOrWhiteSpace(_placeholderToast) && Time.unscaledTime < _placeholderToastUntil)
                GUI.Label(new Rect(x, 700f, 900f, 32f), _placeholderToast, _center);
        }

        private void DrawOperationPreparation()
        {
            DrawTopBar("正式行动准备", true);
            GUI.Label(new Rect(36f, 108f, 420f, 42f), "01  区域选择 / 切城", _heading);
            for (var i = 0; i < OperationAreaNames.Length; i++)
            {
                var selected = i == _selectedOperationArea;
                GUI.backgroundColor = selected ? new Color(0.18f, 0.72f, 0.95f) : Color.white;
                if (GUI.Button(new Rect(36f, 162f + i * 112f, 390f, 92f), OperationAreaNames[i], _button))
                    SetOperationArea(i);
            }

            GUI.backgroundColor = Color.white;
            Fill(new Rect(460f, 108f, 630f, 610f), new Color(0.06f, 0.075f, 0.085f, 1f));
            GUI.Label(new Rect(490f, 138f, 360f, 52f), "切城", _title);
            GUI.Label(new Rect(490f, 194f, 540f, 70f),
                $"当前区域：{SelectedOperationAreaName}\n仅开放切城行动区域。", _label);

            GUI.Label(new Rect(1125f, 108f, 420f, 42f), "02  风险等级", _heading);
            for (var level = 1; level <= 5; level++)
            {
                var selected = level == _selectedRiskLevel;
                GUI.backgroundColor = selected ? new Color(1f, 0.42f, 0.075f) : Color.white;
                if (GUI.Button(new Rect(1125f, 162f + (level - 1) * 92f, 390f, 74f),
                        $"{level}  {RiskLevelName(level)}", _button))
                    SetRiskLevel(level);
            }

            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(1125f, 680f, 180f, 70f), "返回主页", _center))
                ReturnHome();
            if (GUI.Button(new Rect(1320f, 680f, 195f, 70f), "开始行动", _center))
                BeginOperation();
        }

        private static string RiskLevelName(int level) => level switch
        {
            1 => "低风险",
            2 => "标准",
            3 => "高风险",
            4 => "危险",
            5 => "极限",
            _ => "标准"
        };

        private void DrawExtraction()
        {
            DrawTopBar("撤离决策", true);
            var stage = runState != null ? runState.StageIndex : 1;
            var risk = Mathf.Clamp(18 + (_selectedRiskLevel - 1) * 18 + (stage - 1) * 8 + (runState != null ? runState.EmergencyClears * 4 : 0), 0, 95);
            var pendingValue = _scavenging != null ? _scavenging.PendingCollectionValue : 0;

            Panel(new Rect(35f, 115f, 430f, 640f));
            GUI.Label(new Rect(65f, 145f, 350f, 38f), $"行动区域  A-{stage}", _heading);
            GUI.Label(new Rect(65f, 205f, 300f, 26f), "风险评估", _small);
            GUI.Label(new Rect(65f, 240f, 220f, 90f), $"{risk}%", new GUIStyle(_title) { fontSize = 58 });
            DrawBar(new Rect(65f, 337f, 340f, 10f), risk / 100f, new Color(0.95f, 0.58f, 0.18f));
            GUI.Label(new Rect(65f, 380f, 320f, 26f), $"当前阶段     {stage}/3", _label);
            GUI.Label(new Rect(65f, 420f, 320f, 26f), $"已探索区块   {runState?.ExploredBlocks ?? 0}", _label);
            GUI.Label(new Rect(65f, 460f, 320f, 26f), $"紧急作战     {runState?.EmergencyClears ?? 0}", _label);

            Panel(new Rect(500f, 115f, 1065f, 450f));
            GUI.Label(new Rect(530f, 145f, 300f, 36f), "背包", _heading);
            GUI.Label(new Rect(1270f, 150f, 240f, 30f),
                _scavenging != null ? $"{_scavenging.UsedBackpackCells}/{_scavenging.BackpackCellCapacity} 格" : "0/0 格", _label);
            DrawPendingGrid(new Rect(530f, 205f, 995f, 250f));
            GUI.Label(new Rect(530f, 485f, 500f, 38f), $"预计总价值  {pendingValue:N0} 龙门币", _heading);

            if (GUI.Button(new Rect(500f, 600f, 500f, 120f), "继续探索", _button))
                CancelExtraction();
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.66f, 0.20f);
            if (GUI.Button(new Rect(1030f, 600f, 535f, 120f), "立即撤离   >", _button))
                ConfirmExtraction();
            GUI.backgroundColor = previous;

            GUI.Label(new Rect(500f, 748f, 1065f, 25f), "撤离后：普通物资进入系统仓库；藏品与源石锭均仅限本局。", _small);
        }

        private void DrawSettlement()
        {
            DrawTopBar("行动结算", false);
            Panel(new Rect(35f, 115f, 1530f, 650f));

            GUI.Label(new Rect(70f, 150f, 550f, 54f), _settlementSuccess ? "行动完成" : "行动失败", _title);
            GUI.Label(new Rect(70f, 210f, 500f, 28f), _settlementSuccess ? "撤离成功" : "未撤离物资已遗失", _heading);

            GUI.Label(new Rect(70f, 285f, 360f, 30f), "回收物资", _heading);
            if (_settlementSuccess && _settlementItems.Count > 0)
            {
                var shown = Mathf.Min(8, _settlementItems.Count);
                for (var i = 0; i < shown; i++)
                {
                    var entry = _settlementItems[i];
                    var rect = new Rect(70f + (i % 4) * 170f, 335f + (i / 4) * 115f, 155f, 95f);
                    DrawItemTile(rect, entry.Item, entry.Count);
                }
            }
            else
            {
                GUI.Label(new Rect(70f, 335f, 620f, 40f), "无可入库物资", _label);
            }

            Panel(new Rect(850f, 155f, 660f, 500f));
            GUI.Label(new Rect(885f, 185f, 300f, 34f), "行动数据", _heading);
            GUI.Label(new Rect(885f, 250f, 560f, 28f), $"到达阶段        {_settlementStage}/3", _label);
            GUI.Label(new Rect(885f, 295f, 560f, 28f), $"探索区块        {_settlementExplored}", _label);
            GUI.Label(new Rect(885f, 340f, 560f, 28f), $"战斗记录        {_settlementCombatWins}", _label);
            GUI.Label(new Rect(885f, 385f, 560f, 28f), $"行动时间        {FormatDuration(_settlementDuration)}", _label);
            GUI.Label(new Rect(885f, 455f, 560f, 28f), $"回收件数        {_settlementItemCount}", _label);
            GUI.Label(new Rect(885f, 505f, 560f, 38f), $"回收总价值      {_settlementValue:N0}", _heading);
            GUI.Label(new Rect(885f, 560f, 560f, 26f), "物资已进入仓库，出售后才结算为龙门币。", _small);

            if (GUI.Button(new Rect(1110f, 685f, 400f, 62f), "继续   >", _button))
                SetState(RogueliteShellState.Home);
        }

        private void DrawWarehouseTrade()
        {
            DrawTopBar("仓库 / 交易", true);

            Panel(new Rect(28f, 105f, 250f, 735f));
            GUI.Label(new Rect(52f, 132f, 180f, 32f), "分类", _heading);
            var categories = new[] { "全部", "普通物资" };
            for (var i = 0; i < categories.Length; i++)
            {
                var old = GUI.backgroundColor;
                if (_marketCategory == i) GUI.backgroundColor = new Color(0.62f, 0.82f, 1f);
                if (GUI.Button(new Rect(48f, 190f + i * 65f, 210f, 50f), categories[i], _center))
                {
                    _marketCategory = i;
                    _selectedMarketItem = null;
                }
                GUI.backgroundColor = old;
            }
            if (GUI.Button(new Rect(48f, 760f, 210f, 48f), "< 返回主页", _center))
                SetState(RogueliteShellState.Home);

            Panel(new Rect(300f, 105f, 820f, 735f));
            var oldTab = GUI.backgroundColor;
            GUI.backgroundColor = _marketSellMode ? new Color(0.70f, 0.86f, 1f) : oldTab;
            if (GUI.Button(new Rect(330f, 132f, 180f, 48f), "我的物品", _center)) { _marketSellMode = true; _selectedMarketItem = null; }
            GUI.backgroundColor = !_marketSellMode ? new Color(0.70f, 0.86f, 1f) : oldTab;
            if (GUI.Button(new Rect(520f, 132f, 180f, 48f), "商店", _center)) { _marketSellMode = false; _selectedMarketItem = null; }
            GUI.backgroundColor = oldTab;

            var items = FilterMarketItems();
            var view = new Rect(325f, 205f, 765f, 600f);
            var content = new Rect(0f, 0f, 730f, Mathf.Max(600f, items.Count * 62f));
            _marketScroll = GUI.BeginScrollView(view, _marketScroll, content);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var y = i * 62f;
                var selected = item == _selectedMarketItem;
                if (selected) Fill(new Rect(0f, y, 710f, 56f), new Color(0.72f, 0.84f, 0.93f, 0.75f));
                var owned = metaState?.GetCount(item.Id) ?? 0;
                var price = _marketSellMode ? metaState?.GetSellPrice(item) ?? 0 : metaState?.GetBuyPrice(item) ?? 0;
                GUI.Label(new Rect(16f, y + 8f, 320f, 24f), item.DisplayName, _label);
                GUI.Label(new Rect(350f, y + 8f, 120f, 24f), $"持有 {owned}", _small);
                GUI.Label(new Rect(510f, y + 8f, 160f, 24f), $"{price:N0}", _label);
                if (GUI.Button(new Rect(0f, y, 710f, 56f), GUIContent.none, GUIStyle.none))
                    _selectedMarketItem = item;
            }
            GUI.EndScrollView();

            Panel(new Rect(1145f, 105f, 420f, 735f));
            if (_selectedMarketItem == null)
            {
                GUI.Label(new Rect(1180f, 150f, 340f, 40f), "选择物品", _heading);
                return;
            }

            var selectedItem = _selectedMarketItem;
            DrawItemIcon(new Rect(1240f, 160f, 220f, 150f), selectedItem);
            GUI.Label(new Rect(1180f, 335f, 340f, 42f), selectedItem.DisplayName, _heading);
            GUI.Label(new Rect(1180f, 385f, 340f, 28f), selectedItem.IsSalvageCommodity ? "普通物资" : "藏品", _small);
            GUI.Label(new Rect(1180f, 430f, 340f, 100f), string.IsNullOrWhiteSpace(selectedItem.Description) ? "暂无说明" : selectedItem.Description, new GUIStyle(_small) { wordWrap = true });
            var ownedCount = metaState?.GetCount(selectedItem.Id) ?? 0;
            var unitPrice = _marketSellMode ? metaState?.GetSellPrice(selectedItem) ?? 0 : metaState?.GetBuyPrice(selectedItem) ?? 0;
            GUI.Label(new Rect(1180f, 555f, 340f, 28f), $"持有数量  {ownedCount}", _label);
            GUI.Label(new Rect(1180f, 595f, 340f, 34f), $"{(_marketSellMode ? "出售" : "购买")}单价  {unitPrice:N0}", _heading);

            GUI.enabled = _marketSellMode ? ownedCount > 0 : metaState != null && metaState.Lmd >= unitPrice;
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = _marketSellMode ? new Color(0.88f, 0.66f, 0.28f) : new Color(0.34f, 0.67f, 0.96f);
            if (GUI.Button(new Rect(1180f, 680f, 340f, 74f), _marketSellMode ? "出售 1 件" : "购买 1 件", _button))
            {
                if (_marketSellMode) metaState?.TrySell(selectedItem, 1);
                else metaState?.TryBuy(selectedItem, 1);
            }
            GUI.backgroundColor = oldColor;
            GUI.enabled = true;
        }

        private List<CollectibleDefinition> FilterMarketItems()
        {
            var result = new List<CollectibleDefinition>();
            for (var i = 0; i < _marketCatalog.Count; i++)
            {
                var item = _marketCatalog[i];
                if (item == null) continue;
                if (!item.IsSalvageCommodity) continue;
                if (_marketCategory == 1 && !item.IsSalvageCommodity) continue;
                if (_marketSellMode && (metaState == null || metaState.GetCount(item.Id) <= 0)) continue;
                result.Add(item);
            }
            return result;
        }

        private void DrawPendingGrid(Rect rect)
        {
            if (_scavenging == null)
                return;

            const int columns = 6;
            const int rows = 2;
            const float gap = 10f;
            var cellW = (rect.width - gap * (columns - 1)) / columns;
            var cellH = (rect.height - gap * (rows - 1)) / rows;
            var shown = Mathf.Min(columns * rows, _scavenging.PendingCount);
            for (var i = 0; i < columns * rows; i++)
            {
                var cell = new Rect(rect.x + (i % columns) * (cellW + gap), rect.y + (i / columns) * (cellH + gap), cellW, cellH);
                Fill(cell, new Color(0.09f, 0.12f, 0.14f, 1f));
                if (i >= shown) continue;
                var item = _scavenging.Pending[i];
                DrawItemIcon(new Rect(cell.x + 8f, cell.y + 8f, cell.width - 16f, cell.height - 30f), item);
                GUI.Label(new Rect(cell.x + 5f, cell.yMax - 23f, cell.width - 10f, 20f), item.DisplayName, new GUIStyle(_tiny) { alignment = TextAnchor.MiddleCenter });
            }
        }

        private void DrawItemTile(Rect rect, CollectibleDefinition item, int count)
        {
            Fill(rect, new Color(0.09f, 0.12f, 0.14f, 1f));
            DrawItemIcon(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 33f), item);
            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 25f, rect.width - 12f, 20f), $"{item.DisplayName} ×{count}", new GUIStyle(_tiny) { alignment = TextAnchor.MiddleCenter });
        }

        private static void DrawItemIcon(Rect rect, CollectibleDefinition item)
        {
            if (item == null) return;
            var sprite = item.SalvageIcon;
            if (sprite != null && sprite.texture != null)
            {
                var source = sprite.rect;
                var uv = new Rect(source.x / sprite.texture.width, source.y / sprite.texture.height, source.width / sprite.texture.width, source.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
            }
            else if (item.SalvageIconTexture != null)
            {
                GUI.DrawTexture(rect, item.SalvageIconTexture, ScaleMode.ScaleToFit, true);
            }
        }

        private void DrawTopBar(string title, bool back)
        {
            Fill(new Rect(0f, 0f, RefWidth, 78f), new Color(0.07f, 0.09f, 0.105f, 1f));
            GUI.Label(new Rect(32f, 17f, 500f, 45f), title, _title);
            GUI.Label(new Rect(1080f, 22f, 250f, 34f), $"龙门币  {(metaState?.Lmd ?? 0):N0}", _heading);
            GUI.Label(new Rect(1350f, 22f, 210f, 34f), $"源石锭  {runState?.Ingots ?? 0}", _heading);
            if (back && State != RogueliteShellState.ExtractionDecision && GUI.Button(new Rect(930f, 18f, 115f, 42f), "返回", _center))
                SetState(RogueliteShellState.Home);
        }

        private static void Panel(Rect rect)
        {
            Fill(rect, new Color(0.96f, 0.97f, 0.975f, 1f));
            Stroke(rect, new Color(0.63f, 0.67f, 0.70f, 1f), 1f);
        }

        private static void DrawBar(Rect rect, float ratio, Color color)
        {
            Fill(rect, new Color(0.25f, 0.28f, 0.30f, 1f));
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), color);
        }

        private static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Stroke(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static string FormatDuration(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
