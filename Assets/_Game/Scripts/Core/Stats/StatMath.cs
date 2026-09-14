namespace ArknightsACT.Core.Stats
{
    public static class StatMath
    {
        public static float Evaluate(float baseValue, float flatSum, float percentSum)
        {
            return (baseValue + flatSum) * (1f + percentSum);
        }
    }
}
