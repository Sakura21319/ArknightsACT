using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Builds the distant industrial skyline that frames the playable town.
    ///
    /// This controller owns only non-playable north-side silhouette elements. The playable town,
    /// mobile-city underdeck and horizon terraces remain owned by their dedicated controllers so
    /// one pass cannot accidentally cover or replace another pass.
    /// </summary>
    [DefaultExecutionOrder(28)]
    [DisallowMultipleComponent]
    public sealed class ChernobogCityBackdropController : MonoBehaviour
    {
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;
        private const float ResolveIntervalSeconds = 0.18f;

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
            if (_context == null)
                return;

            stageMap ??= _context.StageMap;
            kit ??= _context.EnvironmentKit;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + ResolveIntervalSeconds;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            kit ??= _context.EnvironmentKit;
            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null || stage == _preparedStage)
                return;

            Build(stage.transform);
            _preparedStage = stage;
            Debug.Log(
                $"[ArknightsACT/CityBackdrop] Stage {stageMap.StageIndex}: distant industrial skyline assembled.",
                this);
        }

        private void Build(Transform stage)
        {
            var root = RogueliteStageVisualRootUtility.GetOrCreate(
                stage,
                "[Chernobog_City_Backdrop]",
                true);

            var width = Mathf.Max(ChunkWidth, stageMap.Width * ChunkWidth);
            var depth = Mathf.Max(ChunkDepth, stageMap.Height * ChunkDepth);
            var northEdge = depth * 0.5f;
            var skylineZ = northEdge + 28f;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            BuildSkylineTowers(root, width, skylineZ, wall, inset, steel, grate);
            BuildSkyBridge(root, width, northEdge, steel, grate);
            BuildEnergyPipeRack(root, width, northEdge, steel, inset);
        }

        private void BuildSkylineTowers(
            Transform root,
            float width,
            float skylineZ,
            Material wall,
            Material inset,
            Material steel,
            Material grate)
        {
            var towerCount = Mathf.Clamp(Mathf.RoundToInt(width / 12f), 4, 8);
            for (var i = 0; i < towerCount; i++)
            {
                var t = towerCount == 1 ? 0.5f : i / (float)(towerCount - 1);
                var seed = stageMap.StageIndex * 131 + i * 47;
                var x = Mathf.Lerp(-width * 0.46f, width * 0.46f, t);
                var z = skylineZ + Mathf.Lerp(-3.5f, 4.5f, Hash01(seed + 3));
                var height = ChernobogCityScaleProfile.GetHeight(ChernobogDistrictType.Industrial, seed);
                if (i == towerCount / 2)
                    height = Mathf.Min(ChernobogCityScaleProfile.HorizonBuildingHeight * 0.52f, height + 8f);

                var towerWidth = Mathf.Lerp(4.2f, 8.4f, Hash01(seed + 7));
                var towerDepth = Mathf.Lerp(4.0f, 7.2f, Hash01(seed + 11));
                var material = i % 3 == 0 ? inset : wall;

                CreateVisualBox(root, $"IndustrialTower_{i:00}",
                    new Vector3(x, height * 0.5f - 0.55f, z),
                    new Vector3(towerWidth, height, towerDepth), 0.18f, material);
                CreateVisualBox(root, $"IndustrialTowerCap_{i:00}",
                    new Vector3(x, height + 0.03f, z),
                    new Vector3(towerWidth * 1.08f, 0.28f, towerDepth * 1.06f), 0.06f, steel);

                var faceZ = z - towerDepth * 0.5f - 0.04f;
                CreateVisualBox(root, $"IndustrialTowerInset_{i:00}",
                    new Vector3(x, Mathf.Min(height * 0.52f, 8.4f), faceZ),
                    new Vector3(towerWidth * 0.58f, Mathf.Min(height * 0.45f, 6.6f), 0.07f),
                    0.012f, grate);

                if (i % 2 == 0)
                {
                    CreateVisualBox(root, $"IndustrialTowerMast_{i:00}",
                        new Vector3(x, height + 2.0f, z),
                        new Vector3(0.16f, 4.0f, 0.16f), 0.022f, steel);
                    CreateVisualBox(root, $"IndustrialTowerMastCap_{i:00}",
                        new Vector3(x, height + 4.05f, z),
                        new Vector3(0.48f, 0.12f, 0.48f), 0.022f, inset);
                }
            }
        }

        private static void BuildSkyBridge(
            Transform root,
            float width,
            float northEdge,
            Material steel,
            Material grate)
        {
            var bridgeZ = northEdge + 15.5f;
            var bridgeWidth = Mathf.Max(18f, width * 1.12f);
            const float bridgeY = 10.5f;
            const float postHeight = 10.5f;

            CreateVisualBox(root, "SkyBridgeDeck", new Vector3(0f, bridgeY, bridgeZ),
                new Vector3(bridgeWidth, 0.52f, 1.30f), 0.08f, steel);
            CreateVisualBox(root, "SkyBridgeServiceBand", new Vector3(0f, bridgeY - 0.38f, bridgeZ - 0.58f),
                new Vector3(bridgeWidth * 0.92f, 0.22f, 0.08f), 0.018f, grate);

            var postX = bridgeWidth * 0.43f;
            CreateVisualBox(root, "SkyBridgePost_W", new Vector3(-postX, postHeight * 0.5f, bridgeZ),
                new Vector3(0.48f, postHeight, 0.62f), 0.06f, steel);
            CreateVisualBox(root, "SkyBridgePost_E", new Vector3(postX, postHeight * 0.5f, bridgeZ),
                new Vector3(0.48f, postHeight, 0.62f), 0.06f, steel);
        }

        private static void BuildEnergyPipeRack(
            Transform root,
            float width,
            float northEdge,
            Material steel,
            Material inset)
        {
            var rackZ = northEdge + 8.5f;
            var rackWidth = Mathf.Max(16f, width * 0.82f);
            var rackY = 5.3f;

            for (var i = 0; i < 3; i++)
            {
                CreateVisualBox(root, $"EnergyPipe_{i:00}",
                    new Vector3(0f, rackY + i * 0.34f, rackZ),
                    new Vector3(rackWidth, 0.16f, 0.16f), 0.025f, i == 1 ? inset : steel);
            }

            var supportCount = Mathf.Clamp(Mathf.RoundToInt(rackWidth / 12f), 2, 6);
            for (var i = 0; i < supportCount; i++)
            {
                var x = Mathf.Lerp(-rackWidth * 0.43f, rackWidth * 0.43f, supportCount == 1 ? 0.5f : i / (float)(supportCount - 1));
                CreateVisualBox(root, $"EnergyPipeSupport_{i:00}",
                    new Vector3(x, rackY * 0.5f, rackZ),
                    new Vector3(0.20f, rackY, 0.20f), 0.030f, steel);
            }
        }

        private static GameObject CreateVisualBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
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
    }
}
