using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Thins the older industrial shell layer after district templates have built their authored street
    /// buildings. Only occasional legacy silhouettes survive on ordinary blocks; the city streets pass
    /// and the playable architecture pass provide the intentional focal buildings and usable interiors.
    /// </summary>
    [DefaultExecutionOrder(24)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageUrbanDensityController : MonoBehaviour
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
            if (stageMap == null && _context != null)
                stageMap = _context.StageMap;
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

            var urban = stage.transform.Find("[Chernobog_UrbanArchitecture]");
            var streets = stage.transform.Find("[Chernobog_CityStreets]");
            if (urban == null || streets == null)
                return;

            var disabled = ThinLegacyUrbanArchitecture(urban);
            Physics.SyncTransforms();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/UrbanDensity] Stage {stageMap.StageIndex}: disabled {disabled} redundant legacy shell groups; district street buildings were preserved.", this);
        }

        private int ThinLegacyUrbanArchitecture(Transform root)
        {
            var disabled = 0;
            for (var blockIndex = 0; blockIndex < stageMap.Blocks.Count; blockIndex++)
            {
                var cell = root.Find($"Block_{blockIndex:00}_Architecture");
                if (cell == null)
                    continue;

                Transform keep = null;
                var candidateCount = 0;
                for (var i = 0; i < cell.childCount; i++)
                {
                    var child = cell.GetChild(i);
                    if (child != null && IsMajorUrbanBuilding(child.name))
                        candidateCount++;
                }

                if (candidateCount > 0)
                {
                    var blockData = blockIndex < stageMap.Blocks.Count ? stageMap.Blocks[blockIndex] : null;
                    if (!ShouldKeepLegacyFocalBuilding(blockData, blockIndex))
                    {
                        for (var i = 0; i < cell.childCount; i++)
                        {
                            var child = cell.GetChild(i);
                            if (child == null || !IsMajorUrbanBuilding(child.name))
                                continue;
                            child.gameObject.SetActive(false);
                            disabled++;
                        }
                        continue;
                    }

                    var pick = PositiveMod(stageMap.StageIndex * 7 + blockIndex * 11, candidateCount);
                    var seen = 0;
                    for (var i = 0; i < cell.childCount; i++)
                    {
                        var child = cell.GetChild(i);
                        if (child == null || !IsMajorUrbanBuilding(child.name))
                            continue;
                        if (seen++ == pick)
                        {
                            keep = child;
                            break;
                        }
                    }
                }

                for (var i = 0; i < cell.childCount; i++)
                {
                    var child = cell.GetChild(i);
                    if (child == null || !IsMajorUrbanBuilding(child.name) || child == keep)
                        continue;
                    child.gameObject.SetActive(false);
                    disabled++;
                }
            }
            return disabled;
        }

        private bool ShouldKeepLegacyFocalBuilding(RogueliteBlockState block, int blockIndex)
        {
            if (block == null)
                return false;

            var district = RogueliteStageDistrictTemplateController.ResolveDistrict(block, blockIndex, stageMap.StageIndex);
            if (ShouldPlaceCityStreetFocal(block, district, blockIndex) ||
                ShouldPlacePlayableFocal(block, blockIndex))
                return false;

            // Facility/Boss silhouettes remain useful landmarks. Ordinary blocks are allowed one
            // older mass only occasionally; CityStreets and PlayableArchitecture provide the few
            // intentional street frontages and interactive rooms.
            if (block.Theme == RogueliteChunkTheme.Facility || block.Theme == RogueliteChunkTheme.BossArena)
                return true;

            return PositiveMod(stageMap.StageIndex * 13 + blockIndex * 5 + (int)block.Theme, 3) == 0;
        }

        private bool ShouldPlaceCityStreetFocal(RogueliteBlockState block, ChernobogDistrictType district, int blockIndex)
        {
            // Scheme 1 owns all four production quadrants. The authored street frontage and
            // playable building for each role must be the only focal mass in that block; letting the
            // legacy cadence run here would randomly reintroduce a second warehouse/house on top of it.
            if (district == ChernobogDistrictType.Residential ||
                district == ChernobogDistrictType.Commercial ||
                district == ChernobogDistrictType.Industrial ||
                district == ChernobogDistrictType.Checkpoint)
                return true;

            if (district == ChernobogDistrictType.Plaza ||
                block.Theme == RogueliteChunkTheme.Facility ||
                block.Theme == RogueliteChunkTheme.BossArena)
                return false;

            var cadence = block.Type == RogueliteBlockType.EmergencyCombat ? 2 : 3;
            return PositiveMod(stageMap.StageIndex * 17 + blockIndex * 5 + (int)district, cadence) == 0;
        }

        private bool ShouldPlacePlayableFocal(RogueliteBlockState block, int blockIndex)
        {
            if (block.Theme == RogueliteChunkTheme.Street)
                return PositiveMod(stageMap.StageIndex * 11 + blockIndex * 7 + (int)block.Theme, 3) == 0;
            if (block.Theme == RogueliteChunkTheme.CoverLane)
                return PositiveMod(stageMap.StageIndex * 13 + blockIndex * 5 + (int)block.Theme, 2) == 0;
            return false;
        }

        private static bool IsMajorUrbanBuilding(string name)
        {
            return name == "ServiceBuilding" ||
                   name == "RuinedBuildingShell" ||
                   name == "UtilityWarehouse" ||
                   name == "UtilityRelayTower" ||
                   name == "MaintenanceKiosk" ||
                   name == "ServiceCanopy";
        }

        private static int PositiveMod(int value, int divisor)
        {
            return RogueliteStageMath.PositiveMod(value, divisor);
        }
    }
}
