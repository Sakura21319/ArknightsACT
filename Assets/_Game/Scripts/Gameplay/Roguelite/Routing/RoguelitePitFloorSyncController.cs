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
            return true;
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
