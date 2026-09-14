using ArknightsACT.Combat.Status;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    public sealed class StatusControllerTests
    {
        [Test]
        public void Apply_MakesStatusImmediatelyAvailable()
        {
            var go = new GameObject("StatusTest");
            var status = go.AddComponent<StatusController>();
            status.Apply(CombatStatusType.Shock, 2f);
            Assert.That(status.Has(CombatStatusType.Shock), Is.True);
            Assert.That(status.Remaining(CombatStatusType.Shock), Is.GreaterThan(0f));
            Object.DestroyImmediate(go);
        }
    }
}
