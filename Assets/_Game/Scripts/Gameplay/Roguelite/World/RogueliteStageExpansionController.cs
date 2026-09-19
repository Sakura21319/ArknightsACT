using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Bridges the legacy 14x11 presentation kit to the enlarged 30x24 gameplay chunks.
    /// Physical block ownership and floor sockets come from the current runtime footprint; this
    /// pass expands only legacy presentation roots without stretching gameplay cover colliders or
    /// Facility traversal geometry.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageExpansionController : MonoBehaviour
    {
        private const float LegacyWidth = 14f;
        private const float LegacyDepth = 11f;
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;
        private const int FloorColumns = 6;
        private const int FloorRows = 5;

        private static readonly Vector3 PresentationScale = new(
            ChunkWidth / LegacyWidth,
            1f,
            ChunkDepth / LegacyDepth);

        [SerializeField] private RogueliteStageMapController stageMap;

        private RogueliteStageRuntimeContext _context;
        private readonly HashSet<int> _scaledRoots = new();
        private GameObject _stage;
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
            _nextResolveAt = Time.unscaledTime + 0.08f;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null)
                return;

            if (stage != _stage)
            {
                _stage = stage;
                _scaledRoots.Clear();
            }

            NormalizeFloorSockets(stage.transform);
            ExpandLatePresentation(stage.transform);
        }

        private void NormalizeFloorSockets(Transform stage)
        {
            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var block = FindBlockTransform(stage, i);
                var socketsRoot = block != null ? block.Find("[FloorSockets]") : null;
                if (socketsRoot == null)
                    continue;

                var id = socketsRoot.gameObject.GetHashCode();
                if (!_scaledRoots.Add(id))
                    continue;

                // StageLayout authors sockets directly at the shared 30x24 gameplay footprint.
                // Scaling this root again would enlarge an already-correct floor and make its
                // visual boundary disagree with the physical stage.
                socketsRoot.localScale = Vector3.one;

                var sockets = socketsRoot.GetComponentsInChildren<RogueliteFloorSocket25D>(true);
                var footprint = new Vector2(ChunkWidth / FloorColumns, ChunkDepth / FloorRows);
                for (var s = 0; s < sockets.Length; s++)
                    sockets[s]?.SetFootprint(footprint);
            }
        }

        private void ExpandLatePresentation(Transform stage)
        {
            // Broad floor meshes remain legacy presentation assets. Their extent needs the same
            // X/Z ratio as the enlarged stage, while gameplay floor sockets stay unscaled.
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
            var id = root.gameObject.GetHashCode();
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
                var id = child.gameObject.GetHashCode();
                if (!_scaledRoots.Add(id))
                    continue;
                child.localScale = Vector3.Scale(child.localScale, scale);
            }
        }

        private static Transform FindBlockTransform(Transform stage, int index)
        {
            return RogueliteStageBlockUtility.FindBlockTransform(stage, index);
        }
    }
}
