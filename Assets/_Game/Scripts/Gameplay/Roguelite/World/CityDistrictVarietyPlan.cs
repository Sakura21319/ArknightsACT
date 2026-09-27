using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public enum CityRoomLayout { Open, SideWings, RearGallery, StorageAisle }
    public enum CityBattleLayout { OpenCross, BrokenCover, RubbleCorridor, DepotFunnel, OffsetLanes }
    public enum CityBlockPairPurpose { None, FreightTransfer, RooftopRoute }

    public struct CityPlannedLot
    {
        public bool Generate;
        public CityRoomLayout Interior;
        public ChernobogSearchBuildingKind? BuildingOverride;
        public bool ReturnShortcut;
        public bool LandmarkBeacon;
        public bool RooftopAccess;
        public int RooftopConnectionSide;
        public string ThemeSign;
    }

    public struct CityPlannedBlockPair
    {
        public int A;
        public int B;
        public int LotA;
        public int LotB;
        public bool Horizontal;
        public string Label;
        public CityBlockPairPurpose Purpose;
    }

    /// <summary>
    /// Deterministic, stage-local authored variation. The plan only consumes existing corner lots;
    /// landmarks, start/boss cells, tower reservations and facility courts are filtered first.
    /// </summary>
    public sealed class CityDistrictVarietyPlan
    {
        private readonly Dictionary<int, CityPlannedLot> _lots = new();
        private readonly Dictionary<int, CityBattleLayout> _battles = new();
        private readonly List<CityPlannedBlockPair> _venues = new();

        public IReadOnlyDictionary<int, CityPlannedLot> Lots => _lots;
        public IReadOnlyDictionary<int, CityBattleLayout> Battles => _battles;
        public IReadOnlyList<CityPlannedBlockPair> Venues => _venues;
        public int QuietBlockIndex { get; private set; }
        public int Seed { get; private set; }

        public static CityDistrictVarietyPlan Create(RogueliteStageMapController map)
        {
            var plan = new CityDistrictVarietyPlan();
            if (map == null) return plan;
            plan.Seed = map.GenerationSeed;
            plan.QuietBlockIndex = map.StartIndex;

            for (var blockIndex = 0; blockIndex < map.Blocks.Count; blockIndex++)
            {
                var block = map.Blocks[blockIndex];
                if (block == null) continue;
                var centerX = (block.Coordinate.x - (map.Width - 1) * .5f) * 36f;
                var centerZ = (block.Coordinate.y - (map.Height - 1) * .5f) * 30f;
                var reservesCoreLandmark = map.StageIndex <= 2 && Math.Abs(centerX) < 16f && Math.Abs(centerZ) < 12f;
                if (!reservesCoreLandmark && block.Type != RogueliteBlockType.Start && block.Type != RogueliteBlockType.Boss &&
                    block.Type != RogueliteBlockType.Shop && block.Theme != RogueliteChunkTheme.Facility)
                {
                    plan._battles[blockIndex] = block.Zone switch
                    {
                        CityZone.Outskirts => CityBattleLayout.BrokenCover,
                        CityZone.Ruins => CityBattleLayout.RubbleCorridor,
                        CityZone.Industrial => CityBattleLayout.DepotFunnel,
                        CityZone.Core => CityBattleLayout.OffsetLanes,
                        _ => CityBattleLayout.OpenCross
                    };
                }

                for (var lot = 0; lot < 4; lot++)
                {
                    if (!CanUseLot(map, block, lot)) continue;
                    plan._lots[Key(blockIndex, lot)] = new CityPlannedLot
                    {
                        Generate = lot != 0 || Mix(map.GenerationSeed, blockIndex * 83 + lot * 17 + 29) % 100 >= 30,
                        BuildingOverride = ResolveBuilding(block.Zone, Mix(map.GenerationSeed, blockIndex * 131 + lot * 17 + 41) % 5),
                        Interior = ResolveInterior(block.Zone, Mix(map.GenerationSeed, blockIndex * 131 + lot * 17 + 79) % 4)
                    };
                }
            }

            var adjacent = BuildAdjacentPairs(map);
            // A clinic and pharmacy form a legible neighborhood service pair across one shared street.
            var themeCandidates = SelectZonePairs(map, adjacent, CityZone.Outskirts);
            if (themeCandidates.Count == 0) themeCandidates = SelectZonePairs(map, adjacent, CityZone.Ruins);
            if (themeCandidates.Count == 0) themeCandidates = adjacent;
            var themePairIndex = -1;
            if (themeCandidates.Count > 0)
            {
                themePairIndex = Mix(map.GenerationSeed, 301) % themeCandidates.Count;
                var pair = themeCandidates[themePairIndex];
                SetLot(plan, pair.A, pair.LotA, ChernobogSearchBuildingKind.Clinic, CityRoomLayout.SideWings, "街区医疗点 · 诊疗");
                SetLot(plan, pair.B, pair.LotB, ChernobogSearchBuildingKind.Pharmacy, CityRoomLayout.RearGallery, "街区医疗点 · 配药");
            }

            // One cross-cell freight/market place gives neighboring blocks a shared landmark.
            if (adjacent.Count > 0)
            {
                var candidates = SelectZonePairs(map, adjacent, CityZone.Industrial);
                if (candidates.Count == 0) candidates = SelectZonePairs(map, adjacent, CityZone.Ruins);
                if (candidates.Count == 0) candidates = adjacent;
                var themePair = themePairIndex >= 0 ? themeCandidates[themePairIndex] : default;
                candidates = PreferDifferentPair(candidates, pair =>
                    themePairIndex >= 0 && pair.A == themePair.A && pair.B == themePair.B);
                var venue = candidates.Count > 0 ? candidates[Mix(map.GenerationSeed, 509) % candidates.Count] : adjacent[0];
                ForceLot(plan, venue.A, venue.LotA);
                ForceLot(plan, venue.B, venue.LotB);
                venue.Label = "城际货运 / 街口集市";
                venue.Purpose = CityBlockPairPurpose.FreightTransfer;
                plan._venues.Add(venue);
            }

            // A pair of equal-height houses shares a real roof route over the block boundary.
            var roofPairs = new List<CityPlannedBlockPair>();
            foreach (var pair in adjacent)
                if (pair.Horizontal && map.Blocks[pair.A].Zone == map.Blocks[pair.B].Zone &&
                    !IsRooftopAlreadyUsed(plan, pair) &&
                    !HasThemeOverride(plan, pair.A, pair.LotA) && !HasThemeOverride(plan, pair.B, pair.LotB) &&
                    !HasVenueLot(plan, pair)) roofPairs.Add(pair);
            // Small early-stage maps can have every same-zone seam reserved by the theme or freight pair.
            // Keep the route playable by falling back to a cross-zone seam with the same physical constraints.
            if (roofPairs.Count == 0)
                foreach (var pair in adjacent)
                    if (pair.Horizontal && !IsRooftopAlreadyUsed(plan, pair) &&
                        !HasThemeOverride(plan, pair.A, pair.LotA) && !HasThemeOverride(plan, pair.B, pair.LotB) &&
                        !HasVenueLot(plan, pair)) roofPairs.Add(pair);
            // A small stage can reserve every combat-block seam. Broaden only the roof search
            // to safe/shop blocks; CanUseLot still excludes landmark footprints and courts.
            if (roofPairs.Count == 0)
                roofPairs.AddRange(BuildRoofFallbackPairs(map, plan));
            if (roofPairs.Count > 0)
            {
                var roof = roofPairs[Mix(map.GenerationSeed, 733) % roofPairs.Count];
                SetRooftop(plan, roof.A, roof.LotA, roof.LotA < 2 ? -1 : 1);
                SetRooftop(plan, roof.B, roof.LotB, roof.LotB < 2 ? 1 : -1);
                roof.Label = "街区屋顶检修连廊";
                roof.Purpose = CityBlockPairPurpose.RooftopRoute;
                plan._venues.Add(roof);
            }

            var eligibleLots = new List<int>(plan._lots.Keys);
            eligibleLots.Sort((a, b) =>
            {
                var order = Mix(map.GenerationSeed, a * 31 + 907).CompareTo(Mix(map.GenerationSeed, b * 31 + 907));
                return order != 0 ? order : a.CompareTo(b);
            });
            if (eligibleLots.Count > 0)
            {
                foreach (var key in eligibleLots)
                {
                    var lotPlan = plan._lots[key];
                    if (!string.IsNullOrEmpty(lotPlan.ThemeSign) || lotPlan.RooftopAccess || lotPlan.LandmarkBeacon) continue;
                    lotPlan.LandmarkBeacon = true;
                    lotPlan.BuildingOverride = map.Blocks[key / 4].Zone switch
                    {
                        CityZone.Industrial => ChernobogSearchBuildingKind.PowerStation,
                        CityZone.Ruins => ChernobogSearchBuildingKind.RepairShop,
                        _ => ChernobogSearchBuildingKind.ArchiveOffice
                    };
                    lotPlan.Generate = true;
                    lotPlan.ThemeSign = "远程可见的信号档案点";
                    plan._lots[key] = lotPlan;
                    break;
                }
            }

            // Open one interior side door from the alley; its sealed door panel becomes usable after a hold interaction.
            for (var i = 0; i < eligibleLots.Count; i++)
            {
                var key = eligibleLots[i];
                var lotPlan = plan._lots[key];
                if (lotPlan.RooftopAccess || !string.IsNullOrEmpty(lotPlan.ThemeSign)) continue;
                lotPlan.ReturnShortcut = true;
                lotPlan.Generate = true;
                lotPlan.Interior = CityRoomLayout.Open;
                lotPlan.BuildingOverride = ChernobogSearchBuildingKind.AbandonedHouse;
                plan._lots[key] = lotPlan;
                break;
            }
            return plan;
        }

        public bool TryGetLot(int blockIndex, int lot, out CityPlannedLot result) =>
            _lots.TryGetValue(Key(blockIndex, lot), out result);

        public bool TryGetBattle(int blockIndex, out CityBattleLayout result) => _battles.TryGetValue(blockIndex, out result);

        public static int Key(int blockIndex, int lot) => blockIndex * 4 + lot;

        private static List<CityPlannedBlockPair> BuildAdjacentPairs(RogueliteStageMapController map)
        {
            var result = new List<CityPlannedBlockPair>();
            for (var index = 0; index < map.Blocks.Count; index++)
            {
                var block = map.Blocks[index];
                if (!IsEligibleBlock(block)) continue;
                TryAdd(map, block, block.Coordinate + UnityEngine.Vector2Int.right, true, result);
                TryAdd(map, block, block.Coordinate + UnityEngine.Vector2Int.up, false, result);
            }
            result.Sort((a, b) =>
            {
                var order = a.A.CompareTo(b.A);
                if (order != 0) return order;
                order = a.B.CompareTo(b.B);
                return order != 0 ? order : a.LotA.CompareTo(b.LotA);
            });
            return result;
        }

        private static List<CityPlannedBlockPair> SelectZonePairs(RogueliteStageMapController map,
            List<CityPlannedBlockPair> pairs, CityZone zone)
        {
            var result = new List<CityPlannedBlockPair>();
            foreach (var pair in pairs)
                if (map.Blocks[pair.A].Zone == zone && map.Blocks[pair.B].Zone == zone) result.Add(pair);
            return result;
        }

        private static List<CityPlannedBlockPair> BuildRoofFallbackPairs(RogueliteStageMapController map,
            CityDistrictVarietyPlan plan)
        {
            var result = new List<CityPlannedBlockPair>();
            for (var index = 0; index < map.Blocks.Count; index++)
            {
                var a = map.Blocks[index];
                if (a == null || a.Type == RogueliteBlockType.Boss || a.Theme == RogueliteChunkTheme.Facility ||
                    !map.TryGetBlock(a.Coordinate + UnityEngine.Vector2Int.right, out var b) ||
                    b.Type == RogueliteBlockType.Boss || b.Theme == RogueliteChunkTheme.Facility) continue;
                foreach (var row in new[] { 0, 1 })
                {
                    var lotA = row == 0 ? 1 : 3;
                    var lotB = row == 0 ? 0 : 2;
                    if (!CanUseLot(map, a, lotA) || !CanUseLot(map, b, lotB)) continue;
                    var pair = new CityPlannedBlockPair
                    {
                        A = a.Index, B = b.Index, LotA = lotA, LotB = lotB, Horizontal = true
                    };
                    if (IsRooftopAlreadyUsed(plan, pair) || HasThemeOverride(plan, pair.A, pair.LotA) ||
                        HasThemeOverride(plan, pair.B, pair.LotB) || HasVenueLot(plan, pair)) continue;
                    result.Add(pair);
                }
            }
            return result;
        }

        private static ChernobogSearchBuildingKind ResolveBuilding(CityZone zone, int roll) => zone switch
        {
            CityZone.Outskirts => roll < 3 ? ChernobogSearchBuildingKind.Apartment : ChernobogSearchBuildingKind.AbandonedHouse,
            CityZone.Ruins => roll < 3 ? ChernobogSearchBuildingKind.AbandonedHouse : roll == 3 ? ChernobogSearchBuildingKind.RepairShop : ChernobogSearchBuildingKind.Apartment,
            CityZone.Industrial => roll < 3 ? ChernobogSearchBuildingKind.Warehouse : roll == 3 ? ChernobogSearchBuildingKind.RepairShop : ChernobogSearchBuildingKind.PowerStation,
            CityZone.Core => roll < 2 ? ChernobogSearchBuildingKind.ArchiveOffice : roll < 4 ? ChernobogSearchBuildingKind.Checkpoint : ChernobogSearchBuildingKind.Office,
            _ => ChernobogSearchBuildingKind.Apartment
        };

        private static CityRoomLayout ResolveInterior(CityZone zone, int roll) => zone switch
        {
            CityZone.Outskirts => roll == 0 ? CityRoomLayout.Open : CityRoomLayout.SideWings,
            CityZone.Ruins => roll == 0 ? CityRoomLayout.Open : CityRoomLayout.RearGallery,
            CityZone.Industrial => roll == 0 ? CityRoomLayout.Open : CityRoomLayout.StorageAisle,
            CityZone.Core => roll == 0 ? CityRoomLayout.SideWings : CityRoomLayout.RearGallery,
            _ => CityRoomLayout.Open
        };

        private static void TryAdd(RogueliteStageMapController map, RogueliteBlockState a, UnityEngine.Vector2Int coordinate,
            bool horizontal, List<CityPlannedBlockPair> result)
        {
            if (!map.TryGetBlock(coordinate, out var b) || !IsEligibleBlock(b)) return;
            for (var row = 0; row < 2; row++)
            {
                int lotA, lotB;
                if (horizontal)
                {
                    lotA = row == 0 ? 1 : 3;
                    lotB = row == 0 ? 0 : 2;
                }
                else
                {
                    lotA = row == 0 ? 2 : 3;
                    lotB = row == 0 ? 0 : 1;
                }
                if (!CanUseLot(map, a, lotA) || !CanUseLot(map, b, lotB)) continue;
                result.Add(new CityPlannedBlockPair { A = a.Index, B = b.Index, LotA = lotA, LotB = lotB, Horizontal = horizontal });
            }
        }

        private static bool IsEligibleBlock(RogueliteBlockState block) => block != null &&
            block.Type != RogueliteBlockType.Start && block.Type != RogueliteBlockType.Boss &&
            block.Theme != RogueliteChunkTheme.Facility && block.Zone != CityZone.Core;

        private static bool CanUseLot(RogueliteStageMapController map, RogueliteBlockState block, int lot)
        {
            if (block == null || lot < 0 || lot > 3) return false;
            if (block.Theme == RogueliteChunkTheme.Facility && lot == 3) return false;
            if ((block.Type == RogueliteBlockType.Start || block.Type == RogueliteBlockType.Boss) && lot < 2) return false;
            var xSide = lot % 2 == 0 ? -1f : 1f;
            var zSide = lot < 2 ? -1f : 1f;
            var worldX = (block.Coordinate.x - (map.Width - 1) * .5f) * 36f + xSide * 11.2f;
            var worldZ = (block.Coordinate.y - (map.Height - 1) * .5f) * 30f + zSide * 10.2f;
            if (map.StageIndex <= 2 && Math.Abs(worldX) < 16f && Math.Abs(worldZ) < 12f) return false;
            var hasCourt = block.Type == RogueliteBlockType.Start || block.Index % 3 == 0;
            var courtBlockedLot = Math.Abs((block.Coordinate.x - (map.Width - 1) * .5f) * 36f - 10.5f) < 16f &&
                                  Math.Abs((block.Coordinate.y - (map.Height - 1) * .5f) * 30f - 9.4f) < 12f ? 1 : 0;
            return !hasCourt || lot != courtBlockedLot;
        }

        private static void SetLot(CityDistrictVarietyPlan plan, int block, int lot, ChernobogSearchBuildingKind kind,
            CityRoomLayout layout, string sign)
        {
            var key = Key(block, lot);
            if (!plan._lots.TryGetValue(key, out var value)) return;
            value.Generate = true;
            value.BuildingOverride = kind;
            value.Interior = layout;
            value.ThemeSign = sign;
            plan._lots[key] = value;
        }

        private static void SetRooftop(CityDistrictVarietyPlan plan, int block, int lot, int connectionSide)
        {
            var key = Key(block, lot);
            if (!plan._lots.TryGetValue(key, out var value)) return;
            value.Generate = true;
            value.RooftopAccess = true;
            value.RooftopConnectionSide = connectionSide;
            value.Interior = CityRoomLayout.Open;
            plan._lots[key] = value;
        }

        private static void ForceLot(CityDistrictVarietyPlan plan, int block, int lot)
        {
            var key = Key(block, lot);
            if (!plan._lots.TryGetValue(key, out var value)) return;
            value.Generate = true;
            plan._lots[key] = value;
        }

        private static bool IsRooftopAlreadyUsed(CityDistrictVarietyPlan plan, CityPlannedBlockPair pair) =>
            plan._lots.TryGetValue(Key(pair.A, pair.LotA), out var a) && a.RooftopAccess ||
            plan._lots.TryGetValue(Key(pair.B, pair.LotB), out var b) && b.RooftopAccess;

        private static List<CityPlannedBlockPair> PreferDifferentPair(List<CityPlannedBlockPair> source,
            Predicate<CityPlannedBlockPair> isThemePair)
        {
            var result = new List<CityPlannedBlockPair>();
            foreach (var pair in source)
            {
                if (isThemePair(pair)) continue;
                result.Add(pair);
            }
            if (result.Count == 0)
                foreach (var pair in source)
                    if (!isThemePair(pair)) result.Add(pair);
            return result;
        }

        private static bool HasThemeOverride(CityDistrictVarietyPlan plan, int block, int lot) =>
            plan._lots.TryGetValue(Key(block, lot), out var value) && !string.IsNullOrEmpty(value.ThemeSign);

        private static bool HasVenueLot(CityDistrictVarietyPlan plan, CityPlannedBlockPair pair)
        {
            foreach (var venue in plan._venues)
                if (venue.Purpose == CityBlockPairPurpose.FreightTransfer && venue.A == pair.A && venue.B == pair.B &&
                    venue.LotA == pair.LotA && venue.LotB == pair.LotB) return true;
            return false;
        }

        private static int Mix(int seed, int value)
        {
            unchecked
            {
                uint x = (uint)(seed + 0x9E3779B9) ^ (uint)(value * 0x85EBCA6B);
                x ^= x >> 16;
                x *= 0x7FEB352D;
                x ^= x >> 15;
                x *= 0x846CA68B;
                x ^= x >> 16;
                return (int)(x & 0x7fffffff);
            }
        }
    }
}
