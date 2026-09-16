using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>
    /// Bridges environment-created pit hazards to the physical segmented floor without making the
    /// environment controller own map construction. Runs after the environment pass, snaps each pit
    /// to the nearest eligible socket and opens that floor module.
    /// </summary>
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class RoguelitePitFloorSyncController : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;

        private readonly HashSet<int> _boundPits = new();
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
            _nextResolveAt = Time.unscaledTime + 0.10f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null)
                return;

            if (stage != _stage)
            {
                _stage = stage;
                _boundPits.Clear();
            }

            BindNewPits(stage.transform);
        }

        private void BindNewPits(Transform stage)
        {
            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var block = FindBlockTransform(stage, i);
                if (block == null)
                    continue;

                for (var childIndex = 0; childIndex < block.childCount; childIndex++)
                {
                    var child = block.GetChild(childIndex);
                    if (child == null || !child.name.StartsWith("Hazard_Hole", StringComparison.Ordinal))
                        continue;

                    var id = child.gameObject.GetInstanceID();
                    if (_boundPits.Contains(id))
                        continue;
                    if (TryBindPit(block, child))
                        _boundPits.Add(id);
                }
            }
        }

        private static bool TryBindPit(Transform block, Transform pit)
        {
            var sockets = block.GetComponentsInChildren<RogueliteFloorSocket25D>(true);
            RogueliteFloorSocket25D nearest = null;
            var bestDistance = float.PositiveInfinity;
            var pitLocal = block.InverseTransformPoint(pit.position);

            for (var i = 0; i < sockets.Length; i++)
            {
                var socket = sockets[i];
                if (socket == null || !socket.PitEligible)
                    continue;

                var socketLocal = block.InverseTransformPoint(socket.transform.position);
                var delta = new Vector2(socketLocal.x - pitLocal.x, socketLocal.z - pitLocal.z);
                var distance = delta.sqrMagnitude;
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                nearest = socket;
            }

            if (nearest == null || !nearest.Open())
                return false;

            var snapped = block.InverseTransformPoint(nearest.transform.position);
            pit.localPosition = new Vector3(snapped.x, pit.localPosition.y, snapped.z);
            FitPitPresentationToSocket(pit, nearest.Footprint);

            // Concept-01 floor inserts live in the visual stage layer rather than under the physical
            // socket. Remove a nearby grate/hatch when that socket becomes a real hole so no service
            // plate can float over an opened shaft.
            HideConceptFloorDetails(block.parent, nearest.transform.position);
            return true;
        }

        private static void HideConceptFloorDetails(Transform stage, Vector3 socketWorldPosition)
        {
            if (stage == null)
                return;

            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var current = transforms[i];
                if (current == null ||
                    (!string.Equals(current.name, "MaintenanceGrate", StringComparison.Ordinal) &&
                     !string.Equals(current.name, "ServiceHatch", StringComparison.Ordinal)))
                    continue;

                var delta = current.position - socketWorldPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude <= 1.35f * 1.35f)
                    current.gameObject.SetActive(false);
            }
        }

        private static void FitPitPresentationToSocket(Transform pit, Vector2 footprint)
        {
            var trigger = pit.GetComponent<BoxCollider>();
            if (trigger != null)
            {
                trigger.center = new Vector3(0f, 0.16f, 0f);
                trigger.size = new Vector3(
                    Mathf.Max(0.40f, footprint.x * 0.94f),
                    0.72f,
                    Mathf.Max(0.40f, footprint.y * 0.92f));
            }

            var depth = pit.Find("PitDepth");
            if (depth != null)
            {
                depth.localPosition = new Vector3(0f, -0.32f, 0f);
                depth.localScale = new Vector3(footprint.x * 0.98f, 0.04f, footprint.y * 0.98f);
            }

            var edgeN = pit.Find("PitEdgeN");
            var edgeS = pit.Find("PitEdgeS");
            var edgeE = pit.Find("PitEdgeE");
            var edgeW = pit.Find("PitEdgeW");
            var halfX = footprint.x * 0.5f;
            var halfZ = footprint.y * 0.5f;

            FitEdge(edgeN, new Vector3(0f, 0.060f, halfZ + 0.07f), new Vector3(footprint.x + 0.28f, 0.10f, 0.14f));
            FitEdge(edgeS, new Vector3(0f, 0.060f, -halfZ - 0.07f), new Vector3(footprint.x + 0.28f, 0.10f, 0.14f));
            FitEdge(edgeE, new Vector3(halfX + 0.07f, 0.060f, 0f), new Vector3(0.14f, 0.10f, footprint.y));
            FitEdge(edgeW, new Vector3(-halfX - 0.07f, 0.060f, 0f), new Vector3(0.14f, 0.10f, footprint.y));
        }

        private static void FitEdge(Transform edge, Vector3 localPosition, Vector3 localScale)
        {
            if (edge == null)
                return;
            edge.localPosition = localPosition;
            edge.localScale = localScale;
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
