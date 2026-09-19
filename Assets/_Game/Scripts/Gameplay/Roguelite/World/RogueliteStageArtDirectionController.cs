using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Final art-direction rhythm pass for the modular Chernobog kit.
    ///
    /// The previous prototype looked noisy because every repeated module advertised itself with the
    /// same light/stripe/maintenance detail. This pass deliberately removes repetition: most floor
    /// plates stay quiet, wall lights are sparse, cover accents are intermittent and old blockout
    /// road/ID marks are hidden. No gameplay collider or navigation object is changed.
    /// </summary>
    [DefaultExecutionOrder(17)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageArtDirectionController : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;
        private RogueliteStageRuntimeContext _context;

        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context != null)
                stageMap ??= _context.StageMap;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.12f;

            if (stageMap == null && _context != null)
                stageMap = _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context != null && _context.StageRoot != null
                ? _context.StageRoot.gameObject
                : null;
            if (stage == null || stage == _preparedStage)
                return;

            var kitRoot = stage.transform.Find("[Chernobog_ModularKit]");
            if (kitRoot == null)
                return;

            HidePrototypeMarkings(stage.transform);
            ReduceFloorDetailNoise(kitRoot);
            ApplyPerimeterRhythm(kitRoot);
            ApplyCoverRhythm(stage.transform);
            ApplyInfrastructureRhythm(kitRoot);

            _preparedStage = stage;
            Debug.Log(
                $"[ArknightsACT/ArtDirection] Stage {stageMap.StageIndex}: repeated markings/lights reduced and modular rhythm applied.",
                this);
        }

        private void HidePrototypeMarkings(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var current = transforms[i];
                if (current == null)
                    continue;

                var name = current.name;
                if (!string.Equals(name, "RoadStripe", StringComparison.Ordinal) &&
                    !string.Equals(name, "FacilityAccent", StringComparison.Ordinal) &&
                    !string.Equals(name, "ShopAccent", StringComparison.Ordinal) &&
                    !string.Equals(name, "BlockMarker", StringComparison.Ordinal))
                    continue;

                var renderer = current.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = false;
            }
        }

        private void ReduceFloorDetailNoise(Transform kitRoot)
        {
            var floorDetails = kitRoot.Find("FloorDetails");
            if (floorDetails == null)
                return;

            // The modular placer is already sparse. Keep roughly one third of those inserts so the
            // broad deck reads first and service details become points of interest rather than a grid.
            for (var i = 0; i < floorDetails.childCount; i++)
            {
                var child = floorDetails.GetChild(i);
                if (child == null)
                    continue;
                var keep = PositiveMod(stageMap.StageIndex * 5 + i * 7, 3) == 0;
                child.gameObject.SetActive(keep);
            }
        }

        private void ApplyPerimeterRhythm(Transform kitRoot)
        {
            var perimeter = kitRoot.Find("Perimeter");
            if (perimeter == null)
                return;

            for (var sideIndex = 0; sideIndex < perimeter.childCount; sideIndex++)
            {
                var side = perimeter.GetChild(sideIndex);
                if (side == null || side.name.StartsWith("Corner_", StringComparison.Ordinal))
                    continue;

                for (var i = 0; i < side.childCount; i++)
                {
                    var module = side.GetChild(i);
                    if (module == null)
                        continue;

                    // One practical light every several wall bays is enough. This is intentionally
                    // irregular so the perimeter does not read as a row of orange UI markers.
                    var light = FindDescendant(module, "ServiceLight");
                    if (light != null)
                    {
                        var divisor = side.name == "North" || side.name == "East" ? 5 : 7;
                        light.gameObject.SetActive(PositiveMod(i + stageMap.StageIndex * 2 + sideIndex, divisor) == 1);
                    }

                    var idStrip = FindDescendant(module, "Optional_IDStrip");
                    if (idStrip != null)
                        idStrip.gameObject.SetActive(PositiveMod(i * 3 + sideIndex + stageMap.StageIndex, 6) == 2);

                    var conduit = FindDescendant(module, "Optional_Conduit");
                    if (conduit != null)
                        conduit.gameObject.SetActive(PositiveMod(i + sideIndex * 2 + stageMap.StageIndex, 4) == 0);

                    var bay = FindDescendant(module, "ServiceBay");
                    if (bay != null)
                        bay.gameObject.SetActive(PositiveMod(i + sideIndex + stageMap.StageIndex, 3) != 1);
                }
            }
        }

        private void ApplyCoverRhythm(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            var serial = 0;
            for (var i = 0; i < transforms.Length; i++)
            {
                var cover = transforms[i];
                if (cover == null || !string.Equals(cover.name, "GridCover", StringComparison.Ordinal))
                    continue;

                var visual = cover.Find("[ModularCover]");
                if (visual == null)
                    continue;

                // Alternate equipment service faces without changing the authoritative cover root.
                // This breaks copy/paste repetition while keeping the same collision footprint.
                if (FindDescendant(visual, "FrontGrill") != null && (serial & 1) == 1)
                    visual.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var baseStripe = FindDescendant(visual, "AssetStripe");
                if (baseStripe != null)
                    baseStripe.gameObject.SetActive(PositiveMod(serial + stageMap.StageIndex, 3) == 0);

                var idStrip = FindDescendant(visual, "Optional_IDStrip");
                if (idStrip != null)
                    idStrip.gameObject.SetActive(PositiveMod(serial * 2 + stageMap.StageIndex, 4) == 1);

                var servicePipe = FindDescendant(visual, "Optional_ServicePipe");
                if (servicePipe != null)
                    servicePipe.gameObject.SetActive(PositiveMod(serial + stageMap.StageIndex, 3) != 1);

                var serviceBox = FindDescendant(visual, "Optional_ServiceBox");
                if (serviceBox != null)
                    serviceBox.gameObject.SetActive(PositiveMod(serial + stageMap.StageIndex, 2) == 0);

                serial++;
            }
        }

        private void ApplyInfrastructureRhythm(Transform kitRoot)
        {
            var infrastructure = kitRoot.Find("Infrastructure");
            if (infrastructure == null)
                return;

            // Keep far-background infrastructure asymmetric. Perfect three-pipe repetition made the
            // skyline feel generated rather than like a maintained mobile-city deck.
            var seenPipes = 0;
            for (var i = 0; i < infrastructure.childCount; i++)
            {
                var child = infrastructure.GetChild(i);
                if (child == null || !child.name.StartsWith("North_Pipe_", StringComparison.Ordinal))
                    continue;

                var keep = seenPipes != PositiveMod(stageMap.StageIndex, 3);
                child.gameObject.SetActive(keep);
                seenPipes++;
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var current = transforms[i];
                if (current != null && string.Equals(current.name, name, StringComparison.Ordinal))
                    return current;
            }
            return null;
        }

        private static int PositiveMod(int value, int divisor)
        {
            return RogueliteStageMath.PositiveMod(value, divisor);
        }
    }
}
