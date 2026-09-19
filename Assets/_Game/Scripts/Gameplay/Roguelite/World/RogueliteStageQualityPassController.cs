using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Compile-safe final presentation pass for Concept-01.
    ///
    /// This component intentionally depends only on UnityEngine / UnityEngine.Rendering. The first
    /// quality-pass revision referenced URP volume component types directly, which is unnecessary for
    /// the map skin and can break compilation when package/editor API revisions differ. Post FX can be
    /// authored later as an editor asset once the modular environment kit is stable.
    ///
    /// Gameplay colliders, navigation, encounters and hazards are never modified here.
    /// </summary>
    [DefaultExecutionOrder(18)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageQualityPassController : MonoBehaviour
    {
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;

        [SerializeField] private RogueliteStageMapController stageMap;

        private RogueliteStageRuntimeContext _context;
        private readonly List<Texture2D> _ownedTextures = new();
        private GameObject _preparedStage;
        private float _nextResolveAt;

        private Texture2D _deckNormal;
        private Texture2D _wallNormal;
        private Texture2D _coverNormal;
        private Texture2D _grateNormal;

        public void Configure(RogueliteStageMapController map)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context != null)
                stageMap ??= _context.StageMap;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.15f;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null || stage == _preparedStage)
                return;

            RemoveLegacyTileClutter(stage.transform);
            CullCameraNearBackdrop(stage.transform);
            EnsureSurfaceMaps();
            ApplySurfaceResponse(stage);
            ConfigureWorldLighting(stage.transform);
            _preparedStage = stage;

            Debug.Log(
                $"[ArknightsACT/Quality] Stage {stageMap.StageIndex}: legacy tile marks removed and compile-safe material/lighting quality pass applied.",
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
        }

        private static void RemoveLegacyTileClutter(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null)
                    continue;

                if (string.Equals(t.name, "[ArknightsTileSkin]", StringComparison.Ordinal) ||
                    string.Equals(t.name, "TileIDPlate", StringComparison.Ordinal) ||
                    string.Equals(t.name, "MaintenancePlate", StringComparison.Ordinal) ||
                    string.Equals(t.name, "GrateSlat", StringComparison.Ordinal) ||
                    string.Equals(t.name, "InnerSeamX", StringComparison.Ordinal))
                {
                    t.gameObject.SetActive(false);
                    continue;
                }

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
            var skyline = stage.Find("[MobileCityEnvironment]/ChernobogStyleSkyline");
            if (skyline == null || stageMap == null)
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

            _deckNormal = BuildNormalMap("Concept01_DeckNormal", 128, SurfaceKind.Deck);
            _wallNormal = BuildNormalMap("Concept01_WallNormal", 128, SurfaceKind.Wall);
            _coverNormal = BuildNormalMap("Concept01_CoverNormal", 128, SurfaceKind.Cover);
            _grateNormal = BuildNormalMap("Concept01_GrateNormal", 128, SurfaceKind.Grate);
        }

        private void ApplySurfaceResponse(GameObject stage)
        {
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            var visited = new HashSet<int>();

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null || !visited.Add(material.GetHashCode()))
                    continue;

                var name = material.name ?? string.Empty;
                if (name.StartsWith("Runtime_Concept01_Deck", StringComparison.Ordinal))
                    ApplyNormal(material, _deckNormal, 0.45f, 0.16f);
                else if (name.StartsWith("Runtime_Concept01_WallInset", StringComparison.Ordinal))
                    ApplyNormal(material, _wallNormal, 0.62f, 0.08f);
                else if (name.StartsWith("Runtime_Concept01_Wall", StringComparison.Ordinal))
                    ApplyNormal(material, _wallNormal, 0.52f, 0.13f);
                else if (name.StartsWith("Runtime_Concept01_Cover", StringComparison.Ordinal))
                    ApplyNormal(material, _coverNormal, 0.60f, 0.12f);
                else if (name.StartsWith("Runtime_Concept01_Grate", StringComparison.Ordinal))
                    ApplyNormal(material, _grateNormal, 0.85f, 0.06f);
                else if (name.StartsWith("Runtime_Concept01_SteelEdge", StringComparison.Ordinal))
                {
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.55f);
                    SetSmoothness(material, 0.22f);
                }
                else if (name.StartsWith("Runtime_Concept01_WarmLight", StringComparison.Ordinal))
                {
                    EnableEmission(material, new Color(2.4f, 1.05f, 0.18f, 1f));
                }
            }
        }

        private static void ApplyNormal(Material material, Texture2D normal, float bumpScale, float smoothness)
        {
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", bumpScale);
                material.EnableKeyword("_NORMALMAP");
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
            if (!material.HasProperty("_EmissionColor"))
                return;

            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
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

            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
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

            if (stage.Find("[Concept01_QualityLights]") != null || stageMap == null)
                return;

            var root = new GameObject("[Concept01_QualityLights]").transform;
            root.SetParent(stage, false);

            var width = stageMap.Width * ChunkWidth;
            var depth = stageMap.Height * ChunkDepth;
            CreateWarmPoint(root, new Vector3(-width * 0.26f, 1.15f, depth * 0.5f - 0.75f), 4.8f, 1.20f);
            CreateWarmPoint(root, new Vector3(width * 0.18f, 1.10f, depth * 0.5f - 0.70f), 5.2f, 1.30f);
            CreateWarmPoint(root, new Vector3(width * 0.5f - 0.70f, 1.05f, depth * 0.14f), 4.6f, 1.10f);
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
                anisoLevel = 4
            };

            var strength = kind == SurfaceKind.Grate ? 3.6f : kind == SurfaceKind.Wall ? 2.2f : 1.8f;
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

        private static float HeightAt(int x, int y, int size, SurfaceKind kind)
        {
            var noise = Hash01(x * 11 + (int)kind * 97, y * 13 + (int)kind * 131);
            var h = (noise - 0.5f) * 0.10f;

            var edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            if (kind == SurfaceKind.Deck)
            {
                if (edge < 4) h -= (4 - edge) * 0.08f;
            }
            else if (kind == SurfaceKind.Wall)
            {
                if (x % 64 < 4 || y % 80 < 4) h -= 0.18f;
            }
            else if (kind == SurfaceKind.Cover)
            {
                if (x % 72 < 4 || y % 68 < 4) h -= 0.16f;
            }
            else if (kind == SurfaceKind.Grate)
            {
                h = (x % 18 > 5 && y % 18 > 5) ? -0.55f : 0.18f;
            }

            return h;
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
