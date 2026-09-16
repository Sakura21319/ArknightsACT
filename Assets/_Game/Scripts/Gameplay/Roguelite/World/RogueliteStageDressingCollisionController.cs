using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Promotes selected set-dressing pieces into static blockers after the urban-composition pass has
    /// moved them into safe edge/corner pockets. Rubble/crystal/cargo/slabs use robust aggregate boxes;
    /// scaffold poles, decks and braces use their authored mesh bounds.
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
            _nextResolveAt = Time.unscaledTime + 0.10f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            var dressing = stage.transform.Find("[Chernobog_SetDressing]");
            var composition = stage.transform.Find("[Chernobog_UrbanArchitecture]");
            if (dressing == null || composition == null)
                return;

            var added = Apply(dressing);
            Physics.SyncTransforms();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/DressingCollision] Stage {stageMap.StageIndex}: {added} solid dressing colliders installed.", this);
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
                        if (!HasAncestorNamed(feature, "BrokenDeckSlab") &&
                            !HasAncestorNamed(feature, "BlackOriginiumGrowth"))
                        {
                            added += AddAggregateCollider(feature,
                                new Vector3(0f, 0.46f, 0f),
                                new Vector3(2.75f, 0.92f, 2.20f));
                        }
                        break;
                    case "BlackOriginiumGrowth":
                        added += AddAggregateCollider(feature,
                            new Vector3(0f, 1.02f, 0f),
                            new Vector3(2.65f, 2.10f, 2.10f));
                        break;
                    case "BrokenDeckSlab":
                        added += AddAggregateCollider(feature,
                            new Vector3(0.10f, 0.30f, -0.08f),
                            new Vector3(3.95f, 0.72f, 2.65f));
                        break;
                    case "CargoBlocks":
                        added += AddAggregateCollider(feature,
                            new Vector3(0f, 1.22f, 0f),
                            new Vector3(3.05f, 2.48f, 1.62f));
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

            // The current waypoint graph owns the central cardinal cross. UrbanComposition moves all
            // solid dressing roots outside this reserve before this pass executes.
            const float verticalLaneHalfWidth = 3.0f;
            const float horizontalLaneHalfDepth = 2.55f;
            if (Mathf.Abs(local.x) < verticalLaneHalfWidth || Mathf.Abs(local.z) < horizontalLaneHalfDepth)
                return false;

            // Keep enough clearance from the invisible perimeter containment and visible edge wall.
            if (Mathf.Abs(local.x) > 7.55f || Mathf.Abs(local.z) > 5.75f)
                return false;

            return true;
        }

        private static int AddScaffoldColliders(Transform scaffold)
        {
            var added = 0;
            var parts = scaffold.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < parts.Length; i++)
            {
                var child = parts[i];
                if (child == null || child == scaffold)
                    continue;
                var solid = child.name.StartsWith("Pole_", StringComparison.Ordinal) ||
                            child.name.StartsWith("Platform_", StringComparison.Ordinal) ||
                            child.name.StartsWith("Brace_", StringComparison.Ordinal);
                if (!solid)
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
            collider.isTrigger = false;
            return 1;
        }

        private static int AddAggregateCollider(Transform root, Vector3 center, Vector3 size)
        {
            if (root == null || root.GetComponent<Collider>() != null)
                return 0;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            collider.isTrigger = false;
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
