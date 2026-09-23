using System;

namespace ArknightsACT.Combat
{
    [Serializable]
    public struct CombatStatModifier
    {
        public CombatStatType Stat;
        public float Flat;
        public float AdditivePercent;
        public bool ScaleByApplicationMagnitude;

        public CombatStatModifier(
            CombatStatType stat,
            float flat = 0f,
            float additivePercent = 0f,
            bool scaleByApplicationMagnitude = false)
        {
            Stat = stat;
            Flat = flat;
            AdditivePercent = additivePercent;
            ScaleByApplicationMagnitude = scaleByApplicationMagnitude;
        }
    }
}
