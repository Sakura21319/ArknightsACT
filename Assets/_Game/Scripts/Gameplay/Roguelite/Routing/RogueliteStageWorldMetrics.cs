using System;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>
    /// Shared physical dimensions for the current 2x2 Chernobog town layout. Keeping these values in
    /// one place prevents the runtime floor, presentation passes, lighting and navigation from
    /// drifting apart as the block footprint grows.
    /// </summary>
    public static class RogueliteStageWorldMetrics
    {
        public const float ChunkWidth = 30f;
        public const float ChunkDepth = 24f;
        public const float MainRoadWidth = 6.10f;
        public const float SidewalkWidth = 1.55f;
    }

    /// <summary>
    /// Shared lookup for generated stage blocks. Presentation controllers keep their local wrapper
    /// so their generation code stays readable, while the naming contract lives in one place.
    /// </summary>
    internal static class RogueliteStageBlockUtility
    {
        public static Transform FindBlockTransform(Transform stage, int index)
        {
            if (stage == null || index < 0)
                return null;

            var prefix = $"Block_{index:00}_";
            for (var i = 0; i < stage.childCount; i++)
            {
                var child = stage.GetChild(i);
                if (child != null && child.name.StartsWith(prefix, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }
    }

    internal static class RogueliteStageMath
    {
        public static int PositiveMod(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;

            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
