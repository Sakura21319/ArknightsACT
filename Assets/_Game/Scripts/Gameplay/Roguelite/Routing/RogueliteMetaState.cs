using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>
    /// Persistent out-of-run economy. Extracted items are deposited here and only become LMD
    /// when the player explicitly sells them. Commander level is intentionally a display-only
    /// placeholder until the external progression system is designed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteMetaState : MonoBehaviour
    {
        [Serializable]
        private sealed class StoredEntry
        {
            public string id;
            public int count;
        }

        [Serializable]
        private sealed class OperatorProgressionEntry
        {
            public string operatorId;
            public int eliteLevel = 1;
            public int skill1Mastery;
            public int skill2Mastery;
        }

        [Serializable]
        private sealed class SaveData
        {
            public int lmd = 120000;
            public int commanderLevel = 1;
            public List<StoredEntry> warehouse = new();
            public List<OperatorProgressionEntry> operators = new();
        }

        private const string SaveKey = "ArknightsACT.MetaState.v1";
        [SerializeField, Min(0)] private int developmentStartingLmd = 120000;

        private readonly Dictionary<string, int> _warehouse = new();
        private readonly Dictionary<string, OperatorProgressionEntry> _operatorProgression =
            new(StringComparer.OrdinalIgnoreCase);
        public static RogueliteMetaState Instance { get; private set; }

        public int Lmd { get; private set; }
        public int CommanderLevel { get; private set; } = 1;
        public IReadOnlyDictionary<string, int> Warehouse => _warehouse;

        public event Action Changed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;
            return _warehouse.TryGetValue(itemId, out var count) ? count : 0;
        }

        public int GetOperatorEliteLevel(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId))
                return 1;
            return _operatorProgression.TryGetValue(operatorId, out var entry) && entry != null
                ? Mathf.Max(1, entry.eliteLevel)
                : 1;
        }

        public int GetOperatorSkillMastery(string operatorId, int skillIndex)
        {
            if (string.IsNullOrWhiteSpace(operatorId) ||
                !_operatorProgression.TryGetValue(operatorId, out var entry) ||
                entry == null)
                return 0;

            return skillIndex switch
            {
                1 => Mathf.Clamp(entry.skill1Mastery, 0, 3),
                2 => Mathf.Clamp(entry.skill2Mastery, 0, 3),
                _ => 0
            };
        }

        public bool SetOperatorSkillMastery(
            string operatorId,
            int skillIndex,
            int masteryLevel)
        {
            if (string.IsNullOrWhiteSpace(operatorId) ||
                (skillIndex != 1 && skillIndex != 2))
                return false;

            if (!_operatorProgression.TryGetValue(operatorId, out var entry) || entry == null)
            {
                entry = new OperatorProgressionEntry
                {
                    operatorId = operatorId,
                    eliteLevel = 1
                };
                _operatorProgression[operatorId] = entry;
            }

            var clamped = Mathf.Clamp(masteryLevel, 0, 3);
            if (skillIndex == 1)
            {
                if (entry.skill1Mastery == clamped)
                    return true;
                entry.skill1Mastery = clamped;
            }
            else
            {
                if (entry.skill2Mastery == clamped)
                    return true;
                entry.skill2Mastery = clamped;
            }

            SaveAndNotify();
            return true;
        }

        public bool CanAffordOperatorUpgrade(
            OperatorLevelUpgradeStep step,
            out string reason)
        {
            reason = string.Empty;
            if (step == null)
            {
                reason = "没有可用的升级阶段";
                return false;
            }

            if (Lmd < step.LmdCost)
            {
                reason = $"龙门币不足：需要 {step.LmdCost:N0}";
                return false;
            }

            var materials = step.Materials;
            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material == null || string.IsNullOrWhiteSpace(material.ItemId))
                    continue;

                var owned = GetCount(material.ItemId);
                if (owned < material.Amount)
                {
                    reason = $"升级材料不足：{material.ItemId} {owned}/{material.Amount}";
                    return false;
                }
            }

            return true;
        }

        public bool TryUpgradeOperator(
            string operatorId,
            OperatorLevelUpgradeStep step,
            out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(operatorId) || step == null)
            {
                reason = "升级数据无效";
                return false;
            }

            var currentLevel = GetOperatorEliteLevel(operatorId);
            if (currentLevel != step.FromLevel)
            {
                reason = $"等级状态已变化：当前 Lv.{currentLevel}";
                return false;
            }

            if (!CanAffordOperatorUpgrade(step, out reason))
                return false;

            Lmd -= step.LmdCost;
            var materials = step.Materials;
            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material == null || string.IsNullOrWhiteSpace(material.ItemId))
                    continue;

                var remaining = GetCount(material.ItemId) - material.Amount;
                if (remaining <= 0)
                    _warehouse.Remove(material.ItemId);
                else
                    _warehouse[material.ItemId] = remaining;
            }

            if (!_operatorProgression.TryGetValue(operatorId, out var entry) || entry == null)
            {
                entry = new OperatorProgressionEntry { operatorId = operatorId };
                _operatorProgression[operatorId] = entry;
            }

            entry.eliteLevel = Mathf.Max(1, step.TargetLevel);
            SaveAndNotify();
            return true;
        }

        public bool CanAffordSkillMastery(
            OperatorSkillMasteryCostStep step,
            out string reason)
        {
            reason = string.Empty;
            if (step == null)
            {
                reason = "没有可用的专精阶段";
                return false;
            }

            if (Lmd < step.LmdCost)
            {
                reason = $"龙门币不足：需要 {step.LmdCost:N0}";
                return false;
            }

            var materials = step.Materials;
            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material == null || string.IsNullOrWhiteSpace(material.ItemId))
                    continue;

                var owned = GetCount(material.ItemId);
                if (owned < material.Amount)
                {
                    reason = $"专精材料不足：{material.ItemId} {owned}/{material.Amount}";
                    return false;
                }
            }

            return true;
        }

        public bool TryUpgradeOperatorSkillMastery(
            string operatorId,
            int skillIndex,
            OperatorSkillMasteryCostStep step,
            out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(operatorId) ||
                (skillIndex != 1 && skillIndex != 2) ||
                step == null)
            {
                reason = "专精数据无效";
                return false;
            }

            var currentMastery = GetOperatorSkillMastery(operatorId, skillIndex);
            if (currentMastery != step.FromMastery)
            {
                reason = $"专精状态已变化：当前专精 {currentMastery}/3";
                return false;
            }

            if (!CanAffordSkillMastery(step, out reason))
                return false;

            Lmd -= step.LmdCost;
            var materials = step.Materials;
            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material == null || string.IsNullOrWhiteSpace(material.ItemId))
                    continue;

                var remaining = GetCount(material.ItemId) - material.Amount;
                if (remaining <= 0)
                    _warehouse.Remove(material.ItemId);
                else
                    _warehouse[material.ItemId] = remaining;
            }

            if (!_operatorProgression.TryGetValue(operatorId, out var entry) || entry == null)
            {
                entry = new OperatorProgressionEntry
                {
                    operatorId = operatorId,
                    eliteLevel = 1
                };
                _operatorProgression[operatorId] = entry;
            }

            if (skillIndex == 1)
                entry.skill1Mastery = Mathf.Clamp(step.TargetMastery, 0, 3);
            else
                entry.skill2Mastery = Mathf.Clamp(step.TargetMastery, 0, 3);

            SaveAndNotify();
            return true;
        }

        public void Deposit(IReadOnlyList<CollectibleDefinition> items)
        {
            if (items == null || items.Count == 0)
                return;

            var changed = false;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                // Relics/collectibles are strictly run-only. Only ordinary salvage commodities
                // can cross the extraction boundary into persistent storage.
                if (item == null || !item.IsSalvageCommodity || string.IsNullOrWhiteSpace(item.Id))
                    continue;
                _warehouse[item.Id] = GetCount(item.Id) + 1;
                changed = true;
            }

            if (changed)
                SaveAndNotify();
        }

        public void PurgeRunOnlyItems(IReadOnlyList<CollectibleDefinition> catalog)
        {
            if (catalog == null || catalog.Count == 0 || _warehouse.Count == 0)
                return;

            var changed = false;
            for (var i = 0; i < catalog.Count; i++)
            {
                var item = catalog[i];
                if (item == null || item.IsSalvageCommodity || string.IsNullOrWhiteSpace(item.Id))
                    continue;
                changed |= _warehouse.Remove(item.Id);
            }

            if (changed)
                SaveAndNotify();
        }

        public int GetSellPrice(CollectibleDefinition item)
        {
            if (item == null) return 0;
            return Mathf.Max(1, item.CollectionValue > 0 ? item.CollectionValue : 100);
        }

        public int GetBuyPrice(CollectibleDefinition item)
        {
            var basePrice = GetSellPrice(item);
            return Mathf.Max(1, Mathf.CeilToInt(basePrice * 1.25f / 10f) * 10);
        }

        public bool TryBuy(CollectibleDefinition item, int quantity = 1)
        {
            if (item == null || !item.IsSalvageCommodity || quantity <= 0)
                return false;

            var total = GetBuyPrice(item) * quantity;
            if (Lmd < total)
                return false;

            Lmd -= total;
            _warehouse[item.Id] = GetCount(item.Id) + quantity;
            SaveAndNotify();
            return true;
        }

        public bool TrySell(CollectibleDefinition item, int quantity = 1)
        {
            if (item == null || !item.IsSalvageCommodity || quantity <= 0)
                return false;

            var owned = GetCount(item.Id);
            if (owned < quantity)
                return false;

            var remaining = owned - quantity;
            if (remaining <= 0)
                _warehouse.Remove(item.Id);
            else
                _warehouse[item.Id] = remaining;

            Lmd += GetSellPrice(item) * quantity;
            SaveAndNotify();
            return true;
        }

        public int GetWarehouseTotalValue(IReadOnlyList<CollectibleDefinition> catalog)
        {
            if (catalog == null || catalog.Count == 0 || _warehouse.Count == 0)
                return 0;

            var total = 0;
            var visited = new HashSet<string>();
            for (var i = 0; i < catalog.Count; i++)
            {
                var item = catalog[i];
                if (item == null || !item.IsSalvageCommodity || string.IsNullOrWhiteSpace(item.Id) ||
                    !visited.Add(item.Id))
                    continue;
                total += GetSellPrice(item) * GetCount(item.Id);
            }
            return total;
        }

        public int SellAllOwned(IReadOnlyList<CollectibleDefinition> items)
        {
            if (items == null || items.Count == 0)
                return 0;

            var sold = 0;
            var revenue = 0;
            var visited = new HashSet<string>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || !item.IsSalvageCommodity || string.IsNullOrWhiteSpace(item.Id) ||
                    !visited.Add(item.Id))
                    continue;

                var count = GetCount(item.Id);
                if (count <= 0)
                    continue;
                sold += count;
                revenue += GetSellPrice(item) * count;
                _warehouse.Remove(item.Id);
            }

            if (sold <= 0)
                return 0;

            Lmd += revenue;
            SaveAndNotify();
            return sold;
        }

        public int SellLowValue(IReadOnlyList<CollectibleDefinition> catalog, int maxUnitPrice)
        {
            if (catalog == null || catalog.Count == 0 || maxUnitPrice <= 0)
                return 0;

            var candidates = new List<CollectibleDefinition>();
            for (var i = 0; i < catalog.Count; i++)
            {
                var item = catalog[i];
                if (item == null || !item.IsSalvageCommodity || GetCount(item.Id) <= 0)
                    continue;
                if (GetSellPrice(item) <= maxUnitPrice)
                    candidates.Add(item);
            }
            return SellAllOwned(candidates);
        }

        private void Load()
        {
            _warehouse.Clear();
            _operatorProgression.Clear();
            Lmd = Mathf.Max(0, developmentStartingLmd);
            CommanderLevel = 1;

            if (!PlayerPrefs.HasKey(SaveKey))
                return;

            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
                if (data == null) return;
                Lmd = Mathf.Max(0, data.lmd);
                CommanderLevel = Mathf.Max(1, data.commanderLevel);
                if (data.warehouse != null)
                {
                    for (var i = 0; i < data.warehouse.Count; i++)
                    {
                        var entry = data.warehouse[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.id) || entry.count <= 0)
                            continue;
                        _warehouse[entry.id] = entry.count;
                    }
                }

                if (data.operators != null)
                {
                    for (var i = 0; i < data.operators.Count; i++)
                    {
                        var entry = data.operators[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.operatorId))
                            continue;
                        entry.eliteLevel = Mathf.Max(1, entry.eliteLevel);
                        entry.skill1Mastery = Mathf.Clamp(entry.skill1Mastery, 0, 3);
                        entry.skill2Mastery = Mathf.Clamp(entry.skill2Mastery, 0, 3);
                        _operatorProgression[entry.operatorId] = entry;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ArknightsACT/Meta] Failed to load meta save: {exception.Message}", this);
            }
        }

        private void SaveAndNotify()
        {
            var data = new SaveData
            {
                lmd = Lmd,
                commanderLevel = CommanderLevel
            };
            foreach (var pair in _warehouse)
                if (pair.Value > 0)
                    data.warehouse.Add(new StoredEntry { id = pair.Key, count = pair.Value });

            foreach (var pair in _operatorProgression)
            {
                var entry = pair.Value;
                if (entry == null || string.IsNullOrWhiteSpace(pair.Key))
                    continue;
                data.operators.Add(new OperatorProgressionEntry
                {
                    operatorId = pair.Key,
                    eliteLevel = Mathf.Max(1, entry.eliteLevel),
                    skill1Mastery = Mathf.Clamp(entry.skill1Mastery, 0, 3),
                    skill2Mastery = Mathf.Clamp(entry.skill2Mastery, 0, 3)
                });
            }

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
