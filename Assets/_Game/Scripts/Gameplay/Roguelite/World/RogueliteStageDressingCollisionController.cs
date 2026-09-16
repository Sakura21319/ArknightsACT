using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Promotes selected set-dressing pieces into static blockers while preserving the simple
    /// cardinal waypoint lanes used by the prototype navigation graph. Rubble, black Originium
    /// growths and cargo get coarse footprint colliders; scaffold poles/platforms get exact box
    /// colliders from their generated mesh bounds so the upper decks can be stood on if reached.
    /// </summary>
    [DefaultExecutionOrder(23)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageDressingCollisionController : MonoBehaviour
    {
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
            if (dressing == null)
                return;

            var added = Apply(dressing);
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/DressingCollision] Stage {stageMap.StageIndex}: {added} blocker colliders added outside reserved navigation lanes.", this);
        }

        private static int Apply(Transform dressing)
        {
            var added = 0;
            var transforms = dressing.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var feature = transforms[i];
                if (feature == null || !CanSolidify(feature))
                    continue;

                switch (feature.name)
                {
                    case "Scaffold":
                        added += AddScaffoldColliders(feature);
                        break;
                    case "RubbleCluster":
                        if (!HasAncestorNamed(feature, "BrokenDeckSlab"))
                            added += AddAggregateCollider(feature, new Vector3(0f, 0.42f, 0f), new Vector3(2.55f, 0.84f, 2.05f));
                        break;
                    case "BlackOriginiumGrowth":
                        added += AddAggregateCollider(feature, new Vector3(0f, 1.02f, 0f), new Vector3(2.45f, 2.05f, 1.95f));
                        break;
                    case "BrokenDeckSlab":
                        added += AddAggregateCollider(feature, new Vector3(0.15f, 0.28f, -0.10f), new Vector3(3.75f, 0.62f, 2.45f));
                        break;
                    case "CargoBlocks":
                        added += AddAggregateCollider(feature, new Vector3(0f, 1.20f, 0f), new Vector3(2.90f, 2.42f, 1.48f));
                        break;
                }
            }
            return added;
        }

        private static bool CanSolidify(Transform feature)
        {
            var blockRoot = FindBlockDressingRoot(feature);
            if (blockRoot == null)
                return false;

            var local = blockRoot.InverseTransformPoint(feature.position);

            // The current enemy graph routes through a broad central cross. Keep that entire cross
            // free of new dressing colliders; blockers live in corner/edge pockets instead.
            const float verticalLaneHalfWidth = 3.0f;
            const float horizontalLaneHalfDepth = 2.55f;
            if (Mathf.Abs(local.x) < verticalLaneHalfWidth || Mathf.Abs(local.z) < horizontalLaneHalfDepth)
                return false;

            // Keep a small margin from the physical outer bounds too, so a character cannot be pinched
            // between a dressing object and the perimeter wall.
            if (Mathf.Abs(local.x) > 7.55f || Mathf.Abs(local.z) > 5.75f)
                return false;

            return true;
        }

        private static int AddScaffoldColliders(Transform scaffold)
        {
            var added = 0;
            for (var i = 0; i < scaffold.childCount; i++)
            {
                var child = scaffold.GetChild(i);
                if (child == null)
                    continue;
                if (!child.name.StartsWith("Pole_", StringComparison.Ordinal) &&
                    !child.name.StartsWith("Platform_", StringComparison.Ordinal))
                    continue;
                added += AddMeshBoundsCollider(child.gameObject);
            }
            return added;
        }

        private static int AddMeshBoundsCollider(GameObject go)
        {
            if (go == null || go.GetComponent<Collider>() != null)
                return 0;
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return 0;

            var bounds = filter.sharedMesh.bounds;
            var collider = go.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            return 1;
        }

        private static int AddAggregateCollider(Transform root, Vector3 center, Vector3 size)
        {
            if (root == null || root.GetComponent<Collider>() != null)
                return 0;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            return 1;
        }

        private static Transform FindBlockDressingRoot(Transform current)
        {
            while (current != null)
            {
                if (current.name.StartsWith("Block_", StringComparison.Ordinal) &&
                    current.name.EndsWith("_Dressing", StringComparison.Ordinal))
                    return current;
                current = current.parent;
            }
            return null;
        }

        private static bool HasAncestorNamed(Transform current, string name)
        {
            current = current != null ? current.parent : null;
            while (current != null)
            {
                if (string.Equals(current.name, name, StringComparison.Ordinal))
                    return true;
                if (current.name.StartsWith("Block_", StringComparison.Ordinal) &&
                    current.name.EndsWith("_Dressing", StringComparison.Ordinal))
                    return false;
                current = current.parent;
            }
            return false;
        }
    }
}
