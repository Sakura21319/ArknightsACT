using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Turns the enlarged 18x14 cells into authored combat compositions instead of empty rectangles.
    /// Existing dressing is snapped into corner obstacle pockets, while mid-edge architecture creates
    /// alleys, building silhouettes and layered urban depth without cutting the cardinal navigation cross.
    /// </summary>
    [DefaultExecutionOrder(22)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageUrbanCompositionController : MonoBehaviour
    {
        private static readonly Vector3[] ObstaclePockets =
        {
            new(-7.10f, 0f, 5.10f),
            new(7.10f, 0f, 5.10f),
            new(-7.10f, 0f, -5.10f),
            new(7.10f, 0f, -5.10f)
        };

        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private ChernobogEnvironmentKit kit;

        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map, ChernobogEnvironmentKit environmentKit)
        {
            stageMap = map;
            kit = environmentKit;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.12f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            var dressing = stage.transform.Find("[Chernobog_SetDressing]");
            if (dressing == null)
                return;

            ComposeObstaclePockets(dressing);
            BuildArchitecture(stage.transform);
            Physics.SyncTransforms();
            _preparedStage = stage;

            Debug.Log($"[ArknightsACT/UrbanComposition] Stage {stageMap.StageIndex}: obstacle pockets + urban architecture composed.", this);
        }

        private void ComposeObstaclePockets(Transform dressing)
        {
            for (var blockIndex = 0; blockIndex < stageMap.Blocks.Count; blockIndex++)
            {
                var blockRoot = dressing.Find($"Block_{blockIndex:00}_Dressing");
                if (blockRoot == null)
                    continue;

                var seed = PositiveMod(stageMap.StageIndex * 7 + blockIndex * 11, ObstaclePockets.Length);
                var slot = 0;
                for (var i = 0; i < blockRoot.childCount; i++)
                {
                    var child = blockRoot.GetChild(i);
                    if (child == null || !IsBlockingFeature(child.name))
                        continue;

                    var pocket = ObstaclePockets[(seed + slot) % ObstaclePockets.Length];
                    child.localPosition = new Vector3(pocket.x, child.localPosition.y, pocket.z);
                    slot++;
                }
            }
        }

        private static bool IsBlockingFeature(string name)
        {
            return name == "Scaffold" ||
                   name == "RubbleCluster" ||
                   name == "BlackOriginiumGrowth" ||
                   name == "BrokenDeckSlab" ||
                   name == "CargoBlocks";
        }

        private void BuildArchitecture(Transform stage)
        {
            var old = stage.Find("[Chernobog_UrbanArchitecture]");
            if (old != null)
                Destroy(old.gameObject);

            var root = new GameObject("[Chernobog_UrbanArchitecture]").transform;
            root.SetParent(stage, false);

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                var block = FindBlockTransform(stage, i);
                if (data == null || block == null)
                    continue;

                var cell = new GameObject($"Block_{i:00}_Architecture").transform;
                cell.SetParent(root, false);
                cell.position = block.position;

                var flip = PositiveMod(stageMap.StageIndex + i + (int)data.Theme, 2) == 1;
                switch (data.Theme)
                {
                    case RogueliteChunkTheme.Street:
                        BuildStreetComposition(cell, flip, i);
                        break;
                    case RogueliteChunkTheme.CoverLane:
                        BuildCoverLaneComposition(cell, flip, i);
                        break;
                    case RogueliteChunkTheme.Facility:
                        BuildFacilityComposition(cell, flip, i);
                        break;
                    case RogueliteChunkTheme.SafePlaza:
                        BuildSafePlazaComposition(cell, flip, i);
                        break;
                    case RogueliteChunkTheme.BossArena:
                        BuildBossComposition(cell, flip, i);
                        break;
                    default:
                        BuildOpenComposition(cell, flip, i);
                        break;
                }
            }
        }

        private void BuildOpenComposition(Transform parent, bool flip, int seed)
        {
            var side = flip ? 1f : -1f;
            BuildServiceBuilding(parent, new Vector3(side * 7.35f, 0f, 0f), side, 2.05f, 4.75f, 2, seed);
            BuildRuinedShell(parent, new Vector3(-side * 7.15f, 0f, 0.2f), -side, 4.15f, seed + 17);
            BuildRoofPipeBridge(parent, flip ? 1f : -1f, seed + 31);
        }

        private void BuildStreetComposition(Transform parent, bool flip, int seed)
        {
            var side = flip ? 1f : -1f;
            BuildServiceBuilding(parent, new Vector3(side * 7.30f, 0f, -0.35f), side, 2.15f, 5.20f, 3, seed);
            BuildRuinedShell(parent, new Vector3(-side * 7.20f, 0f, 0.65f), -side, 4.70f, seed + 19);
            BuildOverheadServiceGate(parent, seed + 37);
        }

        private void BuildCoverLaneComposition(Transform parent, bool flip, int seed)
        {
            var side = flip ? 1f : -1f;
            BuildWarehouse(parent, new Vector3(side * 7.25f, 0f, 0f), side, seed);
            BuildUtilityTower(parent, new Vector3(-side * 7.35f, 0f, 0.8f), -side, 5.8f, seed + 23);
            BuildRoofPipeBridge(parent, -side, seed + 47);
        }

        private void BuildFacilityComposition(Transform parent, bool flip, int seed)
        {
            // The existing TwoFloorFacility remains the actual traversable multi-level gameplay space.
            // This pass frames it with a vertical utility annex and an overhead service gate so the cell
            // reads as a district rather than one isolated platform.
            var side = flip ? -1f : 1f;
            BuildServiceBuilding(parent, new Vector3(-7.30f, 0f, 0.15f), -1f, 2.10f, 4.70f, 3, seed);
            BuildUtilityTower(parent, new Vector3(7.35f, 0f, -0.55f), 1f, 6.4f, seed + 29);
            if (side > 0f)
                BuildOverheadServiceGate(parent, seed + 43);
            else
                BuildRoofPipeBridge(parent, side, seed + 43);
        }

        private void BuildSafePlazaComposition(Transform parent, bool flip, int seed)
        {
            var side = flip ? 1f : -1f;
            BuildMaintenanceKiosk(parent, new Vector3(side * 7.25f, 0f, 0.65f), side, seed);
            BuildOpenCanopy(parent, new Vector3(-side * 6.95f, 0f, -0.35f), seed + 13);
        }

        private void BuildBossComposition(Transform parent, bool flip, int seed)
        {
            BuildUtilityTower(parent, new Vector3(-7.35f, 0f, 0.55f), -1f, 7.0f, seed);
            BuildUtilityTower(parent, new Vector3(7.35f, 0f, -0.55f), 1f, 6.2f, seed + 41);
            BuildOverheadServiceGate(parent, seed + 71);
        }

        private void BuildServiceBuilding(
            Transform parent,
            Vector3 anchor,
            float facadeSide,
            float width,
            float depth,
            int floors,
            int seed)
        {
            var root = new GameObject("ServiceBuilding").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;
            const float floorHeight = 2.25f;
            var totalHeight = floors * floorHeight;

            CreateSolidBox(root, "BuildingMass", new Vector3(0f, totalHeight * 0.5f, 0f),
                new Vector3(width, totalHeight, depth), 0.13f, wall);

            for (var floor = 1; floor <= floors; floor++)
            {
                var bandY = floor * floorHeight - 0.10f;
                CreateVisualBox(root, $"FloorBand_{floor}", new Vector3(-facadeSide * (width * 0.5f + 0.025f), bandY, 0f),
                    new Vector3(0.055f, 0.16f, depth * 0.94f), 0.010f, steel);

                var windowY = (floor - 0.5f) * floorHeight + 0.12f;
                for (var panel = -1; panel <= 1; panel++)
                {
                    var z = panel * depth * 0.27f;
                    CreateVisualBox(root, $"FacadeInset_{floor}_{panel + 1}",
                        new Vector3(-facadeSide * (width * 0.5f + 0.035f), windowY, z),
                        new Vector3(0.065f, 0.78f, depth * 0.18f), 0.008f, panel == 0 ? grate : inset);
                }
            }

            CreateVisualBox(root, "RoofCap", new Vector3(0f, totalHeight + 0.10f, 0f),
                new Vector3(width * 1.08f, 0.20f, depth * 1.04f), 0.04f, steel);
            CreateVisualBox(root, "RoofUnit", new Vector3(0f, totalHeight + 0.48f, depth * 0.12f),
                new Vector3(width * 0.72f, 0.62f, Mathf.Min(1.55f, depth * 0.38f)), 0.08f, inset);

            if (kit.wallVent != null && PositiveMod(seed, 2) == 0)
            {
                var vent = Instantiate(kit.wallVent, root);
                vent.name = "RoofVentModule";
                vent.transform.localPosition = new Vector3(0f, totalHeight + 0.22f, -depth * 0.23f);
                vent.transform.localScale = new Vector3(0.60f, 0.48f, 0.45f);
            }
        }

        private void BuildRuinedShell(Transform parent, Vector3 anchor, float openSide, float height, int seed)
        {
            var root = new GameObject("RuinedBuildingShell").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;
            root.localRotation = Quaternion.Euler(0f, PositiveMod(seed, 2) * 180f, 0f);

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            const float width = 2.05f;
            const float depth = 4.55f;

            CreateSolidBox(root, "RearWall", new Vector3(openSide * 0.25f, height * 0.5f, 0f),
                new Vector3(0.42f, height, depth), 0.07f, wall);
            CreateSolidBox(root, "EndWall_N", new Vector3(-openSide * 0.58f, height * 0.38f, depth * 0.5f - 0.24f),
                new Vector3(1.30f, height * 0.76f, 0.42f), 0.065f, inset);
            CreateSolidBox(root, "EndWall_S", new Vector3(-openSide * 0.52f, height * 0.28f, -depth * 0.5f + 0.26f),
                new Vector3(1.18f, height * 0.56f, 0.45f), 0.065f, wall);
            CreateSolidBox(root, "BrokenSecondFloor", new Vector3(-openSide * 0.32f, height * 0.54f, 0.45f),
                new Vector3(1.40f, 0.20f, 2.05f), 0.045f, steel,
                Quaternion.Euler(0f, 0f, openSide * 3.5f));

            CreateVisualBox(root, "ExposedBeam", new Vector3(-openSide * 0.62f, height * 0.78f, -0.55f),
                new Vector3(0.18f, height * 0.46f, 0.18f), 0.035f, steel,
                Quaternion.Euler(0f, 0f, -openSide * 8f));
        }

        private void BuildWarehouse(Transform parent, Vector3 anchor, float facadeSide, int seed)
        {
            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var root = new GameObject("UtilityWarehouse").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            CreateSolidBox(root, "WarehouseMass", new Vector3(0f, 1.65f, 0f), new Vector3(2.25f, 3.30f, 5.15f), 0.14f, wall);
            CreateVisualBox(root, "WarehouseDoor", new Vector3(-facadeSide * 1.155f, 1.10f, 0f),
                new Vector3(0.07f, 2.10f, 2.45f), 0.010f, inset);
            CreateVisualBox(root, "WarehouseHeader", new Vector3(-facadeSide * 1.18f, 2.55f, 0f),
                new Vector3(0.08f, 0.24f, 3.10f), 0.015f, steel);
            CreateVisualBox(root, "WarehouseRoof", new Vector3(0f, 3.44f, 0f), new Vector3(2.50f, 0.20f, 5.35f), 0.05f, steel);
        }

        private void BuildUtilityTower(Transform parent, Vector3 anchor, float facadeSide, float height, int seed)
        {
            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;
            var root = new GameObject("UtilityRelayTower").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            CreateSolidBox(root, "TowerMass", new Vector3(0f, height * 0.5f, 0f), new Vector3(1.85f, height, 2.55f), 0.12f, wall);
            var panelCount = Mathf.Clamp(Mathf.FloorToInt(height / 1.45f), 2, 5);
            for (var i = 0; i < panelCount; i++)
            {
                CreateVisualBox(root, $"TowerVent_{i:00}",
                    new Vector3(-facadeSide * 0.955f, 0.92f + i * 1.18f, 0f),
                    new Vector3(0.06f, 0.58f, 1.52f), 0.009f, i % 2 == 0 ? grate : inset);
            }
            CreateVisualBox(root, "TowerCap", new Vector3(0f, height + 0.12f, 0f), new Vector3(2.12f, 0.24f, 2.82f), 0.05f, steel);
            CreateVisualBox(root, "AntennaMast", new Vector3(0f, height + 1.10f, 0f), new Vector3(0.16f, 2.05f, 0.16f), 0.025f, steel);
            CreateVisualBox(root, "AntennaBar", new Vector3(0f, height + 1.72f, 0f), new Vector3(1.20f, 0.10f, 0.10f), 0.018f, steel);
        }

        private void BuildMaintenanceKiosk(Transform parent, Vector3 anchor, float facadeSide, int seed)
        {
            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var root = new GameObject("MaintenanceKiosk").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            CreateSolidBox(root, "KioskMass", new Vector3(0f, 1.05f, 0f), new Vector3(2.0f, 2.10f, 3.6f), 0.11f, wall);
            CreateVisualBox(root, "ServiceWindow", new Vector3(-facadeSide * 1.035f, 1.25f, 0f),
                new Vector3(0.055f, 0.82f, 1.75f), 0.008f, inset);
            CreateVisualBox(root, "Awning", new Vector3(-facadeSide * 1.38f, 1.95f, 0f),
                new Vector3(0.78f, 0.12f, 2.42f), 0.025f, steel);
            CreateVisualBox(root, "Roof", new Vector3(0f, 2.20f, 0f), new Vector3(2.22f, 0.20f, 3.85f), 0.045f, steel);
        }

        private void BuildOpenCanopy(Transform parent, Vector3 anchor, int seed)
        {
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : steel;
            var root = new GameObject("ServiceCanopy").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var corners = new[]
            {
                new Vector3(-0.78f, 1.55f, -1.45f),
                new Vector3(0.78f, 1.55f, -1.45f),
                new Vector3(-0.78f, 1.55f, 1.45f),
                new Vector3(0.78f, 1.55f, 1.45f)
            };
            for (var i = 0; i < corners.Length; i++)
                CreateSolidBox(root, $"CanopyPost_{i}", corners[i], new Vector3(0.14f, 3.10f, 0.14f), 0.025f, steel);
            CreateSolidBox(root, "CanopyRoof", new Vector3(0f, 3.12f, 0f), new Vector3(1.90f, 0.18f, 3.35f), 0.035f, grate);
        }

        private void BuildOverheadServiceGate(Transform parent, int seed)
        {
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var root = new GameObject("OverheadServiceGate").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, 0f, 5.75f);

            CreateSolidBox(root, "GatePylon_W", new Vector3(-3.65f, 1.55f, 0f), new Vector3(0.52f, 3.10f, 0.72f), 0.07f, inset);
            CreateSolidBox(root, "GatePylon_E", new Vector3(3.65f, 1.55f, 0f), new Vector3(0.52f, 3.10f, 0.72f), 0.07f, inset);
            CreateSolidBox(root, "GateBridge", new Vector3(0f, 3.35f, 0f), new Vector3(7.85f, 0.46f, 0.82f), 0.07f, steel);
            CreateVisualBox(root, "GateServiceBox", new Vector3(1.45f, 3.78f, 0f), new Vector3(1.35f, 0.58f, 0.68f), 0.07f, inset);
        }

        private void BuildRoofPipeBridge(Transform parent, float side, int seed)
        {
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var root = new GameObject("PipeBridge").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(side * 4.75f, 0f, 0f);

            CreateSolidBox(root, "PipeBridgePost_N", new Vector3(0f, 1.65f, 3.15f), new Vector3(0.36f, 3.30f, 0.36f), 0.05f, inset);
            CreateSolidBox(root, "PipeBridgePost_S", new Vector3(0f, 1.65f, -3.15f), new Vector3(0.36f, 3.30f, 0.36f), 0.05f, inset);
            CreateSolidBox(root, "PipeBridgeDeck", new Vector3(0f, 3.25f, 0f), new Vector3(0.70f, 0.28f, 6.65f), 0.05f, steel);
            CreateVisualBox(root, "PipeA", new Vector3(side * 0.26f, 3.68f, 0f), new Vector3(0.18f, 0.18f, 6.05f), 0.04f, steel);
            CreateVisualBox(root, "PipeB", new Vector3(-side * 0.24f, 3.58f, 0f), new Vector3(0.14f, 0.14f, 6.05f), 0.035f, inset);
        }

        private static Transform FindBlockTransform(Transform stage, int index)
        {
            var prefix = $"Block_{index:00}_";
            for (var i = 0; i < stage.childCount; i++)
            {
                var child = stage.GetChild(i);
                if (child != null && child.name.StartsWith(prefix, StringComparison.Ordinal))
                    return child;
            }
            return null;
        }

        private static GameObject CreateSolidBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material,
            Quaternion? localRotation = null)
        {
            var go = CreateVisualBox(parent, name, localPosition, size, bevel, material, localRotation);
            var collider = go.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = size;
            collider.isTrigger = false;
            return go;
        }

        private static GameObject CreateVisualBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material,
            Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
