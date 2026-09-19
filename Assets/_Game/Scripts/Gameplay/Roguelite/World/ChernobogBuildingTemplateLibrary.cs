using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Shared parameters for future Chernobog building generators.
    /// Prevents every district from becoming unrelated primitive boxes.
    /// </summary>
    public static class ChernobogBuildingTemplateLibrary
    {
        public struct Template
        {
            public float width;
            public float depth;
            public float height;
            public bool industrial;
        }

        public static readonly Template ResidentialTower = new Template
        {
            width = 12f,
            depth = 8f,
            height = 14f,
            industrial = false
        };

        public static readonly Template IndustrialFactory = new Template
        {
            width = 22f,
            depth = 14f,
            height = 24f,
            industrial = true
        };

        public static readonly Template CheckpointFacility = new Template
        {
            width = 16f,
            depth = 10f,
            height = 12f,
            industrial = true
        };
    }
}
