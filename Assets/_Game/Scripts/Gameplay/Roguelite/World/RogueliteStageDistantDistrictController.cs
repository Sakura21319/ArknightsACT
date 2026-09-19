using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Fills the north/east horizon behind the playable deck with distant mobile-city districts.
    /// This is visual-only: no colliders, no gameplay ownership, no claim that this is a canonical
    /// Chernobog chassis plan. The goal is to remove the black void and make the stage feel embedded
    /// in a much larger moving city.
    /// </summary>
    [DefaultExecutionOrder(26)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageDistantDistrictController : MonoBehaviour
    {
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;

        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private ChernobogEnvironmentKit kit;
        private RogueliteStageRuntimeContext _context;

        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map, ChernobogEnvironmentKit environmentKit)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
            kit = environmentKit;
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context != null)
            {
                stageMap ??= _context.StageMap;
                kit ??= _context.EnvironmentKit;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.18f;

            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = _context != null && _context.StageRoot != null
                ? _context.StageRoot.gameObject
                : null;
            if (stage == null || stage == _preparedStage)
                return;

            Build(stage.transform);
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/DistantDistrict] Stage {stageMap.StageIndex}: layered north/east city background built.", this);
        }

        private void Build(Transform stage)
        {
            var old = stage.Find("[Chernobog_DistantDistrict]");
            if (old != null)
                Destroy(old.gameObject);

            var root = RogueliteStageVisualRootUtility.GetOrCreate(stage, "[Chernobog_DistantDistrict]", true);

            var width = Mathf.Max(ChunkWidth, stageMap.Width * ChunkWidth);
            var depth = Mathf.Max(ChunkDepth, stageMap.Height * ChunkDepth);
            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            BuildNorthTerraces(root, width, depth, wall, inset, steel, grate);
            BuildEastTerraces(root, width, depth, wall, inset, steel, grate);
            BuildFarIndustrialSilhouette(root, width, depth, wall, inset, steel, grate);
        }

        private void BuildNorthTerraces(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate)
        {
            var northZ = depth * 0.5f;
            var terraceWidth = width * 1.35f + 22f;

            CreateVisualBox(root, "NorthBackgroundDeck", new Vector3(0f, -1.55f, northZ + 9.5f),
                new Vector3(terraceWidth, 2.8f, 12.5f), 0.20f, inset, false);
            CreateVisualBox(root, "NorthBackgroundEdge", new Vector3(0f, -0.20f, northZ + 3.45f),
                new Vector3(terraceWidth, 1.45f, 0.75f), 0.10f, wall, false);
            CreateVisualBox(root, "NorthBackgroundVentBand", new Vector3(0f, -0.12f, northZ + 3.05f),
                new Vector3(terraceWidth * 0.82f, 0.58f, 0.06f), 0.010f, grate, false);

            var count = Mathf.Clamp(Mathf.RoundToInt(width / 6f) + 4, 7, 14);
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5f : i / (float)(count - 1);
                var x = Mathf.Lerp(-terraceWidth * 0.42f, terraceWidth * 0.42f, t);
                var seed = stageMap.StageIndex * 73 + i * 19;
                var h = Mathf.Lerp(4.5f, 11.5f, Hash01(seed + 3));
                var w = Mathf.Lerp(3.1f, 5.7f, Hash01(seed + 7));
                var d = Mathf.Lerp(3.3f, 6.2f, Hash01(seed + 11));
                var z = northZ + Mathf.Lerp(7.2f, 13.8f, Hash01(seed + 17));
                var material = i % 3 == 0 ? inset : wall;

                CreateVisualBox(root, $"NorthBlock_{i:00}", new Vector3(x, h * 0.5f - 0.05f, z),
                    new Vector3(w, h, d), 0.14f, material, false);
                CreateVisualBox(root, $"NorthBlockCap_{i:00}", new Vector3(x, h + 0.15f, z),
                    new Vector3(w * 1.06f, 0.24f, d * 1.04f), 0.05f, steel, false);

                if (i % 2 == 0)
                {
                    CreateVisualBox(root, $"NorthBlockVent_{i:00}",
                        new Vector3(x, Mathf.Min(h * 0.58f, 4.4f), z - d * 0.5f - 0.035f),
                        new Vector3(w * 0.62f, 0.72f, 0.06f), 0.010f, grate, false);
                }
                if (i % 4 == 1)
                {
                    CreateVisualBox(root, $"NorthAntenna_{i:00}", new Vector3(x, h + 1.25f, z),
                        new Vector3(0.13f, 2.2f, 0.13f), 0.020f, steel, false);
                }
            }

            // One long bridge breaks the skyline into readable layers instead of a wall of boxes.
            CreateVisualBox(root, "NorthSkyBridge", new Vector3(width * 0.08f, 5.35f, northZ + 10.4f),
                new Vector3(Mathf.Max(14f, width * 0.62f), 0.55f, 1.0f), 0.08f, steel, false);
            CreateVisualBox(root, "NorthSkyBridgeInset", new Vector3(width * 0.08f, 5.35f, northZ + 9.87f),
                new Vector3(Mathf.Max(12f, width * 0.54f), 0.28f, 0.06f), 0.010f, grate, false);
        }

        private void BuildEastTerraces(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate)
        {
            var eastX = width * 0.5f;
            var terraceDepth = depth * 1.15f + 14f;

            CreateVisualBox(root, "EastBackgroundDeck", new Vector3(eastX + 8.4f, -1.65f, 0f),
                new Vector3(11.0f, 2.9f, terraceDepth), 0.20f, inset, false);
            CreateVisualBox(root, "EastBackgroundEdge", new Vector3(eastX + 3.18f, -0.22f, 0f),
                new Vector3(0.72f, 1.42f, terraceDepth), 0.10f, wall, false);

            var count = Mathf.Clamp(Mathf.RoundToInt(depth / 6f) + 3, 6, 12);
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5f : i / (float)(count - 1);
                var z = Mathf.Lerp(-terraceDepth * 0.38f, terraceDepth * 0.38f, t);
                var seed = stageMap.StageIndex * 101 + i * 31;
                var h = Mathf.Lerp(4.0f, 9.5f, Hash01(seed + 5));
                var w = Mathf.Lerp(2.8f, 5.0f, Hash01(seed + 13));
                var d = Mathf.Lerp(3.2f, 5.7f, Hash01(seed + 29));
                var x = eastX + Mathf.Lerp(6.2f, 12.4f, Hash01(seed + 37));
                var material = i % 3 == 1 ? inset : wall;

                CreateVisualBox(root, $"EastBlock_{i:00}", new Vector3(x, h * 0.5f, z),
                    new Vector3(w, h, d), 0.14f, material, false);
                CreateVisualBox(root, $"EastBlockCap_{i:00}", new Vector3(x, h + 0.14f, z),
                    new Vector3(w * 1.06f, 0.22f, d * 1.05f), 0.05f, steel, false);
                if (i % 2 == 1)
                {
                    CreateVisualBox(root, $"EastVent_{i:00}", new Vector3(x - w * 0.5f - 0.035f, h * 0.52f, z),
                        new Vector3(0.06f, 0.68f, d * 0.58f), 0.010f, grate, false);
                }
            }
        }

        private void BuildFarIndustrialSilhouette(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate)
        {
            var northZ = depth * 0.5f;
            var farZ = northZ + 22f;
            var span = width * 1.65f + 32f;

            CreateVisualBox(root, "FarCityMass", new Vector3(0f, 1.35f, farZ),
                new Vector3(span, 4.4f, 5.8f), 0.18f, inset, false);

            var towers = Mathf.Clamp(Mathf.RoundToInt(span / 12f), 5, 10);
            for (var i = 0; i < towers; i++)
            {
                var x = Mathf.Lerp(-span * 0.42f, span * 0.42f, towers == 1 ? 0.5f : i / (float)(towers - 1));
                var h = 8.0f + (i % 3) * 2.3f;
                CreateVisualBox(root, $"FarTower_{i:00}", new Vector3(x, h * 0.5f + 1.4f, farZ + (i % 2 == 0 ? 1.2f : -0.6f)),
                    new Vector3(3.8f, h, 3.6f), 0.12f, i % 2 == 0 ? wall : inset, false);
                CreateVisualBox(root, $"FarTowerMast_{i:00}", new Vector3(x, h + 2.4f, farZ),
                    new Vector3(0.16f, 3.0f, 0.16f), 0.020f, steel, false);
            }

            // Crane-like service gantry; visual language only, not a canonical machine specification.
            var gantryX = -width * 0.38f;
            CreateVisualBox(root, "FarGantryPost_A", new Vector3(gantryX - 4.2f, 4.0f, northZ + 16f),
                new Vector3(0.34f, 8.0f, 0.34f), 0.045f, steel, false);
            CreateVisualBox(root, "FarGantryPost_B", new Vector3(gantryX + 4.2f, 4.0f, northZ + 16f),
                new Vector3(0.34f, 8.0f, 0.34f), 0.045f, steel, false);
            CreateVisualBox(root, "FarGantryBeam", new Vector3(gantryX, 7.75f, northZ + 16f),
                new Vector3(9.0f, 0.34f, 0.45f), 0.045f, steel, false);
        }

        private static GameObject CreateVisualBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material,
            bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0x00ffffffu) / 16777215f;
            }
        }
    }
}
