using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Adds small pieces of architecture that are meant to be used during combat rather than only read
    /// as skyline dressing: walk-in service rooms, covered alcoves and short raised loading decks.
    /// Everything is built from explicit colliders and kept outside the authoritative cardinal nav cross.
    /// </summary>
    [DefaultExecutionOrder(23)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStagePlayableArchitectureController : MonoBehaviour
    {
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
            if (stage.transform.Find("[Chernobog_UrbanArchitecture]") == null)
                return;

            Build(stage.transform);
            Physics.SyncTransforms();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/PlayableArchitecture] Stage {stageMap.StageIndex}: walk-in rooms and raised service decks built.", this);
        }

        private void Build(Transform stage)
        {
            var old = stage.Find("[Chernobog_PlayableArchitecture]");
            if (old != null)
                Destroy(old.gameObject);

            var root = new GameObject("[Chernobog_PlayableArchitecture]").transform;
            root.SetParent(stage, false);

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                var block = FindBlockTransform(stage, i);
                if (data == null || block == null)
                    continue;

                var cell = new GameObject($"Block_{i:00}_PlayableArchitecture").transform;
                cell.SetParent(root, false);
                cell.position = block.position;

                var flip = PositiveMod(stageMap.StageIndex * 5 + i * 3 + (int)data.Theme, 2) == 1;
                var xSide = flip ? 1f : -1f;

                switch (data.Theme)
                {
                    case RogueliteChunkTheme.Street:
                        BuildWalkInServiceRoom(cell, new Vector3(xSide * 4.55f, 0f, -4.85f), xSide, i);
                        break;
                    case RogueliteChunkTheme.CoverLane:
                        BuildRaisedLoadingDeck(cell, new Vector3(xSide * 4.25f, 0f, -4.80f), -xSide, i);
                        break;
                    case RogueliteChunkTheme.Facility:
                        BuildWalkInServiceRoom(cell, new Vector3(-5.00f, 0f, -4.75f), -1f, i + 31);
                        break;
                    case RogueliteChunkTheme.SafePlaza:
                        BuildCoveredCheckpoint(cell, new Vector3(xSide * 4.70f, 0f, -4.75f), xSide, i);
                        break;
                    case RogueliteChunkTheme.BossArena:
                        BuildRaisedLoadingDeck(cell, new Vector3(-4.35f, 0f, -4.75f), 1f, i + 61);
                        BuildCoveredCheckpoint(cell, new Vector3(4.75f, 0f, -4.70f), -1f, i + 79);
                        break;
                    default:
                        BuildWalkInServiceRoom(cell, new Vector3(xSide * 4.50f, 0f, -4.90f), xSide, i);
                        break;
                }
            }
        }

        private void BuildWalkInServiceRoom(Transform parent, Vector3 anchor, float side, int seed)
        {
            var root = new GameObject("WalkInServiceRoom").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            const float width = 3.00f;
            const float depth = 2.75f;
            const float wallHeight = 2.55f;
            const float thickness = 0.22f;

            // Opening faces toward the centre of the block (+Z). The three solid sides create a real
            // combat alcove while the split front jambs give the player a readable doorway.
            CreateSolidBox(root, "RoomBackWall", new Vector3(0f, wallHeight * 0.5f, -depth * 0.5f),
                new Vector3(width, wallHeight, thickness), 0.045f, wall);
            CreateSolidBox(root, "RoomWall_W", new Vector3(-width * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(thickness, wallHeight, depth), 0.045f, wall);
            CreateSolidBox(root, "RoomWall_E", new Vector3(width * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(thickness, wallHeight, depth), 0.045f, wall);
            CreateSolidBox(root, "DoorJamb_W", new Vector3(-1.12f, 1.10f, depth * 0.5f),
                new Vector3(0.76f, 2.20f, thickness), 0.040f, steel);
            CreateSolidBox(root, "DoorJamb_E", new Vector3(1.12f, 1.10f, depth * 0.5f),
                new Vector3(0.76f, 2.20f, thickness), 0.040f, steel);
            CreateSolidBox(root, "DoorHeader", new Vector3(0f, 2.33f, depth * 0.5f),
                new Vector3(width, 0.30f, thickness), 0.040f, steel);

            // Partial roof keeps the interior readable from the fixed camera instead of hiding it.
            CreateSolidBox(root, "RoomRoofRear", new Vector3(0f, wallHeight + 0.10f, -0.50f),
                new Vector3(width + 0.14f, 0.18f, depth * 0.56f), 0.035f, steel);
            CreateVisualBox(root, "InteriorBackInset", new Vector3(0f, 1.18f, -depth * 0.5f + 0.125f),
                new Vector3(1.60f, 0.78f, 0.035f), 0.006f, grate);

            // One waist-high internal cabinet turns the room into actual cover rather than empty scenery.
            CreateSolidBox(root, "InteriorServiceCabinet", new Vector3(side * 0.72f, 0.48f, -0.55f),
                new Vector3(0.78f, 0.96f, 0.62f), 0.055f, inset);
        }

        private void BuildRaisedLoadingDeck(Transform parent, Vector3 anchor, float rampSide, int seed)
        {
            var root = new GameObject("RaisedLoadingDeck").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var wall = kit.wallMaterial != null ? kit.wallMaterial : steel;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : steel;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;

            const float deckY = 1.05f;
            const float deckWidth = 2.45f;
            const float deckDepth = 2.55f;

            CreateSolidBox(root, "LoadingDeck", new Vector3(0f, deckY, -0.48f),
                new Vector3(deckWidth, 0.20f, deckDepth), 0.035f, grate);
            CreateSolidBox(root, "DeckSupport_W", new Vector3(-0.98f, 0.50f, -0.48f),
                new Vector3(0.24f, 1.00f, 2.20f), 0.035f, inset);
            CreateSolidBox(root, "DeckSupport_E", new Vector3(0.98f, 0.50f, -0.48f),
                new Vector3(0.24f, 1.00f, 2.20f), 0.035f, inset);

            // Ramp approaches from the centre side (+Z); its collider is the same sloped box as the mesh.
            CreateRamp(root, "LoadingRamp",
                new Vector3(0f, 0.12f, 1.65f),
                new Vector3(0f, deckY + 0.02f, 0.65f),
                1.38f,
                0.16f,
                steel);

            CreateSolidBox(root, "DeckRail_Back", new Vector3(0f, deckY + 0.50f, -1.75f),
                new Vector3(deckWidth, 0.12f, 0.12f), 0.020f, steel);
            CreateSolidBox(root, "DeckRail_W", new Vector3(-1.18f, deckY + 0.50f, -0.58f),
                new Vector3(0.12f, 0.12f, 2.15f), 0.020f, steel);
            CreateSolidBox(root, "DeckRail_E", new Vector3(1.18f, deckY + 0.50f, -0.58f),
                new Vector3(0.12f, 0.12f, 2.15f), 0.020f, steel);

            CreateSolidBox(root, "DeckCover", new Vector3(rampSide * 0.62f, deckY + 0.48f, -0.65f),
                new Vector3(0.72f, 0.86f, 0.62f), 0.055f, wall);
        }

        private void BuildCoveredCheckpoint(Transform parent, Vector3 anchor, float side, int seed)
        {
            var root = new GameObject("CoveredCheckpoint").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            CreateSolidBox(root, "CheckpointPost_W", new Vector3(-1.15f, 1.35f, -0.45f),
                new Vector3(0.18f, 2.70f, 0.18f), 0.030f, steel);
            CreateSolidBox(root, "CheckpointPost_E", new Vector3(1.15f, 1.35f, -0.45f),
                new Vector3(0.18f, 2.70f, 0.18f), 0.030f, steel);
            CreateSolidBox(root, "CheckpointRoof", new Vector3(0f, 2.72f, -0.25f),
                new Vector3(2.65f, 0.18f, 2.10f), 0.040f, grate);
            CreateSolidBox(root, "CheckpointBarrier", new Vector3(side * 0.78f, 0.48f, -0.28f),
                new Vector3(0.82f, 0.96f, 0.42f), 0.050f, inset);
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

        private static GameObject CreateSolidBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material)
        {
            var go = CreateVisualBox(parent, name, localPosition, size, bevel, material);
            var collider = go.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = size;
            collider.isTrigger = false;
            return go;
        }

        private static GameObject CreateVisualBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject CreateRamp(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float width,
            float thickness,
            Material material)
        {
            var delta = end - start;
            var length = delta.magnitude;
            if (length < 0.05f)
                return null;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (start + end) * 0.5f;
            go.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            var size = new Vector3(width, thickness, length);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, Mathf.Min(0.035f, thickness * 0.20f));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            var collider = go.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = size;
            return go;
        }

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
