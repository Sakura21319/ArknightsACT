using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Breaks the visible 6x5 socket-grid rhythm without changing the physical floor.
    ///
    /// Pit-eligible side/corner sockets keep one visual plate per socket so opening a pit remains exact.
    /// The guaranteed-safe central cross is visually merged into larger plates, matching the broad
    /// industrial deck masses used by the selected Chernobog art direction.
    /// </summary>
    [DefaultExecutionOrder(16)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageFloorCompositionController : MonoBehaviour
    {
        private const int Columns = 6;
        private const int Rows = 5;
        private const float ChunkWidth = 14f;
        private const float ChunkDepth = 11f;

        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private ChernobogEnvironmentKit kit;

        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map, ChernobogEnvironmentKit environmentKit)
        {
            stageMap = map;
            kit = environmentKit;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.12f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            Apply(stage.transform);
            _preparedStage = stage;

            Debug.Log(
                $"[ArknightsACT/FloorComposition] Stage {stageMap.StageIndex}: broad deck masses and sparse block joints applied.",
                this);
        }

        private void Apply(Transform stage)
        {
            var old = stage.Find("[Chernobog_FloorComposition]");
            if (old != null)
                Destroy(old.gameObject);

            DisableLegacyFloorOverlays(stage);

            var root = new GameObject("[Chernobog_FloorComposition]").transform;
            root.SetParent(stage, false);

            var masses = new GameObject("BroadDeckMasses").transform;
            masses.SetParent(root, false);
            var joints = new GameObject("BlockConnectionJoints").transform;
            joints.SetParent(root, false);

            for (var blockIndex = 0; blockIndex < stageMap.Blocks.Count; blockIndex++)
            {
                var block = FindBlockTransform(stage, blockIndex);
                var sockets = block != null ? block.Find("[FloorSockets]") : null;
                if (block == null || sockets == null)
                    continue;

                var grid = ResolveSurfaceGrid(sockets);
                ApplyBroadBlockTone(grid, blockIndex);
                BuildSafeMergedPlates(masses, block, grid, blockIndex);
            }

            BuildBlockConnectionJoints(joints);
        }

        private static void DisableLegacyFloorOverlays(Transform stage)
        {
            var transitions = stage.Find("[Concept01_ChernobogDeckKit]/DeckTransitions");
            if (transitions != null)
                transitions.gameObject.SetActive(false);

            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var current = transforms[i];
                if (current == null)
                    continue;

                var name = current.name;
                if (!string.Equals(name, "RoadStrip", StringComparison.Ordinal) &&
                    !string.Equals(name, "RoadStripe", StringComparison.Ordinal) &&
                    !string.Equals(name, "PlazaPad", StringComparison.Ordinal) &&
                    !string.Equals(name, "ArenaPad", StringComparison.Ordinal))
                    continue;

                var renderer = current.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = false;
            }
        }

        private Transform[,] ResolveSurfaceGrid(Transform sockets)
        {
            var grid = new Transform[Columns, Rows];
            for (var i = 0; i < sockets.childCount; i++)
            {
                var child = sockets.GetChild(i);
                if (child == null || !child.name.StartsWith("FloorSurface_", StringComparison.Ordinal))
                    continue;
                if (!TryParseCoordinates(child.name, out var x, out var z))
                    continue;
                if (x < 0 || x >= Columns || z < 0 || z >= Rows)
                    continue;
                grid[x, z] = child;
            }
            return grid;
        }

        private void ApplyBroadBlockTone(Transform[,] grid, int blockIndex)
        {
            var block = blockIndex >= 0 && blockIndex < stageMap.Blocks.Count ? stageMap.Blocks[blockIndex] : null;
            var theme = block != null ? block.Theme : RogueliteChunkTheme.Open;
            var secondary = kit.deckSecondaryMaterial != null ? kit.deckSecondaryMaterial : kit.deckHeavyMaterial;
            var blockVariant = PositiveMod(stageMap.StageIndex * 13 + blockIndex * 7, 6);

            var material = kit.deckMaterial;
            if (theme == RogueliteChunkTheme.Street || theme == RogueliteChunkTheme.CoverLane)
                material = secondary != null ? secondary : kit.deckMaterial;
            else if (blockVariant == 0 && secondary != null)
                material = secondary;

            for (var z = 0; z < Rows; z++)
            for (var x = 0; x < Columns; x++)
            {
                var surface = grid[x, z];
                if (surface == null)
                    continue;
                var renderer = surface.GetComponent<Renderer>();
                if (renderer != null && material != null)
                    renderer.sharedMaterial = material;
            }
        }

        private void BuildSafeMergedPlates(Transform parent, Transform block, Transform[,] grid, int blockIndex)
        {
            if (kit.floorPlateLongXMesh == null || kit.floorPlateLongZMesh == null)
                return;

            var secondary = kit.deckSecondaryMaterial != null ? kit.deckSecondaryMaterial : kit.deckHeavyMaterial;
            var horizontalMaterial = PositiveMod(stageMap.StageIndex * 17 + blockIndex * 5, 4) == 0 && secondary != null
                ? secondary
                : kit.deckMaterial;
            var verticalMaterial = PositiveMod(stageMap.StageIndex * 19 + blockIndex * 3, 5) == 0 && secondary != null
                ? secondary
                : kit.deckMaterial;

            // Central E-W row: two 3-cell broad plates. The whole row is pit-ineligible by layout contract.
            MergeRun(parent, block, grid, 0, 2, 2, true, kit.floorPlateLongXMesh, horizontalMaterial, $"Block_{blockIndex:00}_DeckLong_W");
            MergeRun(parent, block, grid, 3, 5, 2, true, kit.floorPlateLongXMesh, horizontalMaterial, $"Block_{blockIndex:00}_DeckLong_E");

            // Central N-S pair of columns: merge top/bottom pairs around the already-covered center row.
            for (var x = 2; x <= 3; x++)
            {
                MergeRun(parent, block, grid, 0, 1, x, false, kit.floorPlateLongZMesh, verticalMaterial,
                    $"Block_{blockIndex:00}_DeckLong_S_{x}");
                MergeRun(parent, block, grid, 3, 4, x, false, kit.floorPlateLongZMesh, verticalMaterial,
                    $"Block_{blockIndex:00}_DeckLong_N_{x}");
            }
        }

        private static void MergeRun(
            Transform parent,
            Transform block,
            Transform[,] grid,
            int start,
            int end,
            int fixedAxis,
            bool horizontal,
            Mesh mesh,
            Material material,
            string name)
        {
            Transform first = null;
            Transform last = null;
            for (var i = start; i <= end; i++)
            {
                var surface = horizontal ? grid[i, fixedAxis] : grid[fixedAxis, i];
                if (surface == null)
                    return;
                first ??= surface;
                last = surface;
            }

            if (first == null || last == null)
                return;

            for (var i = start; i <= end; i++)
            {
                var surface = horizontal ? grid[i, fixedAxis] : grid[fixedAxis, i];
                var renderer = surface.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = false;
            }

            var worldPosition = (first.position + last.position) * 0.5f;
            CreateMeshVisual(parent, name, worldPosition, block.rotation, mesh, material);
        }

        private void BuildBlockConnectionJoints(Transform parent)
        {
            var totalWidth = stageMap.Width * ChunkWidth;
            var totalDepth = stageMap.Height * ChunkDepth;
            var jointMaterial = kit.steelMaterial != null
                ? kit.steelMaterial
                : kit.deckHeavyMaterial != null ? kit.deckHeavyMaterial : kit.deckMaterial;

            if (kit.floorJointXMesh != null)
            {
                for (var x = 1; x < stageMap.Width; x++)
                {
                    var worldX = -totalWidth * 0.5f + x * ChunkWidth;
                    for (var z = 0; z < stageMap.Height; z++)
                    {
                        var worldZ = -totalDepth * 0.5f + ChunkDepth * (z + 0.5f);
                        CreateMeshVisual(parent, $"Joint_X_{x}_{z}", new Vector3(worldX, 0.046f, worldZ),
                            Quaternion.identity, kit.floorJointXMesh, jointMaterial);
                    }
                }
            }

            if (kit.floorJointZMesh != null)
            {
                for (var z = 1; z < stageMap.Height; z++)
                {
                    var worldZ = -totalDepth * 0.5f + z * ChunkDepth;
                    for (var x = 0; x < stageMap.Width; x++)
                    {
                        var worldX = -totalWidth * 0.5f + ChunkWidth * (x + 0.5f);
                        CreateMeshVisual(parent, $"Joint_Z_{x}_{z}", new Vector3(worldX, 0.046f, worldZ),
                            Quaternion.identity, kit.floorJointZMesh, jointMaterial);
                    }
                }
            }
        }

        private static GameObject CreateMeshVisual(
            Transform parent,
            string name,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Mesh mesh,
            Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            go.transform.rotation = worldRotation;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static bool TryParseCoordinates(string name, out int x, out int z)
        {
            x = -1;
            z = -1;
            var parts = name.Split('_');
            return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z);
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

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
