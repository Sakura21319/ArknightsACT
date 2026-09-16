using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Bridges the legacy 14x11 presentation kit to the enlarged 18x14 gameplay chunks.
    /// Physical block ownership comes from RogueliteStageRuntimeController; this pass expands the
    /// segmented floor/presentation roots so the visual kit fills the new combat footprint without
    /// stretching gameplay cover colliders or Facility traversal geometry.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageExpansionController : MonoBehaviour
    {
        private const float LegacyWidth = 14f;
        private const float LegacyDepth = 11f;
        private const float ChunkWidth = 18f;
        private const float ChunkDepth = 14f;
        private const int FloorColumns = 6;
        private const int FloorRows = 5;

        private static readonly Vector3 PresentationScale = new(
            ChunkWidth / LegacyWidth,
            1f,
            ChunkDepth / LegacyDepth);

        [SerializeField] private RogueliteStageMapController stageMap;

        private readonly HashSet<int> _scaledRoots = new();
        private GameObject _stage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map)
        {
            stageMap = map;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.08f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null)
                return;

            if (stage != _stage)
            {
                _stage = stage;
                _scaledRoots.Clear();
            }

            ExpandFloorSockets(stage.transform);
            ExpandLatePresentation(stage.transform);
        }

        private void ExpandFloorSockets(Transform stage)
        {
            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var block = FindBlockTransform(stage, i);
                var socketsRoot = block != null ? block.Find("[FloorSockets]") : null;
                if (socketsRoot == null)
                    continue;

                var id = socketsRoot.gameObject.GetInstanceID();
                if (!_scaledRoots.Add(id))
                    continue;

                socketsRoot.localScale = PresentationScale;

                var sockets = socketsRoot.GetComponentsInChildren<RogueliteFloorSocket25D>(true);
                var footprint = new Vector2(ChunkWidth / FloorColumns, ChunkDepth / FloorRows);
                for (var s = 0; s < sockets.Length; s++)
                    sockets[s]?.SetFootprint(footprint);
            }
        }

        private void ExpandLatePresentation(Transform stage)
        {
            // The broad floor meshes were authored to the old socket size. Their positions already
            // come from expanded floor sockets; only mesh extent needs the same X/Z ratio.
            var broad = stage.Find("[Chernobog_FloorComposition]/BroadDeckMasses");
            ScaleChildrenOnce(broad, PresentationScale);

            // Old compact joint caps no longer match the enlarged connections. Hide them instead of
            // stretching decorative seams across the new open deck; later bridge/service modules own
            // the visual transition between chunks.
            var joints = stage.Find("[Chernobog_FloorComposition]/BlockConnectionJoints");
            if (joints != null)
                joints.gameObject.SetActive(false);

            // Perimeter/backdrop modules are visual-only. Expanding their root moves the architecture
            // to the new boundary while leaving gameplay cover and Facility colliders untouched.
            var perimeter = stage.Find("[Chernobog_ModularKit]/Perimeter");
            ScaleRootOnce(perimeter, PresentationScale);

            var backdrop = stage.Find("[Chernobog_BackdropFacadeKit]");
            ScaleRootOnce(backdrop, PresentationScale);

            var skyline = stage.Find("[MobileCityEnvironment]/ChernobogStyleSkyline");
            ScaleRootOnce(skyline, PresentationScale);
        }

        private void ScaleRootOnce(Transform root, Vector3 scale)
        {
            if (root == null)
                return;
            var id = root.gameObject.GetInstanceID();
            if (!_scaledRoots.Add(id))
                return;
            root.localScale = Vector3.Scale(root.localScale, scale);
        }

        private void ScaleChildrenOnce(Transform root, Vector3 scale)
        {
            if (root == null)
                return;
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;
                var id = child.gameObject.GetInstanceID();
                if (!_scaledRoots.Add(id))
                    continue;
                child.localScale = Vector3.Scale(child.localScale, scale);
            }
        }

        private static Transform FindBlockTransform(Transform stage, int index)
        {
            var prefix = $"Block_{index:00}_";
            for (var i = 0; i < stage.childCount; i++)
            {
                var child = stage.GetChild(i);
                if (child != null && child.name.StartsWith(prefix, StringComparison.Ordinal))
                    return child;
            }
            return null;
        }
    }
}
