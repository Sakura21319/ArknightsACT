using ArknightsACT.Core.Stats;
using NUnit.Framework;

namespace ArknightsACT.Tests
{
    public sealed class StatMathTests
    {
        [Test]
        public void Evaluate_AppliesFlatBeforePercent()
        {
            var result = StatMath.Evaluate(100f, 20f, 0.5f);
            Assert.That(result, Is.EqualTo(180f));
        }
    }
}
