using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public enum ChernobogDistrictType
    {
        MainStreet,
        Alley,
        Plaza,
        ServiceYard,
        RuinedBlock,
        Checkpoint,
        Residential,
        Commercial,
        Industrial
    }

    [DisallowMultipleComponent]
    public sealed class RogueliteDistrictTemplate25D : MonoBehaviour
    {
        [SerializeField] private ChernobogDistrictType districtType;
        public ChernobogDistrictType DistrictType => districtType;

        public void Configure(ChernobogDistrictType type)
        {
            districtType = type;
        }
    }

    /// <summary>
    /// Assigns an explicit urban-space template to every gameplay block. Theme/type still decide
    /// encounters and progression; this component gives the presentation layer a stable city grammar:
    /// main street, alley, plaza, service yard, ruined block or checkpoint.
    /// </summary>
    [DefaultExecutionOrder(18)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageDistrictTemplateController : MonoBehaviour
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
            _nextResolveAt = Time.unscaledTime + 0.10f;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null || stage == _preparedStage)
                return;

            var counts = new Dictionary<ChernobogDistrictType, int>();
            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var block = FindBlockTransform(stage.transform, i);
                var data = stageMap.Blocks[i];
                if (block == null || data == null)
                    continue;

                var type = ResolveDistrict(data, i, stageMap.StageIndex);
                var marker = block.GetComponent<RogueliteDistrictTemplate25D>();
                if (marker == null)
                    marker = block.gameObject.AddComponent<RogueliteDistrictTemplate25D>();
                marker.Configure(type);

                counts.TryGetValue(type, out var old);
                counts[type] = old + 1;
            }

            _preparedStage = stage;
            Debug.Log($"[ArknightsACT/DistrictTemplates] Stage {stageMap.StageIndex}: {FormatCounts(counts)}.", this);
        }

        public static ChernobogDistrictType ResolveDistrict(RogueliteBlockState block, int blockIndex, int stageIndex)
        {
            if (block == null)
                return ChernobogDistrictType.MainStreet;

            // District roles follow the seeded logical plan, not absolute quadrant coordinates.
            if (block.Zone == CityZone.Core) return ChernobogDistrictType.Checkpoint;
            if (block.Zone == CityZone.Industrial) return ChernobogDistrictType.Industrial;
            if (block.Zone == CityZone.Ruins) return ChernobogDistrictType.RuinedBlock;
            if (stageIndex == 2)
            {
                if (block.Type == RogueliteBlockType.Boss || block.Type == RogueliteBlockType.EmergencyCombat) return ChernobogDistrictType.Checkpoint;
                if (block.Type == RogueliteBlockType.Shop) return ChernobogDistrictType.Commercial;
                return blockIndex % 3 == 0 ? ChernobogDistrictType.ServiceYard : ChernobogDistrictType.Industrial;
            }
            if (block.Type == RogueliteBlockType.Start) return ChernobogDistrictType.Residential;
            if (block.Type == RogueliteBlockType.Boss) return ChernobogDistrictType.Checkpoint;
            if (block.Type == RogueliteBlockType.Shop) return ChernobogDistrictType.Commercial;
            if (block.Theme == RogueliteChunkTheme.Facility) return ChernobogDistrictType.Industrial;
            if (block.Type == RogueliteBlockType.EmergencyCombat) return ChernobogDistrictType.Checkpoint;
            return block.Theme switch
            {
                RogueliteChunkTheme.Street => ChernobogDistrictType.Residential,
                RogueliteChunkTheme.CoverLane => ChernobogDistrictType.Commercial,
                _ => ChernobogDistrictType.ServiceYard
            };
        }

        private static string FormatCounts(Dictionary<ChernobogDistrictType, int> counts)
        {
            var parts = new List<string>(counts.Count);
            foreach (ChernobogDistrictType type in Enum.GetValues(typeof(ChernobogDistrictType)))
            {
                if (counts.TryGetValue(type, out var count) && count > 0)
                    parts.Add($"{type}={count}");
            }
            return string.Join(", ", parts);
        }

        private static Transform FindBlockTransform(Transform stage, int index)
        {
            return RogueliteStageBlockUtility.FindBlockTransform(stage, index);
        }

        private static int PositiveMod(int value, int divisor)
        {
            return RogueliteStageMath.PositiveMod(value, divisor);
        }
    }
}
