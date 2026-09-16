using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Thins the older industrial shell layer after district templates have built their authored street
    /// buildings. District street buildings are no longer deleted here: their density is controlled at
    /// generation time, which preserves a readable city silhouette and avoids blocks that suddenly have
    /// no architecture at all.
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
            if (divisor <= 0)
                return 0;
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
