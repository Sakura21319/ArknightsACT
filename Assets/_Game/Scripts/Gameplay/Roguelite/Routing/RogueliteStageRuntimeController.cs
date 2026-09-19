using System;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Navigation;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Progression;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>
    /// Runtime assembler for the exploration prototype. Scheme 1 keeps a 2x2 logical town and
    /// materializes large, connected blocks, activates encounters when cells are first entered,
    /// and keeps all rewards/progression on the same run state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteStageRuntimeController : MonoBehaviour
    {
        // Scheme 1 keeps four logical blocks but gives every block a readable town footprint.
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;
        private const float CoverHeight = 0.82f;
        private const float SecondFloorY = 2.10f;

        [Header("Core")]
        [SerializeField] private Transform player;
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private RogueliteRewardController rewards;
        [SerializeField] private GameObject[] enemyTemplates;
        [SerializeField] private GameObject[] treasureTemplates;

        [Header("Materials")]
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material sidewalkMaterial;
        [SerializeField] private Material facilityWallMaterial;
        [SerializeField] private Material facilityFloorMaterial;
        [SerializeField] private Material coverMaterial;
        [SerializeField] private Material accentMaterial;
        [SerializeField] private Material hazardMaterial;

        private readonly List<BlockRuntime> _runtimeBlocks = new(9);
        private GameObject _stageRoot;
        private GameObject _exitMarker;
        private RogueliteStageRuntimeContext _context;
        private int _currentBlockIndex = -1;
        private bool _exitReady;
        private bool _runComplete;
        private Vector3 _gridOrigin;

        private sealed class BlockRuntime
        {
            public RogueliteBlockState Data;
            public GameObject Root;
            public Transform ContentRoot;
            public readonly List<CombatEntity> Enemies = new();
            public bool ContentsSpawned;
            public bool EncounterSpawned;
            public bool EncounterCleared;
        }

        private struct NavCell
        {
            public int Center;
            public int West;
            public int East;
            public int South;
            public int North;
        }

        public void Configure(
            Transform playerTransform,
            RogueliteRunState state,
            RogueliteStageMapController map,
            RogueliteRewardController rewardController,
            GameObject[] enemies,
            GameObject[] treasures,
            Material ground,
            Material road,
            Material sidewalk,
            Material facilityWall,
            Material facilityFloor,
            Material cover,
            Material accent,
            Material hazard)
        {
            player = playerTransform;
            runState = state;
            stageMap = map;
            rewards = rewardController;
            enemyTemplates = enemies;
            treasureTemplates = treasures;
            groundMaterial = ground;
            roadMaterial = road;
            sidewalkMaterial = sidewalk;
            facilityWallMaterial = facilityWall;
            facilityFloorMaterial = facilityFloor;
            coverMaterial = cover;
            accentMaterial = accent;
            hazardMaterial = hazard;
        }

        private void Start()
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            ResolveReferences();
            if (!CanBuild())
            {
                Debug.LogError("[ArknightsACT/StageRuntime] Missing player, stage map, run state or templates. Rebuild Prototype Scene.", this);
                enabled = false;
                return;
            }

            if (stageMap.Blocks == null || stageMap.Blocks.Count == 0)
                stageMap.GenerateStage(runState.StageIndex);
            BuildCurrentStage();
        }

        private void Update()
        {
            if (_runComplete || player == null || stageMap == null)
                return;
            if (GameplayPauseService.Instance != null && GameplayPauseService.Instance.IsPaused)
                return;

            var blockIndex = ResolveBlockIndex(player.position);
            if (blockIndex >= 0 && blockIndex != _currentBlockIndex)
            {
                _currentBlockIndex = blockIndex;
                EnterBlock(blockIndex);
            }

            CheckEncounterClears();
            CheckStageExitInput();
        }

        private void ResolveReferences()
        {
            if (_context != null)
            {
                runState ??= _context.RunState;
                stageMap ??= _context.StageMap;
            }

            runState ??= RogueliteRunState.Instance;
            if (player == null)
            {
                var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
                for (var i = 0; i < entities.Length; i++)
                {
                    if (entities[i] != null && entities[i].Team == Team.Player)
                    {
                        player = entities[i].transform;
                        break;
                    }
                }
            }
        }

        private bool CanBuild()
        {
            return player != null && runState != null && stageMap != null &&
                   enemyTemplates != null && enemyTemplates.Length >= 4 &&
                   treasureTemplates != null && treasureTemplates.Length >= 3;
        }

        private void BuildCurrentStage()
        {
            TearDownStage();
            _runtimeBlocks.Clear();
            _currentBlockIndex = -1;
            _exitReady = false;

            _stageRoot = new GameObject($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            _gridOrigin = new Vector3(
                -(stageMap.Width - 1) * ChunkWidth * 0.5f,
                0f,
                -(stageMap.Height - 1) * ChunkDepth * 0.5f);

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                var center = GetChunkCenter(data.Coordinate);
                var blockRoot = BuildChunk(data, center, _stageRoot.transform);
                var content = new GameObject("RuntimeContent");
                content.transform.SetParent(blockRoot.transform, false);
                _runtimeBlocks.Add(new BlockRuntime
                {
                    Data = data,
                    Root = blockRoot,
                    ContentRoot = content.transform
                });
            }

            BuildGameplaySafetyDeck();
            BuildOuterBounds();
            BuildNavigationGraph();
            TeleportPlayer(GetChunkCenter(Vector2Int.zero) + new Vector3(0f, 0.08f, 0f));
            _currentBlockIndex = 0;
            EnterBlock(0);
            _context?.SetStageRoot(_stageRoot.transform);

            Debug.Log(
                $"[ArknightsACT/StageRuntime] Physical Stage {stageMap.StageIndex} built: " +
                $"{stageMap.Width}x{stageMap.Height}, chunks={stageMap.Blocks.Count}, chunkSize={ChunkWidth}x{ChunkDepth}.",
                this);
        }

        /// <summary>
        /// Keeps the generated town physically continuous even if a block floor is temporarily
        /// disabled, rebuilt, or exposed to a tiny seam at a chunk boundary. This is collision
        /// only: visual floors and road dressing remain owned by their normal block roots.
        /// </summary>
        private void BuildGameplaySafetyDeck()
        {
            var width = Mathf.Max(1, stageMap.Width) * ChunkWidth;
            var depth = Mathf.Max(1, stageMap.Height) * ChunkDepth;
            var deck = new GameObject("[GameplaySafetyDeck]");
            deck.transform.SetParent(_stageRoot.transform, false);

            var collider = deck.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.14f, 0f);
            collider.size = new Vector3(width, 0.28f, depth);
        }

        private void TearDownStage()
        {
            if (_stageRoot == null)
                return;
            _context?.SetStageRoot(null);
            _stageRoot.SetActive(false);
            Destroy(_stageRoot);
            _stageRoot = null;
            _exitMarker = null;
        }

        private GameObject BuildChunk(RogueliteBlockState block, Vector3 center, Transform parent)
        {
            var root = new GameObject($"Block_{block.Index:00}_{block.Type}_{block.Theme}");
            root.transform.SetParent(parent, true);
            root.transform.position = center;

            var floorMaterial = block.Theme == RogueliteChunkTheme.Facility ? sidewalkMaterial : groundMaterial;
            CreateBlock(root.transform, "Floor", new Vector3(0f, -0.14f, 0f), new Vector3(ChunkWidth, 0.28f, ChunkDepth), floorMaterial, true);
            CreateVisual(root.transform, "ChunkInset", new Vector3(0f, 0.012f, 0f), new Vector3(ChunkWidth - 0.24f, 0.024f, ChunkDepth - 0.24f), ResolveInsetMaterial(block));

            switch (block.Theme)
            {
                case RogueliteChunkTheme.Street:
                    BuildStreetChunk(root.transform);
                    break;
                case RogueliteChunkTheme.CoverLane:
                    BuildCoverLaneChunk(root.transform);
                    break;
                case RogueliteChunkTheme.Facility:
                    BuildFacilityChunk(root.transform);
                    break;
                case RogueliteChunkTheme.SafePlaza:
                    BuildSafePlazaChunk(root.transform, block.Type == RogueliteBlockType.Shop);
                    break;
                case RogueliteChunkTheme.BossArena:
                    BuildBossArenaChunk(root.transform);
                    break;
                default:
                    BuildOpenChunk(root.transform);
                    break;
            }

            CreateVisual(root.transform, "BlockMarker", new Vector3(-ChunkWidth * 0.5f + 0.38f, 0.04f, -ChunkDepth * 0.5f + 0.38f), new Vector3(0.55f, 0.055f, 0.55f), accentMaterial);
            return root;
        }

        private Material ResolveInsetMaterial(RogueliteBlockState block)
        {
            return block.Theme switch
            {
                RogueliteChunkTheme.Street => roadMaterial,
                RogueliteChunkTheme.Facility => facilityFloorMaterial,
                RogueliteChunkTheme.SafePlaza => sidewalkMaterial,
                RogueliteChunkTheme.BossArena => roadMaterial,
                _ => groundMaterial
            };
        }

        private void BuildOpenChunk(Transform root)
        {
            CreateCover(root, new Vector3(-9.2f, 0f, 6.8f), new Vector2(2.10f, 0.72f), 12f);
            CreateCover(root, new Vector3(8.6f, 0f, -7.0f), new Vector2(1.70f, 0.92f), -12f);
        }

        private void BuildStreetChunk(Transform root)
        {
            // CityStreets owns the actual carriageway. Keep only two small lot covers here so the
            // runtime layer does not draw a second arbitrary road across the authored junction.
            CreateCover(root, new Vector3(-10.0f, 0f, 7.2f), new Vector2(2.15f, 0.72f), 0f);
            CreateCover(root, new Vector3(9.8f, 0f, -7.1f), new Vector2(1.85f, 0.72f), 0f);
        }

        private void BuildCoverLaneChunk(Transform root)
        {
            CreateCover(root, new Vector3(-9.8f, 0f, -7.2f), new Vector2(2.8f, 0.68f), 90f);
            CreateCover(root, new Vector3(-3.2f, 0f, 6.9f), new Vector2(2.5f, 0.68f), 0f);
            CreateCover(root, new Vector3(10.0f, 0f, -7.0f), new Vector2(2.35f, 0.68f), 90f);
        }

        private void BuildSafePlazaChunk(Transform root, bool shop)
        {
            CreateVisual(root, "PlazaPad", Vector3.zero, new Vector3(25.8f, 0.035f, 19.8f), sidewalkMaterial);
            if (!shop)
                return;

            CreateBlock(root, "ShopCounter", new Vector3(2.4f, CoverHeight * 0.5f, 1.9f), new Vector3(3.2f, CoverHeight, 0.72f), coverMaterial, true);
            CreateVisual(root, "ShopAccent", new Vector3(2.4f, CoverHeight + 0.08f, 1.9f), new Vector3(2.9f, 0.08f, 0.56f), accentMaterial);
        }

        private void BuildBossArenaChunk(Transform root)
        {
            CreateVisual(root, "ArenaPad", Vector3.zero, new Vector3(ChunkWidth - 0.60f, 0.035f, ChunkDepth - 0.60f), roadMaterial);
            CreateCover(root, new Vector3(-10.2f, 0f, 7.6f), new Vector2(1.65f, 0.72f), 0f);
            CreateCover(root, new Vector3(10.0f, 0f, -7.4f), new Vector2(1.65f, 0.72f), 0f);

            _exitMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _exitMarker.name = "NextStageEntrance";
            _exitMarker.transform.SetParent(root, false);
            _exitMarker.transform.localPosition = new Vector3(11.0f, 0.06f, 9.15f);
            _exitMarker.transform.localScale = new Vector3(1.15f, 0.06f, 1.15f);
            var collider = _exitMarker.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
            var renderer = _exitMarker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = accentMaterial;
            _exitMarker.SetActive(false);
        }

        private void BuildFacilityChunk(Transform root)
        {
            var parent = new GameObject("TwoFloorFacility").transform;
            parent.SetParent(root, false);

            const float centerX = 2.15f;
            const float centerZ = 1.45f;
            const float width = 8.50f;
            const float depth = 6.80f;
            const float slab = 0.18f;
            const float wallHeight = 1.86f;

            CreateBlock(parent, "Floor_Ground", new Vector3(centerX, 0.09f, centerZ), new Vector3(width, slab, depth), facilityFloorMaterial, true);
            CreateBlock(parent, "Floor_Second", new Vector3(centerX, SecondFloorY - slab * 0.5f, centerZ), new Vector3(width, slab, depth), facilityFloorMaterial, true);

            var groundWallY = wallHeight * 0.5f;
            CreateBlock(parent, "Wall_North", new Vector3(centerX, groundWallY, centerZ + depth * 0.5f), new Vector3(width, wallHeight, 0.24f), facilityWallMaterial, true);
            CreateBlock(parent, "Wall_East", new Vector3(centerX + width * 0.5f, groundWallY, centerZ), new Vector3(0.24f, wallHeight, depth), facilityWallMaterial, true);
            CreateBlock(parent, "Column_SW", new Vector3(centerX - width * 0.5f, groundWallY, centerZ - depth * 0.5f + 0.45f), new Vector3(0.42f, wallHeight, 0.90f), facilityWallMaterial, true);

            const float railHeight = 0.78f;
            var railY = SecondFloorY + railHeight * 0.5f;
            CreateBlock(parent, "Rail_North", new Vector3(centerX, railY, centerZ + depth * 0.5f), new Vector3(width, railHeight, 0.22f), facilityWallMaterial, true);
            CreateBlock(parent, "Rail_East", new Vector3(centerX + width * 0.5f, railY, centerZ), new Vector3(0.22f, railHeight, depth), facilityWallMaterial, true);

            CreateRamp(parent, "AccessRamp", new Vector3(-1.25f, 0.06f, -3.45f), new Vector3(-1.25f, SecondFloorY + 0.04f, 2.75f), 0.92f, facilityFloorMaterial);
            CreateBlock(parent, "RampLanding", new Vector3(-0.70f, SecondFloorY - 0.07f, 2.72f), new Vector3(1.30f, 0.14f, 1.05f), facilityFloorMaterial, true);
            CreateCover(parent, new Vector3(2.0f, SecondFloorY, 1.25f), new Vector2(1.45f, 0.70f), 0f);

            CreateVisual(parent, "FacilityAccent", new Vector3(centerX - 0.3f, 1.50f, centerZ - depth * 0.5f - 0.13f), new Vector3(2.5f, 0.16f, 0.05f), accentMaterial);
        }

        private void CreateCover(Transform parent, Vector3 localBase, Vector2 footprint, float yaw)
        {
            var coverRoot = new GameObject("GridCover");
            coverRoot.transform.SetParent(parent, false);
            coverRoot.transform.localPosition = localBase;
            coverRoot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            CreateBlock(coverRoot.transform, "Body", new Vector3(0f, CoverHeight * 0.5f, 0f), new Vector3(footprint.x, CoverHeight, footprint.y), coverMaterial, true);
            CreateVisual(coverRoot.transform, "TopTrim", new Vector3(0f, CoverHeight + 0.025f, 0f), new Vector3(footprint.x * 0.94f, 0.05f, footprint.y * 0.94f), accentMaterial);
            CreateVisual(coverRoot.transform, "HazardBand", new Vector3(0f, CoverHeight * 0.58f, -footprint.y * 0.5f - 0.012f), new Vector3(footprint.x * 0.68f, 0.15f, 0.02f), hazardMaterial != null ? hazardMaterial : accentMaterial);
        }

        private void BuildOuterBounds()
        {
            var totalWidth = stageMap.Width * ChunkWidth;
            var totalDepth = stageMap.Height * ChunkDepth;
            var wallMaterial = facilityWallMaterial != null ? facilityWallMaterial : coverMaterial;
            CreateBlock(_stageRoot.transform, "Bound_N", new Vector3(0f, 0.75f, totalDepth * 0.5f), new Vector3(totalWidth + 0.4f, 1.5f, 0.30f), wallMaterial, true);
            CreateBlock(_stageRoot.transform, "Bound_S", new Vector3(0f, 0.75f, -totalDepth * 0.5f), new Vector3(totalWidth + 0.4f, 1.5f, 0.30f), wallMaterial, true);
            CreateBlock(_stageRoot.transform, "Bound_E", new Vector3(totalWidth * 0.5f, 0.75f, 0f), new Vector3(0.30f, 1.5f, totalDepth), wallMaterial, true);
            CreateBlock(_stageRoot.transform, "Bound_W", new Vector3(-totalWidth * 0.5f, 0.75f, 0f), new Vector3(0.30f, 1.5f, totalDepth), wallMaterial, true);
        }

        private void BuildNavigationGraph()
        {
            var navObject = new GameObject("[Navigation25D]");
            navObject.transform.SetParent(_stageRoot.transform, false);
            var graph = navObject.AddComponent<PrototypeNavigationGraph25D>();
            var nodes = new List<Vector3>(stageMap.Blocks.Count * 7);
            var edges = new List<int>(stageMap.Blocks.Count * 14);
            var cells = new NavCell[stageMap.Blocks.Count];
            var roadReachX = ChunkWidth * 0.5f - RogueliteStageWorldMetrics.MainRoadWidth * 0.25f;
            var roadReachZ = ChunkDepth * 0.5f - RogueliteStageWorldMetrics.MainRoadWidth * 0.25f;

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var block = stageMap.Blocks[i];
                var center = GetChunkCenter(block.Coordinate);
                var nav = new NavCell
                {
                    Center = AddNavNode(nodes, center),
                    West = AddNavNode(nodes, center + new Vector3(-roadReachX, 0f, 0f)),
                    East = AddNavNode(nodes, center + new Vector3(roadReachX, 0f, 0f)),
                    South = AddNavNode(nodes, center + new Vector3(0f, 0f, -roadReachZ)),
                    North = AddNavNode(nodes, center + new Vector3(0f, 0f, roadReachZ))
                };
                cells[i] = nav;
                AddEdge(edges, nav.Center, nav.West);
                AddEdge(edges, nav.Center, nav.East);
                AddEdge(edges, nav.Center, nav.South);
                AddEdge(edges, nav.Center, nav.North);

                var district = RogueliteStageDistrictTemplateController.ResolveDistrict(block, i, stageMap.StageIndex);
                if (block.Theme == RogueliteChunkTheme.Facility || district == ChernobogDistrictType.Industrial)
                {
                    var rampBase = AddNavNode(nodes, center + new Vector3(-1.25f, 0f, -3.45f));
                    var rampTop = AddNavNode(nodes, center + new Vector3(-1.25f, SecondFloorY, 2.75f));
                    var upper = AddNavNode(nodes, center + new Vector3(2.0f, SecondFloorY, 1.35f));
                    AddEdge(edges, nav.Center, rampBase);
                    AddEdge(edges, rampBase, rampTop);
                    AddEdge(edges, rampTop, upper);
                }

                if (district == ChernobogDistrictType.Residential)
                {
                    // Matches WalkInStreetTenement in PlayableArchitecture: the front doorway is
                    // north of the south-west lot, while the ramp returns to the main street spine.
                    var entry = AddNavNode(nodes, center + new Vector3(-7.40f, 0f, -4.23f));
                    var rampBase = AddNavNode(nodes, center + new Vector3(-7.40f, 0f, -2.75f));
                    var rampTop = AddNavNode(nodes, center + new Vector3(-7.40f, SecondFloorY, -5.02f));
                    var upper = AddNavNode(nodes, center + new Vector3(-7.40f, SecondFloorY, -5.36f));
                    AddEdge(edges, nav.Center, entry);
                    AddEdge(edges, nav.Center, rampBase);
                    AddEdge(edges, entry, rampBase);
                    AddEdge(edges, rampBase, rampTop);
                    AddEdge(edges, rampTop, upper);
                }
                else if (district == ChernobogDistrictType.Commercial)
                {
                    // Matches the open-front commercial room at local (7, 0.9). The doorway and
                    // counter are both on the ground plane, so enemies can route through the room.
                    var entry = AddNavNode(nodes, center + new Vector3(7.00f, 0f, 2.83f));
                    var interior = AddNavNode(nodes, center + new Vector3(7.00f, 0f, 0.40f));
                    AddEdge(edges, nav.Center, entry);
                    AddEdge(edges, entry, interior);
                }
                else if (district == ChernobogDistrictType.Checkpoint)
                {
                    var shelter = AddNavNode(nodes, center + new Vector3(5.90f, 0f, 2.25f));
                    AddEdge(edges, nav.Center, shelter);
                }
                else if (ShouldBuildStreetTenement(block, i))
                {
                    var side = ResolvePlayableBuildingSide(i, block);
                    var entry = AddNavNode(nodes, center + new Vector3(side * 4.55f, 0f, -3.25f));
                    var rampBase = AddNavNode(nodes, center + new Vector3(side * 4.55f, 0f, -1.80f));
                    var rampTop = AddNavNode(nodes, center + new Vector3(side * 4.55f, SecondFloorY, -4.07f));
                    var upper = AddNavNode(nodes, center + new Vector3(side * 4.55f, SecondFloorY, -4.66f));
                    AddEdge(edges, nav.Center, entry);
                    AddEdge(edges, nav.Center, rampBase);
                    AddEdge(edges, entry, rampBase);
                    AddEdge(edges, rampBase, rampTop);
                    AddEdge(edges, rampTop, upper);
                }
                else if (ShouldBuildInteractiveDeck(block, i))
                {
                    var side = ResolvePlayableBuildingSide(i, block);
                    var rampBase = AddNavNode(nodes, center + new Vector3(side * 4.25f, 0f, -3.15f));
                    var deckTop = AddNavNode(nodes, center + new Vector3(side * 4.25f, 1.07f, -4.15f));
                    var deckInner = AddNavNode(nodes, center + new Vector3(side * 4.25f, 1.05f, -5.25f));
                    AddEdge(edges, nav.Center, rampBase);
                    AddEdge(edges, rampBase, deckTop);
                    AddEdge(edges, deckTop, deckInner);
                }
            }

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var block = stageMap.Blocks[i];
                var c = block.Coordinate;
                if (c.x + 1 < stageMap.Width)
                {
                    var eastIndex = c.y * stageMap.Width + c.x + 1;
                    AddEdge(edges, cells[i].East, cells[eastIndex].West);
                }
                if (c.y + 1 < stageMap.Height)
                {
                    var northIndex = (c.y + 1) * stageMap.Width + c.x;
                    AddEdge(edges, cells[i].North, cells[northIndex].South);
                }
            }

            graph.Configure(nodes.ToArray(), edges.ToArray());
        }

        private bool ShouldBuildStreetTenement(RogueliteBlockState block, int blockIndex)
        {
            if (block == null || block.Theme != RogueliteChunkTheme.Street)
                return false;
            return PositiveMod(stageMap.StageIndex * 11 + blockIndex * 7 + (int)block.Theme, 3) == 0;
        }

        private bool ShouldBuildInteractiveDeck(RogueliteBlockState block, int blockIndex)
        {
            if (block == null || block.Theme != RogueliteChunkTheme.CoverLane)
                return false;
            return PositiveMod(stageMap.StageIndex * 13 + blockIndex * 5 + (int)block.Theme, 2) == 0;
        }

        private float ResolvePlayableBuildingSide(int blockIndex, RogueliteBlockState block)
        {
            return PositiveMod(stageMap.StageIndex * 5 + blockIndex * 3 + (int)block.Theme, 2) == 1 ? 1f : -1f;
        }

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static int AddNavNode(List<Vector3> nodes, Vector3 position)
        {
            nodes.Add(position);
            return nodes.Count - 1;
        }

        private static void AddEdge(List<int> edges, int a, int b)
        {
            edges.Add(a);
            edges.Add(b);
        }

        private void EnterBlock(int blockIndex)
        {
            if (blockIndex < 0 || blockIndex >= _runtimeBlocks.Count)
                return;
            var runtime = _runtimeBlocks[blockIndex];
            stageMap.MarkExplored(blockIndex);

            if (!runtime.ContentsSpawned)
            {
                runtime.ContentsSpawned = true;
                SpawnTreasure(runtime);
                SpawnEncounter(runtime);
            }

            Debug.Log(
                $"[ArknightsACT/StageRuntime] Enter block {runtime.Data.Index}: " +
                $"type={runtime.Data.Type}, theme={runtime.Data.Theme}.",
                this);
        }

        private void SpawnTreasure(BlockRuntime runtime)
        {
            if (runtime.Data.HasNormalChest)
                SpawnTreasureTemplate(0, runtime, new Vector3(-5.7f, 0.03f, 3.9f), "Normal");
            if (runtime.Data.HasSpikeChest)
                SpawnTreasureTemplate(1, runtime, new Vector3(5.5f, 0.03f, 3.7f), "Spike");
            if (runtime.Data.HasMonsterChest)
                SpawnTreasureTemplate(2, runtime, new Vector3(5.5f, 0.03f, 3.7f), "Monster");
        }

        private void SpawnTreasureTemplate(int templateIndex, BlockRuntime runtime, Vector3 localOffset, string label)
        {
            if (treasureTemplates == null || templateIndex < 0 || templateIndex >= treasureTemplates.Length)
                return;
            var template = treasureTemplates[templateIndex];
            if (template == null)
                return;

            var instance = Instantiate(template, runtime.ContentRoot);
            instance.name = $"Block_{runtime.Data.Index:00}_Treasure_{label}";
            instance.transform.position = GetChunkCenter(runtime.Data.Coordinate) + localOffset;
            instance.transform.rotation = Quaternion.identity;
            instance.SetActive(true);
        }

        private void SpawnEncounter(BlockRuntime runtime)
        {
            var type = runtime.Data.Type;
            if (type == RogueliteBlockType.Start || type == RogueliteBlockType.Shop)
                return;

            runtime.EncounterSpawned = true;
            var center = GetChunkCenter(runtime.Data.Coordinate);
            if (type == RogueliteBlockType.Boss)
            {
                SpawnEnemy(runtime, 3, center + new Vector3(0f, 0.03f, 1.4f), ResolveBossHealthMultiplier(), 2.0f, 0);
                if (stageMap.StageIndex >= 2)
                    SpawnEnemy(runtime, stageMap.StageIndex == 2 ? 1 : 2, center + new Vector3(-4.1f, 0.03f, -2.8f), 1f + 0.18f * (stageMap.StageIndex - 1), 1.25f, 1);
                return;
            }

            var emergency = type == RogueliteBlockType.EmergencyCombat;
            var baseCount = 2 + stageMap.StageIndex;
            var enemyCount = Mathf.Clamp(baseCount + (emergency ? 1 : 0), 3, 6);
            var healthMultiplier = (1f + (stageMap.StageIndex - 1) * 0.22f) * (emergency ? 1.35f : 1f);
            var experienceMultiplier = emergency ? 1.5f : 1f;
            var offsets = ResolveSpawnOffsets(runtime.Data.Theme);

            for (var i = 0; i < enemyCount; i++)
            {
                var available = stageMap.StageIndex == 1 && !emergency ? 2 : 3;
                var templateIndex = i % Mathf.Min(available, enemyTemplates.Length);
                SpawnEnemy(runtime, templateIndex, center + offsets[i % offsets.Length], healthMultiplier, experienceMultiplier, i);
            }
        }

        private void SpawnEnemy(BlockRuntime runtime, int templateIndex, Vector3 position, float healthMultiplier, float expMultiplier, int serial)
        {
            if (enemyTemplates == null || templateIndex < 0 || templateIndex >= enemyTemplates.Length)
                return;
            var template = enemyTemplates[templateIndex];
            if (template == null)
                return;

            var instance = Instantiate(template, position, Quaternion.identity);
            instance.name = $"Stage{stageMap.StageIndex}_Block{runtime.Data.Index:00}_{template.name}_{serial + 1}";
            instance.transform.SetParent(runtime.ContentRoot, true);

            var health = instance.GetComponent<Health>();
            if (health != null)
                health.SetMaxHealth(health.MaxHealth * Mathf.Max(0.1f, healthMultiplier));
            var experience = instance.GetComponent<EnemyExperienceReward>();
            if (experience != null)
                experience.SetRewardMultiplier(expMultiplier);

            instance.SetActive(true);
            IgnoreActorCollision(instance, player != null ? player.gameObject : null);
            for (var i = 0; i < runtime.Enemies.Count; i++)
            {
                var previous = runtime.Enemies[i];
                if (previous != null)
                    IgnoreActorCollision(instance, previous.gameObject);
            }

            var entity = instance.GetComponent<CombatEntity>();
            if (entity != null)
                runtime.Enemies.Add(entity);
        }

        private float ResolveBossHealthMultiplier()
        {
            return stageMap.StageIndex switch
            {
                1 => 2.0f,
                2 => 2.4f,
                _ => 2.8f
            };
        }

        private static Vector3[] ResolveSpawnOffsets(RogueliteChunkTheme theme)
        {
            if (theme == RogueliteChunkTheme.Facility)
            {
                return new[]
                {
                    new Vector3(-10.5f, 0.03f, 0.6f),
                    new Vector3(-8.0f, 0.03f, -8.9f),
                    new Vector3(0.0f, 0.03f, -8.7f),
                    new Vector3(8.6f, 0.03f, -7.1f),
                    new Vector3(-9.0f, 0.03f, 7.5f),
                    new Vector3(9.2f, 0.03f, 8.1f)
                };
            }

            if (theme == RogueliteChunkTheme.CoverLane)
            {
                return new[]
                {
                    new Vector3(-13.0f, 0.03f, -6.0f),
                    new Vector3(-10.0f, 0.03f, 0.0f),
                    new Vector3(-6.0f, 0.03f, 7.0f),
                    new Vector3(0.0f, 0.03f, -7.2f),
                    new Vector3(1.0f, 0.03f, 9.0f),
                    new Vector3(11.0f, 0.03f, -5.4f)
                };
            }

            if (theme == RogueliteChunkTheme.Street)
            {
                return new[]
                {
                    new Vector3(-12.0f, 0.03f, -1.0f),
                    new Vector3(0.0f, 0.03f, -7.8f),
                    new Vector3(5.0f, 0.03f, -6.0f),
                    new Vector3(10.0f, 0.03f, 3.0f),
                    new Vector3(-1.0f, 0.03f, 8.0f),
                    new Vector3(11.0f, 0.03f, 8.0f)
                };
            }

            // Keep encounter spawns on the street spine. South-edge rooms/ramps and north-edge
            // frontages are allowed to be entered by the player, but must never receive an enemy
            // inside their wall volume before the presentation architecture has finished building.
            return new[]
            {
                new Vector3(-10.5f, 0.03f, -1.2f),
                new Vector3(-3.4f, 0.03f, -7.4f),
                new Vector3(3.6f, 0.03f, -1.6f),
                new Vector3(10.4f, 0.03f, 2.8f),
                new Vector3(-7.2f, 0.03f, 7.3f),
                new Vector3(4.4f, 0.03f, 8.4f)
            };
        }

        private void CheckEncounterClears()
        {
            for (var i = 0; i < _runtimeBlocks.Count; i++)
            {
                var runtime = _runtimeBlocks[i];
                if (!runtime.EncounterSpawned || runtime.EncounterCleared || runtime.Enemies.Count == 0)
                    continue;

                var living = 0;
                for (var e = runtime.Enemies.Count - 1; e >= 0; e--)
                {
                    var entity = runtime.Enemies[e];
                    if (entity == null)
                    {
                        runtime.Enemies.RemoveAt(e);
                        continue;
                    }
                    if (entity.Health != null && !entity.Health.IsDead)
                        living++;
                }

                if (living == 0)
                {
                    runtime.EncounterCleared = true;
                    ResolveEncounterReward(runtime);
                }
            }
        }

        private void ResolveEncounterReward(BlockRuntime runtime)
        {
            var type = runtime.Data.Type;
            var boss = type == RogueliteBlockType.Boss;
            var emergency = type == RogueliteBlockType.EmergencyCombat;
            runState.RecordCombatClear(boss, emergency);

            if (boss)
            {
                runState.AddIngots(8);
                OpenCollectibleReward(
                    $"第 {stageMap.StageIndex} 关首领奖励",
                    CollectibleRarity.Rare,
                    3,
                    CompleteBossBlock);
                return;
            }

            if (emergency)
            {
                runState.AddIngots(4);
                OpenCollectibleReward("紧急作战 · 藏品二选一", CollectibleRarity.Rare, 2, null);
                return;
            }

            runState.AddIngots(2);
            if (UnityEngine.Random.value < 0.20f)
                OpenCollectibleReward("普通作战 · 意外收获", CollectibleRarity.Common, 2, null);
        }

        private void OpenCollectibleReward(string title, CollectibleRarity rarity, int choices, Action completed)
        {
            if (rewards != null && rewards.OpenReward(title, rarity, choices, completed))
                return;
            completed?.Invoke();
        }

        private void CompleteBossBlock()
        {
            if (stageMap.StageIndex >= 3)
            {
                _runComplete = true;
                _exitReady = false;
                if (_exitMarker != null)
                    _exitMarker.SetActive(true);
                Debug.Log("[ArknightsACT/StageRuntime] Final boss defeated. Prototype run complete.", this);
                return;
            }

            _exitReady = true;
            if (_exitMarker != null)
                _exitMarker.SetActive(true);
            Debug.Log($"[ArknightsACT/StageRuntime] Stage {stageMap.StageIndex} clear. Next-stage entrance opened.", this);
        }

        private void CheckStageExitInput()
        {
            if (!_exitReady || _exitMarker == null || player == null)
                return;

            var delta = player.position - _exitMarker.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 2.1f * 2.1f)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                AdvanceStage();
        }

        private void AdvanceStage()
        {
            if (stageMap.StageIndex >= 3)
                return;

            var next = stageMap.StageIndex + 1;
            runState.SetStageIndex(next);
            stageMap.GenerateStage(next);
            BuildCurrentStage();
        }

        private int ResolveBlockIndex(Vector3 worldPosition)
        {
            var x = Mathf.FloorToInt((worldPosition.x - (_gridOrigin.x - ChunkWidth * 0.5f)) / ChunkWidth);
            var y = Mathf.FloorToInt((worldPosition.z - (_gridOrigin.z - ChunkDepth * 0.5f)) / ChunkDepth);
            if (x < 0 || y < 0 || x >= stageMap.Width || y >= stageMap.Height)
                return -1;
            return y * stageMap.Width + x;
        }

        private Vector3 GetChunkCenter(Vector2Int coordinate)
        {
            return _gridOrigin + new Vector3(coordinate.x * ChunkWidth, 0f, coordinate.y * ChunkDepth);
        }

        private void TeleportPlayer(Vector3 position)
        {
            if (player == null)
                return;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            player.position = position;
            if (controller != null)
                controller.enabled = true;
            player.GetComponent<PlayerMotor25D>()?.ResetMotion();
        }

        private static void IgnoreActorCollision(GameObject a, GameObject b)
        {
            if (a == null || b == null)
                return;
            var aColliders = a.GetComponentsInChildren<Collider>(true);
            var bColliders = b.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < aColliders.Length; i++)
            for (var j = 0; j < bColliders.Length; j++)
            {
                if (aColliders[i] != null && bColliders[j] != null)
                    Physics.IgnoreCollision(aColliders[i], bColliders[j], true);
            }
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

        private static void CreateRamp(Transform parent, string name, Vector3 localStart, Vector3 localEnd, float width, Material material)
        {
            var delta = localEnd - localStart;
            var length = delta.magnitude;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (localStart + localEnd) * 0.5f;
            go.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            go.transform.localScale = new Vector3(width, 0.16f, length);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
        }

        private void OnGUI()
        {
            if (stageMap == null || runState == null)
                return;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                padding = new RectOffset(12, 12, 8, 8)
            };
            var blockText = _currentBlockIndex >= 0 && _currentBlockIndex < _runtimeBlocks.Count
                ? $"区块 {_currentBlockIndex + 1}/{_runtimeBlocks.Count} · {_runtimeBlocks[_currentBlockIndex].Data.Type}"
                : "区块外";
            GUI.Box(new Rect(Screen.width - 285f, 18f, 265f, 58f), $"第 {stageMap.StageIndex}/3 关\n{blockText}", style);

            if (_currentBlockIndex >= 0 && _currentBlockIndex < _runtimeBlocks.Count &&
                _runtimeBlocks[_currentBlockIndex].Data.Type == RogueliteBlockType.Shop)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 165f, 24f, 330f, 38f), "商店区块 · 商品购买/刷新下一步接入");
            }

            if (_exitReady && _exitMarker != null && player != null)
            {
                var delta = player.position - _exitMarker.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= 2.1f * 2.1f)
                    GUI.Box(new Rect(Screen.width * 0.5f - 155f, Screen.height - 82f, 310f, 44f), "按 E 进入下一关");
            }

            if (_runComplete)
                GUI.Box(new Rect(Screen.width * 0.5f - 190f, 80f, 380f, 58f), "最终首领已击败 · 本次 Run 完成");
        }
    }
}
