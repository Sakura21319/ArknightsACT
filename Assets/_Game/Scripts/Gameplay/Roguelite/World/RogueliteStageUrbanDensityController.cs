using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Opens the procedural city back up after the urban/street passes have finished.
    /// Playable rooms are intentionally untouched; this pass only thins sealed/decorative architecture
    /// so every combat cell keeps readable sightlines and breathing room.
    /// </summary>
    [DefaultExecutionOrder(24)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageUrbanDensityController : MonoBehaviour
    {
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
            _nextResolveAt = Time.unscaledTime + 0.12f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            var urban = stage.transform.Find("[Chernobog_UrbanArchitecture]");
            var streets = stage.transform.Find("[Chernobog_CityStreets]");
            if (urban == null || streets == null)
                return;

            var disabled = ThinUrbanArchitecture(urban) + ThinStreetBuildings(streets);
            Physics.SyncTransforms();
            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/UrbanDensity] Stage {stageMap.StageIndex}: disabled {disabled} decorative building groups to keep combat sightlines open.", this);
        }

        private int ThinUrbanArchitecture(Transform root)
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

        private int ThinStreetBuildings(Transform root)
        {
            var disabled = 0;
            for (var blockIndex = 0; blockIndex < stageMap.Blocks.Count; blockIndex++)
            {
                var cell = root.Find($"Block_{blockIndex:00}_CityStreet");
                if (cell == null)
                    continue;

                var coordinate = stageMap.Blocks[blockIndex].Coordinate;
                var northEdge = coordinate.y == stageMap.Height - 1;
                var rightEdge = coordinate.x == stageMap.Width - 1;

                // Interior cells should feel like streets, not narrow canyons: most keep either zero or
                // one sealed north-side building. The far north edge can keep a denser skyline because
                // actors cannot be visually trapped behind another row of playable cells there.
                var northBudget = northEdge ? (PositiveMod(blockIndex, 2) == 0 ? 2 : 1) :
                    (PositiveMod(blockIndex + stageMap.StageIndex, 2) == 0 ? 1 : 0);
                var eastBudget = rightEdge && PositiveMod(coordinate.y + stageMap.StageIndex, 2) == 0 ? 1 : 0;

                var northSeen = 0;
                var eastSeen = 0;
                for (var i = 0; i < cell.childCount; i++)
                {
                    var child = cell.GetChild(i);
                    if (child == null)
                        continue;

                    if (IsNorthStreetBuilding(child.name))
                    {
                        if (northSeen++ >= northBudget)
                        {
                            child.gameObject.SetActive(false);
                            disabled++;
                        }
                        continue;
                    }

                    if (child.name == "SealedSideBuilding")
                    {
                        if (eastSeen++ >= eastBudget)
                        {
                            child.gameObject.SetActive(false);
                            disabled++;
                        }
                    }
                }
            }
            return disabled;
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

        private static bool IsNorthStreetBuilding(string name)
        {
            return name == "SealedCityBuilding" ||
                   name == "SealedUtilityBuilding" ||
                   name == "ClosedStorefront";
        }

        private static int PositiveMod(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
