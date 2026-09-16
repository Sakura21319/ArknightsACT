using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>
    /// Physical-layout pass layered on top of RogueliteStageRuntimeController. It keeps route and
    /// encounter ownership untouched, but turns monolithic prototype slabs into removable floor
    /// modules and applies deterministic Chernobog-style block variants.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageLayoutController : MonoBehaviour
    {
        private const float ChunkWidth = 14f;
        private const float ChunkDepth = 11f;
        private const int FloorColumns = 6;
        private const int FloorRows = 5;

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

            PrepareStage(stage);
            _preparedStage = stage;
        }

        private void PrepareStage(GameObject stage)
        {
            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                if (data == null)
                    continue;

                var block = FindBlockTransform(stage.transform, i);
                if (block == null)
                    continue;

                BuildSegmentedFloor(block);
                ApplyBlockVariant(block, data);
            }

            Debug.Log(
                $"[ArknightsACT/StageLayout] Stage {stageMap.StageIndex} prepared with segmented floors and Chernobog block variants.",
                this);
        }

        private static void BuildSegmentedFloor(Transform block)
        {
            if (block.Find("[FloorSockets]") != null)
                return;

            var originalFloor = block.Find("Floor");
            var originalInset = block.Find("ChunkInset");
            if (originalFloor == null)
                return;

            var floorRenderer = originalFloor.GetComponent<Renderer>();
            var insetRenderer = originalInset != null ? originalInset.GetComponent<Renderer>() : null;
            var floorMaterial = floorRenderer != null ? floorRenderer.sharedMaterial : null;
            var insetMaterial = insetRenderer != null ? insetRenderer.sharedMaterial : floorMaterial;

            var root = new GameObject("[FloorSockets]").transform;
            root.SetParent(block, false);

            var cellWidth = ChunkWidth / FloorColumns;
            var cellDepth = ChunkDepth / FloorRows;
            for (var z = 0; z < FloorRows; z++)
            for (var x = 0; x < FloorColumns; x++)
            {
                var localX = -ChunkWidth * 0.5f + cellWidth * (x + 0.5f);
                var localZ = -ChunkDepth * 0.5f + cellDepth * (z + 0.5f);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = $"FloorSocket_{x}_{z}";
                floor.transform.SetParent(root, false);
                floor.transform.localPosition = new Vector3(localX, -0.14f, localZ);
                floor.transform.localScale = new Vector3(cellWidth, 0.28f, cellDepth);

                var renderer = floor.GetComponent<Renderer>();
                if (renderer != null && floorMaterial != null)
                    renderer.sharedMaterial = floorMaterial;
                var collider = floor.GetComponent<Collider>();

                var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
                surface.name = $"FloorSurface_{x}_{z}";
                surface.transform.SetParent(root, false);
                surface.transform.localPosition = new Vector3(localX, 0.012f, localZ);
                surface.transform.localScale = new Vector3(
                    Mathf.Max(0.1f, cellWidth - 0.045f),
                    0.024f,
                    Mathf.Max(0.1f, cellDepth - 0.045f));
                var surfaceCollider = surface.GetComponent<Collider>();
                if (surfaceCollider != null)
                    Destroy(surfaceCollider);
                var surfaceRenderer = surface.GetComponent<Renderer>();
                if (surfaceRenderer != null && insetMaterial != null)
                    surfaceRenderer.sharedMaterial = insetMaterial;

                // Keep a clear physical cross through every block. Pits may consume side/corner
                // modules, but cannot sever the four cardinal connections used by the shared nav graph.
                var pitEligible = z != FloorRows / 2 && x != FloorColumns / 2 && x != FloorColumns / 2 - 1;
                floor.AddComponent<RogueliteFloorSocket25D>().Configure(
                    new Vector2(cellWidth, cellDepth),
                    pitEligible,
                    renderer,
                    collider,
                    surface);
            }

            originalFloor.gameObject.SetActive(false);
            if (originalInset != null)
                originalInset.gameObject.SetActive(false);
        }

        private void ApplyBlockVariant(Transform block, RogueliteBlockState data)
        {
            var variant = PositiveMod(stageMap.StageIndex * 17 + data.Index * 7 + (int)data.Theme * 11, 3);
            RepositionExistingCover(block, data.Theme, variant);
            AdjustStreetGeometry(block, data.Theme, variant);
            BuildLowRiseShells(block, data, variant);
        }

        private static void RepositionExistingCover(Transform block, RogueliteChunkTheme theme, int variant)
        {
            var covers = new List<Transform>();
            for (var i = 0; i < block.childCount; i++)
            {
                var child = block.GetChild(i);
                if (child != null && string.Equals(child.name, "GridCover", StringComparison.Ordinal))
                    covers.Add(child);
            }

            if (theme == RogueliteChunkTheme.Open && covers.Count >= 2)
            {
                var layouts = new[]
                {
                    new[] { new Pose2D(-3.0f, 1.4f, 12f), new Pose2D(3.4f, -1.9f, -12f) },
                    new[] { new Pose2D(-4.0f, -2.5f, 0f), new Pose2D(3.6f, 2.6f, 90f) },
                    new[] { new Pose2D(-3.8f, 2.8f, 90f), new Pose2D(3.9f, -2.7f, 0f) }
                };
                ApplyPoses(covers, layouts[variant]);
                return;
            }

            if (theme == RogueliteChunkTheme.Street && covers.Count >= 2)
            {
                var layouts = new[]
                {
                    new[] { new Pose2D(-3.7f, 2.6f, 0f), new Pose2D(3.4f, -2.5f, 0f) },
                    new[] { new Pose2D(-3.8f, -2.7f, 90f), new Pose2D(3.8f, 2.7f, 90f) },
                    new[] { new Pose2D(-4.1f, 2.35f, 15f), new Pose2D(3.7f, -2.25f, -15f) }
                };
                ApplyPoses(covers, layouts[variant]);
                return;
            }

            if (theme == RogueliteChunkTheme.CoverLane && covers.Count >= 3)
            {
                var layouts = new[]
                {
                    new[] { new Pose2D(-3.3f, 0.9f, 90f), new Pose2D(2.0f, 2.35f, 0f), new Pose2D(3.6f, -2.0f, 90f) },
                    new[] { new Pose2D(-3.9f, 2.2f, 0f), new Pose2D(2.35f, 2.55f, 90f), new Pose2D(3.8f, -2.2f, 90f) },
                    new[] { new Pose2D(-3.9f, -2.3f, 0f), new Pose2D(-2.4f, 2.55f, 90f), new Pose2D(3.9f, 2.1f, 0f) }
                };
                ApplyPoses(covers, layouts[variant]);
            }
        }

        private static void ApplyPoses(IReadOnlyList<Transform> transforms, IReadOnlyList<Pose2D> poses)
        {
            var count = Mathf.Min(transforms.Count, poses.Count);
            for (var i = 0; i < count; i++)
            {
                transforms[i].localPosition = new Vector3(poses[i].X, 0f, poses[i].Z);
                transforms[i].localRotation = Quaternion.Euler(0f, poses[i].Yaw, 0f);
            }
        }

        private static void AdjustStreetGeometry(Transform block, RogueliteChunkTheme theme, int variant)
        {
            if (theme != RogueliteChunkTheme.Street)
                return;

            var road = block.Find("RoadStrip");
            var stripe = block.Find("RoadStripe");
            if (road == null || stripe == null)
                return;

            if (variant == 1)
            {
                road.localRotation = Quaternion.Euler(0f, 90f, 0f);
                road.localScale = new Vector3(ChunkDepth - 0.4f, 0.035f, 5.2f);
                stripe.localRotation = Quaternion.Euler(0f, 90f, 0f);
                stripe.localScale = new Vector3(ChunkDepth - 1.0f, 0.02f, 0.10f);
            }
            else
            {
                road.localRotation = Quaternion.identity;
                road.localScale = new Vector3(ChunkWidth - 0.4f, 0.035f, 5.2f);
                stripe.localRotation = Quaternion.identity;
                stripe.localScale = new Vector3(ChunkWidth - 1.0f, 0.02f, 0.10f);
                stripe.localPosition = variant == 2 ? new Vector3(0f, 0.035f, -0.85f) : new Vector3(0f, 0.035f, 0f);
            }
        }

        private void BuildLowRiseShells(Transform block, RogueliteBlockState data, int variant)
        {
            if (data.Theme == RogueliteChunkTheme.Facility || data.Theme == RogueliteChunkTheme.BossArena)
                return;

            var renderers = block.GetComponentsInChildren<Renderer>(true);
            var wall = FindMaterial(renderers, "Facility_Wall") ?? FindMaterial(renderers, "CombatCover");
            var roof = FindMaterial(renderers, "Facility_Floor") ?? FindMaterial(renderers, "Sidewalk_Tactical") ?? wall;
            var accent = FindMaterial(renderers, "TacticalAccent") ?? roof;
            var hazard = FindMaterial(renderers, "HazardBand") ?? accent;
            if (wall == null)
                return;

            var corners = new[]
            {
                new Vector3(-5.25f, 0f, 3.82f),
                new Vector3(5.18f, 0f, 3.78f),
                new Vector3(5.20f, 0f, -3.82f),
                new Vector3(-5.18f, 0f, -3.78f)
            };

            var shellCount = data.Theme == RogueliteChunkTheme.SafePlaza
                ? 1
                : (stageMap.StageIndex >= 2 && data.Theme == RogueliteChunkTheme.Street ? 2 : 1);
            if (stageMap.StageIndex >= 3 && data.Theme == RogueliteChunkTheme.CoverLane)
                shellCount = 2;

            for (var i = 0; i < shellCount; i++)
            {
                var cornerIndex = PositiveMod(variant + data.Index + i * 2, corners.Length);
                var height = 1.55f + 0.32f * stageMap.StageIndex + 0.18f * ((data.Index + i) % 3);
                var width = 2.15f + 0.25f * ((variant + i) % 2);
                var depth = 1.55f + 0.22f * ((data.Index + variant + i) % 2);
                CreateLowRiseShell(
                    block,
                    $"ChernobogLowRise_{variant}_{i}",
                    corners[cornerIndex],
                    new Vector3(width, height, depth),
                    cornerIndex % 2 == 0 ? -4f : 4f,
                    wall,
                    roof,
                    accent,
                    hazard,
                    stageMap.StageIndex >= 3 && i == shellCount - 1);
            }
        }

        private static void CreateLowRiseShell(
            Transform parent,
            string name,
            Vector3 localBase,
            Vector3 size,
            float yaw,
            Material wall,
            Material roof,
            Material accent,
            Material hazard,
            bool damaged)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localBase;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var bodyHeight = damaged ? size.y * 0.72f : size.y;
            CreateBlock(root, "ShellBody", new Vector3(0f, bodyHeight * 0.5f, 0f), new Vector3(size.x, bodyHeight, size.z), wall, true);
            CreateVisual(root, "RoofUnit", new Vector3(size.x * 0.15f, bodyHeight + 0.16f, 0f), new Vector3(size.x * 0.48f, 0.28f, size.z * 0.48f), roof);
            CreateVisual(root, "IDStripe", new Vector3(0f, Mathf.Min(bodyHeight * 0.72f, 1.45f), -size.z * 0.5f - 0.018f), new Vector3(size.x * 0.58f, 0.13f, 0.035f), accent);
            CreateVisual(root, "HazardFoot", new Vector3(0f, 0.12f, -size.z * 0.5f - 0.022f), new Vector3(size.x * 0.72f, 0.18f, 0.040f), hazard);

            if (damaged)
            {
                CreateVisual(root, "BrokenTop", new Vector3(-size.x * 0.28f, bodyHeight + 0.28f, 0f), new Vector3(size.x * 0.28f, 0.55f, size.z * 0.62f), wall)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            }
        }

        private static Material FindMaterial(Renderer[] renderers, string materialName)
        {
            if (renderers == null)
                return null;
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (material != null && string.Equals(material.name, materialName, StringComparison.OrdinalIgnoreCase))
                    return material;
            }
            return null;
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

        private static GameObject CreateBlock(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool colliderEnabled)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = colliderEnabled;
            return go;
        }

        private static GameObject CreateVisual(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            return CreateBlock(parent, name, localPosition, scale, material, false);
        }

        private static int PositiveMod(int value, int modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private readonly struct Pose2D
        {
            public Pose2D(float x, float z, float yaw)
            {
                X = x;
                Z = z;
                Yaw = yaw;
            }

            public float X { get; }
            public float Z { get; }
            public float Yaw { get; }
        }
    }
}
