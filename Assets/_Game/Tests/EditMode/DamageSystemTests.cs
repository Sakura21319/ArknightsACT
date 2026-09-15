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

        [Test]
        public void Apply_UsesOutgoingThenIncomingDamageModifiers()
        {
            var sourceGo = new GameObject("ModifiedSource");
            sourceGo.AddComponent<Health>().SetMaxHealth(100f);
            var source = sourceGo.AddComponent<CombatEntity>();
            source.SetTeam(Team.Player);
            var outgoing = sourceGo.AddComponent<TestDamageModifier>();
            outgoing.OutgoingMultiplier = 1.5f;

            var targetGo = new GameObject("ModifiedTarget");
            var health = targetGo.AddComponent<Health>();
            health.SetMaxHealth(100f);
            var target = targetGo.AddComponent<CombatEntity>();
            target.SetTeam(Team.Enemy);
            var incoming = targetGo.AddComponent<TestDamageModifier>();
            incoming.IncomingMultiplier = 0.8f;

            var context = new DamageContext(source, source, target, 20f, DamageType.Arts, Vector2.zero);
            var result = DamageSystem.Apply(context);

            Assert.That(result.Applied, Is.True);
            Assert.That(result.Damage, Is.EqualTo(24f).Within(0.001f));
            Assert.That(health.CurrentHealth, Is.EqualTo(76f).Within(0.001f));

            Object.DestroyImmediate(sourceGo);
            Object.DestroyImmediate(targetGo);
        }
    }

    public sealed class TestDamageModifier : MonoBehaviour, IDamageModifier
    {
        public float OutgoingMultiplier { get; set; } = 1f;
        public float IncomingMultiplier { get; set; } = 1f;

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage) =>
            currentDamage * OutgoingMultiplier;

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) =>
            currentDamage * IncomingMultiplier;
    }
}
