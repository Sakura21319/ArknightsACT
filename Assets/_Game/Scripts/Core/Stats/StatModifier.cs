namespace ArknightsACT.Core.Stats
{
    public readonly struct StatModifier
    {
        public readonly float Flat;
        public readonly float Percent;

        public StatModifier(float flat, float percent)
        {
            Flat = flat;
            Percent = percent;
        }
    }
}
