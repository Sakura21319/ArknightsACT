using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Builds readable city streets from explicit district templates instead of stamping the same
    /// dark cross into every cell. Roads are broad contiguous surfaces with restrained edges; alleys,
    /// plazas, yards, ruins and checkpoints deliberately use different spatial grammars.
    /// </summary>
    [DefaultExecutionOrder(23)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageCityStreetsController : MonoBehaviour
    {
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;

        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private ChernobogEnvironmentKit kit;
        private RogueliteStageRuntimeContext _context;

        private GameObject _preparedStage;
        private float _nextResolveAt;
        private Material _asphalt;
        private Material _lanePaint;
        private Texture2D _asphaltGrain;

        private void PrepareStreetMaterials()
        {
            if (_asphalt != null) return;
            var source = kit.deckHeavyMaterial != null ? kit.deckHeavyMaterial : kit.deckMaterial;
            _asphalt = new Material(source) { name = "City_Asphalt_Weathered" };
            _lanePaint = new Material(source) { name = "City_WornLanePaint" };
            foreach (var property in new[] { "_BaseColor", "_Color" })
            {
                if (_asphalt.HasProperty(property)) _asphalt.SetColor(property, new Color(0.12f, 0.14f, 0.15f));
                if (_lanePaint.HasProperty(property)) _lanePaint.SetColor(property, new Color(0.65f, 0.61f, 0.46f));
            }
            _asphaltGrain = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = "AsphaltGrain", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[64 * 64];
            for (var i = 0; i < pixels.Length; i++)
            {
                var shade = 0.76f + Hash01(i * 73) * 0.24f;
                pixels[i] = new Color(shade, shade, shade, 1f);
            }
            _asphaltGrain.SetPixels(pixels);
            _asphaltGrain.Apply(true, true);
            foreach (var property in new[] { "_BaseMap", "_MainTex" })
                if (_asphalt.HasProperty(property))
                {
                    _asphalt.SetTexture(property, _asphaltGrain);
                    _asphalt.SetTextureScale(property, new Vector2(12f, 12f));
                }
            foreach (var material in new[] { _asphalt, _lanePaint })
            {
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            }
        }
        private void OnDestroy()
        {
            if (_asphalt != null) Destroy(_asphalt);
            if (_lanePaint != null) Destroy(_lanePaint);
            if (_asphaltGrain != null) Destroy(_asphaltGrain);
        }

        public void Configure(RogueliteStageMapController map, ChernobogEnvironmentKit environmentKit)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
            kit = environmentKit;
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context != null)
            {
                stageMap ??= _context.StageMap;
                kit ??= _context.EnvironmentKit;
            }
        }

        private void Update()
        {
            if (_context != null && stageMap == null)
                stageMap = _context.StageMap;
            if (_context != null && kit == null)
                kit = _context.EnvironmentKit;

            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.12f;

            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = _context != null && _context.StageRoot != null
                ? _context.StageRoot.gameObject
                : null;
            if (stage == null || stage == _preparedStage)
                return;
            if (stage.transform.Find("[Chernobog_UrbanArchitecture]") == null)
                return;

            Build(stage.transform);
            Physics.SyncTransforms();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/CityStreets] Stage {stageMap.StageIndex}: district street templates built.", this);
        }

        private void Build(Transform stage)
        {
            PrepareStreetMaterials();
            var old = stage.Find("[Chernobog_CityStreets]");
            if (old != null)
                Destroy(old.gameObject);

            var root = new GameObject("[Chernobog_CityStreets]").transform;
            root.SetParent(stage, false);

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                var block = FindBlockTransform(stage, i);
                if (data == null || block == null)
                    continue;

                var marker = block.GetComponent<RogueliteDistrictTemplate25D>();
                var district = marker != null
                    ? marker.DistrictType
                    : RogueliteStageDistrictTemplateController.ResolveDistrict(data, i, stageMap.StageIndex);

                var cell = new GameObject($"Block_{i:00}_CityStreet_{district}").transform;
                cell.SetParent(root, false);
                cell.position = block.position;

                BuildDistrictGround(cell, district, i, data.Coordinate);
                BuildDistrictArchitecture(cell, data, district, i, data.Coordinate);
                BuildDistrictFurniture(cell, district, i);
            }
        }

        private void BuildDistrictGround(Transform parent, ChernobogDistrictType district, int seed, Vector2Int coordinate)
        {
            var road = _asphalt;
            var pavement = kit.deckSecondaryMaterial != null ? kit.deckSecondaryMaterial : kit.deckMaterial;
            var edge = kit.steelMaterial != null ? kit.steelMaterial : road;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : kit.insetMaterial;
            var dark = kit.insetMaterial != null ? kit.insetMaterial : road;

            switch (district)
            {
                case ChernobogDistrictType.Residential:
                case ChernobogDistrictType.Commercial:
                case ChernobogDistrictType.Industrial:
                    BuildQuadrantRoads(parent, district, coordinate, road, pavement, edge, grate);
                    break;
                case ChernobogDistrictType.Checkpoint:
                    BuildQuadrantRoads(parent, district, coordinate, road, pavement, edge, grate);
                    break;
                case ChernobogDistrictType.MainStreet:
                    BuildMainStreet(parent, road, pavement, edge, grate);
                    break;
                case ChernobogDistrictType.Alley:
                    BuildAlley(parent, road, pavement, edge, grate);
                    break;
                case ChernobogDistrictType.Plaza:
                    BuildPlaza(parent, pavement, road, edge, grate);
                    break;
                case ChernobogDistrictType.ServiceYard:
                    BuildServiceYard(parent, pavement, road, edge, grate);
                    break;
                case ChernobogDistrictType.RuinedBlock:
                    BuildRuinedStreet(parent, road, pavement, edge, dark, seed);
                    break;
            }
        }

        private void BuildMainStreet(Transform parent, Material road, Material pavement, Material edge, Material grate)
        {
            // One unmistakable, broad E/W street. The narrow N/S connector only communicates the
            // junction and keeps the waypoint cross visually coherent without turning the whole cell
            // into a checkerboard of dark strips.
            CreateBox(parent, "MainStreet_Carriageway", new Vector3(0f, 0.073f, 0f),
                new Vector3(ChunkWidth - 0.28f, 0.035f, 5.80f), 0.006f, road, false);
            CreateBox(parent, "MainStreet_NorthWalk", new Vector3(0f, 0.086f, 3.62f),
                new Vector3(ChunkWidth - 0.35f, 0.055f, 1.35f), 0.008f, pavement, false);
            CreateBox(parent, "MainStreet_SouthWalk", new Vector3(0f, 0.086f, -3.62f),
                new Vector3(ChunkWidth - 0.35f, 0.055f, 1.35f), 0.008f, pavement, false);
            CreateBox(parent, "MainStreet_Junction", new Vector3(0f, 0.075f, 0f),
                new Vector3(4.1f, 0.038f, ChunkDepth - 0.35f), 0.006f, road, false);
            CreateBox(parent, "MainStreet_CenterJoint", new Vector3(0f, 0.098f, 0f),
                new Vector3(ChunkWidth - 1.0f, 0.018f, 0.055f), 0.002f, edge, false);
            CreateBox(parent, "MainStreet_GutterN", new Vector3(0f, 0.103f, 2.96f),
                new Vector3(ChunkWidth - 0.70f, 0.020f, 0.18f), 0.003f, grate, false);
            // Main road visual identity: lane, pedestrian and maintenance markings are added here.
            CreateBox(parent, "MainStreet_GutterS", new Vector3(0f, 0.103f, -2.96f),
                new Vector3(ChunkWidth - 0.70f, 0.020f, 0.18f), 0.003f, grate, false);
        }

        private void BuildStreetMarkings(Transform parent, Material marking)
        {
            // Visual only. Keep markings separate from navigation/collision geometry.
            for (var i = -3; i <= 3; i++)
            {
                CreateBox(parent, $"LaneDash_{i}", new Vector3(i * 2.2f, 0.101f, 0f),
                    new Vector3(0.9f, 0.012f, 0.055f), 0.001f, marking, false);
            }

            CreateBox(parent, "CrosswalkNorth", new Vector3(0f, 0.102f, 2.55f),
                new Vector3(3.8f, 0.012f, 0.12f), 0.001f, marking, false);
            CreateBox(parent, "CrosswalkSouth", new Vector3(0f, 0.102f, -2.55f),
                new Vector3(3.8f, 0.012f, 0.12f), 0.001f, marking, false);
        }

        private void BuildAlley(Transform parent, Material road, Material pavement, Material edge, Material grate)
        {
            CreateBox(parent, "Alley_Main", new Vector3(-1.25f, 0.074f, 0f),
                new Vector3(3.55f, 0.036f, ChunkDepth - 0.30f), 0.006f, road, false);
            CreateBox(parent, "Alley_Branch", new Vector3(2.15f, 0.075f, 1.15f),
                new Vector3(7.0f, 0.036f, 2.65f), 0.006f, road, false);
            CreateBox(parent, "Alley_WalkWest", new Vector3(-3.55f, 0.086f, 0f),
                new Vector3(0.95f, 0.052f, ChunkDepth - 0.55f), 0.008f, pavement, false);
            CreateBox(parent, "Alley_WalkEast", new Vector3(0.95f, 0.086f, -1.35f),
                new Vector3(0.90f, 0.052f, ChunkDepth - 3.1f), 0.008f, pavement, false);
            CreateBox(parent, "Alley_Drain", new Vector3(0.38f, 0.103f, -0.80f),
                new Vector3(0.20f, 0.020f, 5.3f), 0.003f, grate, false);
            CreateBox(parent, "Alley_BranchEdge", new Vector3(2.20f, 0.101f, 2.50f),
                new Vector3(6.5f, 0.018f, 0.055f), 0.002f, edge, false);
        }

        private void BuildPlaza(Transform parent, Material pavement, Material road, Material edge, Material grate)
        {
            CreateBox(parent, "Plaza_MainPad", new Vector3(0f, 0.082f, 0f),
                new Vector3(12.6f, 0.052f, 9.4f), 0.012f, pavement, false);
            CreateBox(parent, "Plaza_EWEntry", new Vector3(0f, 0.074f, 0f),
                new Vector3(ChunkWidth - 0.30f, 0.036f, 3.25f), 0.006f, road, false);
            CreateBox(parent, "Plaza_NSEntry", new Vector3(0f, 0.075f, 0f),
                new Vector3(3.25f, 0.036f, ChunkDepth - 0.30f), 0.006f, road, false);
            CreateBox(parent, "Plaza_FrameN", new Vector3(0f, 0.108f, 4.54f),
                new Vector3(11.5f, 0.025f, 0.10f), 0.003f, edge, false);
            CreateBox(parent, "Plaza_FrameS", new Vector3(0f, 0.108f, -4.54f),
                new Vector3(11.5f, 0.025f, 0.10f), 0.003f, edge, false);
            CreateBox(parent, "Plaza_Drain", new Vector3(4.85f, 0.109f, 0f),
                new Vector3(0.26f, 0.024f, 4.8f), 0.004f, grate, false);
        }

        private void BuildServiceYard(Transform parent, Material pavement, Material road, Material edge, Material grate)
        {
            CreateBox(parent, "Yard_Pad", new Vector3(0f, 0.080f, 0f),
                new Vector3(13.8f, 0.050f, 9.8f), 0.010f, pavement, false);
            CreateBox(parent, "Yard_ServiceLane", new Vector3(0f, 0.088f, -1.25f),
                new Vector3(ChunkWidth - 0.32f, 0.036f, 3.65f), 0.006f, road, false);
            CreateBox(parent, "Yard_LoadingApron", new Vector3(3.75f, 0.102f, 3.15f),
                new Vector3(5.1f, 0.035f, 2.15f), 0.006f, road, false);
            CreateBox(parent, "Yard_ServiceTrench", new Vector3(-4.65f, 0.110f, 2.8f),
                new Vector3(0.50f, 0.026f, 4.2f), 0.004f, grate, false);
            CreateBox(parent, "Yard_Edge", new Vector3(0f, 0.112f, 4.72f),
                new Vector3(12.5f, 0.025f, 0.08f), 0.003f, edge, false);
        }

        private void BuildRuinedStreet(Transform parent, Material road, Material pavement, Material edge, Material dark, int seed)
        {
            CreateBox(parent, "Ruined_MainRoad", new Vector3(0f, 0.072f, 0f),
                new Vector3(ChunkWidth - 0.30f, 0.034f, 4.6f), 0.006f, road, false);
            CreateBox(parent, "Ruined_SidewalkN", new Vector3(-1.6f, 0.084f, 3.42f),
                new Vector3(12.0f, 0.050f, 1.25f), 0.008f, pavement, false);
            CreateBox(parent, "Ruined_PatchA", new Vector3(-4.8f, 0.104f, -0.45f),
                new Vector3(3.6f, 0.022f, 2.15f), 0.004f, dark, false,
                Quaternion.Euler(0f, Hash01(seed + 7) * 8f - 4f, 0f));
            CreateBox(parent, "Ruined_PatchB", new Vector3(3.8f, 0.105f, 0.68f),
                new Vector3(2.8f, 0.024f, 1.65f), 0.004f, dark, false,
                Quaternion.Euler(0f, Hash01(seed + 17) * 10f - 5f, 0f));
            CreateBox(parent, "Ruined_CrackBand", new Vector3(0.8f, 0.110f, -2.28f),
                new Vector3(5.8f, 0.020f, 0.065f), 0.002f, edge, false,
                Quaternion.Euler(0f, -8f, 0f));
        }

        private void BuildCheckpointRoad(Transform parent, Material road, Material pavement, Material edge, Material grate)
        {
            CreateBox(parent, "Checkpoint_Road", new Vector3(0f, 0.073f, 0f),
                new Vector3(ChunkWidth - 0.28f, 0.036f, 5.1f), 0.006f, road, false);
            CreateBox(parent, "Checkpoint_InspectionPad", new Vector3(1.6f, 0.088f, 0f),
                new Vector3(7.6f, 0.045f, 7.6f), 0.010f, pavement, false);
            CreateBox(parent, "Checkpoint_NSAccess", new Vector3(0f, 0.075f, 0f),
                new Vector3(3.45f, 0.036f, ChunkDepth - 0.30f), 0.006f, road, false);
            CreateBox(parent, "Checkpoint_StopLine", new Vector3(-1.55f, 0.109f, 0f),
                new Vector3(0.08f, 0.022f, 4.45f), 0.003f, edge, false);
            CreateBox(parent, "Checkpoint_Drain", new Vector3(5.25f, 0.109f, 0f),
                new Vector3(0.30f, 0.024f, 4.15f), 0.004f, grate, false);
        }

        private void BuildDistrictArchitecture(
            Transform parent,
            RogueliteBlockState data,
            ChernobogDistrictType district,
            int seed,
            Vector2Int coordinate)
        {
            // A district is a street space first and a building set second. Keep only a few focal
            // frontages; the playable architecture pass owns the small number of rooms/decks that can
            // actually be entered. This prevents every 30x24 block from becoming a wall of cubes.
            if (!ShouldPlaceFocalBuilding(data, district, seed))
                return;

            switch (district)
            {
                case ChernobogDistrictType.Residential:
                    // The interactive tenement owns the residential mass. Keep one street sign here
                    // so the quadrant reads as a lived-in frontage without stacking another shell on it.
                    BuildResidentialStreetSign(parent, new Vector3(-10.1f, 0f, 7.65f), seed);
                    BuildResidentialFrontage(parent, seed);
                    break;
                case ChernobogDistrictType.Commercial:
                    BuildCommercialFrontage(parent, coordinate, seed);
                    break;
                case ChernobogDistrictType.Industrial:
                    BuildIndustrialFrontage(parent, coordinate, seed);
                    BuildIndustrialYard(parent, seed);
                    break;
                case ChernobogDistrictType.Checkpoint:
                    BuildCheckpointFrontage(parent, coordinate, seed);
                    BuildCheckpointAccess(parent, seed);
                    break;
                case ChernobogDistrictType.MainStreet:
                    BuildSealedBuilding(parent, new Vector3(-5.35f, 0f, 5.35f), 4.10f, 2.45f,
                        Mathf.Lerp(4.2f, 5.8f, Hash01(seed + 5)), seed, false);
                    if (PositiveMod(seed, 2) == 0)
                        BuildClosedStorefront(parent, new Vector3(5.15f, 0f, 5.38f), 4.0f, 2.40f, 3.25f, seed + 17);
                    break;
                case ChernobogDistrictType.Alley:
                    BuildSealedBuilding(parent, new Vector3(-5.55f, 0f, 5.30f), 3.75f, 2.55f,
                        Mathf.Lerp(5.2f, 6.8f, Hash01(seed + 9)), seed, true);
                    break;
                case ChernobogDistrictType.Plaza:
                    if (PositiveMod(seed, 2) == 0)
                        BuildClosedStorefront(parent, new Vector3(5.45f, 0f, 5.42f), 3.8f, 2.35f, 3.1f, seed + 23);
                    break;
                case ChernobogDistrictType.ServiceYard:
                    BuildSealedBuilding(parent, new Vector3(5.35f, 0f, 5.28f), 4.15f, 2.55f,
                        4.3f, seed + 31, true);
                    break;
                case ChernobogDistrictType.RuinedBlock:
                    BuildSealedBuilding(parent, new Vector3(-5.45f, 0f, 5.35f), 3.9f, 2.45f,
                        4.0f, seed + 41, false);
                    break;
            }
        }

        private void BuildQuadrantRoads(
            Transform parent,
            ChernobogDistrictType district,
            Vector2Int coordinate,
            Material road,
            Material pavement,
            Material edge,
            Material grate)
        {
            var roadWidth = district == ChernobogDistrictType.Checkpoint ? 6.20f : RogueliteStageWorldMetrics.MainRoadWidth;
            var halfRoadSegment = roadWidth * 0.25f;
            var seamX = coordinate.x == 0
                ? ChunkWidth * 0.5f - halfRoadSegment
                : -ChunkWidth * 0.5f + halfRoadSegment;
            var seamZ = coordinate.y == 0
                ? ChunkDepth * 0.5f - halfRoadSegment
                : -ChunkDepth * 0.5f + halfRoadSegment;
            var segmentWidth = roadWidth * 0.5f;

            // Each quadrant owns exactly half of the central cross. The segments meet at x/z = 0
            // without overlapping into a second, wider road, so the player reads one real junction.
            CreateBox(parent, "Road_Main_NS", new Vector3(seamX, 0.074f, 0f),
                new Vector3(segmentWidth + 0.12f, 0.045f, ChunkDepth + 0.12f), 0.010f, road, false);
            CreateBox(parent, "Road_Main_EW", new Vector3(0f, 0.075f, seamZ),
                new Vector3(ChunkWidth + 0.12f, 0.045f, segmentWidth + 0.12f), 0.010f, road, false);

            var inwardX = coordinate.x == 0 ? -1f : 1f;
            var inwardZ = coordinate.y == 0 ? -1f : 1f;
            var sidewalk = RogueliteStageWorldMetrics.SidewalkWidth;
            var sidewalkX = seamX + inwardX * (segmentWidth * 0.5f + sidewalk * 0.5f);
            var sidewalkZ = seamZ + inwardZ * (segmentWidth * 0.5f + sidewalk * 0.5f);
            CreateBox(parent, "Road_Sidewalk_NS", new Vector3(sidewalkX, 0.098f, 0f),
                new Vector3(sidewalk, 0.065f, ChunkDepth - 0.18f), 0.010f, pavement, false);
            CreateBox(parent, "Road_Sidewalk_EW", new Vector3(0f, 0.099f, sidewalkZ),
                new Vector3(ChunkWidth - 0.18f, 0.065f, sidewalk), 0.010f, pavement, false);

            var curbX = seamX + inwardX * (segmentWidth * 0.5f + 0.055f);
            var curbZ = seamZ + inwardZ * (segmentWidth * 0.5f + 0.055f);
            CreateBox(parent, "Road_Curb_NS", new Vector3(curbX, 0.126f, 0f),
                new Vector3(0.11f, 0.075f, ChunkDepth - 0.18f), 0.012f, edge, false);
            CreateBox(parent, "Road_Curb_EW", new Vector3(0f, 0.127f, curbZ),
                new Vector3(ChunkWidth - 0.18f, 0.075f, 0.11f), 0.012f, edge, false);

            BuildRoadMarkings(parent, seamX, seamZ, segmentWidth, inwardX, inwardZ, _lanePaint, grate);
            BuildIntersectionCrosswalks(parent, seamX, seamZ, segmentWidth, inwardX, inwardZ, _lanePaint);
            // Restrained wear at human scale, shared materials and no additional colliders.
            for (var i = 0; i < 10; i++)
            {
                var z = -ChunkDepth * 0.5f + 1.5f + i * (ChunkDepth - 3f) / 9f;
                CreateBox(parent, $"PavementJoint_{i}", new Vector3(sidewalkX, 0.135f, z),
                    new Vector3(sidewalk - 0.12f, 0.008f, 0.026f), 0.001f, grate, false);
                if (i % 3 != 0) continue;
                CreateBox(parent, $"AsphaltRepair_{i}", new Vector3(seamX + inwardX * 0.4f, 0.101f, z),
                    new Vector3(0.78f, 0.006f, 1.18f), 0.001f, pavement, false);
                for (var slot = 0; slot < 5; slot++)
                    CreateBox(parent, $"DrainGrille_{i}_{slot}", new Vector3(curbX - inwardX * 0.25f, 0.119f, z + slot * 0.09f),
                        new Vector3(0.32f, 0.012f, 0.035f), 0.002f, grate, false);
            }
        }

        private void BuildRoadMarkings(
            Transform parent,
            float seamX,
            float seamZ,
            float segmentWidth,
            float inwardX,
            float inwardZ,
            Material edge,
            Material grate)
        {
            for (var i = 0; i < 4; i++)
            {
                var z = -ChunkDepth * 0.5f + 2.5f + i * 6.0f;
                CreateBox(parent, $"Road_NS_Mark_{i:00}", new Vector3(seamX, 0.108f, z),
                    new Vector3(0.095f, 0.018f, 2.45f), 0.004f, edge, false);

                var x = -ChunkWidth * 0.5f + 3.0f + i * 8.0f;
                CreateBox(parent, $"Road_EW_Mark_{i:00}", new Vector3(x, 0.109f, seamZ),
                    new Vector3(3.05f, 0.018f, 0.095f), 0.004f, edge, false);
            }

            CreateBox(parent, "Road_Drain_NS", new Vector3(seamX + inwardX * (segmentWidth * 0.5f - 0.62f), 0.111f, 0f),
                new Vector3(0.18f, 0.020f, ChunkDepth - 0.40f), 0.004f, grate, false);
            CreateBox(parent, "Road_Drain_EW", new Vector3(0f, 0.112f, seamZ + inwardZ * (segmentWidth * 0.5f - 0.62f)),
                new Vector3(ChunkWidth - 0.40f, 0.020f, 0.18f), 0.004f, grate, false);
        }

        private void BuildIntersectionCrosswalks(
            Transform parent,
            float seamX,
            float seamZ,
            float segmentWidth,
            float inwardX,
            float inwardZ,
            Material marking)
        {
            // The generated previews read as a town because the roads have pedestrian-scale rules,
            // not just dark strips. Each quadrant contributes one half of the crosswalk at the
            // shared junction, so the four cells assemble into one continuous intersection.
            var crosswalkZ = seamZ + inwardZ * (segmentWidth * 0.5f - 0.45f);
            var crosswalkX = seamX + inwardX * (segmentWidth * 0.5f - 0.45f);
            for (var i = -2; i <= 2; i++)
            {
                var offset = i * 0.34f;
                CreateBox(parent, $"Crosswalk_NS_{i + 2}", new Vector3(seamX, 0.114f, crosswalkZ + inwardZ * offset),
                    new Vector3(segmentWidth - 0.22f, 0.022f, 0.15f), 0.004f, marking, false);
                CreateBox(parent, $"Crosswalk_EW_{i + 2}", new Vector3(crosswalkX + inwardX * offset, 0.115f, seamZ),
                    new Vector3(0.15f, 0.022f, segmentWidth - 0.22f), 0.004f, marking, false);
            }
        }

        private bool ShouldPlaceFocalBuilding(RogueliteBlockState data, ChernobogDistrictType district, int seed)
        {
            if (data == null || district == ChernobogDistrictType.Plaza)
                return false;

            if (district == ChernobogDistrictType.Residential ||
                district == ChernobogDistrictType.Commercial ||
                district == ChernobogDistrictType.Industrial ||
                district == ChernobogDistrictType.Checkpoint)
                return true;

            if (data.Theme == RogueliteChunkTheme.Facility || data.Theme == RogueliteChunkTheme.BossArena)
                return false;

            // One frontage in roughly three ordinary blocks gives the stage a town rhythm: road,
            // open yard, then a recognizable building edge. Emergency blocks are kept legible and get
            // a frontage slightly more often so they still read as occupied urban space.
            var cadence = data.Type == RogueliteBlockType.EmergencyCombat ? 2 : 3;
            return PositiveMod(stageMap.StageIndex * 17 + seed * 5 + (int)district, cadence) == 0;
        }

        private void BuildResidentialStreetSign(Transform parent, Vector3 anchor, int seed)
        {
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var root = new GameObject("ResidentialStreetSign").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            CreateBox(root, "SignPost", new Vector3(0f, 1.15f, 0f), new Vector3(0.10f, 2.30f, 0.10f),
                0.018f, steel, true);
            CreateBox(root, "SignBoard", new Vector3(0f, 2.30f, 0f), new Vector3(1.35f, 0.62f, 0.10f),
                0.020f, inset, false);
            if (kit.emissiveMaterial != null && PositiveMod(seed, 2) == 0)
                CreateBox(root, "SignLight", new Vector3(0f, 2.68f, -0.065f), new Vector3(0.72f, 0.06f, 0.035f),
                    0.008f, kit.emissiveMaterial, false);
        }

        private void BuildResidentialFrontage(Transform parent, int seed)
        {
            var root = new GameObject("ResidentialNorthFacadeRow").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(-7.70f, 0f, 5.85f);

            BuildApartmentFacade(root, "ApartmentWing_A", new Vector3(0f, 0f, 0f),
                5.25f, 2.75f, 5.35f, seed);
            BuildApartmentFacade(root, "ApartmentWing_B", new Vector3(5.85f, 0f, 0.42f),
                4.35f, 2.45f, 4.45f, seed + 17);
        }

        private void BuildApartmentFacade(
            Transform parent,
            string name,
            Vector3 anchor,
            float width,
            float depth,
            float height,
            int seed)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;

            // These are skyline/frontage shells, deliberately visual-only. Enterable geometry stays
            // in PlayableArchitecture where the doorway and upper-floor graph are authoritative.
            RogueliteStagePlayableArchitectureController.BuildInteriorShell(root, width, depth, height, wall, steel, inset);
            CreateBox(root, "ApartmentRoof", new Vector3(0f, height + 0.11f, 0f),
                new Vector3(width * 1.06f, 0.20f, depth * 1.05f), 0.04f, steel, false);

            var floors = Mathf.Clamp(Mathf.RoundToInt(height / 1.55f), 2, 4);
            for (var floor = 0; floor < floors; floor++)
            {
                var y = 1.02f + floor * 1.42f;
                if (y > height - 0.38f)
                    break;

                for (var panel = -1; panel <= 1; panel++)
                {
                    if (floor == 0 && panel == 0) continue;
                    var x = panel * width * 0.27f;
                    CreateBox(root, $"Window_{floor}_{panel + 1}",
                        new Vector3(x, y, -depth * 0.5f - 0.036f),
                        new Vector3(width * 0.16f, 0.66f, 0.055f), 0.006f, inset, false);
                }

                if (floor > 0 && PositiveMod(seed + floor, 2) == 0)
                {
                    CreateBox(root, $"BalconySlab_{floor}", new Vector3(0f, y - 0.36f, -depth * 0.5f - 0.33f),
                        new Vector3(width * 0.56f, 0.10f, 0.62f), 0.020f, steel, false);
                    CreateBox(root, $"BalconyRail_{floor}", new Vector3(0f, y + 0.02f, -depth * 0.5f - 0.60f),
                        new Vector3(width * 0.54f, 0.48f, 0.08f), 0.016f, steel, false);
                }
            }

        }

        private void BuildCommercialFrontage(Transform parent, Vector2Int coordinate, int seed)
        {
            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var root = new GameObject("CommercialMarketRow").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(7.0f, 0f, 6.2f);
            root.localRotation = Quaternion.Euler(0f, 180f, 0f);

            const float unitWidth = 4.7f;
            const float depth = 4.8f;
            const float height = 3.55f;
            for (var i = 0; i < 2; i++)
            {
                var unit = new GameObject(i == 0 ? "MarketShop_A" : "MarketShop_B").transform;
                unit.SetParent(root, false);
                unit.localPosition = new Vector3((i - 0.5f) * (unitWidth + 0.18f), 0f, 0f);

                RogueliteStagePlayableArchitectureController.BuildInteriorShell(unit, unitWidth, depth, height, wall, steel, inset, 2.6f);
                CreateBox(unit, "ShopUpperWindow", new Vector3(0f, 2.88f, -depth * 0.5f - 0.036f),
                    new Vector3(unitWidth * 0.54f, 0.48f, 0.055f), 0.006f, inset, false);
                CreateBox(unit, "ShopAwning", new Vector3(0f, 2.35f, -depth * 0.5f - 0.42f),
                    new Vector3(unitWidth * 0.90f, 0.12f, 0.82f), 0.025f, steel, false,
                    Quaternion.Euler(7f, 0f, 0f));
                CreateBox(unit, "ShopRoof", new Vector3(0f, height + 0.10f, 0f),
                    new Vector3(unitWidth * 1.06f, 0.18f, depth * 1.04f), 0.035f, steel, false);
                CreateBox(unit, "ArcadePost_W", new Vector3(-unitWidth * 0.38f, 1.05f, -depth * 0.5f - 0.56f),
                    new Vector3(0.08f, 2.10f, 0.08f), 0.012f, steel, false);
                CreateBox(unit, "ArcadePost_E", new Vector3(unitWidth * 0.38f, 1.05f, -depth * 0.5f - 0.56f),
                    new Vector3(0.08f, 2.10f, 0.08f), 0.012f, steel, false);
            }

            CreateBox(root, "MarketLoadingApron", new Vector3(0f, 0.075f, 3.55f),
                new Vector3(unitWidth * 2.15f, 0.04f, 1.25f), 0.008f, inset, false);
            CreateBox(root, "MarketHeader", new Vector3(0f, 3.28f, -depth * 0.5f - 0.06f),
                new Vector3(unitWidth * 2.05f, 0.16f, 0.08f), 0.012f, steel, false);
        }

        private void BuildIndustrialFrontage(Transform parent, Vector2Int coordinate, int seed)
        {
            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;
            var root = new GameObject("IndustrialWorkshopFrontage").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(-9.0f, 0f, -6.2f);

            const float width = 11.0f;
            const float depth = 6.1f;
            const float height = 4.35f;
            RogueliteStagePlayableArchitectureController.BuildInteriorShell(root, width, depth, height, wall, steel, grate, 3.8f);
            CreateBox(root, "WorkshopDoorFrameTop", new Vector3(0f, 2.88f, -depth * 0.5f - 0.075f),
                new Vector3(width * 0.72f, 0.18f, 0.10f), 0.012f, steel, false);
            CreateBox(root, "WorkshopRoof", new Vector3(0f, height + 0.10f, 0f),
                new Vector3(width * 1.05f, 0.20f, depth * 1.05f), 0.040f, steel, false);
            CreateBox(root, "WorkshopLoadingApron", new Vector3(0f, 0.075f, -depth * 0.5f - 1.15f),
                new Vector3(width * 0.84f, 0.05f, 1.80f), 0.008f, grate, false);

            if (kit.pipeRun != null)
            {
                var pipe = Instantiate(kit.pipeRun, root);
                pipe.name = "WorkshopPipeRun";
                pipe.transform.localPosition = new Vector3(width * 0.30f, height + 0.22f, 0.25f);
                pipe.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                pipe.transform.localScale = Vector3.one * 0.82f;
                DisablePrefabColliders(pipe);
            }

            if (PositiveMod(seed, 2) == 0)
                BuildLamp(root, new Vector3(width * 0.40f, 0f, -depth * 0.5f - 1.55f), steel, kit.emissiveMaterial);
        }

        private void BuildIndustrialYard(Transform parent, int seed)
        {
            var root = new GameObject("IndustrialUtilityYard").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(5.35f, 0f, 3.45f);

            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            CreateBox(root, "UtilityYardApron", new Vector3(0f, 0.078f, -0.95f),
                new Vector3(10.0f, 0.045f, 5.65f), 0.008f, grate, false);
            BuildStorageTank(root, "StorageTank_A", new Vector3(-2.75f, 0f, 0.80f), 1.65f, 3.60f, steel, inset);
            BuildStorageTank(root, "StorageTank_B", new Vector3(-0.25f, 0f, 0.95f), 1.42f, 3.10f, steel, inset);

            CreateBox(root, "PipeRackBeam", new Vector3(2.50f, 4.12f, 0.78f),
                new Vector3(5.10f, 0.18f, 0.24f), 0.025f, steel, false);
            CreateBox(root, "PipeRackPost_W", new Vector3(0.15f, 2.08f, 0.78f),
                new Vector3(0.20f, 4.16f, 0.20f), 0.025f, steel, false);
            CreateBox(root, "PipeRackPost_E", new Vector3(4.85f, 2.08f, 0.78f),
                new Vector3(0.20f, 4.16f, 0.20f), 0.025f, steel, false);
            for (var i = 0; i < 3; i++)
            {
                CreateBox(root, $"UtilityPipe_{i}", new Vector3(2.50f, 3.50f + i * 0.28f, 0.78f),
                    new Vector3(4.60f, 0.12f, 0.12f), 0.018f, inset, false);
            }

            if (kit.pipeRun != null)
            {
                var pipe = Instantiate(kit.pipeRun, root);
                pipe.name = "IndustrialYardPipeBridge";
                pipe.transform.localPosition = new Vector3(0.85f, 4.42f, -1.45f);
                pipe.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                pipe.transform.localScale = Vector3.one * 0.72f;
                DisablePrefabColliders(pipe);
            }

            if (PositiveMod(seed, 2) == 0)
                BuildLamp(root, new Vector3(4.85f, 0f, -2.15f), steel, kit.emissiveMaterial);
        }

        private static void BuildStorageTank(
            Transform parent,
            string name,
            Vector3 basePosition,
            float diameter,
            float height,
            Material steel,
            Material inset)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = basePosition;

            CreateCylinder(root, "TankBody", new Vector3(0f, height * 0.5f, 0f), diameter, height, steel);
            CreateBox(root, "TankTop", new Vector3(0f, height + 0.08f, 0f),
                new Vector3(diameter * 0.72f, 0.16f, diameter * 0.72f), 0.025f, inset, false);
            CreateBox(root, "TankFoot_W", new Vector3(-diameter * 0.30f, 0.18f, 0f),
                new Vector3(0.16f, 0.36f, 0.16f), 0.018f, steel, false);
            CreateBox(root, "TankFoot_E", new Vector3(diameter * 0.30f, 0.18f, 0f),
                new Vector3(0.16f, 0.36f, 0.16f), 0.018f, steel, false);
        }

        private void BuildCheckpointFrontage(Transform parent, Vector2Int coordinate, int seed)
        {
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;
            var root = new GameObject("CheckpointGatehouse").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(7.2f, 0f, 6.4f);

            RogueliteStagePlayableArchitectureController.BuildInteriorShell(root, 4.8f, 3.6f, 2.9f, inset, steel, grate);
            CreateBox(root, "GatehouseWindow", new Vector3(-0.85f, 1.55f, -1.84f), new Vector3(1.10f, 0.72f, 0.06f),
                0.008f, steel, false);
            CreateBox(root, "GatehouseRoof", new Vector3(0f, 3.10f, 0f), new Vector3(5.15f, 0.20f, 3.95f),
                0.040f, steel, false);
            CreateBox(root, "CheckpointBarrier", new Vector3(-4.0f, 0.52f, -1.10f), new Vector3(3.20f, 1.04f, 0.38f),
                0.055f, inset, true);
            CreateBox(root, "CheckpointStopLine", new Vector3(-3.95f, 0.11f, 0.25f), new Vector3(0.12f, 0.025f, 4.1f),
                0.004f, grate, false);
        }

        private void BuildCheckpointAccess(Transform parent, int seed)
        {
            var root = new GameObject("CheckpointInspectionLane").transform;
            root.SetParent(parent, false);

            var road = kit.deckHeavyMaterial != null ? kit.deckHeavyMaterial : kit.deckMaterial;
            var pavement = kit.deckSecondaryMaterial != null ? kit.deckSecondaryMaterial : kit.deckMaterial;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;

            // A straight internal lane makes the checkpoint read like a real controlled approach
            // from the southern city road, rather than a random gatehouse placed in a square.
            CreateBox(root, "InspectionLane", new Vector3(5.35f, 0.078f, -0.80f),
                new Vector3(3.55f, 0.045f, 16.90f), 0.008f, road, false);
            CreateBox(root, "InspectionWalkway", new Vector3(7.55f, 0.092f, -0.80f),
                new Vector3(1.10f, 0.060f, 16.35f), 0.008f, pavement, false);

            for (var i = 0; i < 5; i++)
            {
                var z = -6.25f + i * 2.15f;
                CreateBox(root, $"InspectionLaneMark_{i}", new Vector3(5.35f, 0.112f, z),
                    new Vector3(2.20f, 0.022f, 0.10f), 0.004f, inset, false);
            }

            var gateZ = 2.55f;
            CreateBox(root, "GatePylon_W", new Vector3(3.45f, 1.55f, gateZ),
                new Vector3(0.42f, 3.10f, 0.58f), 0.055f, inset, false);
            CreateBox(root, "GatePylon_E", new Vector3(7.25f, 1.55f, gateZ),
                new Vector3(0.42f, 3.10f, 0.58f), 0.055f, inset, false);
            CreateBox(root, "GateOverheadBeam", new Vector3(5.35f, 3.30f, gateZ),
                new Vector3(4.20f, 0.42f, 0.78f), 0.065f, steel, false);
            CreateBox(root, "GateSignal_W", new Vector3(4.12f, 3.70f, gateZ),
                new Vector3(0.18f, 0.22f, 0.18f), 0.025f, kit.emissiveMaterial, false);
            CreateBox(root, "GateSignal_E", new Vector3(6.58f, 3.70f, gateZ),
                new Vector3(0.18f, 0.22f, 0.18f), 0.025f, kit.emissiveMaterial, false);

            CreateBox(root, "InspectionBarrier", new Vector3(5.35f, 0.52f, gateZ + 0.65f),
                new Vector3(2.55f, 1.04f, 0.30f), 0.045f, inset, false);
            if (PositiveMod(seed, 2) == 0)
                BuildLamp(root, new Vector3(2.90f, 0f, 5.70f), steel, kit.emissiveMaterial);
        }

        private void BuildSealedBuilding(Transform parent, Vector3 anchor, float width, float depth, float height, int seed, bool utilityFacade)
        {
            var root = new GameObject(utilityFacade ? "SealedUtilityBuilding" : "SealedCityBuilding").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            RogueliteStagePlayableArchitectureController.BuildInteriorShell(root, width, depth, height, wall, steel, grate);
            CreateBox(root, "RoofCap", new Vector3(0f, height + 0.10f, 0f),
                new Vector3(width * 1.06f, 0.20f, depth * 1.05f), 0.045f, steel, false);

            var floors = Mathf.Clamp(Mathf.RoundToInt(height / 1.75f), 2, 4);
            for (var floor = 0; floor < floors; floor++)
            {
                var y = 1.0f + floor * 1.42f;
                if (y > height - 0.4f)
                    break;
                for (var panel = -1; panel <= 1; panel++)
                {
                    if (floor == 0 && panel == 0) continue;
                    var x = panel * width * 0.25f;
                    CreateBox(root, $"FacadeWindow_{floor}_{panel + 1}",
                        new Vector3(x, y, -depth * 0.5f - 0.036f),
                        new Vector3(width * 0.18f, 0.62f, 0.055f), 0.006f,
                        utilityFacade && panel == 0 ? grate : inset, false);
                }
            }

            if (kit.hvacSmall != null && PositiveMod(seed, 3) == 0)
            {
                var hvac = Instantiate(kit.hvacSmall, root);
                hvac.name = "RoofHVAC";
                hvac.transform.localPosition = new Vector3(width * 0.18f, height + 0.18f, 0f);
                hvac.transform.localScale = Vector3.one * 0.66f;
                DisablePrefabColliders(hvac);
            }
        }

        private void BuildClosedStorefront(Transform parent, Vector3 anchor, float width, float depth, float height, int seed)
        {
            var root = new GameObject("ClosedStorefront").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var wall = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : wall;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : wall;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : inset;

            RogueliteStagePlayableArchitectureController.BuildInteriorShell(root, width, depth, height, wall, steel, grate, 2.1f);
            CreateBox(root, "Awning", new Vector3(0f, 2.58f, -depth * 0.5f - 0.36f),
                new Vector3(width * 0.80f, 0.12f, 0.72f), 0.025f, steel, false,
                Quaternion.Euler(6f, 0f, 0f));
            CreateBox(root, "Roof", new Vector3(0f, height + 0.09f, 0f),
                new Vector3(width * 1.05f, 0.18f, depth * 1.05f), 0.04f, steel, false);
        }

        private void BuildDistrictFurniture(Transform parent, ChernobogDistrictType district, int seed)
        {
            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var inset = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var emissive = kit.emissiveMaterial != null ? kit.emissiveMaterial : kit.accentMaterial;

            switch (district)
            {
                case ChernobogDistrictType.Residential:
                    BuildLamp(parent, new Vector3(-10.35f, 0f, 8.35f), steel, emissive);
                    CreateBox(parent, "ResidentialMailbox", new Vector3(-9.35f, 0.46f, 6.35f),
                        new Vector3(0.48f, 0.92f, 0.38f), 0.045f, inset, true);
                    break;
                case ChernobogDistrictType.Commercial:
                    BuildLamp(parent, new Vector3(10.70f, 0f, 8.15f), steel, emissive);
                    CreateBox(parent, "MarketCrate_A", new Vector3(3.95f, 0.34f, 5.05f),
                        new Vector3(0.72f, 0.68f, 0.72f), 0.04f, inset, true);
                    CreateBox(parent, "MarketCrate_B", new Vector3(4.85f, 0.27f, 5.05f),
                        new Vector3(0.62f, 0.54f, 0.62f), 0.04f, inset, true);
                    break;
                case ChernobogDistrictType.Industrial:
                    CreateBox(parent, "IndustrialBollard_A", new Vector3(-12.20f, 0.34f, -8.85f),
                        new Vector3(0.22f, 0.68f, 0.22f), 0.03f, steel, true);
                    CreateBox(parent, "IndustrialBollard_B", new Vector3(-10.95f, 0.34f, -8.85f),
                        new Vector3(0.22f, 0.68f, 0.22f), 0.03f, steel, true);
                    break;
                case ChernobogDistrictType.MainStreet:
                    BuildLamp(parent, new Vector3(-5.55f, 0f, 3.82f), steel, emissive);
                    BuildLamp(parent, new Vector3(5.55f, 0f, -3.82f), steel, emissive);
                    CreateBox(parent, "StreetCabinet", new Vector3(6.05f, 0.50f, 3.70f),
                        new Vector3(0.72f, 1.0f, 0.58f), 0.05f, inset, true);
                    break;
                case ChernobogDistrictType.Alley:
                    BuildLamp(parent, new Vector3(-3.95f, 0f, -4.85f), steel, emissive);
                    CreateBox(parent, "AlleyCabinet", new Vector3(1.62f, 0.44f, 4.72f),
                        new Vector3(0.62f, 0.88f, 0.54f), 0.05f, inset, true);
                    break;
                case ChernobogDistrictType.Plaza:
                    BuildLamp(parent, new Vector3(-5.35f, 0f, 4.68f), steel, emissive);
                    BuildLamp(parent, new Vector3(5.35f, 0f, 4.68f), steel, emissive);
                    break;
                case ChernobogDistrictType.ServiceYard:
                    CreateBox(parent, "YardCabinetA", new Vector3(-5.4f, 0.58f, 4.35f),
                        new Vector3(0.86f, 1.16f, 0.68f), 0.055f, inset, true);
                    CreateBox(parent, "YardCabinetB", new Vector3(-4.25f, 0.42f, 4.30f),
                        new Vector3(0.70f, 0.84f, 0.62f), 0.050f, inset, true);
                    break;
                case ChernobogDistrictType.Checkpoint:
                    for (var i = 0; i < 3; i++)
                        CreateBox(parent, $"CheckpointBollard_{i}", new Vector3(-2.15f, 0.32f, -1.10f + i * 1.10f),
                            new Vector3(0.18f, 0.64f, 0.18f), 0.025f, steel, true);
                    BuildLamp(parent, new Vector3(5.45f, 0f, 4.2f), steel, emissive);
                    break;
                case ChernobogDistrictType.RuinedBlock:
                    if (PositiveMod(seed, 2) == 0)
                        BuildLamp(parent, new Vector3(-5.25f, 0f, 4.2f), steel, emissive);
                    break;
            }
        }

        private static void BuildLamp(Transform parent, Vector3 basePosition, Material steel, Material emissive)
        {
            var root = new GameObject("StreetLamp").transform;
            root.SetParent(parent, false);
            root.localPosition = basePosition;
            CreateBox(root, "Pole", new Vector3(0f, 1.55f, 0f), new Vector3(0.10f, 3.10f, 0.10f), 0.018f, steel, true);
            CreateBox(root, "Arm", new Vector3(0.34f, 2.92f, 0f), new Vector3(0.74f, 0.09f, 0.09f), 0.015f, steel, false);
            CreateBox(root, "Lamp", new Vector3(0.70f, 2.86f, 0f), new Vector3(0.20f, 0.12f, 0.18f), 0.025f, emissive, false);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, float bevel,
            Material material, bool collider, Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size,
                Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.22f));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = Vector3.zero;
                box.size = size;
                box.isTrigger = false;
            }
            return go;
        }

        private static void CreateCylinder(
            Transform parent,
            string name,
            Vector3 localPosition,
            float diameter,
            float height,
            Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }

        private static void DisablePrefabColliders(GameObject root)
        {
            if (root == null)
                return;
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                if (colliders[i] != null)
                    colliders[i].enabled = false;
        }

        private static Transform FindBlockTransform(Transform stage, int index)
        {
            return RogueliteStageBlockUtility.FindBlockTransform(stage, index);
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint x = (uint)(value + 0x9E3779B9);
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (x & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static int PositiveMod(int value, int divisor)
        {
            return RogueliteStageMath.PositiveMod(value, divisor);
        }
    }
}
