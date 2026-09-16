using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Gives the generated battlefield a believable mobile-city underside instead of a flat deck over
    /// pure black. The assembly is visual-only: armored belly plates, service spine, deep structural
    /// beams, machinery pods and sparse maintenance lights sit below the playable collision surface.
    /// </summary>
    [DefaultExecutionOrder(24)]
    [DisallowMultipleComponent]
    public sealed class RogueliteMobileCityChassisController : MonoBehaviour
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
            _nextResolveAt = Time.unscaledTime + 0.18f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
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

            var root = new GameObject("[MobileCityChassis]").transform;
            root.SetParent(stage, false);

            var width = Mathf.Max(ChunkWidth, stageMap.Width * ChunkWidth);
            var depth = Mathf.Max(ChunkDepth, stageMap.Height * ChunkDepth);

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;
            var light = kit.emissiveMaterial;

            // Deck belly: shallow visible armor immediately beneath the playable slab, then a narrower
            // deep hull so the platform reads as a large machine rather than a paper-thin floating plane.
            CreateBox(root, "UpperBelly", new Vector3(0f, -0.63f, 0f),
                new Vector3(width + 0.9f, 0.72f, depth + 0.9f), 0.10f, wall, true);
            CreateBox(root, "LowerServiceHull", new Vector3(0f, -1.75f, 0f),
                new Vector3(width * 0.88f, 1.45f, depth * 0.86f), 0.16f, inset, true);
            CreateBox(root, "CentralServiceSpine", new Vector3(0f, -2.70f, 0f),
                new Vector3(width * 0.56f, 0.72f, depth * 0.46f), 0.14f, wall, true);

            BuildEdgeArmor(root, width, depth, wall, inset, steel, grate, light);
            BuildStructuralGrid(root, width, depth, steel, inset);
            BuildMachineryPods(root, width, depth, wall, inset, steel, grate, light);

            Debug.Log(
                $"[ArknightsACT/Chassis] Stage {stageMap.StageIndex}: mobile-city underdeck assembled ({width:0.#} x {depth:0.#}).",
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
            const float skirtY = -1.05f;
            const float skirtH = 1.10f;
            const float skirtD = 0.48f;

            CreateBox(root, "SouthArmor", new Vector3(0f, skirtY, -depth * 0.5f - 0.25f),
                new Vector3(width + 0.6f, skirtH, skirtD), 0.065f, wall, true);
            CreateBox(root, "WestArmor", new Vector3(-width * 0.5f - 0.25f, skirtY, 0f),
                new Vector3(skirtD, skirtH, depth + 0.6f), 0.065f, wall, true);
            CreateBox(root, "NorthArmor", new Vector3(0f, skirtY, depth * 0.5f + 0.25f),
                new Vector3(width + 0.6f, skirtH, skirtD), 0.065f, inset, true);
            CreateBox(root, "EastArmor", new Vector3(width * 0.5f + 0.25f, skirtY, 0f),
                new Vector3(skirtD, skirtH, depth + 0.6f), 0.065f, inset, true);

            // Camera-facing recessed service panels break the formerly solid black edge silhouette.
            var southPanelCount = Mathf.Clamp(Mathf.RoundToInt(width / 5.8f), 4, 10);
            for (var i = 0; i < southPanelCount; i++)
            {
                var x = -width * 0.5f + width * (i + 0.5f) / southPanelCount;
                CreateBox(root, $"SouthServiceInset_{i:00}", new Vector3(x, -1.08f, -depth * 0.5f - 0.505f),
                    new Vector3(Mathf.Max(1.4f, width / southPanelCount * 0.62f), 0.52f, 0.035f), 0.008f, grate, false);
                if (light != null && i % 3 == 1)
                    CreateBox(root, $"SouthServiceLight_{i:00}", new Vector3(x, -0.72f, -depth * 0.5f - 0.535f),
                        new Vector3(0.62f, 0.075f, 0.030f), 0.010f, light, false);
            }

            var westPanelCount = Mathf.Clamp(Mathf.RoundToInt(depth / 5.5f), 3, 8);
            for (var i = 0; i < westPanelCount; i++)
            {
                var z = -depth * 0.5f + depth * (i + 0.5f) / westPanelCount;
                CreateBox(root, $"WestServiceInset_{i:00}", new Vector3(-width * 0.5f - 0.505f, -1.08f, z),
                    new Vector3(0.035f, 0.52f, Mathf.Max(1.4f, depth / westPanelCount * 0.62f)), 0.008f, grate, false);
            }

            // Exposed edge rails catch the directional/rim light and give the deck thickness.
            CreateBox(root, "SouthLowerRail", new Vector3(0f, -1.58f, -depth * 0.5f - 0.18f),
                new Vector3(width * 0.96f, 0.22f, 0.26f), 0.045f, steel, true);
            CreateBox(root, "WestLowerRail", new Vector3(-width * 0.5f - 0.18f, -1.58f, 0f),
                new Vector3(0.26f, 0.22f, depth * 0.96f), 0.045f, steel, true);
        }

        private static void BuildStructuralGrid(Transform root, float width, float depth, Material steel, Material inset)
        {
            var xCount = Mathf.Clamp(Mathf.CeilToInt(width / 8.0f), 3, 8);
            for (var i = 0; i < xCount; i++)
            {
                var x = -width * 0.42f + width * 0.84f * (xCount == 1 ? 0.5f : i / (float)(xCount - 1));
                CreateBox(root, $"LongitudinalGirder_{i:00}", new Vector3(x, -2.18f, 0f),
                    new Vector3(0.34f, 0.46f, depth * 0.82f), 0.055f, steel, true);
            }

            var zCount = Mathf.Clamp(Mathf.CeilToInt(depth / 7.0f), 3, 7);
            for (var i = 0; i < zCount; i++)
            {
                var z = -depth * 0.40f + depth * 0.80f * (zCount == 1 ? 0.5f : i / (float)(zCount - 1));
                CreateBox(root, $"CrossGirder_{i:00}", new Vector3(0f, -2.10f, z),
                    new Vector3(width * 0.84f, 0.34f, 0.30f), 0.050f, steel, true);
            }

            // A few diagonal braces are most visible on the south/west camera-facing underside.
            var braceSegments = Mathf.Clamp(Mathf.RoundToInt(width / 8f), 3, 7);
            for (var i = 0; i < braceSegments; i++)
            {
                var x0 = -width * 0.42f + i * width * 0.84f / braceSegments;
                var x1 = -width * 0.42f + (i + 1) * width * 0.84f / braceSegments;
                CreateBeamBetween(root, $"SouthBraceA_{i:00}",
                    new Vector3(x0, -1.62f, -depth * 0.43f),
                    new Vector3(x1, -2.92f, -depth * 0.34f), 0.18f, steel);
                if (i % 2 == 0)
                    CreateBeamBetween(root, $"SouthBraceB_{i:00}",
                        new Vector3(x1, -1.62f, -depth * 0.43f),
                        new Vector3(x0, -2.92f, -depth * 0.34f), 0.16f, inset);
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
                new Vector3(-width * 0.30f, -2.72f, -depth * 0.24f),
                new Vector3(width * 0.27f, -2.68f, -depth * 0.18f),
                new Vector3(-width * 0.24f, -2.70f, depth * 0.22f),
                new Vector3(width * 0.31f, -2.72f, depth * 0.18f)
            };

            for (var i = 0; i < pods.Length; i++)
            {
                var position = pods[i];
                var podWidth = i % 2 == 0 ? 4.2f : 3.4f;
                var podDepth = i % 2 == 0 ? 2.6f : 3.0f;
                CreateBox(root, $"MachineryPod_{i:00}", position,
                    new Vector3(podWidth, 1.25f, podDepth), 0.12f, wall, true);
                CreateBox(root, $"MachineryPodInset_{i:00}", position + new Vector3(0f, -0.05f, -podDepth * 0.5f - 0.025f),
                    new Vector3(podWidth * 0.62f, 0.52f, 0.035f), 0.008f, grate, false);
                CreateBox(root, $"MachineryPodRail_{i:00}", position + new Vector3(0f, 0.70f, 0f),
                    new Vector3(podWidth * 0.86f, 0.14f, podDepth * 0.82f), 0.035f, steel, true);

                if (light != null && i < 2)
                    CreateBox(root, $"MachineryPodLight_{i:00}", position + new Vector3(0f, 0.16f, -podDepth * 0.5f - 0.050f),
                        new Vector3(0.72f, 0.085f, 0.035f), 0.010f, light, false);
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
