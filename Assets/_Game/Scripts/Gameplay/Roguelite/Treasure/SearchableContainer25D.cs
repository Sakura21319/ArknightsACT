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
        Checkpoint,
        // Append only: preserve the five legacy serialized IDs.
        TrashBin,
        ScrapPile,
        Suitcase,
        CarTrunk,
        Wardrobe,
        KitchenCabinet,
        BedsideDrawer,
        StoreShelf,
        CashRegister,
        StockCarton,
        Refrigerator,
        MedicineCabinet,
        MedicalCrate,
        MedicalFridge,
        OfficeDesk,
        FilingCabinet,
        PersonalSafe,
        CommercialSafe,
        ToolChest,
        PartsCabinet,
        EquipmentCrate,
        FreightCrate,
        SealedCargo,
        ValuableCargo,
        DutyLocker,
        MilitaryCrate,
        ArchiveVault,
        OriginiumCase
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
        private Vector3 _visualSize;
        private bool _sideDoor;
        private bool _slidingDrawer;
        private Vector3 _lidHome;

        public SalvageContainerProfile Profile => SalvageContainerProfiles.Get(Kind);
        public int Tier => Profile.Tier;

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
        public bool Initialized => _slots.Count > 0 || Barren;

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
            if (Initialized || factory == null) return;

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
            if (_slots.Count == 0) Barren = true;
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
            if (_slidingDrawer)
                _lid.localPosition = _lidHome + Vector3.back * (Mathf.Clamp01(_lidAngle / 84f) * .35f);
            _lid.localRotation = _slidingDrawer ? Quaternion.identity : _sideDoor ? Quaternion.Euler(0f, _lidAngle, 0f) : Quaternion.Euler(-_lidAngle, 0f, 0f);

            var searching = false;
            foreach (var slot in _slots) if (slot.Searching) { searching = true; break; }
            _scanner.gameObject.SetActive(!Emptied && searching);
            if (searching)
                _scanner.localPosition = new Vector3(0f, 0.12f + Mathf.PingPong(Time.unscaledTime * 0.55f, _visualSize.y * 0.7f), -_visualSize.z * 0.5f - 0.06f);

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

        public static void Create(Transform parent, Vector3 position, Material steel, Material inset, int seedSalt = 0, SalvageContainerKind? kind = null)
        {
            var root = new GameObject("SearchableSalvageContainer");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var container = root.AddComponent<SearchableContainer25D>();
            container.Kind = kind ?? ResolveKind(parent != null ? parent.name : string.Empty);
            container.DisplayName = (int)container.Kind >= 5 ? $"{container.Profile.Label} · {SalvageContainerProfiles.TierName(container.Tier)}" : ResolveLabel(container.Kind);
            container._markerMaterial = steel != null ? steel : inset;
            // Stable spatial roll; does not consume the combat/reward Random stream.
            unchecked
            {
                uint hash = 2166136261;
                for (var current = root.transform; current != null; current = current.parent)
                    foreach (var character in current.name) hash = (hash ^ character) * 16777619;
                // Multiple containers in one room must have independent, repeatable rolls.
                hash = (hash ^ (uint)Mathf.RoundToInt(position.x * 1000f)) * 16777619;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.y * 1000f)) * 16777619;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.z * 1000f)) * 16777619;
                hash = (hash ^ (uint)seedSalt) * 16777619;
                container.RewardSeed = (int)(hash & 0x7fffffff);
            }
            var size = SalvageContainerProfiles.Size(container.Kind);
            container._visualSize = size;
            var shape = container.Profile.Shape;
            container._slidingDrawer = shape == SalvageContainerShape.Drawer;
            container._sideDoor = shape == SalvageContainerShape.Cabinet || shape == SalvageContainerShape.Safe || shape == SalvageContainerShape.Drawer;
            var hollow = shape == SalvageContainerShape.Shelf || container._sideDoor;
            var body = Part(root.transform, "Body", new Vector3(0f, size.y * .5f, hollow ? size.z * .5f - .045f : 0f),
                hollow ? new Vector3(size.x, size.y, .09f) : size, inset);
            if (hollow)
            {
                foreach (var side in new[] { -1f, 1f })
                    Part(root.transform, "SidePanel", new Vector3(side * (size.x * .5f - .035f), size.y * .5f, 0f), new Vector3(.07f, size.y, size.z), steel);
                foreach (var fraction in new[] { .025f, .5f, .975f })
                    Part(root.transform, "InnerShelf", new Vector3(0f, size.y * fraction, 0f), new Vector3(size.x - .08f, .05f, size.z), inset);
            }
            var bodyTint = new MaterialPropertyBlock();
            var bodyColor = shape switch
            {
                SalvageContainerShape.Bin => new Color(.24f, .31f, .26f),
                SalvageContainerShape.Carton => new Color(.48f, .36f, .23f),
                SalvageContainerShape.Safe => new Color(.22f, .26f, .29f),
                SalvageContainerShape.Drawer => new Color(.40f, .36f, .31f),
                _ => new Color(.43f, .47f, .49f)
            };
            var medical = container.Kind == SalvageContainerKind.MedicineCabinet || container.Kind == SalvageContainerKind.MedicalCrate || container.Kind == SalvageContainerKind.MedicalFridge;
            if (medical) bodyColor = new Color(.70f, .74f, .70f);
            if (container.Kind == SalvageContainerKind.DutyLocker || container.Kind == SalvageContainerKind.MilitaryCrate) bodyColor = new Color(.31f, .37f, .29f);
            bodyTint.SetColor("_BaseColor", bodyColor); bodyTint.SetColor("_Color", bodyColor);
            body.GetComponent<Renderer>().SetPropertyBlock(bodyTint);
            var hinge = new GameObject("Opening").transform;
            hinge.SetParent(root.transform, false);
            container._lid = hinge;
            if (container._sideDoor)
            {
                hinge.localPosition = new Vector3(-size.x * 0.5f, size.y * 0.5f, -size.z * 0.5f - 0.035f);
                Part(hinge, "Door", new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(size.x, size.y * 0.96f, 0.07f), steel);
                Part(hinge, "Handle", new Vector3(size.x * 0.83f, 0f, -0.08f), new Vector3(0.055f, 0.28f, 0.08f), inset);
            }
            else
            {
                hinge.localPosition = new Vector3(0f, size.y, size.z * 0.5f);
                if (shape != SalvageContainerShape.Pile && shape != SalvageContainerShape.Shelf && shape != SalvageContainerShape.Register)
                    Part(hinge, "Lid", new Vector3(0f, 0f, -size.z * 0.5f), new Vector3(size.x + 0.06f, 0.09f, size.z + 0.06f), steel);
            }
            container._lidHome = hinge.localPosition;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.SetPropertyBlock(bodyTint);
            var detail = new System.Random(container.RewardSeed);
            if (shape == SalvageContainerShape.Shelf)
                for (var row = 0; row < 2; row++)
                for (var col = 0; col < 3; col++)
                {
                    if (detail.Next(3) == 0) continue;
                    Part(root.transform, "ShelfPackage", new Vector3((col - 1) * size.x * .26f, row * size.y * .48f + .19f, .04f), new Vector3(.18f, .28f, .22f), inset);
                }
            if (shape == SalvageContainerShape.Cabinet)
            {
                for (var vent = 0; vent < 3; vent++)
                    Part(hinge, "VentSlot", new Vector3(size.x * .48f, size.y * .3f + vent * .055f, -.05f), new Vector3(size.x * .46f, .018f, .012f), inset);
                if (container.Kind == SalvageContainerKind.Refrigerator || container.Kind == SalvageContainerKind.MedicalFridge)
                    Part(hinge, "FreezerJoint", new Vector3(size.x * .5f, size.y * .19f, -.055f), new Vector3(size.x * .94f, .022f, .016f), inset);
            }
            if (shape == SalvageContainerShape.Case || shape == SalvageContainerShape.Cargo)
                foreach (var side in new[] { -1f, 1f })
                {
                    Part(root.transform, "CornerGuard", new Vector3(side * size.x * .44f, size.y * .5f, -size.z * .5f - .025f), new Vector3(.065f, size.y * .9f, .06f), steel);
                    Part(root.transform, "LatchClip", new Vector3(side * size.x * .28f, size.y * .82f, -size.z * .5f - .04f), new Vector3(.075f, .16f, .04f), steel);
                }
            if (shape == SalvageContainerShape.Carton)
                Part(root.transform, "ShippingLabel", new Vector3(size.x * .14f, size.y * .5f, -size.z * .5f - .02f), new Vector3(.24f, .15f, .014f), steel);
            if (shape == SalvageContainerShape.Case)
                Part(root.transform, "CarryHandle", new Vector3(0f, size.y + .09f, 0f), new Vector3(.26f, .07f, .09f), steel);
            for (var mark = 0; mark < 2; mark++)
                Part(root.transform, "WearMark", new Vector3(-size.x * .28f + (float)detail.NextDouble() * size.x * .5f, size.y * (.15f + (float)detail.NextDouble() * .2f), -size.z * .5f - .026f),
                    new Vector3(.12f, .012f, .008f), steel).transform.localRotation = Quaternion.Euler(0f, 0f, detail.Next(-18, 19));
            if (shape == SalvageContainerShape.Shelf || shape == SalvageContainerShape.Drawer)
                for (var row = 1; row <= 3; row++)
                    Part(root.transform, "ShelfDivider", new Vector3(0f, size.y * row / 4f, -size.z * 0.5f - 0.055f),
                        new Vector3(size.x, 0.055f, 0.16f), steel);
            if (shape == SalvageContainerShape.Safe)
            {
                Part(hinge, "Lock", new Vector3(size.x * 0.5f, 0f, -0.09f), new Vector3(0.24f, 0.24f, 0.07f), inset);
                Part(hinge, "LockBar", new Vector3(size.x * 0.5f, 0f, -0.14f), new Vector3(0.36f, 0.045f, 0.05f), steel);
            }
            if (shape == SalvageContainerShape.Register)
                Part(root.transform, "TillDisplay", new Vector3(0f, size.y + 0.15f, 0.1f), new Vector3(0.4f, 0.3f, 0.12f), steel);
            if (shape == SalvageContainerShape.Cargo || shape == SalvageContainerShape.Carton)
                for (var side = -1; side <= 1; side += 2)
                    Part(root.transform, "PackingBand", new Vector3(side * size.x * 0.3f, size.y * 0.5f, -size.z * 0.5f - 0.02f),
                        new Vector3(0.07f, size.y, 0.035f), steel);
            if (shape == SalvageContainerShape.Bin)
                for (var side = -1; side <= 1; side += 2)
                    Part(root.transform, "BinHandle", new Vector3(side * (size.x * 0.5f + 0.045f), size.y * 0.75f, 0f),
                        new Vector3(0.09f, 0.08f, 0.24f), steel);
            if (container.Kind == SalvageContainerKind.MedicineCabinet || container.Kind == SalvageContainerKind.MedicalCrate || container.Kind == SalvageContainerKind.MedicalFridge)
            {
                Part(root.transform, "MedicalMarkH", new Vector3(0f, size.y * .5f, -size.z * .5f - .09f), new Vector3(.3f, .07f, .025f), steel);
                Part(root.transform, "MedicalMarkV", new Vector3(0f, size.y * .5f, -size.z * .5f - .09f), new Vector3(.07f, .3f, .025f), steel);
            }
            if (shape == SalvageContainerShape.Pile)
                for (var scrap = 0; scrap < 4; scrap++)
                    Part(root.transform, "LooseScrap", new Vector3(-0.35f + scrap * 0.22f, size.y + scrap % 2 * 0.08f, 0f),
                        new Vector3(0.16f, 0.18f, 0.4f), steel).transform.localRotation = Quaternion.Euler(0f, scrap * 31f, 12f);
            var badge = Part(root.transform, "GradeSeal", new Vector3(-size.x * 0.3f, size.y * 0.8f, -size.z * 0.5f - 0.09f),
                new Vector3(0.13f, 0.1f, 0.025f), steel);
            var tint = new MaterialPropertyBlock();
            var color = container.Tier >= 4 ? new Color(0.76f, 0.55f, 0.22f) : container.Tier == 3 ? new Color(0.28f, 0.52f, 0.59f) : new Color(0.48f, 0.51f, 0.45f);
            tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color);
            badge.GetComponent<Renderer>().SetPropertyBlock(tint);
            var scanner = Part(root.transform, "SearchScan", new Vector3(0f, 0.35f, -size.z * 0.5f - 0.06f),
                new Vector3(size.x * 0.75f, 0.018f, 0.014f), steel);
            container._scanner = scanner.transform;
            scanner.SetActive(false);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.size = size;
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
                var home = new Vector3(-0.27f + col * 0.18f, _visualSize.y + 0.3f + row * 0.15f, -0.22f + row * 0.07f);
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
            if ((int)kind >= 5)
            {
                var profile = SalvageContainerProfiles.Get(kind);
                return profile.MinItems + Mix(seed, 131) % (profile.MaxItems - profile.MinItems + 1);
            }
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
