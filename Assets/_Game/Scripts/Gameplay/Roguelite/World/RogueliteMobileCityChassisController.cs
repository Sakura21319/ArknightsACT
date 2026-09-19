using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Builds a visible mobile-city understructure below the playable deck. The assembly is visual-only:
    /// armor skirts, stepped service decks, structural frames, machinery pods and maintenance lighting
    /// fill the camera-facing south/west void without owning gameplay collision or navigation.
    /// </summary>
    [DefaultExecutionOrder(24)]
    [DisallowMultipleComponent]
    public sealed class RogueliteMobileCityChassisController : MonoBehaviour
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

            BuildChassis(stage.transform);
            _preparedStage = stage;
        }

        private void BuildChassis(Transform stage)
        {
            var old = stage.Find("[MobileCityChassis]");
            if (old != null)
                Destroy(old.gameObject);

            var root = RogueliteStageVisualRootUtility.GetOrCreate(stage, "[MobileCityChassis]", true);

            var width = Mathf.Max(ChunkWidth, stageMap.Width * ChunkWidth);
            var depth = Mathf.Max(ChunkDepth, stageMap.Height * ChunkDepth);

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;
            var light = kit.emissiveMaterial;

            // Three descending body masses give the deck real thickness from the gameplay camera.
            CreateBox(root, "UpperBelly", new Vector3(0f, -0.68f, 0f),
                new Vector3(width + 1.0f, 0.82f, depth + 1.0f), 0.11f, wall, true);
            CreateBox(root, "MidServiceHull", new Vector3(0f, -1.92f, 0f),
                new Vector3(width * 0.91f, 1.62f, depth * 0.89f), 0.17f, inset, true);
            CreateBox(root, "LowerCityBody", new Vector3(0f, -3.78f, 0f),
                new Vector3(width * 0.77f, 2.18f, depth * 0.70f), 0.22f, wall, true);
            CreateBox(root, "CentralServiceSpine", new Vector3(0f, -5.05f, 0f),
                new Vector3(width * 0.49f, 0.72f, depth * 0.40f), 0.14f, steel, true);

            BuildEdgeArmor(root, width, depth, wall, inset, steel, grate, light);
            BuildCameraFacingTerraces(root, width, depth, wall, inset, steel, grate, light);
            BuildStructuralGrid(root, width, depth, steel, inset);
            BuildMachineryPods(root, width, depth, wall, inset, steel, grate, light);
            BuildDriveHousings(root, width, depth, wall, inset, steel, grate, light);

            Debug.Log(
                $"[ArknightsACT/Chassis] Stage {stageMap.StageIndex}: deep mobile-city underdeck assembled ({width:0.#} x {depth:0.#}).",
                this);
        }

        private static void BuildEdgeArmor(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate,
            Material light)
        {
            const float skirtY = -1.10f;
            const float skirtH = 1.18f;
            const float skirtD = 0.52f;

            CreateBox(root, "SouthArmor", new Vector3(0f, skirtY, -depth * 0.5f - 0.28f),
                new Vector3(width + 0.7f, skirtH, skirtD), 0.07f, wall, true);
            CreateBox(root, "WestArmor", new Vector3(-width * 0.5f - 0.28f, skirtY, 0f),
                new Vector3(skirtD, skirtH, depth + 0.7f), 0.07f, wall, true);
            CreateBox(root, "NorthArmor", new Vector3(0f, skirtY, depth * 0.5f + 0.28f),
                new Vector3(width + 0.7f, skirtH, skirtD), 0.07f, inset, true);
            CreateBox(root, "EastArmor", new Vector3(width * 0.5f + 0.28f, skirtY, 0f),
                new Vector3(skirtD, skirtH, depth + 0.7f), 0.07f, inset, true);

            var southPanelCount = Mathf.Clamp(Mathf.RoundToInt(width / 5.4f), 4, 12);
            for (var i = 0; i < southPanelCount; i++)
            {
                var x = -width * 0.5f + width * (i + 0.5f) / southPanelCount;
                var panelW = Mathf.Max(1.35f, width / southPanelCount * 0.65f);
                CreateBox(root, $"SouthServiceInset_{i:00}", new Vector3(x, -1.12f, -depth * 0.5f - 0.555f),
                    new Vector3(panelW, 0.56f, 0.040f), 0.009f, grate, false);
                CreateBox(root, $"SouthServiceFrame_{i:00}", new Vector3(x, -1.46f, -depth * 0.5f - 0.570f),
                    new Vector3(panelW * 0.92f, 0.075f, 0.045f), 0.010f, steel, false);
                if (light != null && i % 3 == 1)
                    CreateBox(root, $"SouthServiceLight_{i:00}", new Vector3(x, -0.76f, -depth * 0.5f - 0.585f),
                        new Vector3(0.70f, 0.080f, 0.035f), 0.010f, light, false);
            }

            var westPanelCount = Mathf.Clamp(Mathf.RoundToInt(depth / 5.2f), 3, 10);
            for (var i = 0; i < westPanelCount; i++)
            {
                var z = -depth * 0.5f + depth * (i + 0.5f) / westPanelCount;
                var panelW = Mathf.Max(1.35f, depth / westPanelCount * 0.65f);
                CreateBox(root, $"WestServiceInset_{i:00}", new Vector3(-width * 0.5f - 0.555f, -1.12f, z),
                    new Vector3(0.040f, 0.56f, panelW), 0.009f, grate, false);
                if (light != null && i % 4 == 2)
                    CreateBox(root, $"WestServiceLight_{i:00}", new Vector3(-width * 0.5f - 0.585f, -0.78f, z),
                        new Vector3(0.035f, 0.075f, 0.62f), 0.010f, light, false);
            }

            CreateBox(root, "SouthLowerRail", new Vector3(0f, -1.70f, -depth * 0.5f - 0.22f),
                new Vector3(width * 0.97f, 0.24f, 0.28f), 0.05f, steel, true);
            CreateBox(root, "WestLowerRail", new Vector3(-width * 0.5f - 0.22f, -1.70f, 0f),
                new Vector3(0.28f, 0.24f, depth * 0.97f), 0.05f, steel, true);
        }

        private static void BuildCameraFacingTerraces(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate,
            Material light)
        {
            // Protrude actual structure toward the camera so the lower half of the frame is not black.
            CreateBox(root, "SouthServiceTerrace", new Vector3(0f, -2.58f, -depth * 0.5f - 1.28f),
                new Vector3(width * 0.82f, 0.62f, 2.30f), 0.12f, wall, true);
            CreateBox(root, "SouthTerraceInset", new Vector3(0f, -2.53f, -depth * 0.5f - 2.45f),
                new Vector3(width * 0.70f, 0.38f, 0.10f), 0.025f, grate, false);
            CreateBox(root, "SouthTerraceRail", new Vector3(0f, -2.12f, -depth * 0.5f - 2.30f),
                new Vector3(width * 0.76f, 0.18f, 0.20f), 0.035f, steel, true);

            CreateBox(root, "WestServiceTerrace", new Vector3(-width * 0.5f - 1.22f, -2.55f, 0f),
                new Vector3(2.18f, 0.60f, depth * 0.76f), 0.12f, wall, true);
            CreateBox(root, "WestTerraceInset", new Vector3(-width * 0.5f - 2.33f, -2.51f, 0f),
                new Vector3(0.10f, 0.36f, depth * 0.64f), 0.025f, grate, false);

            var towerCount = Mathf.Clamp(Mathf.RoundToInt(width / 10f), 3, 7);
            for (var i = 0; i < towerCount; i++)
            {
                var x = -width * 0.36f + width * 0.72f * (towerCount == 1 ? 0.5f : i / (float)(towerCount - 1));
                CreateBox(root, $"SouthSupportTower_{i:00}", new Vector3(x, -3.48f, -depth * 0.5f - 1.25f),
                    new Vector3(1.55f, 2.10f, 1.62f), 0.13f, i % 2 == 0 ? inset : wall, true);
                CreateBox(root, $"SouthSupportVent_{i:00}", new Vector3(x, -3.42f, -depth * 0.5f - 2.08f),
                    new Vector3(0.92f, 0.72f, 0.06f), 0.014f, grate, false);
                if (light != null && i % 2 == 1)
                    CreateBox(root, $"SouthTowerLight_{i:00}", new Vector3(x, -2.68f, -depth * 0.5f - 2.12f),
                        new Vector3(0.52f, 0.075f, 0.04f), 0.010f, light, false);
            }

            var westTowerCount = Mathf.Clamp(Mathf.RoundToInt(depth / 10f), 2, 5);
            for (var i = 0; i < westTowerCount; i++)
            {
                var z = -depth * 0.34f + depth * 0.68f * (westTowerCount == 1 ? 0.5f : i / (float)(westTowerCount - 1));
                CreateBox(root, $"WestSupportTower_{i:00}", new Vector3(-width * 0.5f - 1.18f, -3.46f, z),
                    new Vector3(1.55f, 2.05f, 1.52f), 0.13f, i % 2 == 0 ? wall : inset, true);
            }

            // Deep beams visually connect the protruding terraces back into the main city body.
            for (var i = 0; i < towerCount - 1; i++)
            {
                var x0 = -width * 0.36f + width * 0.72f * i / Mathf.Max(1, towerCount - 1);
                var x1 = -width * 0.36f + width * 0.72f * (i + 1) / Mathf.Max(1, towerCount - 1);
                CreateBeamBetween(root, $"TerraceBraceA_{i:00}",
                    new Vector3(x0, -2.25f, -depth * 0.5f - 0.55f),
                    new Vector3(x1, -4.40f, -depth * 0.36f), 0.20f, steel);
            }
        }

        private static void BuildStructuralGrid(Transform root, float width, float depth, Material steel, Material inset)
        {
            var xCount = Mathf.Clamp(Mathf.CeilToInt(width / 8.0f), 3, 9);
            for (var i = 0; i < xCount; i++)
            {
                var x = -width * 0.42f + width * 0.84f * (xCount == 1 ? 0.5f : i / (float)(xCount - 1));
                CreateBox(root, $"LongitudinalGirder_{i:00}", new Vector3(x, -2.38f, 0f),
                    new Vector3(0.36f, 0.52f, depth * 0.84f), 0.06f, steel, true);
            }

            var zCount = Mathf.Clamp(Mathf.CeilToInt(depth / 7.0f), 3, 8);
            for (var i = 0; i < zCount; i++)
            {
                var z = -depth * 0.40f + depth * 0.80f * (zCount == 1 ? 0.5f : i / (float)(zCount - 1));
                CreateBox(root, $"CrossGirder_{i:00}", new Vector3(0f, -2.30f, z),
                    new Vector3(width * 0.85f, 0.36f, 0.32f), 0.055f, steel, true);
            }

            var braceSegments = Mathf.Clamp(Mathf.RoundToInt(width / 8f), 3, 8);
            for (var i = 0; i < braceSegments; i++)
            {
                var x0 = -width * 0.42f + i * width * 0.84f / braceSegments;
                var x1 = -width * 0.42f + (i + 1) * width * 0.84f / braceSegments;
                CreateBeamBetween(root, $"SouthBraceA_{i:00}",
                    new Vector3(x0, -1.72f, -depth * 0.43f),
                    new Vector3(x1, -3.42f, -depth * 0.32f), 0.19f, steel);
                if (i % 2 == 0)
                    CreateBeamBetween(root, $"SouthBraceB_{i:00}",
                        new Vector3(x1, -1.72f, -depth * 0.43f),
                        new Vector3(x0, -3.42f, -depth * 0.32f), 0.17f, inset);
            }
        }

        private static void BuildMachineryPods(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate,
            Material light)
        {
            var pods = new[]
            {
                new Vector3(-width * 0.30f, -4.10f, -depth * 0.22f),
                new Vector3(width * 0.27f, -4.02f, -depth * 0.17f),
                new Vector3(-width * 0.23f, -4.08f, depth * 0.20f),
                new Vector3(width * 0.30f, -4.12f, depth * 0.18f)
            };

            for (var i = 0; i < pods.Length; i++)
            {
                var position = pods[i];
                var podWidth = i % 2 == 0 ? 4.6f : 3.7f;
                var podDepth = i % 2 == 0 ? 2.8f : 3.2f;
                CreateBox(root, $"MachineryPod_{i:00}", position,
                    new Vector3(podWidth, 1.35f, podDepth), 0.14f, wall, true);
                CreateBox(root, $"MachineryPodInset_{i:00}", position + new Vector3(0f, -0.04f, -podDepth * 0.5f - 0.03f),
                    new Vector3(podWidth * 0.64f, 0.56f, 0.040f), 0.009f, grate, false);
                CreateBox(root, $"MachineryPodRail_{i:00}", position + new Vector3(0f, 0.76f, 0f),
                    new Vector3(podWidth * 0.88f, 0.15f, podDepth * 0.84f), 0.04f, steel, true);

                if (light != null && i < 2)
                    CreateBox(root, $"MachineryPodLight_{i:00}", position + new Vector3(0f, 0.18f, -podDepth * 0.5f - 0.055f),
                        new Vector3(0.80f, 0.090f, 0.040f), 0.011f, light, false);
            }
        }

        private static void BuildDriveHousings(
            Transform root,
            float width,
            float depth,
            Material wall,
            Material inset,
            Material steel,
            Material grate,
            Material light)
        {
            var housings = new[]
            {
                new Vector3(-width * 0.31f, -5.20f, -depth * 0.30f),
                new Vector3(width * 0.31f, -5.18f, -depth * 0.30f),
                new Vector3(-width * 0.31f, -5.16f, depth * 0.28f),
                new Vector3(width * 0.31f, -5.22f, depth * 0.28f)
            };

            for (var i = 0; i < housings.Length; i++)
            {
                var p = housings[i];
                CreateBox(root, $"DriveHousing_{i:00}", p, new Vector3(4.8f, 1.45f, 3.8f), 0.18f,
                    i % 2 == 0 ? inset : wall, true);
                CreateBox(root, $"DriveHousingVent_{i:00}", p + new Vector3(0f, 0f, -1.93f),
                    new Vector3(2.65f, 0.74f, 0.055f), 0.012f, grate, false);
                CreateBox(root, $"DriveHousingBeam_{i:00}", p + new Vector3(0f, 0.86f, 0f),
                    new Vector3(4.10f, 0.18f, 3.15f), 0.04f, steel, true);
                if (light != null && i <= 1)
                    CreateBox(root, $"DriveHousingLight_{i:00}", p + new Vector3(0f, -0.42f, -1.98f),
                        new Vector3(0.92f, 0.095f, 0.040f), 0.010f, light, false);
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
                new Vector3(thickness, thickness, length), Mathf.Min(0.04f, thickness * 0.22f), material, true);
            go.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }
    }
}
