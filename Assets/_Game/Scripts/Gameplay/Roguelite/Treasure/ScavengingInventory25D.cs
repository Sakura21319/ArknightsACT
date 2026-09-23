using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    /// <summary>
    /// Grid-based unsecured haul plus the salvage interaction state machine.
    /// The backpack starts at 4x5 cells. Items occupy their real scavenging footprint and are packed
    /// row-major / first-fit. During a run the player can spend Originium Ingots to expand the grid.
    /// </summary>
    public sealed class ScavengingInventory25D : MonoBehaviour, IPlayerSwitchStateTransfer
    {
        public const int InitialBackpackWidth = 4;
        public const int InitialBackpackHeight = 5;
        public const int MaxBackpackLevel = 5;
        public const float InteractRange = 2.0f;

        private static readonly Vector2Int[] BackpackSizes =
        {
            new(4, 5),
            new(4, 6),
            new(5, 6),
            new(5, 7),
            new(6, 7),
            new(6, 8)
        };

        private static readonly int[] BackpackUpgradeCosts = { 4, 7, 10, 14, 18 };

        public readonly struct BackpackPlacement
        {
            public readonly int PendingIndex;
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;

            public BackpackPlacement(int pendingIndex, int x, int y, int width, int height)
            {
                PendingIndex = pendingIndex;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        private readonly List<CollectibleDefinition> _pending = new();
        private readonly List<Vector2Int> _pendingPositions = new();
        private readonly List<CollectibleDefinition> _catalog = new();
        private readonly HashSet<string> _discoveredCommodities = new();
        private CombatEntity _entity;
        private CollectibleInventory _inventory;
        private SearchableContainer25D _candidate;
        private WorldSalvagePickup25D _groundCandidate;
        private SearchableContainer25D _openContainer;
        private bool _backpackOpen;
        private bool _finished;
        private Vector3 _lastPosition;
        private float _lastPickupTime = -99f;
        private int _lastPickupIndex = -1;
        private int _securedCollectionValue;
        private int _backpackLevel;
        private string _notice = "进入房屋寻找物资 · 靠近容器按 F 检索 · 关卡出口撤离结算";

        public event System.Action Changed;

        public int PendingCount => _pending.Count;
        public IReadOnlyList<CollectibleDefinition> Pending => _pending;
        public int BackpackLevel => _backpackLevel;
        public int BackpackWidth => BackpackSizes[Mathf.Clamp(_backpackLevel, 0, MaxBackpackLevel)].x;
        public int BackpackHeight => BackpackSizes[Mathf.Clamp(_backpackLevel, 0, MaxBackpackLevel)].y;
        public int BackpackCellCapacity => BackpackWidth * BackpackHeight;
        public int UsedBackpackCells
        {
            get
            {
                var used = 0;
                foreach (var item in _pending)
                    if (item != null) used += item.SalvageGridSize.x * item.SalvageGridSize.y;
                return used;
            }
        }
        public int FreeBackpackCells => Mathf.Max(0, BackpackCellCapacity - UsedBackpackCells);
        public bool CanUpgradeBackpack => _backpackLevel < MaxBackpackLevel;
        public int NextBackpackUpgradeCost => CanUpgradeBackpack ? BackpackUpgradeCosts[_backpackLevel] : 0;
        public Vector2Int NextBackpackSize => CanUpgradeBackpack ? BackpackSizes[_backpackLevel + 1] : BackpackSizes[MaxBackpackLevel];
        public IReadOnlyList<CollectibleDefinition> Catalog => _catalog;
        public CollectibleInventory Secured => _inventory;
        /// <summary>Container whose search window is currently open, or null.</summary>
        public SearchableContainer25D ActiveContainer => _openContainer;
        public SearchableContainer25D Candidate => _candidate;
        public WorldSalvagePickup25D GroundCandidate => _groundCandidate;
        public bool BackpackOpen => _backpackOpen;
        public bool InterfaceOpen => _openContainer != null || _backpackOpen;
        public string Notice => _notice;
        public int LastPickupIndex => _lastPickupIndex;
        public float LastPickupTime => _lastPickupTime;
        public int SecuredCollectionValue => _securedCollectionValue;
        public int PendingCollectionValue
        {
            get
            {
                var value = 0;
                foreach (var item in _pending)
                    if (item != null && item.IsSalvageCommodity)
                        value += item.CollectionValue;
                return value;
            }
        }
        public int DiscoveredCommodityCount => _discoveredCommodities.Count;
        public bool Paused => Time.timeScale <= 0f ||
                              (GameplayPauseService.Instance != null && GameplayPauseService.Instance.IsPaused);

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _inventory = GetComponent<CollectibleInventory>();
            _catalog.AddRange(ScavengingCatalog.Load());
            // The saved prototype scene already carries this component, so the window has to be attached here.
            if (GetComponent<ScavengingWindowUI>() == null) gameObject.AddComponent<ScavengingWindowUI>();
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (_entity == null) return;
            _entity.Damaged += OnDamaged;
            if (_entity.Health != null) _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_entity != null)
            {
                _entity.Damaged -= OnDamaged;
                if (_entity.Health != null) _entity.Health.Died -= OnDied;
            }
            _openContainer = null;
            _backpackOpen = false;
            GameplayInputBlocker.Release(this);
        }

        private void OnDamaged(DamageContext context, DamageResult result)
        {
            if (!result.Applied) return;
            if (InterfaceOpen)
                _notice = "受击 · 搜刮界面保持打开，关闭界面后恢复战斗操作";
        }

        private void OnDied()
        {
            CloseContainerWindow();
            _backpackOpen = false;
            UpdateGameplayInputLock();
            var lostCount = _pending.Count;
            ReleasePendingRelics();
            _notice = $"行动失败：遗失 {lostCount} 件未撤离物资";
            _pending.Clear();
            _pendingPositions.Clear();
            _groundCandidate = null;
            WorldSalvagePickup25D.ClearAll();
            Changed?.Invoke();
        }

        public void CopySwitchStateTo(Transform destination)
        {
            if (destination == null)
                return;
            var target = destination.GetComponent<ScavengingInventory25D>();
            if (target == null || target == this)
                return;

            target.CloseContainerWindow();
            target._backpackOpen = false;
            target.UpdateGameplayInputLock();

            target._pending.Clear();
            target._pending.AddRange(_pending);
            target._pendingPositions.Clear();
            target._pendingPositions.AddRange(_pendingPositions);
            target._discoveredCommodities.Clear();
            foreach (var id in _discoveredCommodities)
                target._discoveredCommodities.Add(id);

            target._backpackLevel = _backpackLevel;
            target._securedCollectionValue = _securedCollectionValue;
            target._finished = _finished;
            target._lastPosition = destination.position;
            target._lastPickupTime = _lastPickupTime;
            target._lastPickupIndex = _lastPickupIndex;
            target._candidate = null;
            target._groundCandidate = null;
            target._openContainer = null;
            target._notice = _notice;
            target.Changed?.Invoke();
        }

        public void BeginNewRun()
        {
            CloseContainerWindow();
            _backpackOpen = false;
            UpdateGameplayInputLock();
            ReleasePendingRelics();
            _pending.Clear();
            _pendingPositions.Clear();
            WorldSalvagePickup25D.ClearAll();
            _discoveredCommodities.Clear();
            _backpackLevel = 0;
            _securedCollectionValue = 0;
            _finished = false;
            _candidate = null;
            _groundCandidate = null;
            _lastPickupIndex = -1;
            _lastPickupTime = -99f;
            _notice = "进入房屋寻找物资 · 靠近容器按 F 检索 · 前往撤离点结算";
            Changed?.Invoke();
        }

        public void SecureHaul()
        {
            if (_inventory == null || _entity == null || _entity.Health.IsDead) return;
            CloseContainerWindow();
            _backpackOpen = false;
            UpdateGameplayInputLock();
            var value = 0;
            var collectibles = 0;
            var commodities = 0;
            foreach (var item in _pending)
            {
                if (item == null) continue;
                if (item.IsSalvageCommodity)
                {
                    value += item.CollectionValue;
                    commodities++;
                    _discoveredCommodities.Add(item.Id);
                }
                else
                {
                    // Relics are run-only. Their effects are cleared by the run lifecycle after
                    // extraction and they never become persistent warehouse items.
                    collectibles++;
                }
            }
            _securedCollectionValue += value;
            _pending.Clear();
            _pendingPositions.Clear();
            _groundCandidate = null;
            WorldSalvagePickup25D.ClearAll();
            _notice = $"撤离成功：{commodities} 件普通物资入库 · {collectibles} 件藏品随本局结束";
            Changed?.Invoke();
        }

        public void FinishRun()
        {
            SecureHaul();
            _finished = true;
        }

        // ---- Interaction API (also the seam used by runtime validation) ----

        /// <summary>Opens the container window and rolls its loot list on first contact.</summary>
        public bool OpenContainer(SearchableContainer25D container)
        {
            if (container == null || container.Emptied) return false;
            if ((container.transform.position - transform.position).sqrMagnitude > InteractRange * InteractRange) return false;
            container.EnsureSlots((index, seed) => ResolveContainerReward(seed, container.Tier));
            if (container.SlotCount == 0)
            {
                // Everything it could roll is already at its stack cap; stop offering the prompt.
                container.MarkBarren();
                return false;
            }
            _openContainer = container;
            _backpackOpen = false;
            _candidate = null;
            _groundCandidate = null;
            _lastPosition = transform.position;
            UpdateGameplayInputLock();
            _notice = container.RevealedCount > 0
                ? "取走已识别物资，或继续检索剩余物品 · 游戏操作已锁定"
                : "自动检索中 · 游戏操作已锁定，关闭界面后恢复";
            return true;
        }

        /// <summary>
        /// Drives the automatic queue for the open container. Gameplay input is locked while this
        /// window is open, so normal movement/attacks cannot interrupt it. Only explicit UI close,
        /// death or run completion releases the interaction.
        /// </summary>
        public bool AdvanceSearch(float deltaTime)
        {
            if (_openContainer == null) return false;

            _lastPosition = transform.position;
            if (_finished || _entity == null || _entity.Health == null || _entity.Health.IsDead)
            {
                CloseContainerWindow();
                return false;
            }
            if (Paused) return true;

            var slot = ActiveSlot();
            if (slot == null) return true;
            slot.Progress += Mathf.Max(0f, deltaTime) / Mathf.Max(0.1f, slot.Duration);
            if (slot.Progress < 1f) return true;
            _openContainer.Reveal(slot);
            _notice = $"识别完成 · {slot.Item.DisplayName}";
            return true;
        }

        /// <summary>
        /// Unified reward entry point for containers, combat rewards and treasure chests.
        /// Relics become active immediately and remain run-only; ordinary salvage stays unsecured until extraction.
        /// </summary>
        public bool TryAcceptReward(CollectibleDefinition item, string sourceLabel = null) =>
            TryAcceptRewardInternal(item, sourceLabel, reacquireDroppedRelic: false);

        public bool TryAcceptWorldPickup(CollectibleDefinition item, bool reacquireDroppedRelic) =>
            TryAcceptRewardInternal(item, "地面拾取", reacquireDroppedRelic);

        private bool TryAcceptRewardInternal(CollectibleDefinition item, string sourceLabel, bool reacquireDroppedRelic)
        {
            if (!TryFindFreeSpot(item, out var position))
            {
                _notice = item != null && !item.IsSalvageCommodity && !_inventory.CanAcquire(item)
                    ? $"{item.DisplayName} 已达叠加上限"
                    : "背包没有足够的连续格位 · 拖拽物品整理空间、丢弃物品或使用源石锭扩容";
                return false;
            }

            if (!item.IsSalvageCommodity)
            {
                var acquired = reacquireDroppedRelic
                    ? _inventory.ReacquireDropped(item)
                    : _inventory.Acquire(item);
                if (!acquired)
                {
                    _notice = $"{item.DisplayName} 无法继续叠加";
                    return false;
                }
            }

            _pending.Add(item);
            _pendingPositions.Add(position);
            Changed?.Invoke();
            var source = string.IsNullOrWhiteSpace(sourceLabel) ? string.Empty : $" · {sourceLabel}";
            _notice = item.IsSalvageCommodity
                ? $"已收入背包 · {item.DisplayName}（{UsedBackpackCells}/{BackpackCellCapacity} 格）{source}"
                : $"已获得藏品 · {item.DisplayName} · 效果立即生效（仅本局）{source}";
            return true;
        }

        public bool CanAcceptReward(CollectibleDefinition item) => CanCarry(item);

        public bool TryUpgradeBackpack()
        {
            if (!CanUpgradeBackpack)
            {
                _notice = $"背包已达到最大规格 {BackpackWidth}×{BackpackHeight}";
                return false;
            }

            var runState = RogueliteRunState.Instance ?? FindFirstObjectByType<RogueliteRunState>();
            var cost = NextBackpackUpgradeCost;
            if (runState == null || !runState.TrySpendIngots(cost))
            {
                _notice = $"扩容需要 {cost} 源石锭 · 当前 {(runState != null ? runState.Ingots : 0)}";
                return false;
            }

            _backpackLevel++;
            _notice = $"背包扩容完成 · {BackpackWidth}×{BackpackHeight}（{BackpackCellCapacity} 格）";
            Changed?.Invoke();
            return true;
        }

        public bool BuildBackpackLayout(List<BackpackPlacement> result)
        {
            if (result == null || !EnsurePlacementData()) return false;
            result.Clear();
            for (var i = 0; i < _pending.Count; i++)
            {
                var item = _pending[i];
                if (item == null) continue;
                var position = _pendingPositions[i];
                var size = item.SalvageGridSize;
                result.Add(new BackpackPlacement(i, position.x, position.y, size.x, size.y));
            }
            return true;
        }

        public bool TryMovePending(int index, int gridX, int gridY)
        {
            if (!EnsurePlacementData() || index < 0 || index >= _pending.Count)
                return false;

            var item = _pending[index];
            if (item == null || !CanPlaceAt(item, gridX, gridY, index))
            {
                _notice = "目标位置空间不足";
                return false;
            }

            _pendingPositions[index] = new Vector2Int(gridX, gridY);
            _notice = $"已移动 · {item.DisplayName}";
            return true;
        }

        public bool AutoArrangeBackpack()
        {
            _pendingPositions.Clear();
            var occupied = new bool[BackpackWidth, BackpackHeight];
            for (var i = 0; i < _pending.Count; i++)
            {
                var item = _pending[i];
                if (item == null)
                {
                    _pendingPositions.Add(Vector2Int.zero);
                    continue;
                }

                var size = item.SalvageGridSize;
                if (!TryFindBackpackSpot(occupied, BackpackWidth, BackpackHeight, size.x, size.y, out var x, out var y))
                {
                    _pendingPositions.Clear();
                    EnsurePlacementData();
                    return false;
                }
                MarkOccupied(occupied, x, y, size.x, size.y);
                _pendingPositions.Add(new Vector2Int(x, y));
            }

            _notice = "背包已自动整理";
            return true;
        }

        /// <summary>Moves the identified entry at <paramref name="index"/> into the unsecured haul.</summary>
        public bool TryPickup(int index)
        {
            if (_openContainer == null) return false;
            var slots = _openContainer.Slots;
            if (index < 0 || index >= slots.Count) return false;
            var slot = slots[index];
            if (slot.State != SalvageSlotState.Revealed || slot.Item == null) return false;
            if (!TryAcceptReward(slot.Item, "搜刮"))
                return false;
            _openContainer.Take(slot);
            _lastPickupIndex = index;
            _lastPickupTime = Time.unscaledTime;
            return true;
        }

        /// <summary>Picks up every identified entry that still fits in the unsecured haul.</summary>
        public int TakeAllRevealed()
        {
            if (_openContainer == null) return 0;
            var taken = 0;
            for (var i = 0; i < _openContainer.Slots.Count; i++)
            {
                if (_openContainer.Slots[i].State != SalvageSlotState.Revealed) continue;
                if (TryPickup(i)) taken++;
                if (FreeBackpackCells <= 0) break;
            }
            if (taken > 0) _notice = $"全部拿取 · 收走 {taken} 件（{UsedBackpackCells}/{BackpackCellCapacity} 格）";
            else _notice = FreeBackpackCells <= 0 ? "未结算背包已满 · 无法全部拿取" : "当前没有可拿取的物资";
            return taken;
        }

        /// <summary>Drops one unsecured backpack entry into the current stage as a world pickup.</summary>
        public bool DropPending(int index)
        {
            if (index < 0 || index >= _pending.Count || !EnsurePlacementData()) return false;
            var item = _pending[index];
            if (item == null) return false;

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            var dropPosition = transform.position + forward * 0.9f + Vector3.up * 0.15f;
            if (Physics.Raycast(dropPosition + Vector3.up * 2f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                dropPosition.y = hit.point.y + 0.08f;

            var stageRoot = FindFirstObjectByType<RogueliteStageRuntimeController>()?.CurrentStageRoot;
            if (WorldSalvagePickup25D.Spawn(item, dropPosition, stageRoot, !item.IsSalvageCommodity) == null)
                return false;

            _pending.RemoveAt(index);
            _pendingPositions.RemoveAt(index);
            if (!item.IsSalvageCommodity)
                _inventory?.Release(item);
            _notice = $"已丢到地面 · {item.DisplayName}（靠近按 F 可重新拾取）";
            Changed?.Invoke();
            return true;
        }

        /// <summary>Drops the most recently picked up entry.</summary>
        public bool DropLastPending() =>
            _pending.Count > 0 && DropPending(_pending.Count - 1);

        public void ToggleBackpack()
        {
            var opening = !_backpackOpen;
            if (opening && _openContainer != null)
                CloseContainerWindow("背包已打开 · 容器检索已暂停");

            _backpackOpen = opening;
            _notice = _backpackOpen ? "背包已打开 · 游戏操作已锁定" : "背包已关闭";
            UpdateGameplayInputLock();
        }

        public void CloseBackpack()
        {
            _backpackOpen = false;
            UpdateGameplayInputLock();
        }

        public void CloseContainerWindow(string notice = null)
        {
            if (_openContainer == null) return;
            foreach (var slot in _openContainer.Slots)
            {
                if (slot.State != SalvageSlotState.Searching) continue;
                slot.State = SalvageSlotState.Unsearched;
                slot.Progress = 0f;
            }
            _openContainer = null;
            if (!string.IsNullOrEmpty(notice)) _notice = notice;
            UpdateGameplayInputLock();
        }

        private void UpdateGameplayInputLock() =>
            GameplayInputBlocker.SetBlocked(this, InterfaceOpen);

        public string EffectStatus(CollectibleDefinition item) =>
            item == null ? string.Empty :
            item.IsSalvageCommodity ? "普通物资" :
            !item.HasBuiltInEffect && !_inventory.IsEffectActive(item) ? "藏品 · 预留效果" :
            _inventory.IsApplicable(item) ? "藏品 · 已生效（仅本局）" : "藏品 · 其他职业";

        public bool IsDiscovered(CollectibleDefinition item) => item != null &&
            (item.IsSalvageCommodity ? _discoveredCommodities.Contains(item.Id) : _inventory.GetStackCount(item) > 0);

        public int CountPending(CollectibleDefinition item)
        {
            if (item == null) return 0;
            var count = 0;
            foreach (var pending in _pending) if (pending.Id == item.Id) count++;
            return count;
        }

        private bool CanCarry(CollectibleDefinition item) =>
            TryFindFreeSpot(item, out _);

        private bool TryFindFreeSpot(CollectibleDefinition item, out Vector2Int position)
        {
            position = new Vector2Int(-1, -1);
            if (_inventory == null || item == null)
                return false;
            if (!item.IsSalvageCommodity && !_inventory.CanAcquire(item))
                return false;
            if (!EnsurePlacementData())
                return false;

            var occupied = BuildOccupied();
            var incoming = item.SalvageGridSize;
            if (!TryFindBackpackSpot(occupied, BackpackWidth, BackpackHeight, incoming.x, incoming.y, out var x, out var y))
                return false;
            position = new Vector2Int(x, y);
            return true;
        }

        private bool CanPlaceAt(CollectibleDefinition item, int gridX, int gridY, int ignoreIndex)
        {
            if (item == null || gridX < 0 || gridY < 0)
                return false;
            var size = item.SalvageGridSize;
            if (gridX + size.x > BackpackWidth || gridY + size.y > BackpackHeight)
                return false;

            var occupied = BuildOccupied(ignoreIndex);
            for (var y = 0; y < size.y; y++)
            for (var x = 0; x < size.x; x++)
                if (occupied[gridX + x, gridY + y])
                    return false;
            return true;
        }

        private bool[,] BuildOccupied(int ignoreIndex = -1)
        {
            var occupied = new bool[BackpackWidth, BackpackHeight];
            for (var i = 0; i < _pending.Count && i < _pendingPositions.Count; i++)
            {
                if (i == ignoreIndex) continue;
                var item = _pending[i];
                if (item == null) continue;
                var position = _pendingPositions[i];
                var size = item.SalvageGridSize;
                if (position.x < 0 || position.y < 0 ||
                    position.x + size.x > BackpackWidth || position.y + size.y > BackpackHeight)
                    continue;
                MarkOccupied(occupied, position.x, position.y, size.x, size.y);
            }
            return occupied;
        }

        private bool EnsurePlacementData()
        {
            if (_pendingPositions.Count == _pending.Count)
                return true;

            _pendingPositions.Clear();
            var occupied = new bool[BackpackWidth, BackpackHeight];
            for (var i = 0; i < _pending.Count; i++)
            {
                var item = _pending[i];
                if (item == null)
                {
                    _pendingPositions.Add(Vector2Int.zero);
                    continue;
                }

                var size = item.SalvageGridSize;
                if (!TryFindBackpackSpot(occupied, BackpackWidth, BackpackHeight, size.x, size.y, out var x, out var y))
                {
                    _pendingPositions.Clear();
                    return false;
                }
                MarkOccupied(occupied, x, y, size.x, size.y);
                _pendingPositions.Add(new Vector2Int(x, y));
            }
            return true;
        }

        private static bool TryFindBackpackSpot(bool[,] occupied, int columns, int rows, int width, int height, out int gridX, out int gridY)
        {
            if (width <= 0 || height <= 0 || width > columns || height > rows)
            {
                gridX = -1;
                gridY = -1;
                return false;
            }

            for (var y = 0; y <= rows - height; y++)
            for (var x = 0; x <= columns - width; x++)
            {
                var free = true;
                for (var oy = 0; oy < height && free; oy++)
                for (var ox = 0; ox < width; ox++)
                    if (occupied[x + ox, y + oy])
                    {
                        free = false;
                        break;
                    }

                if (!free) continue;
                gridX = x;
                gridY = y;
                return true;
            }

            gridX = -1;
            gridY = -1;
            return false;
        }

        private static void MarkOccupied(bool[,] occupied, int gridX, int gridY, int width, int height)
        {
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                occupied[gridX + x, gridY + y] = true;
        }

        private void ReleasePendingRelics()
        {
            if (_inventory == null) return;
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var item = _pending[i];
                if (item != null && !item.IsSalvageCommodity)
                    _inventory.Release(item);
            }
        }

        private SalvageLootSlot ActiveSlot()
        {
            foreach (var slot in _openContainer.Slots) if (slot.Searching) return slot;
            foreach (var slot in _openContainer.Slots)
                if (slot.State == SalvageSlotState.Unsearched)
                {
                    slot.State = SalvageSlotState.Searching;
                    slot.Progress = 0f;
                    return slot;
                }
            return null;
        }

        // Normalize within each rarity first: catalog counts cannot make garbage bins rich.
        public CollectibleDefinition ResolveContainerReward(int seed, int tier)
        {
            var masses = new float[4];
            foreach (var item in _catalog)
                if (IsDraftable(item)) masses[(int)item.SalvageRarity] += ItemPreference(item);
            var total = 0f;
            for (var rarity = 0; rarity < 4; rarity++)
                if (masses[rarity] > 0f) total += SalvageContainerProfiles.RarityWeight(tier, (SalvageRarity)rarity);
            if (total <= 0f) return null;
            var random = new System.Random(seed);
            var roll = (float)random.NextDouble() * total;
            var selected = -1;
            for (var rarity = 0; rarity < 4; rarity++)
            {
                if (masses[rarity] <= 0f) continue;
                selected = rarity;
                roll -= SalvageContainerProfiles.RarityWeight(tier, (SalvageRarity)rarity);
                if (roll < 0f) break;
            }
            roll = (float)random.NextDouble() * masses[selected];
            CollectibleDefinition last = null;
            foreach (var item in _catalog)
            {
                if (!IsDraftable(item) || (int)item.SalvageRarity != selected) continue;
                last = item;
                roll -= ItemPreference(item);
                if (roll < 0f) return item;
            }
            return last;
        }

        private float ItemPreference(CollectibleDefinition item) => item.IsSalvageCommodity ? 1f :
            _inventory.IsEffectActive(item) ? 0.46f : 0.34f;

        /// <summary>Deterministic weighted roll from a container slot seed.</summary>
        public CollectibleDefinition ResolveReward(int seed)
        {
            var total = 0f;
            foreach (var item in _catalog)
            {
                if (!IsDraftable(item)) continue;
                total += RollWeight(item);
            }
            if (total <= 0f) return null;
            var roll = (seed % 100000) / 100000f * total;
            CollectibleDefinition last = null;
            foreach (var item in _catalog)
            {
                if (!IsDraftable(item)) continue;
                last = item;
                roll -= RollWeight(item);
                if (roll <= 0f) return item;
            }
            return last;
        }

        /// <summary>Never roll an entry the player could not pick up, so a reveal is always usable loot.</summary>
        private bool IsDraftable(CollectibleDefinition item) =>
            item != null && _inventory != null &&
            (item.IsSalvageCommodity || _inventory.CanAcquire(item));

        private float RollWeight(CollectibleDefinition item)
        {
            if (item == null) return 0f;
            var gradeWeight = item.SalvageRarity switch
            {
                SalvageRarity.Mythic => 0.055f,
                SalvageRarity.Precious => 0.18f,
                SalvageRarity.Rare => 0.52f,
                _ => 1.20f
            };
            // Ordinary valuables are the bread-and-butter haul; combat collectibles stay meaningfully rarer.
            if (!item.IsSalvageCommodity)
                gradeWeight *= _inventory.IsEffectActive(item) ? 0.46f : 0.34f;
            return gradeWeight;
        }

        private SearchableContainer25D FindNearestContainer()
        {
            SearchableContainer25D nearest = null;
            var best = InteractRange * InteractRange;
            foreach (var container in SearchableContainer25D.Active)
            {
                if (container == null || container.Emptied) continue;
                var distance = (container.transform.position - transform.position).sqrMagnitude;
                if (distance >= best) continue;
                var start = transform.position + Vector3.up * 0.65f;
                var end = container.transform.position + Vector3.up * 0.35f;
                if (Physics.Linecast(start, end, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.collider.GetComponentInParent<SearchableContainer25D>() != container) continue;
                best = distance;
                nearest = container;
            }
            return nearest;
        }

        private WorldSalvagePickup25D FindNearestGroundPickup()
        {
            WorldSalvagePickup25D nearest = null;
            var best = InteractRange * InteractRange;
            foreach (var pickup in WorldSalvagePickup25D.Active)
            {
                if (pickup == null || pickup.Item == null) continue;
                var distance = (pickup.transform.position - transform.position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = pickup;
            }
            return nearest;
        }

        private void Update()
        {
            var flow = RogueliteGameFlowController.Instance;
            if (flow != null && !flow.IsRunning) return;
            if (_entity == null || _entity.Health == null) return;
            var keyboard = Keyboard.current;
            var dead = _entity.Health.IsDead || _finished;
            var interactionBlocked = CombatActionUtility.IsBlocked(_entity, CombatActionMask.Interaction);

            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame && !dead)
                {
                    CloseContainerWindow("搜刮界面已关闭");
                    _backpackOpen = false;
                    UpdateGameplayInputLock();
                }
                if (keyboard.bKey.wasPressedThisFrame && !dead) ToggleBackpack();
            }
            if (dead)
            {
                _candidate = null;
                _groundCandidate = null;
                CloseContainerWindow();
                return;
            }

            if (_openContainer != null)
            {
                if (interactionBlocked)
                {
                    CloseContainerWindow("异常状态打断了搜刮");
                    return;
                }

                if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                {
                    CloseContainerWindow("搜刮界面已关闭");
                    return;
                }
                AdvanceSearch(Time.deltaTime);
                return;
            }

            if (_backpackOpen)
            {
                _candidate = null;
                _groundCandidate = null;
                return;
            }

            if (interactionBlocked)
            {
                _candidate = null;
                _groundCandidate = null;
                return;
            }

            _candidate = FindNearestContainer();
            _groundCandidate = FindNearestGroundPickup();
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                var containerDistance = _candidate != null
                    ? (_candidate.transform.position - transform.position).sqrMagnitude
                    : float.PositiveInfinity;
                var pickupDistance = _groundCandidate != null
                    ? (_groundCandidate.transform.position - transform.position).sqrMagnitude
                    : float.PositiveInfinity;

                if (_groundCandidate != null && pickupDistance <= containerDistance)
                {
                    var pickup = _groundCandidate;
                    if (pickup.TryCollect(this))
                        _groundCandidate = null;
                }
                else if (_candidate != null)
                {
                    OpenContainer(_candidate);
                }
            }
        }

        private void OnDestroy()
        {
            GameplayInputBlocker.Release(this);
            foreach (var item in _catalog) if (item != null) Destroy(item);
        }
    }
}
