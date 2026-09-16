using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Adds a low-detail city wrap behind the authored distant district. This pass is intentionally
    /// broad: it seals the camera-right / north-east horizon so no gameplay camera angle can look
    /// through the finite geometry into an empty black background.
    /// </summary>
    [DefaultExecutionOrder(28)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageHorizonCityController : MonoBehaviour
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

            Build(stage.transform);
            ConfigureAtmosphere();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/HorizonCity] Stage {stageMap.StageIndex}: east/north-east city wrap built.", this);
        }

        private void Build(Transform stage)
        {
            var old = stage.Find("[Chernobog_HorizonCity]");
            if (old != null)
                Destroy(old.gameObject);

            var root = new GameObject("[Chernobog_HorizonCity]").transform;
            root.SetParent(stage, false);

            var width = Mathf.Max(ChunkWidth, stageMap.Width * ChunkWidth);
            var depth = Mathf.Max(ChunkDepth, stageMap.Height * ChunkDepth);
            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var dark = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : dark;

            // A broad lower terrace catches any downward-right sightline between nearer buildings.
            CreateVisualBox(root, "FarCityFoundation", new Vector3(13f, -4.8f, 13f),
                new Vector3(width + 82f, 6.5f, depth + 82f), 0.30f, dark);

            BuildEastWrap(root, width, depth, wall, dark, steel, grate);
            BuildNorthWrap(root, width, depth, wall, dark, steel, grate);
            BuildNorthEastCorner(root, width, depth, wall, dark, steel, grate);
        }

        private void BuildEastWrap(Transform root, float width, float depth, Material wall, Material dark, Material steel, Material grate)
        {
            var edgeX = width * 0.5f;
            var wrapX = edgeX + 22f;
            var span = depth + 58f;

            // The far mass is a visual safety net; foreground towers break it into believable city blocks.
            CreateVisualBox(root, "EastHorizonMass", new Vector3(wrapX + 9f, 4.6f, 0f),
                new Vector3(15f, 15.5f, span), 0.22f, dark);
            CreateVisualBox(root, "EastHorizonBase", new Vector3(edgeX + 12f, -1.7f, 0f),
                new Vector3(22f, 3.5f, span), 0.22f, dark);

            var count = Mathf.Clamp(stageMap.Height * 5 + 5, 12, 26);
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5f : i / (float)(count - 1);
                var z = Mathf.Lerp(-span * 0.44f, span * 0.44f, t);
                var seed = stageMap.StageIndex * 211 + i * 37;
                var x = edgeX + Mathf.Lerp(7.5f, 20.5f, Hash01(seed + 3));
                var h = Mathf.Lerp(5.0f, 13.5f, Hash01(seed + 7));
                var w = Mathf.Lerp(3.2f, 6.4f, Hash01(seed + 11));
                var d = Mathf.Lerp(3.0f, 6.8f, Hash01(seed + 19));
                var mat = i % 4 == 0 ? dark : wall;

                CreateVisualBox(root, $"EastHorizonBlock_{i:00}", new Vector3(x, h * 0.5f - 0.25f, z),
                    new Vector3(w, h, d), 0.14f, mat);
                CreateVisualBox(root, $"EastHorizonCap_{i:00}", new Vector3(x, h + 0.03f, z),
                    new Vector3(w * 1.05f, 0.20f, d * 1.04f), 0.04f, steel);

                if (i % 2 == 0)
                {
                    CreateVisualBox(root, $"EastHorizonVent_{i:00}", new Vector3(x - w * 0.5f - 0.035f, Mathf.Min(4.2f, h * 0.55f), z),
                        new Vector3(0.06f, 0.62f, d * 0.56f), 0.006f, grate);
                }
            }
        }

        private void BuildNorthWrap(Transform root, float width, float depth, Material wall, Material dark, Material steel, Material grate)
        {
            var edgeZ = depth * 0.5f;
            var wrapZ = edgeZ + 22f;
            var span = width + 62f;

            CreateVisualBox(root, "NorthHorizonMass", new Vector3(0f, 5.0f, wrapZ + 8f),
                new Vector3(span, 16.5f, 15f), 0.22f, dark);
            CreateVisualBox(root, "NorthHorizonBase", new Vector3(0f, -1.8f, edgeZ + 12f),
                new Vector3(span, 3.6f, 22f), 0.22f, dark);

            var count = Mathf.Clamp(stageMap.Width * 5 + 5, 12, 28);
            for (var i = 0; i < count; i++)
            {
                var t = count == 1 ? 0.5f : i / (float)(count - 1);
                var x = Mathf.Lerp(-span * 0.44f, span * 0.44f, t);
                var seed = stageMap.StageIndex * 271 + i * 43;
                var z = edgeZ + Mathf.Lerp(7.5f, 20.0f, Hash01(seed + 5));
                var h = Mathf.Lerp(5.2f, 14.5f, Hash01(seed + 13));
                var w = Mathf.Lerp(3.4f, 7.0f, Hash01(seed + 17));
                var d = Mathf.Lerp(3.0f, 6.2f, Hash01(seed + 23));
                var mat = i % 5 == 1 ? dark : wall;

                CreateVisualBox(root, $"NorthHorizonBlock_{i:00}", new Vector3(x, h * 0.5f - 0.25f, z),
                    new Vector3(w, h, d), 0.14f, mat);
                CreateVisualBox(root, $"NorthHorizonCap_{i:00}", new Vector3(x, h + 0.03f, z),
                    new Vector3(w * 1.05f, 0.20f, d * 1.04f), 0.04f, steel);

                if (i % 3 == 0)
                {
                    CreateVisualBox(root, $"NorthMast_{i:00}", new Vector3(x, h + 1.35f, z),
                        new Vector3(0.12f, 2.6f, 0.12f), 0.018f, steel);
                }
            }
        }

        private void BuildNorthEastCorner(Transform root, float width, float depth, Material wall, Material dark, Material steel, Material grate)
        {
            var corner = new Vector3(width * 0.5f + 20f, 0f, depth * 0.5f + 20f);
            CreateVisualBox(root, "NorthEastMegastructure", corner + new Vector3(0f, 8.0f, 0f),
                new Vector3(18f, 16f, 18f), 0.25f, wall);
            CreateVisualBox(root, "NorthEastInset", corner + new Vector3(-9.05f, 7.6f, -1.0f),
                new Vector3(0.08f, 4.2f, 11.0f), 0.008f, grate);
            CreateVisualBox(root, "NorthEastRoof", corner + new Vector3(0f, 16.25f, 0f),
                new Vector3(18.8f, 0.34f, 18.8f), 0.07f, steel);
            CreateVisualBox(root, "NorthEastTower", corner + new Vector3(3.5f, 21.0f, 2.0f),
                new Vector3(4.6f, 9.2f, 4.6f), 0.14f, dark);
            CreateVisualBox(root, "NorthEastAntenna", corner + new Vector3(3.5f, 27.0f, 2.0f),
                new Vector3(0.18f, 4.0f, 0.18f), 0.022f, steel);
        }

        private static void ConfigureAtmosphere()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.050f, 0.060f, 0.078f, 1f);
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.075f, 0.083f, 0.098f, 1f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 145f;
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
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.18f));
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
