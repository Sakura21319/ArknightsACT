using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Extends the camera-facing mobile-city chassis farther downward so the lower half of the frame
    /// reads as a layered city machine instead of fading immediately into empty black. This pass is
    /// visual-only and deliberately avoids claiming a specific canonical Chernobog locomotion design.
    /// </summary>
    [DefaultExecutionOrder(25)]
    [DisallowMultipleComponent]
    public sealed class RogueliteMobileCityDeepBaseController : MonoBehaviour
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

            // Wait until the upper chassis exists. This prevents the two passes from visually racing
            // when a stage is regenerated during a run.
            if (stage.transform.Find("[MobileCityChassis]") == null)
                return;

            BuildDeepBase(stage.transform);
            _preparedStage = stage;
        }

        private void BuildDeepBase(Transform stage)
        {
            var old = stage.Find("[MobileCityDeepBase]");
            if (old != null)
                Destroy(old.gameObject);

            var root = RogueliteStageVisualRootUtility.GetOrCreate(stage, "[MobileCityDeepBase]", true);

            var width = Mathf.Max(ChunkWidth, stageMap.Width * ChunkWidth);
            var depth = Mathf.Max(ChunkDepth, stageMap.Height * ChunkDepth);

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;
            var light = kit.emissiveMaterial;

            // Large stepped masses continue below the Phase-08 chassis instead of ending around y=-5.
            CreateBox(root, "LowerCarrierBody", new Vector3(0f, -6.55f, 0f),
                new Vector3(width * 0.90f, 2.25f, depth * 0.80f), 0.24f, inset, true);
            CreateBox(root, "LowerCarrierArmor", new Vector3(0f, -6.28f, -depth * 0.33f),
                new Vector3(width * 0.82f, 1.18f, depth * 0.18f), 0.16f, wall, true);
            CreateBox(root, "DeepFoundationBody", new Vector3(0f, -8.35f, 0f),
                new Vector3(width * 0.73f, 1.70f, depth * 0.62f), 0.24f, wall, true);
            CreateBox(root, "FoundationKeel", new Vector3(0f, -9.70f, 0f),
                new Vector3(width * 0.52f, 1.25f, depth * 0.42f), 0.20f, inset, true);
            CreateBox(root, "DeepCoreBlock", new Vector3(0f, -10.72f, 0f),
                new Vector3(width * 0.36f, 0.92f, depth * 0.30f), 0.16f, steel, true);

            BuildCameraFacingLowerAprons(root, width, depth, wall, inset, steel, grate, light);
            BuildVerticalShafts(root, width, depth, wall, inset, steel, grate, light);
            BuildLowerServiceBays(root, width, depth, wall, inset, steel, grate, light);
            BuildDeepBracing(root, width, depth, steel, inset);

            Debug.Log($"[ArknightsACT/DeepBase] Stage {stageMap.StageIndex}: lower mobile-city foundation extended to y=-11.", this);
        }

        private static void BuildCameraFacingLowerAprons(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate,
            Material light)
        {
            // These masses deliberately protrude south/west toward the gameplay camera. The previous
            // chassis was present but mostly hidden beneath the deck projection.
            CreateBox(root, "SouthLowerApron", new Vector3(0f, -6.10f, -depth * 0.5f - 3.65f),
                new Vector3(width * 0.84f, 1.20f, 5.35f), 0.18f, wall, true);
            CreateBox(root, "SouthLowerInsetBand", new Vector3(0f, -6.18f, -depth * 0.5f - 6.36f),
                new Vector3(width * 0.72f, 0.64f, 0.10f), 0.020f, grate, false);
            CreateBox(root, "SouthLowerRail", new Vector3(0f, -5.36f, -depth * 0.5f - 6.12f),
                new Vector3(width * 0.79f, 0.22f, 0.24f), 0.045f, steel, true);

            CreateBox(root, "WestLowerApron", new Vector3(-width * 0.5f - 3.50f, -6.05f, 0f),
                new Vector3(5.05f, 1.18f, depth * 0.74f), 0.18f, wall, true);
            CreateBox(root, "WestLowerInsetBand", new Vector3(-width * 0.5f - 6.06f, -6.12f, 0f),
                new Vector3(0.10f, 0.62f, depth * 0.63f), 0.020f, grate, false);

            // A second lower ledge gives a visible silhouette break instead of one giant wall face.
            CreateBox(root, "SouthFoundationLedge", new Vector3(0f, -8.22f, -depth * 0.5f - 2.55f),
                new Vector3(width * 0.68f, 0.72f, 3.25f), 0.14f, inset, true);
            CreateBox(root, "WestFoundationLedge", new Vector3(-width * 0.5f - 2.45f, -8.18f, 0f),
                new Vector3(3.10f, 0.70f, depth * 0.60f), 0.14f, inset, true);

            if (light != null)
            {
                var lightCount = Mathf.Clamp(Mathf.RoundToInt(width / 11f), 3, 7);
                for (var i = 0; i < lightCount; i++)
                {
                    var x = -width * 0.31f + width * 0.62f * (lightCount == 1 ? 0.5f : i / (float)(lightCount - 1));
                    CreateBox(root, $"SouthLowerLight_{i:00}", new Vector3(x, -5.55f, -depth * 0.5f - 6.43f),
                        new Vector3(0.78f, 0.085f, 0.045f), 0.010f, light, false);
                }
            }
        }

        private static void BuildVerticalShafts(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate,
            Material light)
        {
            var count = Mathf.Clamp(Mathf.RoundToInt(width / 9f), 3, 8);
            for (var i = 0; i < count; i++)
            {
                var x = -width * 0.36f + width * 0.72f * (count == 1 ? 0.5f : i / (float)(count - 1));
                var material = i % 2 == 0 ? inset : wall;
                CreateBox(root, $"LowerSupportShaft_{i:00}", new Vector3(x, -7.46f, -depth * 0.5f - 3.18f),
                    new Vector3(1.75f, 3.05f, 1.95f), 0.16f, material, true);
                CreateBox(root, $"LowerSupportVent_{i:00}", new Vector3(x, -7.42f, -depth * 0.5f - 4.18f),
                    new Vector3(1.06f, 1.18f, 0.06f), 0.014f, grate, false);
                CreateBox(root, $"LowerSupportCap_{i:00}", new Vector3(x, -5.83f, -depth * 0.5f - 3.18f),
                    new Vector3(2.02f, 0.22f, 2.16f), 0.045f, steel, true);

                if (light != null && i % 3 == 1)
                    CreateBox(root, $"LowerSupportLight_{i:00}", new Vector3(x, -6.52f, -depth * 0.5f - 4.23f),
                        new Vector3(0.62f, 0.08f, 0.04f), 0.010f, light, false);
            }
        }

        private static void BuildLowerServiceBays(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate,
            Material light)
        {
            var bayCount = Mathf.Clamp(Mathf.RoundToInt(width / 7.5f), 4, 9);
            for (var i = 0; i < bayCount; i++)
            {
                var x = -width * 0.39f + width * 0.78f * (bayCount == 1 ? 0.5f : i / (float)(bayCount - 1));
                var y = i % 2 == 0 ? -8.58f : -8.78f;
                CreateBox(root, $"DeepServiceBay_{i:00}", new Vector3(x, y, -depth * 0.5f - 0.95f),
                    new Vector3(3.15f, 1.38f, 2.05f), 0.14f, i % 2 == 0 ? wall : inset, true);
                CreateBox(root, $"DeepServiceBayVent_{i:00}", new Vector3(x, y, -depth * 0.5f - 2.01f),
                    new Vector3(1.72f, 0.70f, 0.06f), 0.012f, grate, false);
                CreateBox(root, $"DeepServiceBayTop_{i:00}", new Vector3(x, y + 0.82f, -depth * 0.5f - 0.95f),
                    new Vector3(2.70f, 0.18f, 1.68f), 0.040f, steel, true);

                if (light != null && i % 3 == 0)
                    CreateBox(root, $"DeepServiceBayLight_{i:00}", new Vector3(x, y - 0.32f, -depth * 0.5f - 2.06f),
                        new Vector3(0.74f, 0.085f, 0.040f), 0.010f, light, false);
            }
        }

        private static void BuildDeepBracing(Transform root, float width, float depth, Material steel, Material inset)
        {
            var count = Mathf.Clamp(Mathf.RoundToInt(width / 9f), 3, 7);
            for (var i = 0; i < count; i++)
            {
                var x0 = -width * 0.36f + width * 0.72f * i / count;
                var x1 = -width * 0.36f + width * 0.72f * (i + 1) / count;
                CreateBeamBetween(root, $"DeepBraceA_{i:00}",
                    new Vector3(x0, -5.35f, -depth * 0.5f - 1.55f),
                    new Vector3(x1, -9.05f, -depth * 0.30f), 0.22f, steel);
                if (i % 2 == 0)
                    CreateBeamBetween(root, $"DeepBraceB_{i:00}",
                        new Vector3(x1, -5.48f, -depth * 0.5f - 1.48f),
                        new Vector3(x0, -8.88f, -depth * 0.31f), 0.18f, inset);
            }
        }

        private static GameObject CreateBox(
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
            renderer.receiveShadows = true;
            return go;
        }

        private static void CreateBeamBetween(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float thickness,
            Material material)
        {
            var delta = end - start;
            var length = delta.magnitude;
            if (length <= 0.01f)
                return;

            var go = CreateBox(parent, name, (start + end) * 0.5f,
                new Vector3(thickness, thickness, length), Mathf.Min(0.045f, thickness * 0.22f), material, true);
            go.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }
    }
}
