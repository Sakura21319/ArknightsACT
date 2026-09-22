using System;
using System.Collections.Generic;
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
        private sealed class SaveData
        {
            public int lmd = 120000;
            public int commanderLevel = 1;
            public List<StoredEntry> warehouse = new();
        }

        private const string SaveKey = "ArknightsACT.MetaState.v1";
        [SerializeField, Min(0)] private int developmentStartingLmd = 120000;

        private readonly Dictionary<string, int> _warehouse = new();
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
                if (data.warehouse == null) return;
                for (var i = 0; i < data.warehouse.Count; i++)
                {
                    var entry = data.warehouse[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id) || entry.count <= 0)
                        continue;
                    _warehouse[entry.id] = entry.count;
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

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
