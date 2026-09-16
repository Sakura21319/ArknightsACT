using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Visual-only authenticity pass for the procedural ACT stage.
    ///
    /// The runtime map/encounter/navigation code remains authoritative. This component only skins
    /// the generated geometry so the result reads closer to an Arknights combat board: modular
    /// mobile-city deck plates, segmented industrial boundary walls, RIIC-like utility cover,
    /// deck-joint seams and dense Chernobog edge infrastructure outside the playable space.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageAuthenticityController : MonoBehaviour
    {
        private const float ChunkWidth = 14f;
        private const float ChunkDepth = 11f;

        [SerializeField] private RogueliteStageMapController stageMap;

        private readonly List<Material> _ownedMaterials = new();
        private GameObject _preparedStage;
        private float _nextResolveAt;

        private Material _wallMaterial;
        private Material _deckMaterial;
        private Material _coverMaterial;
        private Material _accentMaterial;
        private Material _hazardMaterial;
        private Material _darkMaterial;
        private Material _mutedAccentMaterial;
        private Material _maintenanceMaterial;

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

            ResolveMaterials(stage);
            ApplyWorldLook();
            PrepareStage(stage);
            _preparedStage = stage;
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null)
                    Destroy(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
        }

        private void PrepareStage(GameObject stage)
        {
            // The Phase-08 first pass put tall prototype building cubes inside playable chunks.
            // They read as blockout geometry rather than Arknights terrain, so retire them and move
            // the architectural mass outside the combat board where it belongs visually.
            DisablePrototypeLowRiseShells(stage.transform);

            SkinFloorSockets(stage.transform);
            SkinCombatCover(stage.transform);
            SkinOuterBounds(stage.transform);
            BuildDeckJointSeams(stage.transform);
            BuildChernobogEdgeInfrastructure(stage.transform);

            Debug.Log(
                $"[ArknightsACT/Authenticity] Stage {stageMap.StageIndex} visual pass applied: " +
                "modular deck, industrial boundary panels, utility cover and Chernobog edge structures.",
                this);
        }

        private void ResolveMaterials(GameObject stage)
        {
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            _wallMaterial = FindMaterial(renderers, "Facility_Wall") ?? FindMaterial(renderers, "CombatCover");
            _deckMaterial = FindMaterial(renderers, "Facility_Floor") ?? FindMaterial(renderers, "Ground_Tactical") ?? _wallMaterial;
            _coverMaterial = FindMaterial(renderers, "CombatCover") ?? _wallMaterial;
            _accentMaterial = FindMaterial(renderers, "TacticalAccent") ?? _deckMaterial;
            _hazardMaterial = FindMaterial(renderers, "HazardBand") ?? _accentMaterial;

            if (_darkMaterial == null)
                _darkMaterial = CloneTint(_wallMaterial ?? _coverMaterial ?? _deckMaterial,
                    new Color(0.105f, 0.125f, 0.150f), "Runtime_StageDarkPanel", 0.34f, 0.16f);
            if (_mutedAccentMaterial == null)
                _mutedAccentMaterial = CloneTint(_accentMaterial ?? _deckMaterial,
                    new Color(0.72f, 0.38f, 0.10f), "Runtime_StageMutedOrange", 0.18f, 0.24f);
            if (_maintenanceMaterial == null)
                _maintenanceMaterial = CloneTint(_deckMaterial ?? _coverMaterial,
                    new Color(0.255f, 0.285f, 0.325f), "Runtime_StageMaintenance", 0.40f, 0.20f);
        }

        private void ApplyWorldLook()
        {
            // The previous ambient value made the whole board read as one flat grey plane. Early
            // Chernobog stages rely on cold ambient fill with stronger directional separation.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.285f, 0.315f, 0.365f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.045f, 0.055f, 0.072f);
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 54f;

            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null || light.type != LightType.Directional)
                    continue;

                light.color = new Color(0.82f, 0.88f, 1f);
                light.intensity = 1.20f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(49f, -36f, 0f);
                break;
            }
        }

        private static void DisablePrototypeLowRiseShells(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var current = transforms[i];
                if (current == null || !current.name.StartsWith("ChernobogLowRise_", StringComparison.Ordinal))
                    continue;
                current.gameObject.SetActive(false);
            }
        }

        private void SkinFloorSockets(Transform stage)
        {
            for (var blockIndex = 0; blockIndex < stageMap.Blocks.Count; blockIndex++)
            {
                var block = FindBlockTransform(stage, blockIndex);
                var socketsRoot = block != null ? block.Find("[FloorSockets]") : null;
                if (socketsRoot == null)
                    continue;

                var surfaces = socketsRoot.GetComponentsInChildren<Transform>(true);
                var serial = 0;
                for (var i = 0; i < surfaces.Length; i++)
                {
                    var surface = surfaces[i];
                    if (surface == null || !surface.name.StartsWith("FloorSurface_", StringComparison.Ordinal))
                        continue;
                    if (surface.Find("[ArknightsTileSkin]") != null)
                        continue;

                    BuildFloorSocketSkin(surface, blockIndex, serial++);
                }
            }
        }

        private void BuildFloorSocketSkin(Transform surface, int blockIndex, int serial)
        {
            var root = new GameObject("[ArknightsTileSkin]").transform;
            root.SetParent(surface, false);

            var width = Mathf.Abs(surface.localScale.x);
            var depth = Mathf.Abs(surface.localScale.z);
            if (width < 0.20f || depth < 0.20f)
                return;

            // A small inset maintenance plate on a subset of tiles gives the deck the same layered,
            // serviceable mobile-city language as the source game without turning every cell noisy.
            var detailSelector = PositiveMod(stageMap.StageIndex * 31 + blockIndex * 13 + serial * 7, 7);
            if (detailSelector <= 2)
            {
                var side = detailSelector == 0 ? -1f : 1f;
                CreateMetricChild(
                    surface,
                    "MaintenancePlate",
                    new Vector3(width * 0.20f * side, 0.020f, -depth * 0.18f),
                    new Vector3(width * 0.38f, 0.014f, depth * 0.20f),
                    _darkMaterial ?? _coverMaterial);

                for (var bar = -1; bar <= 1; bar++)
                {
                    CreateMetricChild(
                        surface,
                        "GrateSlat",
                        new Vector3(width * 0.20f * side, 0.029f, -depth * 0.18f + bar * depth * 0.045f),
                        new Vector3(width * 0.30f, 0.010f, 0.025f),
                        _maintenanceMaterial ?? _deckMaterial);
                }
            }

            if (detailSelector == 3 || detailSelector == 5)
            {
                CreateMetricChild(
                    surface,
                    "TileIDPlate",
                    new Vector3(width * 0.31f, 0.020f, depth * 0.30f),
                    new Vector3(width * 0.18f, 0.014f, depth * 0.10f),
                    _mutedAccentMaterial ?? _accentMaterial);
            }

            // Two restrained inner seams reinforce the tactical-cell read. The real physical gap
            // remains the outer seam, so pit sockets can still disappear cleanly as one unit.
            if ((serial + blockIndex) % 2 == 0)
            {
                CreateMetricChild(
                    surface,
                    "InnerSeamX",
                    new Vector3(0f, 0.019f, depth * 0.34f),
                    new Vector3(width * 0.70f, 0.009f, 0.025f),
                    _darkMaterial ?? _coverMaterial);
            }
        }

        private void SkinCombatCover(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            var serial = 0;
            for (var i = 0; i < transforms.Length; i++)
            {
                var cover = transforms[i];
                if (cover == null || !string.Equals(cover.name, "GridCover", StringComparison.Ordinal))
                    continue;
                if (cover.Find("[ArknightsCoverSkin]") != null)
                    continue;

                var body = cover.Find("Body");
                if (body == null)
                    continue;

                var size = body.localScale;
                var height = Mathf.Max(0.40f, Mathf.Abs(size.y));
                var width = Mathf.Max(0.35f, Mathf.Abs(size.x));
                var depth = Mathf.Max(0.35f, Mathf.Abs(size.z));

                var oldTop = cover.Find("TopTrim");
                var oldTopRenderer = oldTop != null ? oldTop.GetComponent<Renderer>() : null;
                if (oldTopRenderer != null && _maintenanceMaterial != null)
                    oldTopRenderer.sharedMaterial = _maintenanceMaterial;

                var skin = new GameObject("[ArknightsCoverSkin]").transform;
                skin.SetParent(cover, false);

                CreateVisual(skin, "FrontInset",
                    new Vector3(0f, height * 0.53f, -depth * 0.5f - 0.022f),
                    new Vector3(width * 0.82f, height * 0.48f, 0.038f),
                    _darkMaterial ?? _wallMaterial);
                CreateVisual(skin, "BackInset",
                    new Vector3(0f, height * 0.53f, depth * 0.5f + 0.022f),
                    new Vector3(width * 0.82f, height * 0.48f, 0.038f),
                    _darkMaterial ?? _wallMaterial);

                var postScale = new Vector3(0.075f, height * 0.88f, depth + 0.07f);
                CreateVisual(skin, "PostL", new Vector3(-width * 0.5f + 0.045f, height * 0.50f, 0f), postScale, _wallMaterial ?? _coverMaterial);
                CreateVisual(skin, "PostR", new Vector3(width * 0.5f - 0.045f, height * 0.50f, 0f), postScale, _wallMaterial ?? _coverMaterial);

                CreateVisual(skin, "TopCap",
                    new Vector3(0f, height + 0.052f, 0f),
                    new Vector3(width * 0.90f, 0.045f, depth * 0.88f),
                    _maintenanceMaterial ?? _deckMaterial);
                CreateVisual(skin, "OrangeLip",
                    new Vector3(0f, height + 0.078f, -depth * 0.31f),
                    new Vector3(width * 0.58f, 0.026f, 0.060f),
                    _mutedAccentMaterial ?? _accentMaterial);

                for (var slat = -1; slat <= 1; slat++)
                {
                    CreateVisual(skin, "FrontVent",
                        new Vector3(0f, height * (0.44f + slat * 0.10f), -depth * 0.5f - 0.044f),
                        new Vector3(width * 0.54f, 0.025f, 0.018f),
                        _maintenanceMaterial ?? _deckMaterial);
                }

                if (serial++ % 3 == 1)
                {
                    CreateVisual(skin, "UtilityBox",
                        new Vector3(width * 0.24f, height + 0.13f, depth * 0.05f),
                        new Vector3(width * 0.24f, 0.18f, depth * 0.32f),
                        _coverMaterial ?? _wallMaterial);
                }
            }
        }

        private void SkinOuterBounds(Transform stage)
        {
            var totalWidth = stageMap.Width * ChunkWidth;
            var totalDepth = stageMap.Height * ChunkDepth;

            var n = stage.Find("Bound_N");
            var s = stage.Find("Bound_S");
            var e = stage.Find("Bound_E");
            var w = stage.Find("Bound_W");
            HideBoundRenderer(n);
            HideBoundRenderer(s);
            HideBoundRenderer(e);
            HideBoundRenderer(w);

            var skinRoot = stage.Find("[ArknightsBoundarySkin]");
            if (skinRoot != null)
                return;
            skinRoot = new GameObject("[ArknightsBoundarySkin]").transform;
            skinRoot.SetParent(stage, false);

            BuildBoundarySide(skinRoot, "NorthWall",
                new Vector3(0f, 0f, totalDepth * 0.5f), totalWidth + 0.28f, Vector3.back, 1.42f, true);
            BuildBoundarySide(skinRoot, "EastWall",
                new Vector3(totalWidth * 0.5f, 0f, 0f), totalDepth, Vector3.left, 1.34f, true);

            // Camera-near sides stay deliberately low so they frame the board instead of hiding it.
            BuildBoundarySide(skinRoot, "SouthGuard",
                new Vector3(0f, 0f, -totalDepth * 0.5f), totalWidth + 0.28f, Vector3.forward, 0.64f, false);
            BuildBoundarySide(skinRoot, "WestGuard",
                new Vector3(-totalWidth * 0.5f, 0f, 0f), totalDepth, Vector3.right, 0.64f, false);

            BuildCornerPylon(skinRoot, new Vector3(-totalWidth * 0.5f, 0f, -totalDepth * 0.5f));
            BuildCornerPylon(skinRoot, new Vector3(totalWidth * 0.5f, 0f, -totalDepth * 0.5f));
            BuildCornerPylon(skinRoot, new Vector3(-totalWidth * 0.5f, 0f, totalDepth * 0.5f));
            BuildCornerPylon(skinRoot, new Vector3(totalWidth * 0.5f, 0f, totalDepth * 0.5f));
        }

        private void BuildBoundarySide(
            Transform parent,
            string name,
            Vector3 localCenter,
            float length,
            Vector3 inward,
            float height,
            bool fullPanel)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localCenter;
            root.localRotation = Quaternion.LookRotation(inward, Vector3.up);

            const float targetSegment = 2.55f;
            var count = Mathf.Max(1, Mathf.CeilToInt(length / targetSegment));
            var segmentLength = length / count;

            for (var i = 0; i < count; i++)
            {
                var segment = new GameObject($"Panel_{i:00}").transform;
                segment.SetParent(root, false);
                segment.localPosition = new Vector3(-length * 0.5f + segmentLength * (i + 0.5f), 0f, 0f);

                CreateVisual(segment, "Body",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(segmentLength - 0.035f, height, 0.28f),
                    _wallMaterial ?? _coverMaterial);
                CreateVisual(segment, "TopRail",
                    new Vector3(0f, height + 0.045f, 0f),
                    new Vector3(segmentLength - 0.08f, 0.09f, 0.34f),
                    _maintenanceMaterial ?? _deckMaterial);
                CreateVisual(segment, "PostL",
                    new Vector3(-segmentLength * 0.5f + 0.055f, height * 0.52f, 0.035f),
                    new Vector3(0.10f, height + 0.10f, 0.37f),
                    _darkMaterial ?? _wallMaterial);
                CreateVisual(segment, "PostR",
                    new Vector3(segmentLength * 0.5f - 0.055f, height * 0.52f, 0.035f),
                    new Vector3(0.10f, height + 0.10f, 0.37f),
                    _darkMaterial ?? _wallMaterial);

                if (fullPanel)
                {
                    CreateVisual(segment, "Inset",
                        new Vector3(0f, height * 0.52f, 0.155f),
                        new Vector3(segmentLength * 0.80f, height * 0.55f, 0.028f),
                        _darkMaterial ?? _coverMaterial);
                    for (var slat = -2; slat <= 2; slat++)
                    {
                        CreateVisual(segment, "VentSlat",
                            new Vector3(0f, height * (0.52f + slat * 0.075f), 0.176f),
                            new Vector3(segmentLength * 0.69f, 0.025f, 0.018f),
                            _maintenanceMaterial ?? _deckMaterial);
                    }
                }

                if (i % 2 == 0)
                {
                    CreateVisual(segment, "FootLight",
                        new Vector3(0f, 0.16f, 0.182f),
                        new Vector3(Mathf.Min(0.46f, segmentLength * 0.22f), 0.075f, 0.025f),
                        _mutedAccentMaterial ?? _accentMaterial);
                }
            }
        }

        private void BuildCornerPylon(Transform parent, Vector3 localPosition)
        {
            var root = new GameObject("BoundaryCornerPylon").transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            CreateVisual(root, "Base", new Vector3(0f, 0.70f, 0f), new Vector3(0.46f, 1.40f, 0.46f), _darkMaterial ?? _wallMaterial);
            CreateVisual(root, "Cap", new Vector3(0f, 1.44f, 0f), new Vector3(0.54f, 0.12f, 0.54f), _maintenanceMaterial ?? _deckMaterial);
            CreateVisual(root, "Signal", new Vector3(0f, 1.62f, 0f), new Vector3(0.09f, 0.26f, 0.09f), _mutedAccentMaterial ?? _accentMaterial);
        }

        private void BuildDeckJointSeams(Transform stage)
        {
            if (stage.Find("[DeckJointSeams]") != null)
                return;

            var root = new GameObject("[DeckJointSeams]").transform;
            root.SetParent(stage, false);

            var totalWidth = stageMap.Width * ChunkWidth;
            var totalDepth = stageMap.Height * ChunkDepth;

            for (var x = 1; x < stageMap.Width; x++)
            {
                var seamX = -totalWidth * 0.5f + x * ChunkWidth;
                CreateVisual(root, "VerticalDeckJoint",
                    new Vector3(seamX, 0.030f, 0f),
                    new Vector3(0.085f, 0.025f, totalDepth - 0.45f),
                    _darkMaterial ?? _wallMaterial);
                CreateVisual(root, "VerticalBridgePlate",
                    new Vector3(seamX, 0.048f, 0f),
                    new Vector3(0.42f, 0.025f, 2.20f),
                    _maintenanceMaterial ?? _deckMaterial);
            }

            for (var z = 1; z < stageMap.Height; z++)
            {
                var seamZ = -totalDepth * 0.5f + z * ChunkDepth;
                CreateVisual(root, "HorizontalDeckJoint",
                    new Vector3(0f, 0.030f, seamZ),
                    new Vector3(totalWidth - 0.45f, 0.025f, 0.085f),
                    _darkMaterial ?? _wallMaterial);
                CreateVisual(root, "HorizontalBridgePlate",
                    new Vector3(0f, 0.048f, seamZ),
                    new Vector3(2.20f, 0.025f, 0.42f),
                    _maintenanceMaterial ?? _deckMaterial);
            }
        }

        private void BuildChernobogEdgeInfrastructure(Transform stage)
        {
            if (stage.Find("[ChernobogEdgeInfrastructure]") != null)
                return;

            var root = new GameObject("[ChernobogEdgeInfrastructure]").transform;
            root.SetParent(stage, false);

            var width = stageMap.Width * ChunkWidth;
            var depth = stageMap.Height * ChunkDepth;
            var stageBoost = Mathf.Clamp(stageMap.StageIndex - 1, 0, 2);

            // Far/north edge: dense mobile-city utility blocks that sit outside the combat board.
            BuildEdgeModule(root, "NorthUtility_A",
                new Vector3(-width * 0.32f, 0f, depth * 0.5f + 3.2f),
                new Vector3(4.8f, 5.2f + stageBoost * 0.8f, 3.2f), Vector3.back, 0);
            BuildEdgeModule(root, "NorthUtility_B",
                new Vector3(width * 0.02f, 0f, depth * 0.5f + 4.2f),
                new Vector3(6.0f, 7.0f + stageBoost * 1.0f, 3.8f), Vector3.back, 1);
            BuildEdgeModule(root, "NorthUtility_C",
                new Vector3(width * 0.36f, 0f, depth * 0.5f + 3.0f),
                new Vector3(4.2f, 4.6f + stageBoost * 0.7f, 3.0f), Vector3.back, 2);

            // Far/east edge layers another silhouette behind the stage and avoids the old isolated
            // giant-cube look visible in the first screenshot.
            BuildEdgeModule(root, "EastUtility_A",
                new Vector3(width * 0.5f + 3.2f, 0f, -depth * 0.22f),
                new Vector3(3.5f, 5.6f + stageBoost * 0.8f, 4.6f), Vector3.left, 3);
            BuildEdgeModule(root, "EastUtility_B",
                new Vector3(width * 0.5f + 4.2f, 0f, depth * 0.20f),
                new Vector3(4.0f, 7.8f + stageBoost * 1.0f, 5.4f), Vector3.left, 4);
        }

        private void BuildEdgeModule(
            Transform parent,
            string name,
            Vector3 localBase,
            Vector3 size,
            Vector3 inward,
            int variant)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localBase;
            root.localRotation = Quaternion.LookRotation(inward, Vector3.up);

            CreateVisual(root, "Body",
                new Vector3(0f, size.y * 0.5f, 0f),
                size,
                _wallMaterial ?? _coverMaterial,
                castShadows: true);
            CreateVisual(root, "FacadeInset",
                new Vector3(0f, size.y * 0.52f, size.z * 0.5f + 0.025f),
                new Vector3(size.x * 0.76f, size.y * 0.48f, 0.045f),
                _darkMaterial ?? _coverMaterial);

            for (var slat = -2; slat <= 2; slat++)
            {
                CreateVisual(root, "FacadeSlat",
                    new Vector3(0f, size.y * (0.50f + slat * 0.065f), size.z * 0.5f + 0.052f),
                    new Vector3(size.x * 0.62f, 0.045f, 0.025f),
                    _maintenanceMaterial ?? _deckMaterial);
            }

            CreateVisual(root, "IDStripe",
                new Vector3(0f, Mathf.Min(1.05f + variant * 0.08f, size.y * 0.34f), size.z * 0.5f + 0.064f),
                new Vector3(size.x * 0.52f, 0.13f, 0.028f),
                _mutedAccentMaterial ?? _accentMaterial);
            CreateVisual(root, "RoofUnit",
                new Vector3(size.x * 0.16f, size.y + 0.24f, -size.z * 0.10f),
                new Vector3(size.x * 0.38f, 0.48f, size.z * 0.34f),
                _maintenanceMaterial ?? _deckMaterial,
                castShadows: true);
            CreateVisual(root, "RoofAntenna",
                new Vector3(-size.x * 0.24f, size.y + 0.90f, 0f),
                new Vector3(0.075f, 1.75f, 0.075f),
                _darkMaterial ?? _wallMaterial,
                castShadows: true);
            CreateVisual(root, "Beacon",
                new Vector3(-size.x * 0.24f, size.y + 1.80f, 0f),
                new Vector3(0.12f, 0.12f, 0.12f),
                _mutedAccentMaterial ?? _accentMaterial);
        }

        private static void HideBoundRenderer(Transform bound)
        {
            if (bound == null)
                return;
            var renderer = bound.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
        }

        private GameObject CreateMetricChild(
            Transform scaledParent,
            string name,
            Vector3 metricLocalPosition,
            Vector3 metricScale,
            Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(scaledParent, false);

            var parentScale = scaledParent.localScale;
            go.transform.localPosition = new Vector3(
                SafeDivide(metricLocalPosition.x, parentScale.x),
                SafeDivide(metricLocalPosition.y, parentScale.y),
                SafeDivide(metricLocalPosition.z, parentScale.z));
            go.transform.localScale = new Vector3(
                SafeDivide(metricScale.x, parentScale.x),
                SafeDivide(metricScale.y, parentScale.y),
                SafeDivide(metricScale.z, parentScale.z));

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return go;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
        }

        private static GameObject CreateVisual(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool castShadows = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null)
                    renderer.sharedMaterial = material;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return go;
        }

        private Material CloneTint(Material source, Color color, string name, float metallic, float smoothness)
        {
            if (source == null)
                return null;

            var clone = new Material(source) { name = name };
            if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", color);
            if (clone.HasProperty("_Color")) clone.SetColor("_Color", color);
            if (clone.HasProperty("_Metallic")) clone.SetFloat("_Metallic", metallic);
            if (clone.HasProperty("_Smoothness")) clone.SetFloat("_Smoothness", smoothness);
            if (clone.HasProperty("_Glossiness")) clone.SetFloat("_Glossiness", smoothness);
            _ownedMaterials.Add(clone);
            return clone;
        }

        private static Material FindMaterial(Renderer[] renderers, string materialName)
        {
            if (renderers == null || string.IsNullOrWhiteSpace(materialName))
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

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
