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
        private const float SecondFloorY = 2.10f;

        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private ChernobogEnvironmentKit kit;

        private RogueliteStageRuntimeContext _context;
        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map, ChernobogEnvironmentKit environmentKit)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
            kit = environmentKit;
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context == null)
                return;

            stageMap ??= _context.StageMap;
            kit ??= _context.EnvironmentKit;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.12f;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            kit ??= _context.EnvironmentKit;
            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
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

                var marker = block.GetComponent<RogueliteDistrictTemplate25D>();
                var district = marker != null
                    ? marker.DistrictType
                    : RogueliteStageDistrictTemplateController.ResolveDistrict(data, i, stageMap.StageIndex);

                switch (district)
                {
                    case ChernobogDistrictType.Residential:
                        // Bottom-left is the residential quadrant in Scheme 1. Keep the footprint
                        // compact, but give it a real ground entrance, exterior ramp and visible
                        // second floor so it reads as a place rather than a cover cube.
                        BuildStreetTenement(cell, new Vector3(-7.40f, 0f, -5.80f), 1f, i);
                        break;
                    case ChernobogDistrictType.Commercial:
                        // The market row is the street-facing landmark. This smaller open unit is
                        // set back in the same lot and supplies an actual walk-in combat room.
                        BuildWalkInCommercialUnit(cell, new Vector3(7.00f, 0f, 0.90f), i);
                        break;
                    case ChernobogDistrictType.Industrial:
                        // TwoFloorFacility from StageRuntime is the authoritative facility interior.
                        break;
                    case ChernobogDistrictType.Checkpoint:
                        // The gatehouse is a visual landmark; the canopy is an open, traversable
                        // inspection shelter that gives the boss quadrant a second readable layer.
                        BuildCoveredCheckpoint(cell, new Vector3(5.90f, 0f, 2.70f), -1f, i + 79);
                        break;
                    default:
                        // Preserve the older theme-driven placements for non-production maps.
                        var xSide = ResolveBuildingSide(i, data);
                        switch (data.Theme)
                        {
                            case RogueliteChunkTheme.Street:
                                if (ShouldBuildInteractiveStreetBuilding(data, i))
                                    BuildStreetTenement(cell, new Vector3(xSide * 4.55f, 0f, -4.85f), xSide, i);
                                break;
                            case RogueliteChunkTheme.CoverLane:
                                if (ShouldBuildInteractiveDeck(data, i))
                                    BuildRaisedLoadingDeck(cell, new Vector3(xSide * 4.25f, 0f, -4.80f), -xSide, i);
                                break;
                            case RogueliteChunkTheme.Facility:
                                break;
                            case RogueliteChunkTheme.SafePlaza:
                                if (data.Type == RogueliteBlockType.Shop)
                                    BuildCoveredCheckpoint(cell, new Vector3(xSide * 4.70f, 0f, -4.75f), xSide, i);
                                break;
                            case RogueliteChunkTheme.BossArena:
                                BuildCoveredCheckpoint(cell, new Vector3(4.75f, 0f, -4.70f), -1f, i + 79);
                                break;
                            default:
                                if (ShouldBuildInteractiveStreetBuilding(data, i))
                                    BuildWalkInServiceRoom(cell, new Vector3(xSide * 4.50f, 0f, -4.90f), xSide, i);
                                break;
                        }
                        break;
                }
            }
        }

        private bool ShouldBuildInteractiveStreetBuilding(RogueliteBlockState data, int blockIndex)
        {
            if (data == null || data.Theme != RogueliteChunkTheme.Street)
                return false;

            // Only a few street blocks become enterable tenements. The rest remain readable open road,
            // which keeps the town legible and leaves the encounter space available for combat.
            return PositiveMod(stageMap.StageIndex * 11 + blockIndex * 7 + (int)data.Theme, 3) == 0;
        }

        private bool ShouldBuildInteractiveDeck(RogueliteBlockState data, int blockIndex)
        {
            if (data == null || data.Theme != RogueliteChunkTheme.CoverLane)
                return false;
            return PositiveMod(stageMap.StageIndex * 13 + blockIndex * 5 + (int)data.Theme, 2) == 0;
        }

        private float ResolveBuildingSide(int blockIndex, RogueliteBlockState data)
        {
            return PositiveMod(stageMap.StageIndex * 5 + blockIndex * 3 + (int)data.Theme, 2) == 1 ? 1f : -1f;
        }

        private void BuildWalkInCommercialUnit(Transform parent, Vector3 anchor, int seed)
        {
            var root = new GameObject("WalkInCommercialUnit").transform;
            root.gameObject.AddComponent<EnterableBuilding25D>().Configure(new Vector3(0f, 1.5f, 0f), new Vector3(5.0f, 3.3f, 4.1f));
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            ArknightsACT.Gameplay.Roguelite.Treasure.SearchableContainer25D.Create(root, new Vector3(-1.55f, 0.05f, -1.0f), steel, inset);
            const float width = 4.80f;
            const float depth = 3.85f;
            const float height = 2.85f;
            const float thickness = 0.22f;
            var frontZ = depth * 0.5f;

            // Three walls plus a split front header leave a central doorway wide enough for the
            // player and enemies. The low counter makes the room useful cover during a fight.
            CreateSolidBox(root, "ShopRoomBack", new Vector3(0f, height * 0.5f, -depth * 0.5f),
                new Vector3(width, height, thickness), 0.045f, wall);
            CreateSolidBox(root, "ShopRoomWall_W", new Vector3(-width * 0.5f, height * 0.5f, 0f),
                new Vector3(thickness, height, depth), 0.045f, wall);
            CreateSolidBox(root, "ShopRoomWall_E", new Vector3(width * 0.5f, height * 0.5f, 0f),
                new Vector3(thickness, height, depth), 0.045f, wall);
            CreateSolidBox(root, "ShopDoorJamb_W", new Vector3(-1.62f, 1.08f, frontZ),
                new Vector3(0.58f, 2.16f, thickness), 0.035f, steel);
            CreateSolidBox(root, "ShopDoorJamb_E", new Vector3(1.62f, 1.08f, frontZ),
                new Vector3(0.58f, 2.16f, thickness), 0.035f, steel);
            CreateSolidBox(root, "ShopDoorHeader", new Vector3(0f, 2.48f, frontZ),
                new Vector3(width, 0.28f, thickness), 0.035f, steel);
            CreateVisualBox(root, "ShopWindow_W", new Vector3(-2.02f, 1.35f, frontZ + 0.018f),
                new Vector3(0.48f, 0.72f, 0.035f), 0.006f, inset);
            CreateVisualBox(root, "ShopWindow_E", new Vector3(2.02f, 1.35f, frontZ + 0.018f),
                new Vector3(0.48f, 0.72f, 0.035f), 0.006f, inset);
            CreateSolidBox(root, "ShopCounter", new Vector3(0f, 0.48f, -0.55f),
                new Vector3(2.35f, 0.96f, 0.60f), 0.055f, grate);
            CreateSolidBox(root, "ShopAwning", new Vector3(0f, height + 0.14f, frontZ + 0.36f),
                new Vector3(width + 0.18f, 0.14f, 0.78f), 0.025f, steel);
            CreateVisualBox(root, "ShopRoofRear", new Vector3(0f, height + 0.16f, -0.52f),
                new Vector3(width + 0.16f, 0.18f, depth * 0.54f), 0.035f, steel);

            if (kit.hvacSmall != null && PositiveMod(seed, 2) == 0)
            {
                var hvac = Instantiate(kit.hvacSmall, root);
                hvac.name = "ShopRoofHVAC";
                hvac.transform.localPosition = new Vector3(1.25f, height + 0.35f, -0.55f);
                hvac.transform.localScale = Vector3.one * 0.48f;
                DisablePrefabColliders(hvac);
            }
        }

        private void BuildStreetTenement(Transform parent, Vector3 anchor, float side, int seed)
        {
            var root = new GameObject("WalkInStreetTenement").transform;
            root.gameObject.AddComponent<EnterableBuilding25D>().Configure(new Vector3(0f, 2.1f, 0f), new Vector3(4.3f, 4.4f, 3.5f));
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            ArknightsACT.Gameplay.Roguelite.Treasure.SearchableContainer25D.Create(root, new Vector3(-1.05f, 0.05f, -0.95f), steel, inset);
            const float width = 4.05f;
            const float depth = 3.15f;
            const float floorHeight = 2.10f;
            const float thickness = 0.22f;
            var frontZ = depth * 0.5f;
            var backZ = -depth * 0.5f;

            // Ground floor: three walls and a wide central doorway. The room is real cover/space,
            // rather than a solid decorative mass that the player can only look at.
            CreateSolidBox(root, "TenementGroundBack", new Vector3(0f, floorHeight * 0.5f, backZ),
                new Vector3(width, floorHeight, thickness), 0.045f, wall);
            CreateSolidBox(root, "TenementGroundWall_W", new Vector3(-width * 0.5f, floorHeight * 0.5f, 0f),
                new Vector3(thickness, floorHeight, depth), 0.045f, wall);
            CreateSolidBox(root, "TenementGroundWall_E", new Vector3(width * 0.5f, floorHeight * 0.5f, 0f),
                new Vector3(thickness, floorHeight, depth), 0.045f, wall);
            CreateSolidBox(root, "TenementDoorJamb_W", new Vector3(-1.42f, 1.00f, frontZ),
                new Vector3(0.64f, 2.00f, thickness), 0.035f, steel);
            CreateSolidBox(root, "TenementDoorJamb_E", new Vector3(1.42f, 1.00f, frontZ),
                new Vector3(0.64f, 2.00f, thickness), 0.035f, steel);
            CreateSolidBox(root, "TenementDoorHeader", new Vector3(0f, 1.98f, frontZ),
                new Vector3(width, 0.24f, thickness), 0.035f, steel);

            // A partial second floor leaves the ramp landing open at the front and makes the upper
            // level visible from the camera instead of hiding the whole building behind a roof.
            CreateSolidBox(root, "TenementSecondFloor", new Vector3(0f, SecondFloorY - 0.08f, -0.22f),
                new Vector3(width + 0.16f, 0.16f, depth * 0.72f), 0.035f, grate);
            CreateSolidBox(root, "TenementUpperBack", new Vector3(0f, SecondFloorY + 0.86f, backZ),
                new Vector3(width, 1.72f, thickness), 0.045f, wall);
            CreateSolidBox(root, "TenementUpperWall_W", new Vector3(-width * 0.5f, SecondFloorY + 0.86f, -0.22f),
                new Vector3(thickness, 1.72f, depth * 0.72f), 0.045f, wall);
            CreateSolidBox(root, "TenementUpperRail_W", new Vector3(-1.42f, SecondFloorY + 0.43f, 0.92f),
                new Vector3(1.05f, 0.66f, 0.12f), 0.025f, steel);
            CreateSolidBox(root, "TenementUpperRail_E", new Vector3(1.42f, SecondFloorY + 0.43f, 0.92f),
                new Vector3(1.05f, 0.66f, 0.12f), 0.025f, steel);

            CreateSolidBox(root, "TenementRampLanding", new Vector3(1.7f, SecondFloorY - 0.08f, 0.65f),
                new Vector3(3.7f, 0.16f, 0.7f), 0.02f, steel);
            // Exterior ramp approaches from the central street and ends on the second-floor slab.
            // Its shallow slope is climbable by the same CharacterController used by the player/enemies.
            CreateRamp(root, "TenementUpperRamp",
                new Vector3(3.0f, 0.10f, 4.20f),
                new Vector3(3.0f, SecondFloorY + 0.01f, 0.78f),
                1.05f,
                0.16f,
                steel);

            CreateVisualBox(root, "TenementUpperFloorBand", new Vector3(-side * (width * 0.5f + 0.035f), SecondFloorY + 0.12f, -0.22f),
                new Vector3(0.06f, 0.14f, depth * 0.68f), 0.008f, steel);
            CreateVisualBox(root, "TenementRoofRear", new Vector3(0f, SecondFloorY + 1.82f, -0.58f),
                new Vector3(width + 0.14f, 0.18f, depth * 0.48f), 0.035f, steel);

            if (kit.hvacSmall != null && PositiveMod(seed, 2) == 0)
            {
                var hvac = Instantiate(kit.hvacSmall, root);
                hvac.name = "TenementRoofHVAC";
                hvac.transform.localPosition = new Vector3(side * 1.15f, SecondFloorY + 2.05f, -0.58f);
                hvac.transform.localScale = Vector3.one * 0.48f;
                DisablePrefabColliders(hvac);
            }
        }

        private void BuildWalkInServiceRoom(Transform parent, Vector3 anchor, float side, int seed)
        {
            var root = new GameObject("WalkInServiceRoom").transform;
            root.gameObject.AddComponent<EnterableBuilding25D>().Configure(new Vector3(0f, 1.4f, 0f), new Vector3(3.3f, 3.0f, 3.0f));
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
            root.gameObject.AddComponent<EnterableBuilding25D>().Configure(new Vector3(0f, 1.4f, 0f), new Vector3(2.9f, 3.1f, 2.5f));
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
            return RogueliteStageBlockUtility.FindBlockTransform(stage, index);
        }

        // Street frontages delegate their physical room to this existing architecture owner.
        // Open loading bays and narrow residential doors share one collision contract.
        public static void BuildInteriorShell(Transform root, float width, float depth, float height,
            Material wall, Material steel, Material floor, float doorWidth = 1.8f)
        {
            doorWidth = Mathf.Min(doorWidth, width - 0.6f);
            var wing = (width - doorWidth) * 0.5f;
            CreateSolidBox(root, "InteriorFloor", new Vector3(0f, 0.08f, 0f),
                new Vector3(width, 0.16f, depth), 0.02f, floor);
            CreateSolidBox(root, "ShellBack", new Vector3(0f, height * 0.5f, depth * 0.5f),
                new Vector3(width, height, 0.22f), 0.04f, wall);
            foreach (var side in new[] { -1f, 1f })
            {
                CreateSolidBox(root, "ShellSide", new Vector3(side * width * 0.5f, height * 0.5f, 0f),
                    new Vector3(0.22f, height, depth), 0.04f, wall);
                CreateSolidBox(root, "ShellDoorWing", new Vector3(side * (doorWidth + wing) * 0.5f, height * 0.5f, -depth * 0.5f),
                    new Vector3(wing, height, 0.22f), 0.04f, wall);
                CreateVisualBox(root, "DoorEdge", new Vector3(side * (doorWidth * 0.5f + 0.04f), 1.1f, -depth * 0.5f - 0.13f),
                    new Vector3(0.08f, 2.2f, 0.06f), 0.01f, steel);
            }
            if (height > 2.3f)
                CreateSolidBox(root, "ShellHeader", new Vector3(0f, (height + 2.3f) * 0.5f, -depth * 0.5f),
                    new Vector3(doorWidth, height - 2.3f, 0.22f), 0.02f, wall);
            CreateVisualBox(root, "EntranceThreshold", new Vector3(0f, 0.10f, -depth * 0.5f - 0.45f),
                new Vector3(doorWidth, 0.05f, 0.85f), 0.01f, steel);
            var trim = CreateVisualBox(root, "DistrictIdentification", new Vector3(0f, 2.22f, -depth * 0.5f - 0.14f),
                new Vector3(doorWidth + 0.18f, 0.09f, 0.06f), 0.01f, steel);
            var color = root.name.Contains("Workshop") ? new Color(0.8f, 0.49f, 0.18f) : new Color(0.27f, 0.56f, 0.53f);
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            trim.GetComponent<Renderer>().SetPropertyBlock(tint);
            var label = new GameObject("BuildingStencil").AddComponent<TextMesh>();
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(-width * 0.42f, 2.55f, -depth * 0.5f - 0.15f);
            label.text = root.name.Contains("Apartment") ? "RES / 01" : root.name.Contains("Workshop") ? "IND / 03" :
                root.name.Contains("Gatehouse") ? "SEC / 04" : "TRADE / 02";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.fontSize = 40;
            label.characterSize = 0.045f;
            label.color = new Color(0.72f, 0.75f, 0.69f);
            label.anchor = TextAnchor.MiddleLeft;
            for (var i = 0; i < 3; i++)
                CreateVisualBox(root, "WallPanelJoint", new Vector3(-width * 0.3f + i * width * 0.3f, height * 0.5f, depth * 0.5f + 0.12f),
                    new Vector3(0.045f, height * 0.85f, 0.025f), 0.003f, steel);
            var marker = root.gameObject.AddComponent<EnterableBuilding25D>();
            marker.Configure(new Vector3(0f, height * 0.5f, 0f), new Vector3(width + 0.4f, height + 0.5f, depth + 0.4f));
            marker.SetNavigationRoute(new[]
            {
                root.TransformPoint(new Vector3(-width * 0.5f - 1.2f, 0.18f, -depth * 0.5f - 1.4f)),
                root.TransformPoint(new Vector3(0f, 0.18f, -depth * 0.5f - 1.4f)),
                root.TransformPoint(new Vector3(0f, 0.18f, -depth * 0.5f)),
                root.TransformPoint(new Vector3(0f, 0.18f, 0f))
            });
            ArknightsACT.Gameplay.Roguelite.Treasure.SearchableContainer25D.Create(root,
                new Vector3(-width * 0.25f, 0.18f, depth * 0.5f - 0.65f), steel, floor);
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

        private static void DisablePrefabColliders(GameObject root)
        {
            if (root == null)
                return;
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        private static int PositiveMod(int value, int divisor)
        {
            return RogueliteStageMath.PositiveMod(value, divisor);
        }
    }
}
