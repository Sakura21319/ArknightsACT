using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Final presentation pass for the selected Concept-01 look.
    ///
    /// This pass fixes two weaknesses visible in the first Unity screenshots:
    /// 1) legacy Phase-08 tile markers could still survive in generated scenes;
    /// 2) most geometry only had flat albedo + a directional light, so it read as grey blockout.
    ///
    /// Gameplay geometry/colliders remain authoritative. This component only removes legacy visual
    /// clutter, adds tangent-space surface response, enables restrained emission/post FX and builds
    /// a small lighting rig around the far walls.
    /// </summary>
    [DefaultExecutionOrder(18)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageQualityPassController : MonoBehaviour
    {
        private const float ChunkWidth = 14f;
        private const float ChunkDepth = 11f;

        [SerializeField] private RogueliteStageMapController stageMap;

        private readonly List<Texture2D> _ownedTextures = new();
        private GameObject _preparedStage;
        private float _nextResolveAt;

        private Texture2D _deckNormal;
        private Texture2D _deckOcclusion;
        private Texture2D _wallNormal;
        private Texture2D _wallOcclusion;
        private Texture2D _coverNormal;
        private Texture2D _coverOcclusion;
        private Texture2D _grateNormal;
        private Texture2D _grateOcclusion;
        private VolumeProfile _volumeProfile;

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

            RemoveLegacyTileClutter(stage.transform);
            CullCameraNearBackdrop(stage.transform);
            EnsureSurfaceMaps();
            ApplySurfaceResponse(stage);
            ConfigureWorldLighting(stage.transform);
            ConfigurePostProcessing();
            _preparedStage = stage;

            Debug.Log(
                $"[ArknightsACT/Quality] Stage {stageMap.StageIndex}: legacy tile marks removed, " +
                "normal/AO response, warm practical lights and restrained URP post FX applied.",
                this);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _ownedTextures.Count; i++)
            {
                if (_ownedTextures[i] != null)
                    Destroy(_ownedTextures[i]);
            }
            _ownedTextures.Clear();

            if (_volumeProfile != null)
                Destroy(_volumeProfile);
        }

        private static void RemoveLegacyTileClutter(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null)
                    continue;

                // Old authenticity pass: this root contained the repeated grate + orange ID pattern
                // visible in the screenshot. Disable the whole visual-only root, never the floor.
                if (string.Equals(t.name, "[ArknightsTileSkin]", StringComparison.Ordinal) ||
                    string.Equals(t.name, "TileIDPlate", StringComparison.Ordinal) ||
                    string.Equals(t.name, "MaintenancePlate", StringComparison.Ordinal) ||
                    string.Equals(t.name, "GrateSlat", StringComparison.Ordinal) ||
                    string.Equals(t.name, "InnerSeamX", StringComparison.Ordinal))
                {
                    t.gameObject.SetActive(false);
                    continue;
                }

                // Runtime used one orange block marker per chunk. It is useful for blockout/debugging,
                // but it does not belong in the final visual language.
                if (string.Equals(t.name, "BlockMarker", StringComparison.Ordinal))
                {
                    var renderer = t.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.enabled = false;
                }
            }
        }

        private void CullCameraNearBackdrop(Transform stage)
        {
            // Camera follows from the south-west (-X/-Z). Procedural skyline masses on that side can
            // become giant foreground slabs and hide the board. Keep the skyline on north/east only.
            var skyline = stage.Find("[MobileCityEnvironment]/ChernobogStyleSkyline");
            if (skyline == null)
                return;

            var width = stageMap.Width * ChunkWidth;
            var depth = stageMap.Height * ChunkDepth;
            for (var i = 0; i < skyline.childCount; i++)
            {
                var child = skyline.GetChild(i);
                if (child == null || !child.name.StartsWith("DistantBuilding", StringComparison.Ordinal))
                    continue;

                var p = child.localPosition;
                if (p.z < -depth * 0.48f || p.x < -width * 0.48f)
                    child.gameObject.SetActive(false);
            }
        }

        private void EnsureSurfaceMaps()
        {
            if (_deckNormal != null)
                return;

            _deckNormal = BuildNormalMap("Concept01_DeckNormal", 256, SurfaceKind.Deck);
            _deckOcclusion = BuildOcclusionMap("Concept01_DeckAO", 256, SurfaceKind.Deck);
            _wallNormal = BuildNormalMap("Concept01_WallNormal", 256, SurfaceKind.Wall);
            _wallOcclusion = BuildOcclusionMap("Concept01_WallAO", 256, SurfaceKind.Wall);
            _coverNormal = BuildNormalMap("Concept01_CoverNormal", 256, SurfaceKind.Cover);
            _coverOcclusion = BuildOcclusionMap("Concept01_CoverAO", 256, SurfaceKind.Cover);
            _grateNormal = BuildNormalMap("Concept01_GrateNormal", 256, SurfaceKind.Grate);
            _grateOcclusion = BuildOcclusionMap("Concept01_GrateAO", 256, SurfaceKind.Grate);
        }

        private void ApplySurfaceResponse(GameObject stage)
        {
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            var visited = new HashSet<int>();

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null || !visited.Add(material.GetInstanceID()))
                    continue;

                var name = material.name ?? string.Empty;
                if (name.StartsWith("Runtime_Concept01_Deck", StringComparison.Ordinal))
                {
                    ApplyPbrMaps(material, _deckNormal, _deckOcclusion, 0.62f, 0.17f);
                }
                else if (name.StartsWith("Runtime_Concept01_WallInset", StringComparison.Ordinal))
                {
                    ApplyPbrMaps(material, _wallNormal, _wallOcclusion, 0.82f, 0.09f);
                }
                else if (name.StartsWith("Runtime_Concept01_Wall", StringComparison.Ordinal))
                {
                    ApplyPbrMaps(material, _wallNormal, _wallOcclusion, 0.70f, 0.14f);
                }
                else if (name.StartsWith("Runtime_Concept01_Cover", StringComparison.Ordinal))
                {
                    ApplyPbrMaps(material, _coverNormal, _coverOcclusion, 0.78f, 0.13f);
                }
                else if (name.StartsWith("Runtime_Concept01_Grate", StringComparison.Ordinal))
                {
                    ApplyPbrMaps(material, _grateNormal, _grateOcclusion, 1.05f, 0.07f);
                }
                else if (name.StartsWith("Runtime_Concept01_SteelEdge", StringComparison.Ordinal))
                {
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.58f);
                    SetSmoothness(material, 0.24f);
                }
                else if (name.StartsWith("Runtime_Concept01_WarmLight", StringComparison.Ordinal))
                {
                    EnableEmission(material, new Color(2.8f, 1.25f, 0.22f, 1f));
                }
            }
        }

        private static void ApplyPbrMaps(Material material, Texture2D normal, Texture2D occlusion, float bumpScale, float smoothness)
        {
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", bumpScale);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_OcclusionMap"))
            {
                material.SetTexture("_OcclusionMap", occlusion);
                if (material.HasProperty("_OcclusionStrength"))
                    material.SetFloat("_OcclusionStrength", 0.82f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            SetSmoothness(material, smoothness);
        }

        private static void SetSmoothness(Material material, float smoothness)
        {
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        }

        private static void EnableEmission(Material material, Color emission)
        {
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        private void ConfigureWorldLighting(Transform stage)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.205f, 0.235f, 0.290f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.038f, 0.047f, 0.061f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 58f;

            var directionalLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (var i = 0; i < directionalLights.Length; i++)
            {
                var light = directionalLights[i];
                if (light == null || light.type != LightType.Directional)
                    continue;

                light.color = new Color(0.76f, 0.84f, 1.0f);
                light.intensity = 1.08f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.88f;
                light.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
                break;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                camera.allowHDR = true;
                camera.allowMSAA = true;
            }

            if (stage.Find("[Concept01_QualityLights]") != null)
                return;

            var root = new GameObject("[Concept01_QualityLights]").transform;
            root.SetParent(stage, false);

            var width = stageMap.Width * ChunkWidth;
            var depth = stageMap.Height * ChunkDepth;
            CreateWarmPoint(root, new Vector3(-width * 0.26f, 1.15f, depth * 0.5f - 0.75f), 4.8f, 1.35f);
            CreateWarmPoint(root, new Vector3(width * 0.18f, 1.10f, depth * 0.5f - 0.70f), 5.2f, 1.45f);
            CreateWarmPoint(root, new Vector3(width * 0.5f - 0.70f, 1.05f, depth * 0.14f), 4.6f, 1.20f);
        }

        private static void CreateWarmPoint(Transform parent, Vector3 localPosition, float range, float intensity)
        {
            var go = new GameObject("WarmPracticalLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.53f, 0.20f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void ConfigurePostProcessing()
        {
            var existing = GameObject.Find("[Concept01_PostFX]");
            if (existing != null)
                return;

            var go = new GameObject("[Concept01_PostFX]");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            _volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = _volumeProfile;

            var color = _volumeProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.05f);
            color.contrast.Override(10f);
            color.saturation.Override(-7f);

            var tonemapping = _volumeProfile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = _volumeProfile.Add<Bloom>(true);
            bloom.intensity.Override(0.16f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.55f);

            var vignette = _volumeProfile.Add<Vignette>(true);
            vignette.intensity.Override(0.10f);
            vignette.smoothness.Override(0.42f);
        }

        private Texture2D BuildNormalMap(string name, int size, SurfaceKind kind)
        {
            var height = new float[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                height[y * size + x] = HeightAt(x, y, size, kind);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };

            var strength = kind == SurfaceKind.Grate ? 4.5f : kind == SurfaceKind.Wall ? 2.8f : 2.2f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var xm = (x - 1 + size) % size;
                var xp = (x + 1) % size;
                var ym = (y - 1 + size) % size;
                var yp = (y + 1) % size;
                var dx = (height[y * size + xm] - height[y * size + xp]) * strength;
                var dy = (height[ym * size + x] - height[yp * size + x]) * strength;
                var normal = new Vector3(dx, dy, 1f).normalized;
                texture.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f));
            }
            texture.Apply(true, false);
            _ownedTextures.Add(texture);
            return texture;
        }

        private Texture2D BuildOcclusionMap(string name, int size, SurfaceKind kind)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var h = HeightAt(x, y, size, kind);
                var ao = Mathf.Clamp01(0.70f + h * 0.28f);
                if (kind == SurfaceKind.Grate && IsGrateGap(x, y))
                    ao = 0.28f;
                texture.SetPixel(x, y, new Color(ao, ao, ao, 1f));
            }
            texture.Apply(true, false);
            _ownedTextures.Add(texture);
            return texture;
        }

        private static float HeightAt(int x, int y, int size, SurfaceKind kind)
        {
            var noise = Hash01(x * 11 + (int)kind * 97, y * 13 + (int)kind * 131);
            var h = (noise - 0.5f) * 0.12f;

            var edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            if (kind == SurfaceKind.Deck)
            {
                if (edge < 4) h -= (4 - edge) * 0.09f;
                if ((x + y * 5) % 113 == 0) h -= 0.12f;
            }
            else if (kind == SurfaceKind.Wall)
            {
                if (x % 64 < 4 || y % 80 < 4) h -= 0.22f;
            }
            else if (kind == SurfaceKind.Cover)
            {
                if (x % 86 < 4 || y % 74 < 4) h -= 0.18f;
            }
            else if (kind == SurfaceKind.Grate)
            {
                h = IsGrateGap(x, y) ? -0.65f : 0.20f;
            }

            return h;
        }

        private static bool IsGrateGap(int x, int y)
        {
            return x % 18 > 5 && y % 18 > 5;
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                var n = x * 374761393 + y * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                n ^= n >> 16;
                return (n & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        private enum SurfaceKind
        {
            Deck,
            Wall,
            Cover,
            Grate
        }
    }
}
