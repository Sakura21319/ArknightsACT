using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Converts the combat-cell floor language into a recognizable mobile-city street grid.
    /// Roads and sidewalks own the central traversal cross; dense sealed facades sit on the camera-far
    /// north/east edges, while existing PlayableArchitecture supplies the smaller enterable buildings.
    /// </summary>
    [DefaultExecutionOrder(23)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageCityStreetsController : MonoBehaviour
    {
        private const float ChunkWidth = 18f;
        private const float ChunkDepth = 14f;
        private const float RoadHalfWidth = 2.55f;

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
            Debug.Log($"[ArknightsACT/CityStreets] Stage {stageMap.StageIndex}: roads, sidewalks, sealed facades and street clutter built.", this);
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

                var cell = new GameObject($"Block_{i:00}_CityStreet").transform;
                cell.SetParent(root, false);
                cell.position = block.position;

                BuildRoadGrid(cell, data.Theme, i);
                BuildNorthStreetWall(cell, data, i);
                if (data.Coordinate.x == stageMap.Width - 1)
                    BuildEastStreetWall(cell, data, i + 97);
                BuildStreetFurniture(cell, data.Theme, i);
            }
        }

        private void BuildRoadGrid(Transform parent, RogueliteChunkTheme theme, int seed)
        {
            var road = kit.insetMaterial != null ? kit.insetMaterial : kit.deckHeavyMaterial;
            var sidewalk = kit.deckSecondaryMaterial != null ? kit.deckSecondaryMaterial : kit.deckMaterial;
            var edge = kit.steelMaterial != null ? kit.steelMaterial : kit.deckHeavyMaterial;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : road;

            // Central cross is deliberately uninterrupted so the current waypoint graph still owns a
            // broad, reliable route between cells. The material change is visual, not a collider seam.
            CreateBox(parent, "Road_EW", new Vector3(0f, 0.072f, 0f),
                new Vector3(ChunkWidth - 0.36f, 0.030f, RoadHalfWidth * 2f), 0.006f, road, false);
            CreateBox(parent, "Road_NS", new Vector3(0f, 0.074f, 0f),
                new Vector3(RoadHalfWidth * 2f, 0.032f, ChunkDepth - 0.36f), 0.006f, road, false);

            // Sidewalk/service strips make the cell read as a street block instead of a single metal pad.
            CreateBox(parent, "Sidewalk_N", new Vector3(0f, 0.082f, 3.30f),
                new Vector3(ChunkWidth - 0.45f, 0.050f, 1.06f), 0.008f, sidewalk, false);
            CreateBox(parent, "Sidewalk_S", new Vector3(0f, 0.082f, -3.30f),
                new Vector3(ChunkWidth - 0.45f, 0.050f, 1.06f), 0.008f, sidewalk, false);
            CreateBox(parent, "Sidewalk_W", new Vector3(-3.35f, 0.084f, 0f),
                new Vector3(1.08f, 0.052f, ChunkDepth - 0.45f), 0.008f, sidewalk, false);
            CreateBox(parent, "Sidewalk_E", new Vector3(3.35f, 0.084f, 0f),
                new Vector3(1.08f, 0.052f, ChunkDepth - 0.45f), 0.008f, sidewalk, false);

            // Sparse road joints / drainage. No repeated orange IDs or bright tactical markings.
            CreateBox(parent, "Drain_N", new Vector3(-1.65f, 0.101f, 2.72f),
                new Vector3(2.2f, 0.026f, 0.30f), 0.004f, grate, false);
            CreateBox(parent, "Drain_S", new Vector3(1.90f, 0.101f, -2.72f),
                new Vector3(2.0f, 0.026f, 0.30f), 0.004f, grate, false);

            if (theme == RogueliteChunkTheme.Street || theme == RogueliteChunkTheme.SafePlaza)
            {
                CreateBox(parent, "RoadJoint_EW", new Vector3(0f, 0.097f, 0f),
                    new Vector3(7.2f, 0.020f, 0.055f), 0.002f, edge, false);
                CreateBox(parent, "RoadJoint_NS", new Vector3(0f, 0.098f, 0f),
                    new Vector3(0.055f, 0.020f, 6.1f), 0.002f, edge, false);
            }
        }

        private void BuildNorthStreetWall(Transform parent, RogueliteBlockState data, int seed)
        {
            // The fixed gameplay camera looks toward north/east. Dense facades here create the same
            // readable street-wall layering as story backgrounds without hiding the player in foreground.
            var leftHeight = data.Theme == RogueliteChunkTheme.BossArena ? 7.4f : Mathf.Lerp(4.2f, 6.4f, Hash01(seed * 31 + 7));
            var rightHeight = data.Theme == RogueliteChunkTheme.Facility ? 7.0f : Mathf.Lerp(3.8f, 6.8f, Hash01(seed * 47 + 13));

            if (data.Theme == RogueliteChunkTheme.SafePlaza)
            {
                BuildClosedStorefront(parent, new Vector3(-5.15f, 0f, 5.18f), 4.55f, 2.72f, 3.25f, seed);
                BuildSealedBuilding(parent, new Vector3(5.15f, 0f, 5.18f), 4.55f, 2.72f, 4.6f, seed + 11, true);
                return;
            }

            BuildSealedBuilding(parent, new Vector3(-5.15f, 0f, 5.12f), 4.55f, 2.78f, leftHeight, seed, false);
            if (data.Theme == RogueliteChunkTheme.Open && PositiveMod(seed, 3) == 1)
                BuildClosedStorefront(parent, new Vector3(5.15f, 0f, 5.14f), 4.55f, 2.74f, 3.45f, seed + 19);
            else
                BuildSealedBuilding(parent, new Vector3(5.15f, 0f, 5.12f), 4.55f, 2.78f, rightHeight, seed + 19, true);
        }

        private void BuildEastStreetWall(Transform parent, RogueliteBlockState data, int seed)
        {
            // Only the map's right-most column gets this extra external-facing wall. It closes the
            // camera-right silhouette while keeping the E/W traversal opening around z=0 completely clear.
            BuildSideBuilding(parent, new Vector3(7.25f, 0f, 4.55f), 2.65f, 3.65f,
                Mathf.Lerp(4.5f, 7.2f, Hash01(seed + 3)), seed);
            BuildSideBuilding(parent, new Vector3(7.25f, 0f, -4.55f), 2.65f, 3.65f,
                Mathf.Lerp(3.8f, 6.4f, Hash01(seed + 17)), seed + 17);
        }

        private void BuildSealedBuilding(
            Transform parent,
            Vector3 anchor,
            float width,
            float depth,
            float height,
            int seed,
            bool utilityFacade)
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
                var y = 1.05f + floor * 1.45f;
                if (y > height - 0.45f)
                    break;

                for (var panel = -1; panel <= 1; panel++)
                {
                    var x = panel * width * 0.25f;
                    CreateBox(root, $"FacadeWindow_{floor}_{panel + 1}",
                        new Vector3(x, y, -depth * 0.5f - 0.036f),
                        new Vector3(width * 0.18f, 0.66f, 0.055f), 0.006f,
                        utilityFacade && panel == 0 ? grate : inset, false);
                }
            }

            CreateBox(root, "FacadeBand", new Vector3(0f, Mathf.Min(height - 0.42f, 2.72f), -depth * 0.5f - 0.052f),
                new Vector3(width * 0.84f, 0.12f, 0.07f), 0.012f, steel, false);

            if (kit.hvacSmall != null && PositiveMod(seed, 2) == 0)
            {
                var hvac = Instantiate(kit.hvacSmall, root);
                hvac.name = "RoofHVAC";
                hvac.transform.localPosition = new Vector3(width * 0.18f, height + 0.18f, 0f);
                hvac.transform.localScale = new Vector3(0.72f, 0.72f, 0.72f);
                DisablePrefabColliders(hvac);
            }
        }

        private void BuildClosedStorefront(
            Transform parent,
            Vector3 anchor,
            float width,
            float depth,
            float height,
            int seed)
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
            CreateBox(root, "RollerShutter", new Vector3(0f, 1.35f, -depth * 0.5f - 0.045f),
                new Vector3(width * 0.70f, 2.20f, 0.065f), 0.008f, grate, false);
            CreateBox(root, "DoorFrameTop", new Vector3(0f, 2.55f, -depth * 0.5f - 0.065f),
                new Vector3(width * 0.78f, 0.16f, 0.09f), 0.018f, steel, false);
            CreateBox(root, "Awning", new Vector3(0f, 2.82f, -depth * 0.5f - 0.42f),
                new Vector3(width * 0.82f, 0.12f, 0.86f), 0.025f, steel, false,
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
                new Vector3(0.055f, Mathf.Min(1.5f, height * 0.32f), depth * 0.62f), 0.006f, grate, false);
            CreateBox(root, "Cap", new Vector3(0f, height + 0.10f, 0f),
                new Vector3(width * 1.06f, 0.20f, depth * 1.05f), 0.04f, steel, false);
        }

        private void BuildStreetFurniture(Transform parent, RogueliteChunkTheme theme, int seed)
        {
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var emissive = kit.emissiveMaterial != null ? kit.emissiveMaterial : kit.accentMaterial;

            BuildLamp(parent, new Vector3(-4.15f, 0f, 3.78f), steel, emissive);
            if (PositiveMod(seed, 2) == 0)
                BuildLamp(parent, new Vector3(4.15f, 0f, -3.78f), steel, emissive);

            CreateBox(parent, "StreetServiceCabinet", new Vector3(5.55f, 0.52f, 3.70f),
                new Vector3(0.80f, 1.04f, 0.62f), 0.055f, inset, true);

            // A small line of bollards defines the pedestrian/service edge without sealing the route.
            for (var i = 0; i < 3; i++)
            {
                var x = -5.65f + i * 0.62f;
                CreateBox(parent, $"Bollard_{i}", new Vector3(x, 0.34f, -3.82f),
                    new Vector3(0.16f, 0.68f, 0.16f), 0.025f, steel, true);
            }

            if (theme == RogueliteChunkTheme.Facility && kit.pipeRun != null)
            {
                var pipe = Instantiate(kit.pipeRun, parent);
                pipe.name = "StreetPipeService";
                pipe.transform.localPosition = new Vector3(6.05f, 0.10f, -3.70f);
                pipe.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                pipe.transform.localScale = new Vector3(0.72f, 0.72f, 0.72f);
                DisablePrefabColliders(pipe);
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

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material,
            bool collider,
            Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.22f));
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
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
