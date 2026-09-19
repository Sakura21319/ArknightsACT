using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Shared visual scale rules for Chernobog city generation.
    /// Keeps city proportions out of individual generators.
    /// </summary>
    public static class ChernobogCityScaleProfile
    {
        public const float ResidentialHeightMin = 14f;
        public const float ResidentialHeightMax = 24f;
        public const float CommercialHeightMin = 10f;
        public const float CommercialHeightMax = 18f;
        public const float IndustrialHeightMin = 25f;
        public const float IndustrialHeightMax = 45f;
        public const float HorizonBuildingHeight = 80f;

        public static float GetHeight(ChernobogDistrictType district, int seed)
        {
            var t = Mathf.Abs(seed % 100) / 100f;
            switch (district)
            {
                case ChernobogDistrictType.Industrial:
                    return Mathf.Lerp(IndustrialHeightMin, IndustrialHeightMax, t);
                case ChernobogDistrictType.Commercial:
                    return Mathf.Lerp(CommercialHeightMin, CommercialHeightMax, t);
                default:
                    return Mathf.Lerp(ResidentialHeightMin, ResidentialHeightMax, t);
            }
        }
    }
}
