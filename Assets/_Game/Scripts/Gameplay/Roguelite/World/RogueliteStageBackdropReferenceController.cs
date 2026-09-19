using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Optional local-reference backdrop layer. When PRTS Chernobog story-background images were
    /// downloaded through the editor workflow, a dimmed camera-facing card sits behind the procedural
    /// city geometry. The 3D streets/buildings remain authoritative for gameplay and collision while
    /// the verified story art supplies the distant atmosphere that procedural boxes cannot reproduce.
    /// </summary>
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageBackdropReferenceController : MonoBehaviour
    {
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;

        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private Texture2D[] cernobogBackdrops;
        [SerializeField, Range(0.1f, 1f)] private float backdropBrightness = 0.42f;

        private RogueliteStageRuntimeContext _context;
        private GameObject _resolvedStage;
        private GameObject _backdropCard;
        private Material _runtimeMaterial;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map, Texture2D[] backdrops)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
            cernobogBackdrops = backdrops;
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
            _nextResolveAt = Time.unscaledTime + 0.20f;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null || stage == _resolvedStage)
                return;

            _resolvedStage = stage;
            RebuildBackdrop(stage);
        }

        private void RebuildBackdrop(GameObject stage)
        {
            ReleaseRuntimeBackdrop();
            var texture = ResolveBackdropTexture(stageMap.StageIndex);
            if (texture == null)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            var sourceMaterial = ResolveVerifiedMaterial(stage);
            if (sourceMaterial == null)
                return;

            _runtimeMaterial = new Material(sourceMaterial)
            {
                name = $"Runtime_PRTS_ChernobogBackdrop_{stageMap.StageIndex}"
            };
            SetTexture(_runtimeMaterial, texture);
            SetColor(_runtimeMaterial, new Color(backdropBrightness, backdropBrightness, backdropBrightness, 1f));
            if (_runtimeMaterial.HasProperty("_Metallic")) _runtimeMaterial.SetFloat("_Metallic", 0f);
            if (_runtimeMaterial.HasProperty("_Smoothness")) _runtimeMaterial.SetFloat("_Smoothness", 0f);
            if (_runtimeMaterial.HasProperty("_Glossiness")) _runtimeMaterial.SetFloat("_Glossiness", 0f);

            _backdropCard = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _backdropCard.name = $"PRTS_Chernobog_Backdrop_Stage{stageMap.StageIndex}";
            _backdropCard.transform.SetParent(stage.transform, true);

            var horizontalForward = camera.transform.forward;
            horizontalForward.y = 0f;
            if (horizontalForward.sqrMagnitude < 0.001f)
                horizontalForward = new Vector3(1f, 0f, 1f);
            horizontalForward.Normalize();

            var stageWidth = stageMap.Width * ChunkWidth;
            var stageDepth = stageMap.Height * ChunkDepth;
            var stageSpan = Mathf.Max(stageWidth, stageDepth);
            _backdropCard.transform.position = stage.transform.position +
                                               horizontalForward * (stageSpan * 0.86f + 26f) +
                                               Vector3.up * Mathf.Max(10f, stageSpan * 0.16f);
            _backdropCard.transform.rotation = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up);

            // Overscan heavily so the fixed 2.5D camera never exposes the card edges at the expanded
            // 3x3 / 4x3 / 4x4 city sizes. Keep the source's wide story-background proportion.
            var backdropWidth = Mathf.Max(58f, stageSpan * 1.72f);
            var backdropHeight = backdropWidth * 0.5625f;
            _backdropCard.transform.localScale = new Vector3(backdropWidth, backdropHeight, 1f);

            var collider = _backdropCard.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            var renderer = _backdropCard.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _runtimeMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private Texture2D ResolveBackdropTexture(int stageIndex)
        {
            if (cernobogBackdrops == null || cernobogBackdrops.Length == 0)
                return null;

            var preferred = Mathf.Clamp(stageIndex - 1, 0, cernobogBackdrops.Length - 1);
            if (cernobogBackdrops[preferred] != null)
                return cernobogBackdrops[preferred];

            for (var i = 0; i < cernobogBackdrops.Length; i++)
            {
                if (cernobogBackdrops[i] != null)
                    return cernobogBackdrops[i];
            }
            return null;
        }

        private static Material ResolveVerifiedMaterial(GameObject stage)
        {
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            var preferredNames = new[]
            {
                "Facility_Wall",
                "Ground_Tactical",
                "CombatCover"
            };

            for (var p = 0; p < preferredNames.Length; p++)
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (material != null &&
                    material.shader != null &&
                    material.shader.isSupported &&
                    string.Equals(material.name, preferredNames[p], StringComparison.OrdinalIgnoreCase))
                    return material;
            }
            return null;
        }

        private static void SetTexture(Material material, Texture texture)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", Vector2.one);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", Vector2.one);
            }
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private void OnDestroy()
        {
            ReleaseRuntimeBackdrop();
        }

        private void ReleaseRuntimeBackdrop()
        {
            if (_backdropCard != null)
                Destroy(_backdropCard);
            _backdropCard = null;

            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }
}
