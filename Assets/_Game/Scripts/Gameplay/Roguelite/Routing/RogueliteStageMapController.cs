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

    [Serializable]
    public sealed class RogueliteBlockState
    {
        [SerializeField] private int index;
        [SerializeField] private Vector2Int coordinate;
        [SerializeField] private RogueliteBlockType type;
        [SerializeField] private bool explored;
        [SerializeField] private bool hasNormalChest;
        [SerializeField] private bool hasSpikeChest;
        [SerializeField] private bool hasMonsterChest;

        public int Index => index;
        public Vector2Int Coordinate => coordinate;
        public RogueliteBlockType Type => type;
        public bool Explored => explored;
        public bool HasNormalChest => hasNormalChest;
        public bool HasSpikeChest => hasSpikeChest;
        public bool HasMonsterChest => hasMonsterChest;

        public RogueliteBlockState(
            int blockIndex,
            Vector2Int gridCoordinate,
            RogueliteBlockType blockType,
            bool normalChest,
            bool spikeChest,
            bool monsterChest)
        {
            index = blockIndex;
            coordinate = gridCoordinate;
            type = blockType;
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
    /// Logical stage map used before physical chunk assembly is introduced.
    /// Stage 1 = 2x2 (4 blocks), Stage 2 = 3x2 (6), Stage 3 = 3x3 (9).
    /// Start is fixed at bottom-left and Boss/exit is fixed at top-right. All other block
    /// contents are rerolled per stage/run while remaining fully connected by cardinal edges.
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

        private readonly List<RogueliteBlockState> _blocks = new(9);
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
                ? unchecked(Environment.TickCount * 397 ^ GetInstanceID() ^ StageIndex * 7919)
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
                _blocks.Add(new RogueliteBlockState(index, coordinate, type, false, false, false));
            }

            AssignSpecialCombatBlocks();
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
            var candidates = BuildMutableCandidateIndices();
            if (candidates.Count == 0)
                return;

            var shopChance = Mathf.Clamp01(baseShopChance + (StageIndex - 1) * 0.15f);
            if (_random.NextDouble() < shopChance && candidates.Count > 0)
            {
                var shop = RemoveRandom(candidates);
                ReplaceBlockType(shop, RogueliteBlockType.Shop);
            }

            var emergencyCount = StageIndex switch
            {
                1 => _random.NextDouble() < 0.35 ? 1 : 0,
                2 => 1,
                _ => _random.NextDouble() < 0.45 ? 2 : 1
            };
            for (var i = 0; i < emergencyCount && candidates.Count > 0; i++)
            {
                var emergency = RemoveRandom(candidates);
                ReplaceBlockType(emergency, RogueliteBlockType.EmergencyCombat);
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
                _blocks[i] = new RogueliteBlockState(old.Index, old.Coordinate, old.Type, normal, spike, monster);
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

        private void ReplaceBlockType(int index, RogueliteBlockType type)
        {
            var old = _blocks[index];
            _blocks[index] = new RogueliteBlockState(old.Index, old.Coordinate, type, false, false, false);
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
                    var treasure = block.HasMonsterChest ? "M" : (block.HasSpikeChest ? "X" : (block.HasNormalChest ? "N" : "-"));
                    row += $"[{code}{treasure}] ";
                }
                lines.Add(row.TrimEnd());
            }
            return string.Join("\n", lines);
        }

        private static void ResolveDimensions(int stageIndex, out int width, out int height)
        {
            switch (stageIndex)
            {
                case 2:
                    width = 3;
                    height = 2;
                    break;
                case 3:
                    width = 3;
                    height = 3;
                    break;
                default:
                    width = 2;
                    height = 2;
                    break;
            }
        }
    }
}
