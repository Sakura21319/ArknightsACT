using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    public enum RogueliteBlockType
    {
        Start,
        Combat,
        EmergencyCombat,
        Shop,
        Boss
    }

    public enum RogueliteChunkTheme
    {
        Open,
        Street,
        CoverLane,
        Facility,
        SafePlaza,
        BossArena
    }

    [Serializable]
    public sealed class RogueliteBlockState
    {
        [SerializeField] private int index;
        [SerializeField] private Vector2Int coordinate;
        [SerializeField] private RogueliteBlockType type;
        [SerializeField] private RogueliteChunkTheme theme;
        [SerializeField] private bool explored;
        [SerializeField] private bool hasNormalChest;
        [SerializeField] private bool hasSpikeChest;
        [SerializeField] private bool hasMonsterChest;

        public int Index => index;
        public Vector2Int Coordinate => coordinate;
        public RogueliteBlockType Type => type;
        public RogueliteChunkTheme Theme => theme;
        public bool Explored => explored;
        public bool HasNormalChest => hasNormalChest;
        public bool HasSpikeChest => hasSpikeChest;
        public bool HasMonsterChest => hasMonsterChest;

        public RogueliteBlockState(
            int blockIndex,
            Vector2Int gridCoordinate,
            RogueliteBlockType blockType,
            RogueliteChunkTheme chunkTheme,
            bool normalChest,
            bool spikeChest,
            bool monsterChest)
        {
            index = blockIndex;
            coordinate = gridCoordinate;
            type = blockType;
            theme = chunkTheme;
            explored = false;
            hasNormalChest = normalChest;
            hasSpikeChest = spikeChest;
            hasMonsterChest = monsterChest;
        }

        public bool MarkExplored()
        {
            if (explored)
                return false;
            explored = true;
            return true;
        }
    }

    /// <summary>
    /// Logical city-scale stage map. Every stage keeps the same 2x2 district skeleton; progression
    /// changes the encounter pressure and dressing, while the physical block footprint is large
    /// enough for recognizable roads, lots and usable buildings. Start is bottom-left and Boss/exit
    /// is top-right so the four quadrants remain easy to read.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteStageMapController : MonoBehaviour
    {
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int fixedSeed = 21319;
        [SerializeField, Range(0f, 1f)] private float baseShopChance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float normalChestChance = 0.34f;
        [SerializeField, Range(0f, 1f)] private float spikeChestChance = 0.14f;
        [SerializeField, Range(0f, 1f)] private float monsterChestChance = 0.10f;

        private readonly List<RogueliteBlockState> _blocks = new(16);
        private System.Random _random;

        public IReadOnlyList<RogueliteBlockState> Blocks => _blocks;
        public int StageIndex { get; private set; } = 1;
        public int Width { get; private set; } = 2;
        public int Height { get; private set; } = 2;
        public int StartIndex => 0;
        public int BossIndex => _blocks.Count > 0 ? _blocks.Count - 1 : -1;

        public event Action<int> LayoutGenerated;

        public void Configure(RogueliteRunState state)
        {
            runState = state;
        }

        private void OnEnable()
        {
            if (runState == null)
                runState = RogueliteRunState.Instance;
            GenerateStage(runState != null ? runState.StageIndex : 1);
        }

        public void GenerateStage(int requestedStageIndex)
        {
            StageIndex = Mathf.Clamp(requestedStageIndex, 1, 3);
            ResolveDimensions(StageIndex, out var width, out var height);
            Width = width;
            Height = height;

            var seed = useRandomSeed
                ? unchecked(Environment.TickCount * 397 ^ GetHashCode() ^ StageIndex * 7919)
                : fixedSeed + StageIndex * 7919;
            _random = new System.Random(seed);

            _blocks.Clear();
            var count = Width * Height;
            for (var index = 0; index < count; index++)
            {
                var coordinate = new Vector2Int(index % Width, index / Width);
                var type = index == 0
                    ? RogueliteBlockType.Start
                    : (index == count - 1 ? RogueliteBlockType.Boss : RogueliteBlockType.Combat);
                _blocks.Add(new RogueliteBlockState(
                    index,
                    coordinate,
                    type,
                    RogueliteChunkTheme.Open,
                    false,
                    false,
                    false));
            }

            AssignSpecialCombatBlocks();
            AssignChunkThemes();
            RollTreasureContents();
            LayoutGenerated?.Invoke(StageIndex);
            Debug.Log(BuildDebugSummary(), this);
        }

        public bool TryGetBlock(Vector2Int coordinate, out RogueliteBlockState block)
        {
            block = null;
            if (coordinate.x < 0 || coordinate.y < 0 || coordinate.x >= Width || coordinate.y >= Height)
                return false;
            var index = coordinate.y * Width + coordinate.x;
            if (index < 0 || index >= _blocks.Count)
                return false;
            block = _blocks[index];
            return block != null;
        }

        public void GetNeighborIndices(int blockIndex, List<int> result)
        {
            if (result == null)
                return;
            result.Clear();
            if (blockIndex < 0 || blockIndex >= _blocks.Count)
                return;

            var coordinate = _blocks[blockIndex].Coordinate;
            AddNeighbor(coordinate + Vector2Int.left, result);
            AddNeighbor(coordinate + Vector2Int.right, result);
            AddNeighbor(coordinate + Vector2Int.down, result);
            AddNeighbor(coordinate + Vector2Int.up, result);
        }

        public bool MarkExplored(int blockIndex)
        {
            if (blockIndex < 0 || blockIndex >= _blocks.Count)
                return false;
            var block = _blocks[blockIndex];
            if (block == null || !block.MarkExplored())
                return false;
            runState?.RecordBlockExplored();
            return true;
        }

        private void AssignSpecialCombatBlocks()
        {
            if (Width == 2 && Height == 2)
            {
                // The four quadrants have stable urban roles. Keep the commercial slot available for
                // the shop and reserve the industrial slot for the encounter so the town layout does
                // not change into four visually interchangeable combat cells.
                var fixedLayoutShopChance = Mathf.Clamp01(baseShopChance + (StageIndex - 1) * 0.15f);
                if (_random.NextDouble() < fixedLayoutShopChance)
                    ReplaceBlock(1, RogueliteBlockType.Shop, RogueliteChunkTheme.CoverLane);

                ReplaceBlock(2, RogueliteBlockType.EmergencyCombat, RogueliteChunkTheme.Facility);
                return;
            }

            var candidates = BuildMutableCandidateIndices();
            if (candidates.Count == 0)
                return;

            var shopChance = Mathf.Clamp01(baseShopChance + (StageIndex - 1) * 0.15f);
            if (_random.NextDouble() < shopChance && candidates.Count > 0)
            {
                var shop = RemoveRandom(candidates);
                ReplaceBlock(shop, RogueliteBlockType.Shop, RogueliteChunkTheme.SafePlaza);
            }

            var emergencyCount = StageIndex switch
            {
                1 => 1,
                2 => _random.NextDouble() < 0.45 ? 2 : 1,
                _ => 2
            };
            for (var i = 0; i < emergencyCount && candidates.Count > 0; i++)
            {
                var emergency = RemoveRandom(candidates);
                ReplaceBlock(emergency, RogueliteBlockType.EmergencyCombat, RogueliteChunkTheme.Open);
            }
        }

        private void AssignChunkThemes()
        {
            if (Width == 2 && Height == 2)
            {
                // Scheme 1: residential / commercial / industrial / checkpoint quadrants.
                ReplaceBlock(0, RogueliteBlockType.Start, RogueliteChunkTheme.Street);
                ReplaceBlock(1, _blocks[1].Type, RogueliteChunkTheme.CoverLane);
                ReplaceBlock(2, _blocks[2].Type, RogueliteChunkTheme.Facility);
                ReplaceBlock(3, RogueliteBlockType.Boss, RogueliteChunkTheme.BossArena);
                return;
            }

            ReplaceBlock(StartIndex, RogueliteBlockType.Start, RogueliteChunkTheme.SafePlaza);
            ReplaceBlock(BossIndex, RogueliteBlockType.Boss, RogueliteChunkTheme.BossArena);

            // One authoritative walkable two-floor facility remains enough for the waypoint graph.
            // Other city buildings are supplied by the urban/playable architecture passes.
            var facilityCandidates = new List<int>();
            for (var i = 1; i < _blocks.Count - 1; i++)
            {
                if (_blocks[i].Type != RogueliteBlockType.Shop)
                    facilityCandidates.Add(i);
            }
            if (facilityCandidates.Count > 0)
            {
                var facility = facilityCandidates[_random.Next(facilityCandidates.Count)];
                ReplaceBlock(facility, _blocks[facility].Type, RogueliteChunkTheme.Facility);
            }

            for (var i = 1; i < _blocks.Count - 1; i++)
            {
                var block = _blocks[i];
                if (block.Type == RogueliteBlockType.Shop || block.Theme == RogueliteChunkTheme.Facility)
                    continue;

                var roll = _random.Next(3);
                var theme = roll switch
                {
                    1 => RogueliteChunkTheme.Street,
                    2 => RogueliteChunkTheme.CoverLane,
                    _ => RogueliteChunkTheme.Open
                };
                ReplaceBlock(i, block.Type, theme);
            }
        }

        private void RollTreasureContents()
        {
            for (var i = 0; i < _blocks.Count; i++)
            {
                var old = _blocks[i];
                if (old == null || old.Type == RogueliteBlockType.Start || old.Type == RogueliteBlockType.Boss || old.Type == RogueliteBlockType.Shop)
                    continue;

                var specialRoll = _random.NextDouble();
                var monster = specialRoll < monsterChestChance;
                var spike = !monster && specialRoll < monsterChestChance + spikeChestChance;
                var normal = _random.NextDouble() < normalChestChance;
                _blocks[i] = new RogueliteBlockState(
                    old.Index,
                    old.Coordinate,
                    old.Type,
                    old.Theme,
                    normal,
                    spike,
                    monster);
            }
        }

        private List<int> BuildMutableCandidateIndices()
        {
            var result = new List<int>(_blocks.Count);
            for (var i = 1; i < _blocks.Count - 1; i++)
                result.Add(i);
            return result;
        }

        private int RemoveRandom(List<int> candidates)
        {
            var selected = _random.Next(candidates.Count);
            var value = candidates[selected];
            candidates.RemoveAt(selected);
            return value;
        }

        private void ReplaceBlock(int index, RogueliteBlockType type, RogueliteChunkTheme theme)
        {
            if (index < 0 || index >= _blocks.Count)
                return;
            var old = _blocks[index];
            _blocks[index] = new RogueliteBlockState(
                old.Index,
                old.Coordinate,
                type,
                theme,
                old.HasNormalChest,
                old.HasSpikeChest,
                old.HasMonsterChest);
        }

        private void AddNeighbor(Vector2Int coordinate, List<int> result)
        {
            if (coordinate.x < 0 || coordinate.y < 0 || coordinate.x >= Width || coordinate.y >= Height)
                return;
            result.Add(coordinate.y * Width + coordinate.x);
        }

        private string BuildDebugSummary()
        {
            var lines = new List<string>
            {
                $"[ArknightsACT/StageMap] Stage {StageIndex}: {Width}x{Height} = {_blocks.Count} blocks"
            };
            for (var y = Height - 1; y >= 0; y--)
            {
                var row = string.Empty;
                for (var x = 0; x < Width; x++)
                {
                    var block = _blocks[y * Width + x];
                    var code = block.Type switch
                    {
                        RogueliteBlockType.Start => "S",
                        RogueliteBlockType.Boss => "B",
                        RogueliteBlockType.Shop => "$",
                        RogueliteBlockType.EmergencyCombat => "!",
                        _ => "C"
                    };
                    var theme = block.Theme switch
                    {
                        RogueliteChunkTheme.Facility => "F",
                        RogueliteChunkTheme.Street => "R",
                        RogueliteChunkTheme.CoverLane => "L",
                        RogueliteChunkTheme.SafePlaza => "P",
                        RogueliteChunkTheme.BossArena => "A",
                        _ => "O"
                    };
                    var treasure = block.HasMonsterChest ? "M" : (block.HasSpikeChest ? "X" : (block.HasNormalChest ? "N" : "-"));
                    row += $"[{code}{theme}{treasure}] ";
                }
                lines.Add(row.TrimEnd());
            }
            return string.Join("\n", lines);
        }

        private static void ResolveDimensions(int stageIndex, out int width, out int height)
        {
            width = 2;
            height = 2;
        }
    }
}
