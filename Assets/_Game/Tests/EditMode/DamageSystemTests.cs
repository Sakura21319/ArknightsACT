using ArknightsACT.Combat;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    public sealed class DamageSystemTests
    {
        [Test]
        public void Apply_DamagesEnemy()
        {
            var sourceGo = new GameObject("Source");
            sourceGo.AddComponent<Health>().SetMaxHealth(100f);
            var source = sourceGo.AddComponent<CombatEntity>();
            source.SetTeam(Team.Player);

            var targetGo = new GameObject("Target");
            var health = targetGo.AddComponent<Health>();
            health.SetMaxHealth(100f);
            var target = targetGo.AddComponent<CombatEntity>();
            target.SetTeam(Team.Enemy);

            var context = new DamageContext(source, source, target, 20f, DamageType.Physical, Vector2.zero);
            var result = DamageSystem.Apply(context);

            Assert.That(result.Applied, Is.True);
            Assert.That(result.Damage, Is.EqualTo(20f));
            Assert.That(health.CurrentHealth, Is.EqualTo(80f));

            Object.DestroyImmediate(sourceGo);
            Object.DestroyImmediate(targetGo);
        }
    }
}
