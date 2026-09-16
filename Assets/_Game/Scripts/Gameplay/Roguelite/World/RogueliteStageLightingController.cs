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
        private const float ChunkWidth = 18f;
        private const float ChunkDepth = 14f;

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
            Debug.Log($"[ArknightsACT/Lighting] Stage {stageMap.StageIndex}: expanded deck + underside lighting rig applied.", this);
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.255f, 0.300f, 0.385f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.145f, 0.170f, 0.225f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.060f, 0.070f, 0.090f, 1f);
            RenderSettings.reflectionIntensity = 0.78f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.052f, 0.063f, 0.082f, 1f);
            RenderSettings.fogStartDistance = 38f;
            RenderSettings.fogEndDistance = 96f;
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
            key.color = new Color(0.77f, 0.86f, 1.0f, 1f);
            key.intensity = 1.30f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.72f;
            key.shadowBias = 0.045f;
            key.shadowNormalBias = 0.30f;
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
                UnityEngine.Object.Destroy(oldRig.gameObject);
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

            CreateSpot(
                root,
                "CoolDeckFill",
                new Vector3(-width * 0.34f, 11.2f, -depth * 0.28f),
                new Vector3(width * 0.02f, 0.2f, depth * 0.03f),
                new Color(0.50f, 0.65f, 0.90f, 1f),
                Mathf.Clamp(diagonal * 0.82f, 24f, 58f),
                86f,
                0.92f,
                false);

            CreateSpot(
                root,
                "CoolIndustrialRim",
                new Vector3(width * 0.34f, 9.8f, depth * 0.42f),
                new Vector3(width * 0.04f, 0.65f, depth * 0.10f),
                new Color(0.41f, 0.55f, 0.79f, 1f),
                Mathf.Clamp(diagonal * 0.68f, 22f, 48f),
                70f,
                0.62f,
                false);

            // The chassis is deliberately darker than the deck, but it still needs a readable silhouette.
            // A broad shadowless blue-grey bounce from the camera side reveals terraces/girders without
            // making the underside look self-illuminated.
            CreateSpot(
                root,
                "CoolUndersideLift",
                new Vector3(-width * 0.26f, 1.8f, -depth * 0.72f),
                new Vector3(-width * 0.04f, -3.2f, -depth * 0.18f),
                new Color(0.31f, 0.41f, 0.59f, 1f),
                Mathf.Clamp(diagonal * 0.70f, 24f, 52f),
                92f,
                0.52f,
                false);

            CreateSpot(
                root,
                "WarmNorthServiceA",
                new Vector3(-width * 0.24f, 4.8f, depth * 0.47f),
                new Vector3(-width * 0.18f, 0.1f, depth * 0.24f),
                new Color(1.0f, 0.50f, 0.18f, 1f),
                12.5f,
                54f,
                1.55f,
                false);

            CreateSpot(
                root,
                "WarmNorthServiceB",
                new Vector3(width * 0.18f, 4.6f, depth * 0.47f),
                new Vector3(width * 0.14f, 0.1f, depth * 0.22f),
                new Color(1.0f, 0.46f, 0.15f, 1f),
                11.5f,
                50f,
                1.34f,
                false);

            CreateSpot(
                root,
                "WarmEastService",
                new Vector3(width * 0.47f, 4.4f, depth * 0.08f),
                new Vector3(width * 0.24f, 0.1f, depth * 0.05f),
                new Color(1.0f, 0.48f, 0.16f, 1f),
                11.0f,
                52f,
                1.26f,
                false);

            CreatePoint(root, "WarmPractical_North", new Vector3(0f, 1.15f, depth * 0.47f), 5.8f, 0.76f);
            CreatePoint(root, "WarmPractical_East", new Vector3(width * 0.47f, 1.05f, -depth * 0.10f), 5.2f, 0.66f);
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
            light.color = new Color(1.0f, 0.47f, 0.16f, 1f);
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
