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
            var source = CreateEntity("Source", Team.Player, 100f);
            var target = CreateEntity("Target", Team.Enemy, 100f);

            var result = DamageSystem.Apply(new DamageContext(
                source, source, target, 20f, DamageType.Physical, Vector2.zero));

            Assert.That(result.Applied, Is.True);
            Assert.That(result.Damage, Is.EqualTo(20f));
            Assert.That(target.Health.CurrentHealth, Is.EqualTo(80f));
            Destroy(source, target);
        }

        [Test]
        public void Apply_UsesOutgoingThenMitigationThenIncomingModifiers()
        {
            var source = CreateEntity("ModifiedSource", Team.Player, 100f);
            source.gameObject.AddComponent<TestDamageModifier>().OutgoingMultiplier = 1.5f;

            var target = CreateEntity("ModifiedTarget", Team.Enemy, 100f);
            target.Stats.SetBaseArtsResistance(20f);
            target.gameObject.AddComponent<TestDamageModifier>().IncomingMultiplier = 0.8f;

            var result = DamageSystem.Apply(new DamageContext(
                source, source, target, 20f, DamageType.Arts, Vector2.zero));

            Assert.That(result.RawDamage, Is.EqualTo(20f).Within(0.001f));
            Assert.That(result.PreMitigationDamage, Is.EqualTo(30f).Within(0.001f));
            Assert.That(result.MitigatedDamage, Is.EqualTo(24f).Within(0.001f));
            Assert.That(result.Damage, Is.EqualTo(19.2f).Within(0.001f));
            Destroy(source, target);
        }

        [Test]
        public void PhysicalDamage_UsesDefenseAndFivePercentFloor()
        {
            var source = CreateEntity("PhysicalSource", Team.Player, 100f);
            var target = CreateEntity("PhysicalTarget", Team.Enemy, 200f);
            target.Stats.SetBasePhysicalDefense(30f);

            var normal = DamageSystem.Apply(new DamageContext(
                source, source, target, 100f, DamageType.Physical, Vector2.zero));

            Assert.That(normal.Damage, Is.EqualTo(70f).Within(0.001f));
            Assert.That(normal.EffectiveDefense, Is.EqualTo(30f).Within(0.001f));

            target.Health.SetMaxHealth(200f, refill: true);
            target.Stats.SetBasePhysicalDefense(1000f);

            var floored = DamageSystem.Apply(new DamageContext(
                source, source, target, 100f, DamageType.Physical, Vector2.zero));

            Assert.That(floored.Damage, Is.EqualTo(5f).Within(0.001f));
            Destroy(source, target);
        }

        [Test]
        public void ArtsDamage_UsesResistanceAndAllowsNegativeResistance()
        {
            var source = CreateEntity("ArtsSource", Team.Player, 100f);
            var target = CreateEntity("ArtsTarget", Team.Enemy, 300f);
            target.Stats.SetBaseArtsResistance(25f);

            var resisted = DamageSystem.Apply(new DamageContext(
                source, source, target, 100f, DamageType.Arts, Vector2.zero));

            Assert.That(resisted.Damage, Is.EqualTo(75f).Within(0.001f));

            target.Health.SetMaxHealth(300f, refill: true);
            target.Stats.SetBaseArtsResistance(-20f);

            var amplified = DamageSystem.Apply(new DamageContext(
                source, source, target, 100f, DamageType.Arts, Vector2.zero));

            Assert.That(amplified.Damage, Is.EqualTo(120f).Within(0.001f));
            Destroy(source, target);
        }

        [Test]
        public void TrueDamage_IgnoresDefenseAndResistance()
        {
            var source = CreateEntity("TrueSource", Team.Player, 100f);
            var target = CreateEntity("TrueTarget", Team.Enemy, 300f);
            target.Stats.SetBasePhysicalDefense(999f);
            target.Stats.SetBaseArtsResistance(95f);

            var result = DamageSystem.Apply(new DamageContext(
                source, source, target, 100f, DamageType.True, Vector2.zero));

            Assert.That(result.Damage, Is.EqualTo(100f).Within(0.001f));
            Assert.That(result.EffectiveDefense, Is.EqualTo(0f));
            Assert.That(result.EffectiveResistance, Is.EqualTo(0f));
            Destroy(source, target);
        }

        [Test]
        public void Penetration_IsPerHitAndDoesNotMutateTargetDefense()
        {
            var source = CreateEntity("PenSource", Team.Player, 100f);
            var target = CreateEntity("PenTarget", Team.Enemy, 300f);
            target.Stats.SetBasePhysicalDefense(80f);

            var result = DamageSystem.Apply(new DamageContext(
                source,
                source,
                target,
                100f,
                DamageType.Physical,
                Vector2.zero,
                penetration: new DamagePenetration(
                    physicalDefenseIgnoreFlat: 10f,
                    physicalDefenseIgnorePercent: 0.25f)));

            Assert.That(result.EffectiveDefense, Is.EqualTo(50f).Within(0.001f));
            Assert.That(result.Damage, Is.EqualTo(50f).Within(0.001f));
            Assert.That(target.Stats.PhysicalDefense, Is.EqualTo(80f).Within(0.001f));
            Destroy(source, target);
        }

        [Test]
        public void TargetStatAura_ReducesDefenseBeforePenetrationWithoutMutatingTarget()
        {
            var source = CreateEntity("AuraSource", Team.Player, 100f);
            source.gameObject.AddComponent<TestTargetStatModifier>().PhysicalDefensePercent = -0.20f;

            var target = CreateEntity("AuraTarget", Team.Enemy, 300f);
            target.Stats.SetBasePhysicalDefense(100f);

            var result = DamageSystem.Apply(new DamageContext(
                source,
                source,
                target,
                100f,
                DamageType.Physical,
                Vector2.zero,
                penetration: new DamagePenetration(physicalDefenseIgnoreFlat: 10f)));

            // Target aura: 100 -> 80; per-hit penetration: 80 -> 70.
            Assert.That(result.EffectiveDefense, Is.EqualTo(70f).Within(0.001f));
            Assert.That(result.Damage, Is.EqualTo(30f).Within(0.001f));
            Assert.That(target.Stats.PhysicalDefense, Is.EqualTo(100f).Within(0.001f));
            Destroy(source, target);
        }

        [Test]
        public void PenetrationModifier_ExtendsPerHitPenetration()
        {
            var source = CreateEntity("ModifierSource", Team.Player, 100f);
            source.gameObject.AddComponent<TestPenetrationModifier>().PhysicalPercent = 0.5f;

            var target = CreateEntity("ModifierTarget", Team.Enemy, 300f);
            target.Stats.SetBasePhysicalDefense(60f);

            var result = DamageSystem.Apply(new DamageContext(
                source, source, target, 100f, DamageType.Physical, Vector2.zero));

            Assert.That(result.EffectiveDefense, Is.EqualTo(30f).Within(0.001f));
            Assert.That(result.Damage, Is.EqualTo(70f).Within(0.001f));
            Destroy(source, target);
        }

        private static CombatEntity CreateEntity(string name, Team team, float maxHealth)
        {
            var go = new GameObject(name);
            go.AddComponent<Health>().SetMaxHealth(maxHealth);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(team);
            return entity;
        }

        private static void Destroy(params CombatEntity[] entities)
        {
            for (var i = 0; i < entities.Length; i++)
                if (entities[i] != null)
                    Object.DestroyImmediate(entities[i].gameObject);
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

    public sealed class TestTargetStatModifier : MonoBehaviour, ICombatTargetStatModifier
    {
        public float PhysicalDefensePercent { get; set; }

        public float ModifyTargetStat(in DamageContext context, CombatStatType stat, float currentValue)
        {
            if (stat != CombatStatType.PhysicalDefense)
                return currentValue;
            return currentValue * Mathf.Max(0f, 1f + PhysicalDefensePercent);
        }
    }

    public sealed class TestPenetrationModifier : MonoBehaviour, IDamagePenetrationModifier
    {
        public float PhysicalPercent { get; set; }

        public DamagePenetration ModifyPenetration(in DamageContext context, DamagePenetration current) =>
            current.Add(new DamagePenetration(physicalDefenseIgnorePercent: PhysicalPercent));
    }
}
