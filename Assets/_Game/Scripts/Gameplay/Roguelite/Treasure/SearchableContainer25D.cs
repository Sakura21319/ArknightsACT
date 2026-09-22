using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    /// <summary>Container flavour drives both the slot count and the on-box label.</summary>
    public enum SalvageContainerKind
    {
        Residential,
        Commercial,
        Service,
        Industrial,
        Checkpoint
    }

    /// <summary>Per-item search state. Searching and picking up are separate steps on purpose.</summary>
    public enum SalvageSlotState
    {
        Unsearched,
        Searching,
        Revealed,
        Taken
    }

    /// <summary>One loot entry inside a container, revealed by its own search pass.</summary>
    public sealed class SalvageLootSlot
    {
        public CollectibleDefinition Item;
        public SalvageSlotState State;
        public float Progress;
        public float Duration = 1.6f;
        public float RevealTime = -99f;
        public int Seed;
        public int GridX;
        public int GridY;
        public int Width = 1;
        public int Height = 1;
        public Transform Marker;
        public Renderer MarkerRenderer;
        public Vector3 MarkerHome;
        public bool Searched => State == SalvageSlotState.Revealed || State == SalvageSlotState.Taken;
        public bool Searching => State == SalvageSlotState.Searching;
    }

    /// <summary>
    /// A searchable salvage container. The loot list is rolled once, then unsealed one entry at a
    /// time by <see cref="ScavengingInventory25D"/>. The 3D lid and scanning bar follow that progress.
    /// </summary>
    public sealed class SearchableContainer25D : MonoBehaviour
    {
        public static readonly HashSet<SearchableContainer25D> Active = new();

        private readonly List<SalvageLootSlot> _slots = new();
        private Transform _lid;
        private Transform _scanner;
        private Material _markerMaterial;
        private float _lidAngle;

        public int RewardSeed { get; private set; }
        public string DisplayName { get; private set; }
        public SalvageContainerKind Kind { get; private set; }
        /// <summary>Set when a roll found nothing worth carrying, so the container stops advertising itself.</summary>
        public bool Barren { get; private set; }
        public IReadOnlyList<SalvageLootSlot> Slots => _slots;
        public int SlotCount => _slots.Count;
        public Vector2Int GridSize => ResolveGridSize(Kind);
        public int GridWidth => GridSize.x;
        public int GridHeight => GridSize.y;
        public int GridCapacity => GridWidth * GridHeight;
        public bool Initialized => _slots.Count > 0;

        /// <summary>Nothing left to search and nothing left to carry away.</summary>
        public bool Emptied
        {
            get
            {
                if (_slots.Count == 0) return Barren;
                foreach (var slot in _slots)
                    if (slot.State != SalvageSlotState.Taken) return false;
                return true;
            }
        }

        public void MarkBarren() => Barren = true;

        public bool HasUnsearched
        {
            get
            {
                foreach (var slot in _slots)
                    if (slot.State == SalvageSlotState.Unsearched || slot.State == SalvageSlotState.Searching) return true;
                return false;
            }
        }

        public int RevealedCount
        {
            get
            {
                var count = 0;
                foreach (var slot in _slots) if (slot.State == SalvageSlotState.Revealed) count++;
                return count;
            }
        }

        public int TakenCount
        {
            get
            {
                var count = 0;
                foreach (var slot in _slots) if (slot.State == SalvageSlotState.Taken) count++;
                return count;
            }
        }

        /// <summary>0..1 across the whole container; drives the hinged lid.</summary>
        public float SearchProgress01
        {
            get
            {
                if (_slots.Count == 0) return 0f;
                var progress = 0f;
                foreach (var slot in _slots)
                {
                    progress += slot.State switch
                    {
                        SalvageSlotState.Searching => Mathf.Clamp01(slot.Progress),
                        SalvageSlotState.Revealed or SalvageSlotState.Taken => 1f,
                        _ => 0f
                    };
                }
                return progress / _slots.Count;
            }
        }

        /// <summary>
        /// Rolls the loot list once. <paramref name="factory"/> receives (slotIndex, deterministicSeed)
        /// so the same container always yields the same entries without touching the combat Random stream.
        /// </summary>
        public void EnsureSlots(Func<int, int, CollectibleDefinition> factory)
        {
            if (_slots.Count > 0 || factory == null) return;

            var grid = ResolveGridSize(Kind);
            var occupied = new bool[grid.x, grid.y];
            var requested = ResolveItemCount(Kind, RewardSeed);
            var used = new HashSet<string>();

            for (var i = 0; i < requested; i++)
            {
                var seed = Mix(RewardSeed, i);
                var item = factory(i, seed);
                for (var attempt = 0; attempt < 8 && item != null && used.Contains(item.Id); attempt++)
                {
                    seed = Mix(seed, attempt + 1);
                    item = factory(i, seed);
                }
                if (item == null) continue;

                var size = item.SalvageGridSize;
                if (!TryPlace(size.x, size.y, occupied, out var gridX, out var gridY))
                    continue;

                MarkOccupied(gridX, gridY, size.x, size.y, occupied);
                used.Add(item.Id);
                _slots.Add(new SalvageLootSlot
                {
                    Item = item,
                    Seed = seed,
                    GridX = gridX,
                    GridY = gridY,
                    Width = size.x,
                    Height = size.y,
                    Duration = SearchDuration(item.SalvageRarity) + i * 0.05f
                });
            }

            _slots.Sort((a, b) =>
            {
                var row = a.GridY.CompareTo(b.GridY);
                return row != 0 ? row : a.GridX.CompareTo(b.GridX);
            });
            BuildMarkers();
        }

        /// <summary>Marks one entry as identified and pops its 3D marker out of the case.</summary>
        public void Reveal(SalvageLootSlot slot)
        {
            if (slot == null) return;
            slot.State = SalvageSlotState.Revealed;
            slot.Progress = 1f;
            slot.RevealTime = Time.unscaledTime;
            if (slot.MarkerRenderer != null)
            {
                var color = RarityColor(slot.Item);
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                properties.SetColor("_EmissionColor", color * 1.6f);
                slot.MarkerRenderer.SetPropertyBlock(properties);
            }
        }

        public void Take(SalvageLootSlot slot)
        {
            if (slot != null) slot.State = SalvageSlotState.Taken;
        }

        private void Update()
        {
            if (_lid == null) return;

            var target = Emptied ? 118f : Mathf.Lerp(0f, 84f, Mathf.Clamp01(SearchProgress01));
            _lidAngle = Mathf.MoveTowards(_lidAngle, target, Time.deltaTime * 150f);
            _lid.localRotation = Quaternion.Euler(-_lidAngle, 0f, 0f);

            var searching = false;
            foreach (var slot in _slots) if (slot.Searching) { searching = true; break; }
            _scanner.gameObject.SetActive(!Emptied && searching);
            if (searching)
                _scanner.localPosition = new Vector3(0f, 0.22f + Mathf.PingPong(Time.unscaledTime * 0.55f, 0.42f), -0.31f);

            AnimateMarkers();
        }

        private void AnimateMarkers()
        {
            foreach (var slot in _slots)
            {
                if (slot.Marker == null) continue;
                var visible = slot.State == SalvageSlotState.Revealed;
                if (slot.Marker.gameObject.activeSelf != visible) slot.Marker.gameObject.SetActive(visible);
                if (!visible) continue;

                // Overshooting pop so the entry reads as "弹出" rather than fading in.
                var age = Mathf.Max(0f, Time.unscaledTime - slot.RevealTime);
                var pop = Mathf.Clamp01(age / 0.34f);
                var ease = 1f - Mathf.Pow(1f - pop, 3f);
                var scale = Mathf.Lerp(0.12f, 1f, ease) * (1f + 0.22f * Mathf.Sin(ease * Mathf.PI));
                slot.Marker.localScale = Vector3.one * scale;
                slot.Marker.localPosition = slot.MarkerHome + Vector3.up * (Mathf.Sin(Time.unscaledTime * 2.2f + slot.Seed * 0.001f) * 0.04f);
                slot.Marker.localRotation = Quaternion.Euler(0f, Time.unscaledTime * 70f, 0f);
            }
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public static void Create(Transform parent, Vector3 position, Material steel, Material inset)
        {
            var root = new GameObject("SearchableSalvageContainer");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var container = root.AddComponent<SearchableContainer25D>();
            container.Kind = ResolveKind(parent != null ? parent.name : string.Empty);
            container.DisplayName = ResolveLabel(container.Kind);
            container._markerMaterial = steel != null ? steel : inset;
            // Stable spatial roll; does not consume the combat/reward Random stream.
            unchecked
            {
                uint hash = 2166136261;
                for (var current = root.transform; current != null; current = current.parent)
                    foreach (var character in current.name) hash = (hash ^ character) * 16777619;
                container.RewardSeed = (int)(hash & 0x7fffffff);
            }
            Part(root.transform, "Case", new Vector3(0f, 0.30f, 0f), new Vector3(0.85f, 0.60f, 0.58f), inset);
            var hinge = new GameObject("LidHinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(0f, 0.63f, 0.29f);
            container._lid = hinge;
            Part(hinge, "Lid", new Vector3(0f, 0f, -0.29f), new Vector3(0.91f, 0.12f, 0.65f), steel);
            foreach (var x in new[] { -0.35f, 0.35f })
                Part(root.transform, "ArmoredCorner", new Vector3(x, 0.30f, -0.30f), new Vector3(0.09f, 0.57f, 0.07f), steel);
            Part(root.transform, "Latch", new Vector3(0f, 0.47f, -0.32f), new Vector3(0.15f, 0.22f, 0.08f), steel);
            var scanner = Part(root.transform, "SearchScan", new Vector3(0f, 0.35f, -0.31f), new Vector3(0.66f, 0.018f, 0.014f), steel);
            container._scanner = scanner.transform;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", new Color(0.30f, 0.95f, 0.82f));
            properties.SetColor("_Color", new Color(0.30f, 0.95f, 0.82f));
            scanner.GetComponent<Renderer>().SetPropertyBlock(properties);
            scanner.SetActive(false);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(0.85f, 0.6f, 0.58f);
        }

        /// <summary>Placeholder pickups that float above the case once their entry is identified.</summary>
        private void BuildMarkers()
        {
            var count = _slots.Count;
            const int columns = 4;
            for (var i = 0; i < count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var home = new Vector3(-0.27f + col * 0.18f, 0.91f + row * 0.15f, -0.22f + row * 0.07f);
                var marker = Part(transform, $"LootMarker_{i}", home, new Vector3(0.14f, 0.14f, 0.14f), _markerMaterial);
                marker.transform.localScale = Vector3.zero;
                var renderer = marker.GetComponent<Renderer>();
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", new Color(0.30f, 0.95f, 0.82f));
                properties.SetColor("_Color", new Color(0.30f, 0.95f, 0.82f));
                renderer.SetPropertyBlock(properties);
                _slots[i].Marker = marker.transform;
                _slots[i].MarkerRenderer = renderer;
                _slots[i].MarkerHome = home;
                marker.SetActive(false);
            }
        }

        public static Color RarityColor(CollectibleDefinition item) => item == null ? new Color(0.46f, 0.49f, 0.50f) : item.SalvageRarity switch
        {
            SalvageRarity.Mythic => new Color(0.96f, 0.57f, 0.12f),
            SalvageRarity.Precious => new Color(0.61f, 0.34f, 0.88f),
            SalvageRarity.Rare => new Color(0.22f, 0.49f, 0.90f),
            _ => new Color(0.25f, 0.56f, 0.34f)
        };

        public static string RarityName(CollectibleDefinition item) => item == null ? "未知" : item.SalvageRarity switch
        {
            SalvageRarity.Mythic => "绝世",
            SalvageRarity.Precious => "珍贵",
            SalvageRarity.Rare => "稀有",
            _ => "普通"
        };

        private static Vector2Int ResolveGridSize(SalvageContainerKind kind) => kind switch
        {
            // Delta-style containers read better with many compact cells. Item footprint still
            // carries the visual weight (1x1 / 1x2 / 2x1 / 2x2), rather than oversized base cells.
            SalvageContainerKind.Industrial => new Vector2Int(12, 8),
            SalvageContainerKind.Checkpoint => new Vector2Int(12, 8),
            SalvageContainerKind.Commercial => new Vector2Int(11, 7),
            SalvageContainerKind.Service => new Vector2Int(11, 7),
            _ => new Vector2Int(10, 7)
        };

        private static int ResolveItemCount(SalvageContainerKind kind, int seed)
        {
            var baseCount = kind switch
            {
                SalvageContainerKind.Residential => 5,
                SalvageContainerKind.Service => 5,
                SalvageContainerKind.Commercial => 6,
                SalvageContainerKind.Industrial => 7,
                SalvageContainerKind.Checkpoint => 7,
                _ => 5
            };
            return Mathf.Min(10, baseCount + Mix(seed, 131) % 3);
        }

        private static float SearchDuration(SalvageRarity rarity) => rarity switch
        {
            // Keep each pass on-screen long enough for the scanning feedback to read clearly.
            SalvageRarity.Mythic => 3.00f,
            SalvageRarity.Precious => 2.55f,
            SalvageRarity.Rare => 2.10f,
            _ => 1.70f
        };

        private static bool TryPlace(int width, int height, bool[,] occupied, out int gridX, out int gridY)
        {
            var gridWidth = occupied.GetLength(0);
            var gridHeight = occupied.GetLength(1);
            width = Mathf.Clamp(width, 1, gridWidth);
            height = Mathf.Clamp(height, 1, gridHeight);

            // Stable row-major packing: left-to-right, then top-to-bottom. Loot selection stays
            // deterministic/randomized; only the visual placement is no longer scattered.
            for (var y = 0; y < gridHeight; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                if (!CanPlace(x, y, width, height, occupied)) continue;
                gridX = x;
                gridY = y;
                return true;
            }

            gridX = -1;
            gridY = -1;
            return false;
        }

        private static bool CanPlace(int x, int y, int width, int height, bool[,] occupied)
        {
            if (x < 0 || y < 0 || x + width > occupied.GetLength(0) || y + height > occupied.GetLength(1))
                return false;
            for (var iy = 0; iy < height; iy++)
            for (var ix = 0; ix < width; ix++)
                if (occupied[x + ix, y + iy]) return false;
            return true;
        }

        private static void MarkOccupied(int x, int y, int width, int height, bool[,] occupied)
        {
            for (var iy = 0; iy < height; iy++)
            for (var ix = 0; ix < width; ix++)
                occupied[x + ix, y + iy] = true;
        }

        private static string ResolveLabel(SalvageContainerKind kind) => kind switch
        {
            SalvageContainerKind.Residential => "居民封存箱",
            SalvageContainerKind.Service => "后勤补给箱",
            SalvageContainerKind.Industrial => "工业设备箱",
            SalvageContainerKind.Checkpoint => "检查站档案箱",
            _ => "商用物资箱"
        };

        /// <summary>Building root names are the only stable signal available at build time.</summary>
        private static SalvageContainerKind ResolveKind(string name)
        {
            if (Contains(name, "Tenement") || Contains(name, "Apartment") || Contains(name, "Residential"))
                return SalvageContainerKind.Residential;
            if (Contains(name, "Checkpoint") || Contains(name, "Gatehouse"))
                return SalvageContainerKind.Checkpoint;
            if (Contains(name, "LoadingDeck") || Contains(name, "Facility") || Contains(name, "Workshop") || Contains(name, "Industrial"))
                return SalvageContainerKind.Industrial;
            if (Contains(name, "Service"))
                return SalvageContainerKind.Service;
            return SalvageContainerKind.Commercial;
        }

        private static bool Contains(string value, string token) =>
            !string.IsNullOrEmpty(value) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static int Mix(int seed, int index)
        {
            unchecked
            {
                var value = (uint)(seed * 73856093) ^ (uint)((index + 1) * 19349663);
                value ^= value >> 13;
                value *= 1274126177u;
                value ^= value >> 16;
                return (int)(value & 0x7fffffff);
            }
        }

        private static GameObject Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, 0.018f);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
