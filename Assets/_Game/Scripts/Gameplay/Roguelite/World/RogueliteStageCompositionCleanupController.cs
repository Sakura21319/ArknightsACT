using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Final one-shot spatial cleanup for procedural dressing. It moves bulky dressing between a set of
    /// safe corner pockets until its visible bounds no longer intersect authored architecture colliders.
    /// This removes the most common scaffold/rubble/building interpenetration without changing gameplay
    /// routes or deleting visual variety.
    /// </summary>
    [DefaultExecutionOrder(24)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageCompositionCleanupController : MonoBehaviour
    {
        private static readonly Vector3[] CandidatePockets =
        {
            new(-5.20f, 0f, -4.55f),
            new(5.20f, 0f, -4.55f),
            new(-5.25f, 0f, 4.45f),
            new(5.25f, 0f, 4.45f),
            new(-6.05f, 0f, -3.35f),
            new(6.05f, 0f, 3.35f),
            new(-6.05f, 0f, 3.35f),
            new(6.05f, 0f, -3.35f)
        };

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
            _nextResolveAt = Time.unscaledTime + 0.12f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            var dressing = stage.transform.Find("[Chernobog_SetDressing]");
            var urban = stage.transform.Find("[Chernobog_UrbanArchitecture]");
            var playable = stage.transform.Find("[Chernobog_PlayableArchitecture]");
            if (dressing == null || urban == null || playable == null)
                return;

            var architectureColliders = new List<Collider>(128);
            architectureColliders.AddRange(urban.GetComponentsInChildren<Collider>(true));
            architectureColliders.AddRange(playable.GetComponentsInChildren<Collider>(true));

            var moved = ResolveDressing(dressing, architectureColliders);
            Physics.SyncTransforms();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/CompositionCleanup] Stage {stageMap.StageIndex}: {moved} dressing features repositioned away from architecture.", this);
        }

        private static int ResolveDressing(Transform dressing, List<Collider> architectureColliders)
        {
            var moved = 0;
            for (var b = 0; b < dressing.childCount; b++)
            {
                var block = dressing.GetChild(b);
                if (block == null || !block.name.StartsWith("Block_", StringComparison.Ordinal))
                    continue;

                var featureSerial = 0;
                for (var i = 0; i < block.childCount; i++)
                {
                    var feature = block.GetChild(i);
                    if (feature == null || !IsBlockingFeature(feature.name))
                        continue;

                    var original = feature.localPosition;
                    var placed = false;
                    var start = PositiveMod(b * 5 + featureSerial * 3, CandidatePockets.Length);
                    for (var attempt = 0; attempt < CandidatePockets.Length; attempt++)
                    {
                        var candidate = CandidatePockets[(start + attempt) % CandidatePockets.Length];
                        feature.localPosition = new Vector3(candidate.x, original.y, candidate.z);
                        if (!IntersectsArchitecture(feature, architectureColliders, 0.10f))
                        {
                            placed = true;
                            break;
                        }
                    }

                    if (!placed)
                    {
                        // Keep the least intrusive deterministic fallback rather than allowing a feature
                        // to remain embedded in a building facade.
                        var fallback = CandidatePockets[start];
                        feature.localPosition = new Vector3(fallback.x, original.y, fallback.z);
                    }

                    if ((feature.localPosition - original).sqrMagnitude > 0.001f)
                        moved++;
                    featureSerial++;
                }
            }
            return moved;
        }

        private static bool IntersectsArchitecture(Transform feature, List<Collider> architectureColliders, float padding)
        {
            if (!TryGetRendererBounds(feature, out var featureBounds))
                return false;

            featureBounds.Expand(new Vector3(padding * 2f, padding, padding * 2f));
            for (var i = 0; i < architectureColliders.Count; i++)
            {
                var collider = architectureColliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                    continue;
                if (featureBounds.Intersects(collider.bounds))
                    return true;
            }
            return false;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            var initialized = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return initialized;
        }

        private static bool IsBlockingFeature(string name)
        {
            return name == "Scaffold" ||
                   name == "RubbleCluster" ||
                   name == "BlackOriginiumGrowth" ||
                   name == "BrokenDeckSlab" ||
                   name == "CargoBlocks";
        }

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
