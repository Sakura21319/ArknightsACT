using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Runtime representation of an Arknights tile-shape attack range.
    /// row maps to planar lateral offset and col maps to planar forward offset.
    /// World scale is deliberately supplied by the caller so existing ACT combat spacing is preserved.
    /// </summary>
    public sealed class OperatorRangePattern
    {
        private readonly Vector2Int[] _grids;

        public string Id { get; }
        public IReadOnlyList<Vector2Int> Grids => _grids;
        public float ForwardExtentCells { get; }

        public OperatorRangePattern(string id, params Vector2Int[] grids)
        {
            Id = id ?? string.Empty;
            _grids = grids ?? Array.Empty<Vector2Int>();

            var maxAbsCol = 0;
            for (var i = 0; i < _grids.Length; i++)
                maxAbsCol = Mathf.Max(maxAbsCol, Mathf.Abs(_grids[i].y));
            ForwardExtentCells = Mathf.Max(0.5f, maxAbsCol + 0.5f);
        }

        public bool Contains(
            Vector3 origin,
            Vector3 planarForward,
            Vector3 target,
            float worldForwardReach,
            float cellTolerance = 0.08f)
        {
            if (_grids.Length == 0 || worldForwardReach <= 0f)
                return true;

            var forward = planarForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            var right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            var delta = target - origin;
            delta.y = 0f;
            var forwardDistance = Vector3.Dot(delta, forward);
            var lateralDistance = Vector3.Dot(delta, right);

            var cellSize = Mathf.Max(0.01f, worldForwardReach / ForwardExtentCells);
            var halfCell = cellSize * (0.5f + Mathf.Clamp(cellTolerance, 0f, 0.25f));

            for (var i = 0; i < _grids.Length; i++)
            {
                var grid = _grids[i];
                var cellForward = grid.y * cellSize;
                var cellLateral = grid.x * cellSize;
                if (Mathf.Abs(forwardDistance - cellForward) <= halfCell &&
                    Mathf.Abs(lateralDistance - cellLateral) <= halfCell)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Minimal official range catalog required by the currently playable operators.
    /// Source: Arknights range_table.json. Add new ids here as new operators are integrated.
    /// </summary>
    public static class OperatorRangeCatalog
    {
        private static readonly Dictionary<string, OperatorRangePattern> Patterns =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["1-1"] = Pattern("1-1",
                    Cell(0, 0), Cell(0, 1)),
                ["3-6"] = Pattern("3-6",
                    Cell(1, 0), Cell(1, 1), Cell(1, 2),
                    Cell(0, 0), Cell(0, 1), Cell(0, 2),
                    Cell(-1, 0), Cell(-1, 1), Cell(-1, 2)),
                ["3-9"] = Pattern("3-9",
                    Cell(2, 0), Cell(2, 1), Cell(2, 2),
                    Cell(1, 0), Cell(1, 1), Cell(1, 2), Cell(1, 3),
                    Cell(0, 0), Cell(0, 1), Cell(0, 2), Cell(0, 3), Cell(0, 4),
                    Cell(-1, 0), Cell(-1, 1), Cell(-1, 2), Cell(-1, 3),
                    Cell(-2, 0), Cell(-2, 1), Cell(-2, 2)),
                ["3-12"] = Pattern("3-12",
                    Cell(1, 0), Cell(1, 1),
                    Cell(0, 0), Cell(0, 1), Cell(0, 2), Cell(0, 3),
                    Cell(-1, 0), Cell(-1, 1)),
                ["3-2"] = Pattern("3-2",
                    Cell(0, 0), Cell(0, 1), Cell(0, 2), Cell(0, 3)),
                ["4-1"] = Pattern("4-1",
                    Cell(0, 0), Cell(0, 1), Cell(0, 2), Cell(0, 3), Cell(0, 4)),
                ["x-1"] = Pattern("x-1",
                    Cell(2, 0),
                    Cell(1, -1), Cell(1, 0), Cell(1, 1),
                    Cell(0, -2), Cell(0, -1), Cell(0, 0), Cell(0, 1), Cell(0, 2),
                    Cell(-1, -1), Cell(-1, 0), Cell(-1, 1),
                    Cell(-2, 0))
            };

        public static bool TryGet(string rangeId, out OperatorRangePattern pattern)
        {
            if (string.IsNullOrWhiteSpace(rangeId))
            {
                pattern = null;
                return false;
            }

            return Patterns.TryGetValue(rangeId, out pattern);
        }

        public static float GetForwardReachRatio(
            string baseRangeId,
            string overrideRangeId,
            float fallback = 1f)
        {
            if (!TryGet(baseRangeId, out var basePattern) ||
                !TryGet(overrideRangeId, out var overridePattern) ||
                basePattern.ForwardExtentCells <= 0f)
                return Mathf.Max(0.1f, fallback);

            return Mathf.Max(
                0.1f,
                overridePattern.ForwardExtentCells / basePattern.ForwardExtentCells);
        }

        private static OperatorRangePattern Pattern(string id, params Vector2Int[] cells) =>
            new(id, cells);

        private static Vector2Int Cell(int row, int col) => new(row, col);
    }

    public static class OperatorRangeUtility
    {
        public static string ResolveBasicRangeId(Component owner)
        {
            var progression = owner != null
                ? owner.GetComponent<OperatorProgressionController>()
                : null;
            return progression?.E2Progression?.RangeId ?? string.Empty;
        }

        public static string ResolveSkillRangeId(Component owner, int slot)
        {
            var mastery = owner != null
                ? owner.GetComponent<OperatorSkillMasteryController>()
                : null;
            return mastery != null
                ? mastery.GetRangeId(slot)
                : string.Empty;
        }

        public static float ResolveSkillRangeMultiplier(
            Component owner,
            int slot,
            float fallback = 1f)
        {
            var baseRangeId = ResolveBasicRangeId(owner);
            var skillRangeId = ResolveSkillRangeId(owner, slot);
            return OperatorRangeCatalog.GetForwardReachRatio(
                baseRangeId,
                skillRangeId,
                fallback);
        }

        public static bool Contains(
            string rangeId,
            Vector3 origin,
            Vector3 planarForward,
            Vector3 target,
            float worldForwardReach)
        {
            return !OperatorRangeCatalog.TryGet(rangeId, out var pattern) ||
                   pattern.Contains(
                       origin,
                       planarForward,
                       target,
                       worldForwardReach);
        }

        public static bool ContainsBasic(
            Component owner,
            Vector3 origin,
            Vector3 planarForward,
            Vector3 target,
            float worldForwardReach)
        {
            return Contains(
                ResolveBasicRangeId(owner),
                origin,
                planarForward,
                target,
                worldForwardReach);
        }

        public static bool ContainsSkill(
            Component owner,
            int slot,
            Vector3 origin,
            Vector3 planarForward,
            Vector3 target,
            float worldForwardReach,
            bool fallbackToBasic = true)
        {
            var rangeId = ResolveSkillRangeId(owner, slot);
            if (string.IsNullOrWhiteSpace(rangeId) && fallbackToBasic)
                rangeId = ResolveBasicRangeId(owner);
            return Contains(
                rangeId,
                origin,
                planarForward,
                target,
                worldForwardReach);
        }
    }
}
