using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Adds very small per-module tint/roughness variation through MaterialPropertyBlock so repeated
    /// modular assets do not read as cloned plastic pieces. Shared material assets stay untouched and
    /// no gameplay colliders/navigation are involved.
    /// </summary>
    [DefaultExecutionOrder(19)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageMaterialVariationController : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        [SerializeField] private RogueliteStageMapController stageMap;

        private RogueliteStageRuntimeContext _context;
        private GameObject _preparedStage;
        private float _nextResolveAt;

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

            ApplyVariation(stage);
            _preparedStage = stage;
        }

        private void ApplyVariation(GameObject stage)
        {
            // Do not keep MaterialPropertyBlock as a readonly MonoBehaviour field. Unity can restore
            // serialized component instances without preserving that managed field initializer on some
            // editor/domain-reload paths, which leaves Renderer.GetPropertyBlock with a null destination.
            // One reusable local block per pass is cheap and removes that failure mode completely.
            var propertyBlock = new MaterialPropertyBlock();
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null || string.IsNullOrEmpty(material.name) || !material.name.StartsWith("Kit_", StringComparison.Ordinal))
                    continue;
                if (material.name.StartsWith("Kit_Orange", StringComparison.Ordinal) ||
                    material.name.StartsWith("Kit_WarmLight", StringComparison.Ordinal))
                    continue;

                if (!TryGetResponse(material.name, out var response))
                    continue;

                var t = renderer.transform;
                var seed = unchecked(
                    Mathf.RoundToInt(t.position.x * 37f) * 73856093 ^
                    Mathf.RoundToInt(t.position.z * 41f) * 19349663 ^
                    StableStringHash(t.name) ^
                    stageMap.StageIndex * 83492791);
                var h = Hash01(seed);
                var h2 = Hash01(seed ^ 0x2c1b3c6d);

                // Keep the value variation extremely restrained. Most of the material read should come
                // from lighting, roughness and micro normal response rather than visible colour patches.
                var value = Mathf.Lerp(0.975f, 1.025f, h);
                var coolShift = Mathf.Lerp(-0.007f, 0.008f, h2);
                var tint = new Color(
                    Mathf.Clamp(value - coolShift * 0.45f, 0.93f, 1.06f),
                    Mathf.Clamp(value, 0.93f, 1.06f),
                    Mathf.Clamp(value + coolShift, 0.93f, 1.06f),
                    1f);

                var smoothness = Mathf.Clamp01(response.Smoothness + (h2 - 0.5f) * response.SmoothnessSpread);
                var metallic = Mathf.Clamp01(response.Metallic + (h - 0.5f) * response.MetallicSpread);

                propertyBlock.Clear();
                renderer.GetPropertyBlock(propertyBlock);
                if (material.HasProperty(BaseColorId)) propertyBlock.SetColor(BaseColorId, tint);
                if (material.HasProperty(ColorId)) propertyBlock.SetColor(ColorId, tint);
                if (material.HasProperty(SmoothnessId)) propertyBlock.SetFloat(SmoothnessId, smoothness);
                if (material.HasProperty(GlossinessId)) propertyBlock.SetFloat(GlossinessId, smoothness);
                if (material.HasProperty(MetallicId)) propertyBlock.SetFloat(MetallicId, metallic);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static bool TryGetResponse(string materialName, out SurfaceResponse response)
        {
            if (materialName.StartsWith("Kit_DeckSecondary", StringComparison.Ordinal))
                response = new SurfaceResponse(0.44f, 0.125f, 0.030f, 0.018f);
            else if (materialName.StartsWith("Kit_DeckHeavy", StringComparison.Ordinal))
                response = new SurfaceResponse(0.47f, 0.120f, 0.030f, 0.018f);
            else if (materialName.StartsWith("Kit_Deck", StringComparison.Ordinal))
                response = new SurfaceResponse(0.42f, 0.140f, 0.025f, 0.016f);
            else if (materialName.StartsWith("Kit_Wall", StringComparison.Ordinal))
                response = new SurfaceResponse(0.38f, 0.105f, 0.025f, 0.016f);
            else if (materialName.StartsWith("Kit_Inset", StringComparison.Ordinal))
                response = new SurfaceResponse(0.16f, 0.045f, 0.018f, 0.010f);
            else if (materialName.StartsWith("Kit_Steel", StringComparison.Ordinal))
                response = new SurfaceResponse(0.72f, 0.190f, 0.035f, 0.022f);
            else if (materialName.StartsWith("Kit_Grate", StringComparison.Ordinal))
                response = new SurfaceResponse(0.52f, 0.055f, 0.030f, 0.010f);
            else
            {
                response = default;
                return false;
            }
            return true;
        }

        private static int StableStringHash(string value)
        {
            unchecked
            {
                var hash = (int)2166136261;
                if (value == null)
                    return hash;
                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }
                return hash;
            }
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= unchecked((int)0x7feb352d);
                value ^= value >> 15;
                value *= unchecked((int)0x846ca68b);
                value ^= value >> 16;
                return (value & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        private readonly struct SurfaceResponse
        {
            public SurfaceResponse(float metallic, float smoothness, float metallicSpread, float smoothnessSpread)
            {
                Metallic = metallic;
                Smoothness = smoothness;
                MetallicSpread = metallicSpread;
                SmoothnessSpread = smoothnessSpread;
            }

            public float Metallic { get; }
            public float Smoothness { get; }
            public float MetallicSpread { get; }
            public float SmoothnessSpread { get; }
        }
    }
}
