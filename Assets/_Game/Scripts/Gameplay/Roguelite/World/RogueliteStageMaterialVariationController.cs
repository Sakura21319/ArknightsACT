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

        private readonly MaterialPropertyBlock _block = new();
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

            ApplyVariation(stage);
            _preparedStage = stage;
        }

        private void ApplyVariation(GameObject stage)
        {
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

                // Variation remains deliberately subtle: enough to break clone repetition without
                // turning the deck into checkerboard tiles again.
                var value = Mathf.Lerp(0.955f, 1.035f, h);
                var coolShift = Mathf.Lerp(-0.010f, 0.012f, h2);
                var tint = new Color(
                    Mathf.Clamp(value - coolShift * 0.45f, 0.90f, 1.08f),
                    Mathf.Clamp(value, 0.90f, 1.08f),
                    Mathf.Clamp(value + coolShift, 0.90f, 1.08f),
                    1f);

                var smoothness = Mathf.Clamp01(response.Smoothness + (h2 - 0.5f) * response.SmoothnessSpread);
                var metallic = Mathf.Clamp01(response.Metallic + (h - 0.5f) * response.MetallicSpread);

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tint);
                _block.SetColor(ColorId, tint);
                _block.SetFloat(SmoothnessId, smoothness);
                _block.SetFloat(GlossinessId, smoothness);
                _block.SetFloat(MetallicId, metallic);
                renderer.SetPropertyBlock(_block);
                _block.Clear();
            }
        }

        private static bool TryGetResponse(string materialName, out SurfaceResponse response)
        {
            if (materialName.StartsWith("Kit_DeckSecondary", StringComparison.Ordinal))
                response = new SurfaceResponse(0.44f, 0.125f, 0.030f, 0.020f);
            else if (materialName.StartsWith("Kit_DeckHeavy", StringComparison.Ordinal))
                response = new SurfaceResponse(0.47f, 0.120f, 0.030f, 0.020f);
            else if (materialName.StartsWith("Kit_Deck", StringComparison.Ordinal))
                response = new SurfaceResponse(0.42f, 0.140f, 0.025f, 0.018f);
            else if (materialName.StartsWith("Kit_Wall", StringComparison.Ordinal))
                response = new SurfaceResponse(0.38f, 0.105f, 0.025f, 0.018f);
            else if (materialName.StartsWith("Kit_Inset", StringComparison.Ordinal))
                response = new SurfaceResponse(0.16f, 0.045f, 0.018f, 0.012f);
            else if (materialName.StartsWith("Kit_Steel", StringComparison.Ordinal))
                response = new SurfaceResponse(0.72f, 0.190f, 0.035f, 0.025f);
            else if (materialName.StartsWith("Kit_Grate", StringComparison.Ordinal))
                response = new SurfaceResponse(0.52f, 0.055f, 0.030f, 0.012f);
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
