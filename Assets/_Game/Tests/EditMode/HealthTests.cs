using ArknightsACT.Combat;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    public sealed class HealthTests
    {
        [Test]
        public void TakeDamage_ReducesHealth()
        {
            var go = new GameObject("HealthTest");
            var health = go.AddComponent<Health>();
            health.SetMaxHealth(100f);
            var dealt = health.TakeDamage(25f);
            Assert.That(dealt, Is.EqualTo(25f));
            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
            Object.DestroyImmediate(go);
        }
    }
}
