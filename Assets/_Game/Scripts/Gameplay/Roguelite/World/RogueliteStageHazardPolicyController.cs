using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Current environment policy for the Concept-01 prototype.
    /// Pit hazards are retired from the run while other terrain hazards remain active.
    /// This pass intentionally removes only presentation/gameplay objects named Hazard_Hole and does
    /// not touch the segmented floor system, navigation graph, Active Originium or ballista hazards.
    /// </summary>
    [DefaultExecutionOrder(8)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageHazardPolicyController : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;

        private GameObject _stage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map)
        {
            stageMap = map;
        }

        private void Awake()
        {
            // The old pit-floor bridge should never open floor sockets in the no-pit policy.
            var legacyPitSync = GetComponent<RoguelitePitFloorSyncController>();
            if (legacyPitSync != null)
                legacyPitSync.enabled = false;
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
                _stage = stage;

            RemovePitHazards(stage.transform);
        }

        private static void RemovePitHazards(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = transforms.Length - 1; i >= 0; i--)
            {
                var current = transforms[i];
                if (current == null || !current.name.StartsWith("Hazard_Hole", StringComparison.Ordinal))
                    continue;

                // Rename immediately so later presentation passes in the same frame ignore it even
                // though Destroy() itself is deferred until the end of the frame.
                current.name = "[Removed_Hazard_Hole]";

                var colliders = current.GetComponentsInChildren<Collider>(true);
                for (var c = 0; c < colliders.Length; c++)
                    colliders[c].enabled = false;

                var pit = current.GetComponent<PitHazard25D>();
                if (pit != null)
                    pit.enabled = false;

                var body = current.GetComponent<Rigidbody>();
                if (body != null)
                    body.detectCollisions = false;

                current.gameObject.SetActive(false);
                Destroy(current.gameObject);
            }
        }
    }
}
