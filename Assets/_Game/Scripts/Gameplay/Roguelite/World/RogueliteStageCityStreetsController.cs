using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Builds readable city streets from explicit district templates instead of stamping the same
    /// dark cross into every cell. Roads are broad contiguous surfaces with restrained edges; alleys,
    /// plazas, yards, ruins and checkpoints deliberately use different spatial grammars.
    /// </summary>
    [DefaultExecutionOrder(23)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageCityStreetsController : MonoBehaviour
    {
        private const float ChunkWidth = 18f;
        private const float ChunkDepth = 14f;

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
            if (stage.transform.Find("[Chernobog_UrbanArchitecture]") == null)
                return;

            Build(stage.transform);
            Physics.SyncTransforms();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/CityStreets] Stage {stageMap.StageIndex}: district street templates built.", this);
        }

        private void Build(Transform stage)
        {
            var old = stage.Find("[Chernobog_CityStreets]");
            if (old != null)
                Destroy(old.gameObject);

            var root = new GameObject("[Chernobog_CityStreets]").transform;
            root.SetParent(stage, false);

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                var block = FindBlockTransform(stage, i);
                if (data == null || block == null)
                    continue;

                var marker = block.GetComponent<RogueliteDistrictTemplate25D>();
                var district = marker != null
                    ? marker.DistrictType
                    : RogueliteStageDistrictTemplateController.ResolveDistrict(data, i, stageMap.StageIndex);

                var cell = new GameObject($"Block_{i:00}_CityStreet_{district}").transform;
                cell.SetParent(root, false);
                cell.position = block.position;

                BuildDistrictGround(cell, district, i);
                BuildDistrictArchitecture(cell, data, district, i);
                BuildDistrictFurniture(cell, district, i);
            }
        }

        private void BuildDistrictGround(Transform parent, ChernobogDistrictType district, int seed)
        {
            var road = kit.deckHeavyMaterial != null ? kit.deckHeavyMaterial : kit.deckMaterial;
            var pavement = kit.deckSecondaryMaterial != null ? kit.deckSecondaryMaterial : kit.deckMaterial;
            var edge = kit.steelMaterial != null ? kit.steelMaterial : road;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : kit.insetMaterial;
            var dark = kit.insetMaterial != null ? kit.insetMaterial : road;

            switch (district)
            {
                case ChernobogDistrictType.MainStreet:
                    BuildMainStreet(parent, road, pavement, edge, grate);
                    break;
                case ChernobogDistrictType.Alley:
                    BuildAlley(parent, road, pavement, edge, grate);
                    break;
                case ChernobogDistrictType.Plaza:
                    BuildPlaza(parent, pavement, road, edge, grate);
                    break;
                case ChernobogDistrictType.ServiceYard:
                    BuildServiceYard(parent, pavement, road, edge, grate);
                    break;
                case ChernobogDistrictType.RuinedBlock:
                    BuildRuinedStreet(parent, road, pavement, edge, dark, seed);
                    break;
                case ChernobogDistrictType.Checkpoint:
                    BuildCheckpointRoad(parent, road, pavement, edge, grate);
                    break;
            }
        }

        private void BuildMainStreet(Transform parent, Material road, Material pavement, Material edge, Material grate)
        {
            // One unmistakable, broad E/W street. The narrow N/S connector only communicates the
            // junction and keeps the waypoint cross visually coherent without turning the whole cell
            // into a checkerboard of dark strips.
            CreateBox(parent, "MainStreet_Carriageway", new Vector3(0f, 0.073f, 0f),
                new Vector3(ChunkWidth - 0.28f, 0.035f, 5.80f), 0.006f, road, false);
            CreateBox(parent, "MainStreet_NorthWalk", new Vector3(0f, 0.086f, 3.62f),
                new Vector3(ChunkWidth - 0.35f, 0.055f, 1.35f), 0.008f, pavement, false);
            CreateBox(parent, "MainStreet_SouthWalk", new Vector3(0f, 0.086f, -3.62f),
                new Vector3(ChunkWidth - 0.35f, 0.055f, 1.35f), 0.008f, pavement, false);
            CreateBox(parent, "MainStreet_Junction", new Vector3(0f, 0.075f, 0f),
                new Vector3(4.1f, 0.038f, ChunkDepth - 0.35f), 0.006f, road, false);
            CreateBox(parent, "MainStreet_CenterJoint", new Vector3(0f, 0.098f, 0f),
                new Vector3(ChunkWidth - 1.0f, 0.018f, 0.055f), 0.002f, edge, false);
            CreateBox(parent, "MainStreet_GutterN", new Vector3(0f, 0.103f, 2.96f),
                new Vector3(ChunkWidth - 0.70f, 0.020f, 0.18f), 0.003f, grate, false);
            CreateBox(parent, "MainStreet_GutterS", new Vector3(0f, 0.103f, -2.96f),
                new Vector3(ChunkWidth - 0.70f, 0.020f, 0.18f), 0.003f, grate, false);
        }

        private void BuildAlley(Transform parent, Material road, Material pavement, Material edge, Material grate)
        {
            CreateBox(parent, "Alley_Main", new Vector3(-1.25f, 0.074f, 0f),
                new Vector3(3.55f, 0.036f, ChunkDepth - 0.30f), 0.006f, road, false);
            CreateBox(parent, "Alley_Branch", new Vector3(2.15f, 0.075f, 1.15f),
                new Vector3(7.0f, 0.036f, 2.65f), 0.006f, road, false);
            CreateBox(parent, "Alley_WalkWest", new Vector3(-3.55f, 0.086f, 0f),
                new Vector3(0.95f, 0.052f, ChunkDepth - 0.55f), 0.008f, pavement, false);
            CreateBox(parent, "Alley_WalkEast", new Vector3(0.95f, 0.086f, -1.35f),
                new Vector3(0.90f, 0.052f, ChunkDepth - 3.1f), 0.008f, pavement, false);
            CreateBox(parent, "Alley_Drain", new Vector3(0.38f, 0.103f, -0.80f),
                new Vector3(0.20f, 0.020f, 5.3f), 0.003f, grate, false);
            CreateBox(parent, "Alley_BranchEdge", new Vector3(2.20f, 0.101f, 2.50f),
                new Vector3(6.5f, 0.018f, 0.055f), 0.002f, edge, false);
        }

        private void BuildPlaza(Transform parent, Material pavement, Material road, Material edge, Material grate)
        {
            CreateBox(parent, "Plaza_MainPad", new Vector3(0f, 0.082f, 0f),
                new Vector3(12.6f, 0.052f, 9.4f), 0.012f, pavement, false);
            CreateBox(parent, "Plaza_EWEntry", new Vector3(0f, 0.074f, 0f),
                new Vector3(ChunkWidth - 0.30f, 0.036f, 3.25f), 0.006f, road, false);
            CreateBox(parent, "Plaza_NSEntry", new Vector3(0f, 0.075f, 0f),
                new Vector3(3.25f, 0.036f, ChunkDepth - 0.30f), 0.006f, road, false);
            CreateBox(parent, "Plaza_FrameN", new Vector3(0f, 0.108f, 4.54f),
                new Vector3(11.5f, 0.025f, 0.10f), 0.003f, edge, false);
            CreateBox(parent, "Plaza_FrameS", new Vector3(0f, 0.108f, -4.54f),
                new Vector3(11.5f, 0.025f, 0.10f), 0.003f, edge, false);
            CreateBox(parent, "Plaza_Drain", new Vector3(4.85f, 0.109f, 0f),
                new Vector3(0.26f, 0.024f, 4.8f), 0.004f, grate, false);
        }

        private void BuildServiceYard(Transform parent, Material pavement, Material road, Material edge, Material grate)
        {
            CreateBox(parent, "Yard_Pad", new Vector3(0f, 0.080f, 0f),
                new Vector3(13.8f, 0.050f, 9.8f), 0.010f, pavement, false);
            CreateBox(parent, "Yard_ServiceLane", new Vector3(0f, 0.088f, -1.25f),
                new Vector3(ChunkWidth - 0.32f, 0.036f, 3.65f), 0.006f, road, false);
            CreateBox(parent, "Yard_LoadingApron", new Vector3(3.75f, 0.102f, 3.15f),
                new Vector3(5.1f, 0.035f, 2.15f), 0.006f, road, false);
            CreateBox(parent, "Yard_ServiceTrench", new Vector3(-4.65f, 0.110f, 2.8f),
                new Vector3(0.50f, 0.026f, 4.2f), 0.004f, grate, false);
            CreateBox(parent, "Yard_Edge", new Vector3(0f, 0.112f, 4.72f),
                new Vector3(12.5f, 0.025f, 0.08f), 0.003f, edge, false);
        }

        private void BuildRuinedStreet(Transform parent, Material road, Material pavement, Material edge, Material dark, int seed)
        {
            CreateBox(parent, "Ruined_MainRoad", new Vector3(0f, 0.072f, 0f),
                new Vector3(ChunkWidth - 0.30f, 0.034f, 4.6f), 0.006f, road, false);
            CreateBox(parent, "Ruined_SidewalkN", new Vector3(-1.6f, 0.084f, 3.42f),
                new Vector3(12.0f, 0.050f, 1.25f), 0.008f, pavement, false);
            CreateBox(parent, "Ruined_PatchA", new Vector3(-4.8f, 0.104f, -0.45f),
                new Vector3(3.6f, 0.022f, 2.15f), 0.004f, dark, false,
                Quaternion.Euler(0f, Hash01(seed + 7) * 8f - 4f, 0f));
            CreateBox(parent, "Ruined_PatchB", new Vector3(3.8f, 0.105f, 0.68f),
                new Vector3(2.8f, 0.024f, 1.65f), 0.004f, dark, false,
                Quaternion.Euler(0f, Hash01(seed + 17) * 10f - 5f, 0f));
            CreateBox(parent, "Ruined_CrackBand", new Vector3(0.8f, 0.110f, -2.28f),
                new Vector3(5.8f, 0.020f, 0.065f), 0.002f, edge, false,
                Quaternion.Euler(0f, -8f, 0f));
        }

        private void BuildCheckpointRoad(Transform parent, Material road, Material pavement, Material edge, Material grate)
        {
            CreateBox(parent, "Checkpoint_Road", new Vector3(0f, 0.073f, 0f),
                new Vector3(ChunkWidth - 0.28f, 0.036f, 5.1f), 0.006f, road, false);
            CreateBox(parent, "Checkpoint_InspectionPad", new Vector3(1.6f, 0.088f, 0f),
                new Vector3(7.6f, 0.045f, 7.6f), 0.010f, pavement, false);
            CreateBox(parent, "Checkpoint_NSAccess", new Vector3(0f, 0.075f, 0f),
                new Vector3(3.45f, 0.036f, ChunkDepth - 0.30f), 0.006f, road, false);
            CreateBox(parent, "Checkpoint_StopLine", new Vector3(-1.55f, 0.109f, 0f),
                new Vector3(0.08f, 0.022f, 4.45f), 0.003f, edge, false);
            CreateBox(parent, "Checkpoint_Drain", new Vector3(5.25f, 0.109f, 0f),
                new Vector3(0.30f, 0.024f, 4.15f), 0.004f, grate, false);
        }

        private void BuildDistrictArchitecture(Transform parent, RogueliteBlockState data, ChernobogDistrictType district, int seed)
        {
            // Buildings stay concentrated on camera-far edges. District templates decide how much street
            // wall exists so open/plaza cells no longer feel randomly packed with the same tall masses.
            switch (district)
            {
                case ChernobogDistrictType.MainStreet:
                    BuildSealedBuilding(parent, new Vector3(-5.35f, 0f, 5.35f), 4.10f, 2.45f,
                        Mathf.Lerp(4.2f, 5.8f, Hash01(seed + 5)), seed, false);
                    if (PositiveMod(seed, 2) == 0)
                        BuildClosedStorefront(parent, new Vector3(5.15f, 0f, 5.38f), 4.0f, 2.40f, 3.25f, seed + 17);
                    break;
                case ChernobogDistrictType.Alley:
                    BuildSealedBuilding(parent, new Vector3(-5.55f, 0f, 5.30f), 3.75f, 2.55f,
                        Mathf.Lerp(5.2f, 6.8f, Hash01(seed + 9)), seed, true);
                    break;
                case ChernobogDistrictType.Plaza:
                    if (PositiveMod(seed, 2) == 0)
                        BuildClosedStorefront(parent, new Vector3(5.45f, 0f, 5.42f), 3.8f, 2.35f, 3.1f, seed + 23);
                    break;
                case ChernobogDistrictType.ServiceYard:
                    BuildSealedBuilding(parent, new Vector3(5.35f, 0f, 5.28f), 4.15f, 2.55f,
                        4.3f, seed + 31, true);
                    break;
                case ChernobogDistrictType.RuinedBlock:
                    BuildSealedBuilding(parent, new Vector3(-5.45f, 0f, 5.35f), 3.9f, 2.45f,
                        4.0f, seed + 41, false);
                    break;
                case ChernobogDistrictType.Checkpoint:
                    BuildSealedBuilding(parent, new Vector3(5.35f, 0f, 5.30f), 3.8f, 2.45f,
                        4.4f, seed + 53, true);
                    break;
            }

            if (data.Coordinate.x == stageMap.Width - 1 && district != ChernobogDistrictType.Plaza)
            {
                var z = PositiveMod(seed, 2) == 0 ? 4.6f : -4.6f;
                BuildSideBuilding(parent, new Vector3(7.35f, 0f, z), 2.35f, 3.15f,
                    Mathf.Lerp(3.8f, 5.4f, Hash01(seed + 71)), seed + 71);
            }
        }

        private void BuildSealedBuilding(Transform parent, Vector3 anchor, float width, float depth, float height, int seed, bool utilityFacade)
        {
            var root = new GameObject(utilityFacade ? "SealedUtilityBuilding" : "SealedCityBuilding").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            CreateBox(root, "BuildingMass", new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, depth), 0.13f, wall, true);
            CreateBox(root, "RoofCap", new Vector3(0f, height + 0.10f, 0f),
                new Vector3(width * 1.06f, 0.20f, depth * 1.05f), 0.045f, steel, false);

            var floors = Mathf.Clamp(Mathf.RoundToInt(height / 1.75f), 2, 4);
            for (var floor = 0; floor < floors; floor++)
            {
                var y = 1.0f + floor * 1.42f;
                if (y > height - 0.4f)
                    break;
                for (var panel = -1; panel <= 1; panel++)
                {
                    var x = panel * width * 0.25f;
                    CreateBox(root, $"FacadeWindow_{floor}_{panel + 1}",
                        new Vector3(x, y, -depth * 0.5f - 0.036f),
                        new Vector3(width * 0.18f, 0.62f, 0.055f), 0.006f,
                        utilityFacade && panel == 0 ? grate : inset, false);
                }
            }

            if (kit.hvacSmall != null && PositiveMod(seed, 3) == 0)
            {
                var hvac = Instantiate(kit.hvacSmall, root);
                hvac.name = "RoofHVAC";
                hvac.transform.localPosition = new Vector3(width * 0.18f, height + 0.18f, 0f);
                hvac.transform.localScale = Vector3.one * 0.66f;
                DisablePrefabColliders(hvac);
            }
        }

        private void BuildClosedStorefront(Transform parent, Vector3 anchor, float width, float depth, float height, int seed)
        {
            var root = new GameObject("ClosedStorefront").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            CreateBox(root, "StoreMass", new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, depth), 0.12f, wall, true);
            CreateBox(root, "RollerShutter", new Vector3(0f, 1.25f, -depth * 0.5f - 0.045f),
                new Vector3(width * 0.68f, 1.95f, 0.065f), 0.008f, grate, false);
            CreateBox(root, "Awning", new Vector3(0f, 2.58f, -depth * 0.5f - 0.36f),
                new Vector3(width * 0.80f, 0.12f, 0.72f), 0.025f, steel, false,
                Quaternion.Euler(6f, 0f, 0f));
            CreateBox(root, "Roof", new Vector3(0f, height + 0.09f, 0f),
                new Vector3(width * 1.05f, 0.18f, depth * 1.05f), 0.04f, steel, false);
        }

        private void BuildSideBuilding(Transform parent, Vector3 anchor, float width, float depth, float height, int seed)
        {
            var root = new GameObject("SealedSideBuilding").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            CreateBox(root, "Mass", new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, depth), 0.12f, wall, true);
            CreateBox(root, "EastFacadeInset", new Vector3(-width * 0.5f - 0.035f, height * 0.48f, 0f),
                new Vector3(0.055f, Mathf.Min(1.45f, height * 0.32f), depth * 0.62f), 0.006f, grate, false);
            CreateBox(root, "Cap", new Vector3(0f, height + 0.10f, 0f),
                new Vector3(width * 1.06f, 0.20f, depth * 1.05f), 0.04f, steel, false);
        }

        private void BuildDistrictFurniture(Transform parent, ChernobogDistrictType district, int seed)
        {
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var emissive = kit.emissiveMaterial != null ? kit.emissiveMaterial : kit.accentMaterial;

            switch (district)
            {
                case ChernobogDistrictType.MainStreet:
                    BuildLamp(parent, new Vector3(-5.55f, 0f, 3.82f), steel, emissive);
                    BuildLamp(parent, new Vector3(5.55f, 0f, -3.82f), steel, emissive);
                    CreateBox(parent, "StreetCabinet", new Vector3(6.05f, 0.50f, 3.70f),
                        new Vector3(0.72f, 1.0f, 0.58f), 0.05f, inset, true);
                    break;
                case ChernobogDistrictType.Alley:
                    BuildLamp(parent, new Vector3(-3.95f, 0f, -4.85f), steel, emissive);
                    CreateBox(parent, "AlleyCabinet", new Vector3(1.62f, 0.44f, 4.72f),
                        new Vector3(0.62f, 0.88f, 0.54f), 0.05f, inset, true);
                    break;
                case ChernobogDistrictType.Plaza:
                    BuildLamp(parent, new Vector3(-5.35f, 0f, 4.68f), steel, emissive);
                    BuildLamp(parent, new Vector3(5.35f, 0f, 4.68f), steel, emissive);
                    break;
                case ChernobogDistrictType.ServiceYard:
                    CreateBox(parent, "YardCabinetA", new Vector3(-5.4f, 0.58f, 4.35f),
                        new Vector3(0.86f, 1.16f, 0.68f), 0.055f, inset, true);
                    CreateBox(parent, "YardCabinetB", new Vector3(-4.25f, 0.42f, 4.30f),
                        new Vector3(0.70f, 0.84f, 0.62f), 0.050f, inset, true);
                    break;
                case ChernobogDistrictType.Checkpoint:
                    for (var i = 0; i < 3; i++)
                        CreateBox(parent, $"CheckpointBollard_{i}", new Vector3(-2.15f, 0.32f, -1.10f + i * 1.10f),
                            new Vector3(0.18f, 0.64f, 0.18f), 0.025f, steel, true);
                    BuildLamp(parent, new Vector3(5.45f, 0f, 4.2f), steel, emissive);
                    break;
                case ChernobogDistrictType.RuinedBlock:
                    if (PositiveMod(seed, 2) == 0)
                        BuildLamp(parent, new Vector3(-5.25f, 0f, 4.2f), steel, emissive);
                    break;
            }
        }

        private static void BuildLamp(Transform parent, Vector3 basePosition, Material steel, Material emissive)
        {
            var root = new GameObject("StreetLamp").transform;
            root.SetParent(parent, false);
            root.localPosition = basePosition;
            CreateBox(root, "Pole", new Vector3(0f, 1.55f, 0f), new Vector3(0.10f, 3.10f, 0.10f), 0.018f, steel, true);
            CreateBox(root, "Arm", new Vector3(0.34f, 2.92f, 0f), new Vector3(0.74f, 0.09f, 0.09f), 0.015f, steel, false);
            CreateBox(root, "Lamp", new Vector3(0.70f, 2.86f, 0f), new Vector3(0.20f, 0.12f, 0.18f), 0.025f, emissive, false);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, float bevel,
            Material material, bool collider, Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size,
                Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.22f));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = Vector3.zero;
                box.size = size;
                box.isTrigger = false;
            }
            return go;
        }

        private static void DisablePrefabColliders(GameObject root)
        {
            if (root == null)
                return;
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                if (colliders[i] != null)
                    colliders[i].enabled = false;
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

        private static float Hash01(int value)
        {
            unchecked
            {
                uint x = (uint)(value + 0x9E3779B9);
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (x & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static int PositiveMod(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
