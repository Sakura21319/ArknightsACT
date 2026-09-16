using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Second-generation visual skin based on the selected Concept-01 direction.
    ///
    /// It deliberately does not own traversal, encounter, pit or reward rules. Existing colliders and
    /// runtime chunk geometry remain authoritative; this pass replaces the first visual skin with a
    /// cleaner Chernobog/mobile-city kit: broad quiet deck plates, sparse maintenance inserts,
    /// vented perimeter walls, HVAC-like combat cover and assembled edge architecture.
    /// </summary>
    [DefaultExecutionOrder(14)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageConceptOneController : MonoBehaviour
    {
        private const float ChunkWidth = 14f;
        private const float ChunkDepth = 11f;

        [SerializeField] private RogueliteStageMapController stageMap;

        private readonly List<Material> _ownedMaterials = new();
        private readonly List<Texture2D> _ownedTextures = new();
        private GameObject _preparedStage;
        private float _nextResolveAt;

        private Material _deckA;
        private Material _deckB;
        private Material _deckDark;
        private Material _wall;
        private Material _wallInset;
        private Material _cover;
        private Material _grate;
        private Material _steelEdge;
        private Material _orange;
        private Material _warmLight;
        private Material _black;

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

            DisablePreviousSkin(stage.transform);
            ResolveMaterialKit(stage);
            BuildSelectedConcept(stage.transform);
            _preparedStage = stage;

            Debug.Log(
                $"[ArknightsACT/Concept01] Stage {stageMap.StageIndex} rebuilt with the selected clean industrial deck kit.",
                this);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null)
                    Destroy(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();

            for (var i = 0; i < _ownedTextures.Count; i++)
            {
                if (_ownedTextures[i] != null)
                    Destroy(_ownedTextures[i]);
            }
            _ownedTextures.Clear();
        }

        private void BuildSelectedConcept(Transform stage)
        {
            var root = new GameObject("[Concept01_ChernobogDeckKit]").transform;
            root.SetParent(stage, false);

            BuildQuietDeck(stage, root);
            BuildUtilityCover(stage, root);
            BuildFacilitySkin(stage, root);
            BuildPerimeterKit(stage, root);
            BuildChunkTransitions(root);
            BuildEdgeArchitecture(root);
        }

        private static void DisablePreviousSkin(Transform stage)
        {
            DisableNamedChild(stage, "[ArknightsBoundarySkin]");
            DisableNamedChild(stage, "[DeckJointSeams]");
            DisableNamedChild(stage, "[ChernobogEdgeInfrastructure]");

            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null)
                    continue;
                if (string.Equals(t.name, "[ArknightsTileSkin]", StringComparison.Ordinal) ||
                    string.Equals(t.name, "[ArknightsCoverSkin]", StringComparison.Ordinal))
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        private static void DisableNamedChild(Transform stage, string name)
        {
            var child = stage.Find(name);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        private void ResolveMaterialKit(GameObject stage)
        {
            if (_deckA != null)
                return;

            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            var ground = FindMaterial(renderers, "Ground_Tactical") ?? FirstMaterial(renderers);
            var facilityFloor = FindMaterial(renderers, "Facility_Floor") ?? ground;
            var facilityWall = FindMaterial(renderers, "Facility_Wall") ?? ground;
            var combatCover = FindMaterial(renderers, "CombatCover") ?? facilityWall;
            var accent = FindMaterial(renderers, "TacticalAccent") ?? facilityFloor;

            var deckATexture = BuildDeckTexture("Concept01_DeckA", 128, 0);
            var deckBTexture = BuildDeckTexture("Concept01_DeckB", 128, 1);
            var wallTexture = BuildWallTexture("Concept01_Wall", 128);
            var coverTexture = BuildCoverTexture("Concept01_Cover", 128);
            var grateTexture = BuildGrateTexture("Concept01_Grate", 128);

            _deckA = CloneMaterial(facilityFloor ?? ground, "Runtime_Concept01_DeckA",
                new Color(0.40f, 0.435f, 0.475f), deckATexture, 0.32f, 0.22f, new Vector2(1.0f, 1.0f));
            _deckB = CloneMaterial(facilityFloor ?? ground, "Runtime_Concept01_DeckB",
                new Color(0.315f, 0.345f, 0.385f), deckBTexture, 0.34f, 0.20f, new Vector2(1.0f, 1.0f));
            _deckDark = CloneMaterial(ground ?? facilityFloor, "Runtime_Concept01_DeckDark",
                new Color(0.17f, 0.195f, 0.225f), deckATexture, 0.28f, 0.14f, new Vector2(1.0f, 1.0f));
            _wall = CloneMaterial(facilityWall ?? combatCover, "Runtime_Concept01_Wall",
                new Color(0.245f, 0.285f, 0.335f), wallTexture, 0.36f, 0.18f, new Vector2(1.0f, 1.0f));
            _wallInset = CloneMaterial(facilityWall ?? combatCover, "Runtime_Concept01_WallInset",
                new Color(0.095f, 0.115f, 0.140f), wallTexture, 0.30f, 0.12f, new Vector2(1.0f, 1.0f));
            _cover = CloneMaterial(combatCover ?? facilityWall, "Runtime_Concept01_Cover",
                new Color(0.205f, 0.235f, 0.275f), coverTexture, 0.40f, 0.18f, new Vector2(1.0f, 1.0f));
            _grate = CloneMaterial(facilityWall ?? ground, "Runtime_Concept01_Grate",
                new Color(0.12f, 0.135f, 0.155f), grateTexture, 0.46f, 0.10f, new Vector2(1.0f, 1.0f));
            _steelEdge = CloneMaterial(facilityWall ?? ground, "Runtime_Concept01_SteelEdge",
                new Color(0.34f, 0.375f, 0.415f), null, 0.48f, 0.24f, Vector2.one);
            _orange = CloneMaterial(accent ?? facilityFloor, "Runtime_Concept01_Orange",
                new Color(0.78f, 0.36f, 0.075f), null, 0.16f, 0.20f, Vector2.one);
            _warmLight = CloneMaterial(accent ?? facilityFloor, "Runtime_Concept01_WarmLight",
                new Color(1.0f, 0.58f, 0.16f), null, 0.08f, 0.44f, Vector2.one);
            _black = CloneMaterial(ground ?? facilityWall, "Runtime_Concept01_Black",
                new Color(0.035f, 0.042f, 0.052f), null, 0.12f, 0.06f, Vector2.one);
        }

        private void BuildQuietDeck(Transform stage, Transform conceptRoot)
        {
            var root = new GameObject("Deck").transform;
            root.SetParent(conceptRoot, false);

            for (var blockIndex = 0; blockIndex < stageMap.Blocks.Count; blockIndex++)
            {
                var block = FindBlockTransform(stage, blockIndex);
                var sockets = block != null ? block.Find("[FloorSockets]") : null;
                if (block == null || sockets == null)
                    continue;

                var blockSkin = new GameObject($"Block_{blockIndex:00}_DeckSkin").transform;
                blockSkin.SetParent(root, false);
                blockSkin.position = block.position;
                blockSkin.rotation = block.rotation;

                var surfaces = sockets.GetComponentsInChildren<Transform>(true);
                var serial = 0;
                for (var i = 0; i < surfaces.Length; i++)
                {
                    var surface = surfaces[i];
                    if (surface == null || !surface.name.StartsWith("FloorSurface_", StringComparison.Ordinal))
                        continue;

                    var renderer = surface.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        var materialSelector = PositiveMod(stageMap.StageIndex * 19 + blockIndex * 11 + serial * 5, 9);
                        renderer.sharedMaterial = materialSelector <= 1 ? _deckB : _deckA;
                    }

                    // Concept-01 intentionally keeps the broad floor quiet. Only a small minority of
                    // sockets receive service hardware; the previous every-other-tile marks are gone.
                    var detail = PositiveMod(stageMap.StageIndex * 67 + blockIndex * 29 + serial * 17, 23);
                    if (detail == 0 || detail == 11)
                        BuildFloorGrate(blockSkin, surface.localPosition, surface.localScale, detail == 11);
                    else if (detail == 7)
                        BuildServiceHatch(blockSkin, surface.localPosition, surface.localScale);

                    serial++;
                }
            }
        }

        private void BuildFloorGrate(Transform parent, Vector3 cellCenter, Vector3 cellScale, bool rotate)
        {
            var width = Mathf.Min(Mathf.Abs(cellScale.x) * 0.48f, 1.15f);
            var depth = Mathf.Min(Mathf.Abs(cellScale.z) * 0.28f, 0.52f);
            if (rotate)
                (width, depth) = (depth, width);

            var frame = new GameObject("MaintenanceGrate").transform;
            frame.SetParent(parent, false);
            frame.localPosition = cellCenter + new Vector3(0.22f, 0.052f, -0.12f);

            CreateVisual(frame, "Recess", Vector3.zero, new Vector3(width + 0.10f, 0.026f, depth + 0.10f), _black);
            CreateVisual(frame, "Grid", new Vector3(0f, 0.020f, 0f), new Vector3(width, 0.018f, depth), _grate);
            CreateEdgeFrame(frame, width + 0.10f, depth + 0.10f, 0.034f, 0.034f, _steelEdge);
        }

        private void BuildServiceHatch(Transform parent, Vector3 cellCenter, Vector3 cellScale)
        {
            var width = Mathf.Min(Mathf.Abs(cellScale.x) * 0.50f, 1.10f);
            var depth = Mathf.Min(Mathf.Abs(cellScale.z) * 0.45f, 0.86f);
            var hatch = new GameObject("ServiceHatch").transform;
            hatch.SetParent(parent, false);
            hatch.localPosition = cellCenter + new Vector3(-0.18f, 0.052f, 0.15f);

            CreateVisual(hatch, "Plate", Vector3.zero, new Vector3(width, 0.022f, depth), _deckDark);
            CreateEdgeFrame(hatch, width, depth, 0.028f, 0.032f, _steelEdge);

            // One small orange service tab is enough to keep the original Arknights-like accent.
            CreateVisual(hatch, "ServiceTab", new Vector3(width * 0.31f, 0.025f, -depth * 0.42f),
                new Vector3(width * 0.16f, 0.018f, 0.055f), _orange);
        }

        private void BuildUtilityCover(Transform stage, Transform conceptRoot)
        {
            var root = new GameObject("UtilityCover").transform;
            root.SetParent(conceptRoot, false);

            var transforms = stage.GetComponentsInChildren<Transform>(true);
            var serial = 0;
            for (var i = 0; i < transforms.Length; i++)
            {
                var coverRoot = transforms[i];
                if (coverRoot == null || !string.Equals(coverRoot.name, "GridCover", StringComparison.Ordinal))
                    continue;

                var body = coverRoot.Find("Body");
                if (body == null)
                    continue;

                HideRenderer(coverRoot.Find("TopTrim"));
                HideRenderer(coverRoot.Find("HazardBand"));
                var bodyRenderer = body.GetComponent<Renderer>();
                if (bodyRenderer != null)
                    bodyRenderer.sharedMaterial = _cover;

                var width = Mathf.Max(0.40f, Mathf.Abs(body.localScale.x));
                var height = Mathf.Max(0.45f, Mathf.Abs(body.localScale.y));
                var depth = Mathf.Max(0.40f, Mathf.Abs(body.localScale.z));

                var skin = new GameObject("[Concept01Cover]").transform;
                skin.SetParent(coverRoot, false);

                // Thick raised corner extrusions remove the plain-cube read.
                CreateVisual(skin, "TopPlate", new Vector3(0f, height + 0.038f, 0f),
                    new Vector3(width * 0.92f, 0.075f, depth * 0.90f), _steelEdge);
                CreateVisual(skin, "FrontRecess", new Vector3(0f, height * 0.52f, -depth * 0.5f - 0.024f),
                    new Vector3(width * 0.74f, height * 0.54f, 0.040f), _wallInset);
                CreateVisual(skin, "BackRecess", new Vector3(0f, height * 0.52f, depth * 0.5f + 0.024f),
                    new Vector3(width * 0.74f, height * 0.54f, 0.040f), _wallInset);

                var postWidth = Mathf.Min(0.12f, width * 0.12f);
                CreateVisual(skin, "CornerL", new Vector3(-width * 0.5f + postWidth * 0.5f, height * 0.52f, 0f),
                    new Vector3(postWidth, height * 0.96f, depth + 0.09f), _steelEdge);
                CreateVisual(skin, "CornerR", new Vector3(width * 0.5f - postWidth * 0.5f, height * 0.52f, 0f),
                    new Vector3(postWidth, height * 0.96f, depth + 0.09f), _steelEdge);

                for (var slat = -2; slat <= 2; slat++)
                {
                    CreateVisual(skin, "FrontVent", new Vector3(0f, height * (0.50f + slat * 0.075f), -depth * 0.5f - 0.049f),
                        new Vector3(width * 0.55f, 0.022f, 0.020f), _grate);
                }

                // Thin, offset accent only; no full orange roof.
                CreateVisual(skin, "AssetStripe", new Vector3(-width * 0.16f, height + 0.080f, -depth * 0.18f),
                    new Vector3(width * 0.44f, 0.025f, 0.052f), _orange);

                if (serial % 4 == 1)
                {
                    CreateVisual(skin, "TopServiceBox", new Vector3(width * 0.18f, height + 0.15f, depth * 0.06f),
                        new Vector3(width * 0.28f, 0.22f, depth * 0.34f), _wall);
                    CreateVisual(skin, "TopServiceCap", new Vector3(width * 0.18f, height + 0.275f, depth * 0.06f),
                        new Vector3(width * 0.32f, 0.045f, depth * 0.38f), _steelEdge);
                }

                serial++;
            }
        }

        private void BuildFacilitySkin(Transform stage, Transform conceptRoot)
        {
            var root = new GameObject("FacilityArchitecture").transform;
            root.SetParent(conceptRoot, false);

            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var facility = transforms[i];
                if (facility == null || !string.Equals(facility.name, "TwoFloorFacility", StringComparison.Ordinal))
                    continue;

                var skin = new GameObject("[Concept01Facility]").transform;
                skin.SetParent(facility, false);

                // A broad north facade, equipment frame and vents make the facility feel assembled
                // rather than like a few intersecting prototype cubes. Colliders stay untouched.
                CreateVisual(skin, "NorthFacadeBand", new Vector3(2.15f, 1.16f, 4.075f),
                    new Vector3(5.58f, 0.78f, 0.065f), _wallInset);
                for (var panel = -2; panel <= 2; panel++)
                {
                    CreateVisual(skin, "FacadeVent", new Vector3(2.15f + panel * 0.92f, 1.17f, 4.115f),
                        new Vector3(0.70f, 0.46f, 0.035f), _grate);
                }

                CreateVisual(skin, "FacadeTopCap", new Vector3(2.15f, 1.82f, 4.06f),
                    new Vector3(5.82f, 0.12f, 0.16f), _steelEdge);
                CreateVisual(skin, "FacadeLight", new Vector3(0.60f, 0.55f, 4.115f),
                    new Vector3(0.70f, 0.09f, 0.030f), _warmLight);

                BuildPipePair(skin, new Vector3(4.72f, 0.52f, 3.98f), 1.46f, 0.075f);

                // Upper-deck HVAC block mirrors the selected concept's small service machinery.
                var hvac = new GameObject("UpperHVAC").transform;
                hvac.SetParent(skin, false);
                hvac.localPosition = new Vector3(3.45f, 2.25f, 2.28f);
                CreateVisual(hvac, "Body", Vector3.zero, new Vector3(1.18f, 0.56f, 0.82f), _cover);
                CreateVisual(hvac, "FrontGrill", new Vector3(0f, 0f, -0.431f), new Vector3(0.84f, 0.32f, 0.028f), _grate);
                CreateVisual(hvac, "Top", new Vector3(0f, 0.31f, 0f), new Vector3(1.24f, 0.07f, 0.88f), _steelEdge);
            }
        }

        private void BuildPerimeterKit(Transform stage, Transform conceptRoot)
        {
            HideRenderer(stage.Find("Bound_N"));
            HideRenderer(stage.Find("Bound_S"));
            HideRenderer(stage.Find("Bound_E"));
            HideRenderer(stage.Find("Bound_W"));

            var totalWidth = stageMap.Width * ChunkWidth;
            var totalDepth = stageMap.Height * ChunkDepth;
            var root = new GameObject("Perimeter").transform;
            root.SetParent(conceptRoot, false);

            BuildBoundarySide(root, "North", new Vector3(0f, 0f, totalDepth * 0.5f), totalWidth + 0.32f, Vector3.back, 1.58f, true);
            BuildBoundarySide(root, "East", new Vector3(totalWidth * 0.5f, 0f, 0f), totalDepth, Vector3.left, 1.50f, true);
            BuildBoundarySide(root, "South", new Vector3(0f, 0f, -totalDepth * 0.5f), totalWidth + 0.32f, Vector3.forward, 0.70f, false);
            BuildBoundarySide(root, "West", new Vector3(-totalWidth * 0.5f, 0f, 0f), totalDepth, Vector3.right, 0.70f, false);

            BuildCornerPylon(root, new Vector3(-totalWidth * 0.5f, 0f, -totalDepth * 0.5f));
            BuildCornerPylon(root, new Vector3(totalWidth * 0.5f, 0f, -totalDepth * 0.5f));
            BuildCornerPylon(root, new Vector3(-totalWidth * 0.5f, 0f, totalDepth * 0.5f));
            BuildCornerPylon(root, new Vector3(totalWidth * 0.5f, 0f, totalDepth * 0.5f));
        }

        private void BuildBoundarySide(Transform parent, string name, Vector3 center, float length, Vector3 inward, float height, bool vented)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = center;
            root.localRotation = Quaternion.LookRotation(inward, Vector3.up);

            const float preferred = 2.35f;
            var count = Mathf.Max(1, Mathf.CeilToInt(length / preferred));
            var segmentLength = length / count;

            for (var i = 0; i < count; i++)
            {
                var panel = new GameObject($"WallPanel_{i:00}").transform;
                panel.SetParent(root, false);
                panel.localPosition = new Vector3(-length * 0.5f + segmentLength * (i + 0.5f), 0f, 0f);

                CreateVisual(panel, "Backer", new Vector3(0f, height * 0.50f, 0f),
                    new Vector3(segmentLength - 0.035f, height, 0.30f), _wall);
                CreateVisual(panel, "TopCap", new Vector3(0f, height + 0.055f, 0f),
                    new Vector3(segmentLength - 0.04f, 0.11f, 0.38f), _steelEdge);
                CreateVisual(panel, "LeftPost", new Vector3(-segmentLength * 0.5f + 0.07f, height * 0.52f, 0.015f),
                    new Vector3(0.14f, height + 0.12f, 0.40f), _steelEdge);
                CreateVisual(panel, "RightPost", new Vector3(segmentLength * 0.5f - 0.07f, height * 0.52f, 0.015f),
                    new Vector3(0.14f, height + 0.12f, 0.40f), _steelEdge);

                if (vented)
                {
                    CreateVisual(panel, "VentRecess", new Vector3(0f, height * 0.55f, 0.168f),
                        new Vector3(segmentLength * 0.77f, height * 0.52f, 0.030f), _wallInset);
                    CreateVisual(panel, "VentFace", new Vector3(0f, height * 0.55f, 0.187f),
                        new Vector3(segmentLength * 0.68f, height * 0.38f, 0.022f), _grate);
                }
                else
                {
                    CreateVisual(panel, "GuardInset", new Vector3(0f, height * 0.56f, 0.168f),
                        new Vector3(segmentLength * 0.72f, height * 0.36f, 0.030f), _wallInset);
                }

                if (i % 2 == 0)
                {
                    CreateVisual(panel, "FootLight", new Vector3(0f, 0.13f, 0.190f),
                        new Vector3(Mathf.Min(0.62f, segmentLength * 0.30f), 0.075f, 0.026f), _warmLight);
                }
            }
        }

        private void BuildCornerPylon(Transform parent, Vector3 localPosition)
        {
            var pylon = new GameObject("CornerPylon").transform;
            pylon.SetParent(parent, false);
            pylon.localPosition = localPosition;

            CreateVisual(pylon, "Body", new Vector3(0f, 0.82f, 0f), new Vector3(0.54f, 1.64f, 0.54f), _wallInset);
            CreateVisual(pylon, "Cap", new Vector3(0f, 1.68f, 0f), new Vector3(0.66f, 0.14f, 0.66f), _steelEdge);
            CreateVisual(pylon, "BeaconStem", new Vector3(0f, 1.90f, 0f), new Vector3(0.08f, 0.34f, 0.08f), _wall);
            CreateVisual(pylon, "Beacon", new Vector3(0f, 2.09f, 0f), new Vector3(0.12f, 0.08f, 0.12f), _orange);
        }

        private void BuildChunkTransitions(Transform conceptRoot)
        {
            var root = new GameObject("DeckTransitions").transform;
            root.SetParent(conceptRoot, false);

            var totalWidth = stageMap.Width * ChunkWidth;
            var totalDepth = stageMap.Height * ChunkDepth;

            // Large chunk seams are now built as mechanical joints rather than bright markings.
            for (var x = 1; x < stageMap.Width; x++)
            {
                var seamX = -totalWidth * 0.5f + x * ChunkWidth;
                CreateVisual(root, "JointX", new Vector3(seamX, 0.037f, 0f),
                    new Vector3(0.075f, 0.024f, totalDepth - 0.70f), _black);
                BuildBridgePlate(root, new Vector3(seamX, 0.052f, 0f), true);
            }

            for (var z = 1; z < stageMap.Height; z++)
            {
                var seamZ = -totalDepth * 0.5f + z * ChunkDepth;
                CreateVisual(root, "JointZ", new Vector3(0f, 0.037f, seamZ),
                    new Vector3(totalWidth - 0.70f, 0.024f, 0.075f), _black);
                BuildBridgePlate(root, new Vector3(0f, 0.052f, seamZ), false);
            }
        }

        private void BuildBridgePlate(Transform parent, Vector3 position, bool verticalJoint)
        {
            var plate = new GameObject("BridgePlate").transform;
            plate.SetParent(parent, false);
            plate.localPosition = position;
            var width = verticalJoint ? 0.42f : 2.20f;
            var depth = verticalJoint ? 2.20f : 0.42f;
            CreateVisual(plate, "Plate", Vector3.zero, new Vector3(width, 0.030f, depth), _deckDark);
            CreateEdgeFrame(plate, width, depth, 0.028f, 0.030f, _steelEdge);
        }

        private void BuildEdgeArchitecture(Transform conceptRoot)
        {
            var root = new GameObject("EdgeArchitecture").transform;
            root.SetParent(conceptRoot, false);

            var width = stageMap.Width * ChunkWidth;
            var depth = stageMap.Height * ChunkDepth;
            var stageExtra = Mathf.Clamp(stageMap.StageIndex - 1, 0, 2);

            // Selected Concept-01: one strong north facade plus smaller utility masses, rather than
            // evenly spaced giant blocks. This creates a believable location and stronger silhouette.
            BuildFacadeBuilding(root, "NorthFacility",
                new Vector3(-width * 0.16f, 0f, depth * 0.5f + 4.4f),
                new Vector3(Mathf.Min(11.5f, width * 0.52f), 6.0f + stageExtra * 0.7f, 3.6f), Vector3.back, true);

            BuildFacadeBuilding(root, "NorthUtility",
                new Vector3(width * 0.34f, 0f, depth * 0.5f + 3.2f),
                new Vector3(4.2f, 4.0f + stageExtra * 0.45f, 3.0f), Vector3.back, false);

            BuildFacadeBuilding(root, "EastUtility",
                new Vector3(width * 0.5f + 3.3f, 0f, depth * 0.15f),
                new Vector3(3.8f, 5.0f + stageExtra * 0.55f, 5.6f), Vector3.left, false);

            BuildCatwalkSilhouette(root, new Vector3(-width * 0.33f, 3.5f + stageExtra * 0.25f, depth * 0.5f + 7.0f), width * 0.38f);
        }

        private void BuildFacadeBuilding(Transform parent, string name, Vector3 basePosition, Vector3 size, Vector3 inward, bool doorBay)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = basePosition;
            root.localRotation = Quaternion.LookRotation(inward, Vector3.up);

            CreateVisual(root, "Body", new Vector3(0f, size.y * 0.5f, 0f), size, _wall, true);
            CreateVisual(root, "FacadeInset", new Vector3(0f, size.y * 0.52f, size.z * 0.5f + 0.032f),
                new Vector3(size.x * 0.78f, size.y * 0.54f, 0.045f), _wallInset);

            var bays = Mathf.Clamp(Mathf.RoundToInt(size.x / 2.1f), 2, 6);
            for (var bay = 0; bay < bays; bay++)
            {
                var x = -size.x * 0.5f + size.x * (bay + 0.5f) / bays;
                CreateVisual(root, "FacadePost", new Vector3(x - size.x / bays * 0.48f, size.y * 0.52f, size.z * 0.5f + 0.060f),
                    new Vector3(0.12f, size.y * 0.60f, 0.085f), _steelEdge);

                if (!doorBay || bay != bays / 2)
                {
                    CreateVisual(root, "VentBay", new Vector3(x, size.y * 0.57f, size.z * 0.5f + 0.062f),
                        new Vector3(size.x / bays * 0.70f, size.y * 0.34f, 0.032f), _grate);
                }
            }

            if (doorBay)
            {
                CreateVisual(root, "ServiceDoor", new Vector3(0f, size.y * 0.31f, size.z * 0.5f + 0.070f),
                    new Vector3(Mathf.Min(2.0f, size.x * 0.20f), size.y * 0.50f, 0.060f), _deckDark);
                CreateVisual(root, "DoorHeaderLight", new Vector3(0f, size.y * 0.61f, size.z * 0.5f + 0.108f),
                    new Vector3(0.78f, 0.10f, 0.030f), _warmLight);
            }

            CreateVisual(root, "RoofCap", new Vector3(0f, size.y + 0.08f, 0f), new Vector3(size.x + 0.18f, 0.16f, size.z + 0.16f), _steelEdge);
            BuildRoofMachinery(root, new Vector3(size.x * 0.22f, size.y + 0.42f, -size.z * 0.08f), Mathf.Min(1.8f, size.x * 0.24f));
            BuildPipePair(root, new Vector3(-size.x * 0.34f, size.y * 0.62f, size.z * 0.5f + 0.10f), Mathf.Min(1.8f, size.y * 0.30f), 0.075f);
        }

        private void BuildRoofMachinery(Transform parent, Vector3 position, float width)
        {
            var machine = new GameObject("RoofMachinery").transform;
            machine.SetParent(parent, false);
            machine.localPosition = position;

            CreateVisual(machine, "Base", Vector3.zero, new Vector3(width, 0.54f, width * 0.72f), _cover);
            CreateVisual(machine, "FrontGrill", new Vector3(0f, 0f, -width * 0.37f), new Vector3(width * 0.72f, 0.28f, 0.030f), _grate);
            CreateVisual(machine, "Cap", new Vector3(0f, 0.30f, 0f), new Vector3(width + 0.10f, 0.07f, width * 0.78f), _steelEdge);
        }

        private void BuildPipePair(Transform parent, Vector3 position, float height, float thickness)
        {
            for (var i = 0; i < 2; i++)
            {
                var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipe.name = "UtilityPipe";
                pipe.transform.SetParent(parent, false);
                pipe.transform.localPosition = position + new Vector3(i * thickness * 2.2f, 0f, 0f);
                pipe.transform.localScale = new Vector3(thickness, height * 0.5f, thickness);
                var renderer = pipe.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _steelEdge;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
                var collider = pipe.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
            }
        }

        private void BuildCatwalkSilhouette(Transform parent, Vector3 position, float length)
        {
            var root = new GameObject("BackgroundCatwalk").transform;
            root.SetParent(parent, false);
            root.localPosition = position;

            CreateVisual(root, "Walkway", Vector3.zero, new Vector3(length, 0.16f, 0.72f), _deckDark, true);
            CreateVisual(root, "RailTop", new Vector3(0f, 0.62f, -0.32f), new Vector3(length, 0.07f, 0.07f), _steelEdge);
            CreateVisual(root, "RailTopBack", new Vector3(0f, 0.62f, 0.32f), new Vector3(length, 0.07f, 0.07f), _steelEdge);
            var posts = Mathf.Max(3, Mathf.CeilToInt(length / 2.0f));
            for (var i = 0; i < posts; i++)
            {
                var x = -length * 0.5f + length * i / Mathf.Max(1, posts - 1);
                CreateVisual(root, "RailPost", new Vector3(x, 0.33f, -0.32f), new Vector3(0.055f, 0.62f, 0.055f), _steelEdge);
                CreateVisual(root, "RailPostBack", new Vector3(x, 0.33f, 0.32f), new Vector3(0.055f, 0.62f, 0.055f), _steelEdge);
            }
        }

        private Texture2D BuildDeckTexture(string name, int size, int variant)
        {
            var texture = CreateTexture(name, size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var noise = Hash01(x + variant * 173, y + variant * 311);
                var broad = Hash01(x / 8 + variant * 47, y / 8 + variant * 89);
                var value = 0.76f + (noise - 0.5f) * 0.045f + (broad - 0.5f) * 0.035f;

                // Fine dark rim reads as a plate bevel after tinting, without painting arbitrary icons.
                var rim = x <= 2 || y <= 2 || x >= size - 3 || y >= size - 3;
                if (rim)
                    value *= 0.70f;

                // Very subtle directional scuffs, not a repeating tactical marker.
                if (variant == 1 && (x + y * 3) % 61 == 0)
                    value *= 0.92f;

                texture.SetPixel(x, y, new Color(value, value * 1.01f, value * 1.03f, 1f));
            }
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D BuildWallTexture(string name, int size)
        {
            var texture = CreateTexture(name, size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var noise = Hash01(x * 3 + 17, y * 5 + 29);
                var seam = x % 32 <= 2 || y % 48 <= 2;
                var value = seam ? 0.50f : 0.74f + (noise - 0.5f) * 0.055f;
                texture.SetPixel(x, y, new Color(value * 0.94f, value * 0.98f, value, 1f));
            }
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D BuildCoverTexture(string name, int size)
        {
            var texture = CreateTexture(name, size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var noise = Hash01(x * 7 + 3, y * 11 + 7);
                var seam = x % 40 <= 2 || y % 42 <= 2;
                var value = seam ? 0.46f : 0.72f + (noise - 0.5f) * 0.05f;
                texture.SetPixel(x, y, new Color(value * 0.93f, value * 0.97f, value, 1f));
            }
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D BuildGrateTexture(string name, int size)
        {
            var texture = CreateTexture(name, size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var bar = x % 12 <= 3 || y % 12 <= 3;
                var v = bar ? 0.32f : 0.08f;
                texture.SetPixel(x, y, new Color(v, v * 1.02f, v * 1.05f, 1f));
            }
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D CreateTexture(string name, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            _ownedTextures.Add(texture);
            return texture;
        }

        private Material CloneMaterial(Material source, string name, Color tint, Texture texture, float metallic, float smoothness, Vector2 scale)
        {
            if (source == null)
                return null;

            var clone = new Material(source) { name = name };
            if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", tint);
            if (clone.HasProperty("_Color")) clone.SetColor("_Color", tint);
            if (texture != null)
            {
                if (clone.HasProperty("_BaseMap"))
                {
                    clone.SetTexture("_BaseMap", texture);
                    clone.SetTextureScale("_BaseMap", scale);
                }
                if (clone.HasProperty("_MainTex"))
                {
                    clone.SetTexture("_MainTex", texture);
                    clone.SetTextureScale("_MainTex", scale);
                }
            }
            if (clone.HasProperty("_Metallic")) clone.SetFloat("_Metallic", metallic);
            if (clone.HasProperty("_Smoothness")) clone.SetFloat("_Smoothness", smoothness);
            if (clone.HasProperty("_Glossiness")) clone.SetFloat("_Glossiness", smoothness);
            _ownedMaterials.Add(clone);
            return clone;
        }

        private static void CreateEdgeFrame(Transform parent, float width, float depth, float thickness, float height, Material material)
        {
            CreateVisual(parent, "FrameN", new Vector3(0f, height * 0.5f, depth * 0.5f - thickness * 0.5f),
                new Vector3(width, height, thickness), material);
            CreateVisual(parent, "FrameS", new Vector3(0f, height * 0.5f, -depth * 0.5f + thickness * 0.5f),
                new Vector3(width, height, thickness), material);
            CreateVisual(parent, "FrameE", new Vector3(width * 0.5f - thickness * 0.5f, height * 0.5f, 0f),
                new Vector3(thickness, height, depth), material);
            CreateVisual(parent, "FrameW", new Vector3(-width * 0.5f + thickness * 0.5f, height * 0.5f, 0f),
                new Vector3(thickness, height, depth), material);
        }

        private static GameObject CreateVisual(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool castShadows = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return go;
        }

        private static void HideRenderer(Transform transform)
        {
            if (transform == null)
                return;
            var renderer = transform.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
        }

        private static Material FindMaterial(Renderer[] renderers, string materialName)
        {
            if (renderers == null)
                return null;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var material = renderer != null ? renderer.sharedMaterial : null;
                if (material != null && string.Equals(material.name, materialName, StringComparison.OrdinalIgnoreCase))
                    return material;
            }
            return null;
        }

        private static Material FirstMaterial(Renderer[] renderers)
        {
            if (renderers == null)
                return null;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial != null)
                    return renderers[i].sharedMaterial;
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

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                var n = x * 374761393 + y * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                n ^= n >> 16;
                return (n & 0x7fffffff) / (float)int.MaxValue;
            }
        }
    }
}
