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

            // The current production layout is a fixed 2x2 town. Its coordinates are the urban
            // grammar, not a random theme roll: residential, commercial, industrial, checkpoint.
            // The older theme-driven fallback below remains useful if a larger experimental map is
            // brought back later.
            if (block.Coordinate == new Vector2Int(0, 0))
                return ChernobogDistrictType.Residential;
            if (block.Coordinate == new Vector2Int(1, 0))
                return ChernobogDistrictType.Commercial;
            if (block.Coordinate == new Vector2Int(0, 1))
                return ChernobogDistrictType.Industrial;
            if (block.Coordinate == new Vector2Int(1, 1))
                return ChernobogDistrictType.Checkpoint;

            if (block.Type == RogueliteBlockType.Start || block.Type == RogueliteBlockType.Shop ||
                block.Theme == RogueliteChunkTheme.SafePlaza)
                return ChernobogDistrictType.Plaza;

            if (block.Type == RogueliteBlockType.Boss || block.Theme == RogueliteChunkTheme.BossArena)
                return ChernobogDistrictType.Checkpoint;

            if (block.Theme == RogueliteChunkTheme.Facility)
                return ChernobogDistrictType.ServiceYard;

            if (block.Type == RogueliteBlockType.EmergencyCombat)
                return PositiveMod(blockIndex + stageIndex, 2) == 0
                    ? ChernobogDistrictType.RuinedBlock
                    : ChernobogDistrictType.Checkpoint;

            switch (block.Theme)
            {
                case RogueliteChunkTheme.Street:
                    return PositiveMod(blockIndex + stageIndex, 3) == 0
                        ? ChernobogDistrictType.Alley
                        : ChernobogDistrictType.MainStreet;
                case RogueliteChunkTheme.CoverLane:
                    return PositiveMod(blockIndex + stageIndex, 2) == 0
                        ? ChernobogDistrictType.ServiceYard
                        : ChernobogDistrictType.Checkpoint;
                default:
                    var roll = PositiveMod(blockIndex * 7 + stageIndex * 5, 4);
                    return roll switch
                    {
                        0 => ChernobogDistrictType.RuinedBlock,
                        1 => ChernobogDistrictType.Alley,
                        2 => ChernobogDistrictType.MainStreet,
                        _ => ChernobogDistrictType.ServiceYard
                    };
            }
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
