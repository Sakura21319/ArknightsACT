using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Promotes selected set-dressing pieces into reliable static blockers after urban/playable composition
    /// and overlap cleanup have completed. Aggregate blockers derive their size from the actual rendered
    /// bounds so visible rubble/crystal/cargo no longer protrudes through a smaller hard-coded collider.
    /// </summary>
    [DefaultExecutionOrder(25)]
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
            var urban = stage.transform.Find("[Chernobog_UrbanArchitecture]");
            var playable = stage.transform.Find("[Chernobog_PlayableArchitecture]");
            if (dressing == null || urban == null || playable == null)
                return;

            var added = Apply(dressing);
            Physics.SyncTransforms();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/DressingCollision] Stage {stageMap.StageIndex}: {added} solid dressing colliders installed from visual bounds.", this);
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
                            added += AddAggregateRendererBoundsCollider(feature, 0.08f, 0.04f);
                        break;
                    case "BlackOriginiumGrowth":
                        added += AddAggregateRendererBoundsCollider(feature, 0.10f, 0.05f);
                        break;
                    case "BrokenDeckSlab":
                        added += AddAggregateRendererBoundsCollider(feature, 0.08f, 0.04f);
                        break;
                    case "CargoBlocks":
                        added += AddAggregateRendererBoundsCollider(feature, 0.06f, 0.03f);
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

            // The waypoint graph owns this broad cross. Solid dressing must remain in the side/corner
            // combat pockets so inter-block traversal cannot be cut by procedural decoration.
            const float verticalLaneHalfWidth = 3.0f;
            const float horizontalLaneHalfDepth = 2.55f;
            if (Mathf.Abs(local.x) < verticalLaneHalfWidth || Mathf.Abs(local.z) < horizontalLaneHalfDepth)
                return false;

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

                var name = child.name;
                var solid = name.StartsWith("Pole_", StringComparison.Ordinal) ||
                            name.StartsWith("Platform_", StringComparison.Ordinal) ||
                            name.StartsWith("Brace_", StringComparison.Ordinal) ||
                            name.StartsWith("Rail", StringComparison.Ordinal);
                if (!solid)
                    continue;

                added += AddMeshBoundsCollider(child.gameObject, 0.025f);
            }
            return added;
        }

        private static int AddMeshBoundsCollider(GameObject go, float padding)
        {
            if (go == null || go.GetComponent<Collider>() != null)
                return 0;
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return 0;

            var bounds = filter.sharedMesh.bounds;
            var collider = go.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size + Vector3.one * padding * 2f;
            collider.isTrigger = false;
            return 1;
        }

        private static int AddAggregateRendererBoundsCollider(Transform root, float horizontalPadding, float verticalPadding)
        {
            if (root == null || root.GetComponent<Collider>() != null)
                return 0;
            if (!TryGetLocalRendererBounds(root, out var bounds))
                return 0;

            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = new Vector3(
                bounds.size.x + horizontalPadding * 2f,
                bounds.size.y + verticalPadding * 2f,
                bounds.size.z + horizontalPadding * 2f);
            collider.isTrigger = false;
            return 1;
        }

        private static bool TryGetLocalRendererBounds(Transform root, out Bounds localBounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            localBounds = default;
            var initialized = false;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                var world = renderer.bounds;
                var min = world.min;
                var max = world.max;
                for (var corner = 0; corner < 8; corner++)
                {
                    var worldPoint = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    var localPoint = root.InverseTransformPoint(worldPoint);
                    if (!initialized)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }

            return initialized;
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
