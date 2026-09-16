using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Compile-safe authored lighting rig for the generated Chernobog deck.
    /// Uses only UnityEngine Light / RenderSettings so it works without a direct URP package API dependency.
    /// The rig is rebuilt per generated stage and never touches gameplay colliders/navigation.
    /// </summary>
    [DefaultExecutionOrder(22)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageLightingController : MonoBehaviour
    {
        private const float ChunkWidth = 14f;
        private const float ChunkDepth = 11f;

        [SerializeField] private RogueliteStageMapController stageMap;

        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map)
        {
            stageMap = map;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.15f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            ConfigureEnvironment();
            ConfigureMainKey();
            DisableLegacyPracticalLights(stage.transform);
            BuildStageRig(stage.transform);
            ConfigureCamera();

            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/Lighting] Stage {stageMap.StageIndex}: Chernobog key/fill/practical lighting rig applied.", this);
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.185f, 0.220f, 0.285f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.092f, 0.112f, 0.150f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.041f, 0.052f, 1f);
            RenderSettings.reflectionIntensity = 0.62f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.035f, 0.043f, 0.056f, 1f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 68f;
        }

        private static void ConfigureMainKey()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light key = null;
            for (var i = 0; i < lights.Length; i++)
            {
                var candidate = lights[i];
                if (candidate == null || candidate.type != LightType.Directional)
                    continue;
                key = candidate;
                break;
            }

            if (key == null)
            {
                var go = new GameObject("Chernobog_KeyDirectional");
                key = go.AddComponent<Light>();
                key.type = LightType.Directional;
            }

            key.name = "Chernobog_KeyDirectional";
            key.color = new Color(0.72f, 0.82f, 1.0f, 1f);
            key.intensity = 1.16f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.86f;
            key.shadowBias = 0.045f;
            key.shadowNormalBias = 0.32f;
            key.shadowNearPlane = 0.18f;
            key.renderMode = LightRenderMode.ForcePixel;
            key.transform.rotation = Quaternion.Euler(48f, -36f, 0f);
        }

        private static void DisableLegacyPracticalLights(Transform stage)
        {
            var legacy = stage.Find("[Concept01_QualityLights]");
            if (legacy != null)
                legacy.gameObject.SetActive(false);

            var oldRig = stage.Find("[ChernobogLightingRig]");
            if (oldRig != null)
                Destroy(oldRig.gameObject);
        }

        private void BuildStageRig(Transform stage)
        {
            if (stageMap == null)
                return;

            var width = Mathf.Max(ChunkWidth, stageMap.Width * ChunkWidth);
            var depth = Mathf.Max(ChunkDepth, stageMap.Height * ChunkDepth);
            var diagonal = Mathf.Sqrt(width * width + depth * depth);

            var root = new GameObject("[ChernobogLightingRig]").transform;
            root.SetParent(stage, false);

            // Large, soft-feeling cool fill from the south-west camera side. It does not cast shadows;
            // the directional key remains the only expensive shadow owner.
            CreateSpot(
                root,
                "CoolDeckFill",
                new Vector3(-width * 0.34f, 9.5f, -depth * 0.28f),
                new Vector3(width * 0.02f, 0.2f, depth * 0.03f),
                new Color(0.42f, 0.57f, 0.82f, 1f),
                Mathf.Clamp(diagonal * 0.70f, 18f, 42f),
                78f,
                0.62f,
                false);

            // Narrower cool rim from the far industrial side. This catches HVAC / wall top edges and
            // helps the metal normal/roughness maps read without flattening the whole deck.
            CreateSpot(
                root,
                "CoolIndustrialRim",
                new Vector3(width * 0.34f, 8.4f, depth * 0.42f),
                new Vector3(width * 0.04f, 0.65f, depth * 0.10f),
                new Color(0.36f, 0.49f, 0.72f, 1f),
                Mathf.Clamp(diagonal * 0.58f, 16f, 34f),
                64f,
                0.48f,
                false);

            // Warm industrial practicals live near the north/east architecture so the orange accent
            // is expressed by light pools rather than painted markings across the floor.
            CreateSpot(
                root,
                "WarmNorthServiceA",
                new Vector3(-width * 0.24f, 4.2f, depth * 0.47f),
                new Vector3(-width * 0.18f, 0.1f, depth * 0.24f),
                new Color(1.0f, 0.48f, 0.16f, 1f),
                10.0f,
                52f,
                1.45f,
                false);

            CreateSpot(
                root,
                "WarmNorthServiceB",
                new Vector3(width * 0.18f, 4.0f, depth * 0.47f),
                new Vector3(width * 0.14f, 0.1f, depth * 0.22f),
                new Color(1.0f, 0.44f, 0.13f, 1f),
                9.0f,
                48f,
                1.25f,
                false);

            CreateSpot(
                root,
                "WarmEastService",
                new Vector3(width * 0.47f, 3.8f, depth * 0.08f),
                new Vector3(width * 0.24f, 0.1f, depth * 0.05f),
                new Color(1.0f, 0.46f, 0.14f, 1f),
                8.5f,
                50f,
                1.18f,
                false);

            // A few low-energy point lights provide small specular kicks near infrastructure but stay
            // deliberately local so the stage does not become evenly lit again.
            CreatePoint(root, "WarmPractical_North", new Vector3(0f, 1.15f, depth * 0.47f), 4.8f, 0.72f);
            CreatePoint(root, "WarmPractical_East", new Vector3(width * 0.47f, 1.05f, -depth * 0.10f), 4.2f, 0.62f);
        }

        private static void CreateSpot(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 targetLocalPosition,
            Color color,
            float range,
            float spotAngle,
            float intensity,
            bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var direction = targetLocalPosition - localPosition;
            if (direction.sqrMagnitude > 0.001f)
                go.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.range = range;
            light.spotAngle = spotAngle;
            light.intensity = intensity;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static void CreatePoint(Transform parent, string name, Vector3 localPosition, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.46f, 0.15f, 1f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            camera.allowHDR = true;
            camera.allowMSAA = true;
        }
    }
}
