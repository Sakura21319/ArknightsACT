using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Characters.Schwarz;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    public sealed class SchwarzArmorBreakTalentTests
    {
        [Test]
        public void Proc_AppliesDefenseDownBeforeBasicAttackMitigation()
        {
            var sourceGo = new GameObject("SchwarzTalentSource");
            sourceGo.AddComponent<Health>().SetMaxHealth(100f);
            var source = sourceGo.AddComponent<CombatEntity>();
            source.SetTeam(Team.Player);

            var talent = sourceGo.AddComponent<SchwarzArmorBreakTalent>();
            talent.ConfigureOfficialBaseTalent(
                procChance: 1f,
                attackMultiplier: 1.6f,
                defenseDownMagnitude: -0.2f,
                defenseDownSeconds: 5f);

            var targetGo = new GameObject("SchwarzTalentTarget");
            targetGo.AddComponent<Health>().SetMaxHealth(300f);
            var target = targetGo.AddComponent<CombatEntity>();
            target.SetTeam(Team.Enemy);
            target.Stats.SetBasePhysicalDefense(100f);

            var result = DamageSystem.Apply(new DamageContext(
                source,
                source,
                target,
                100f,
                DamageType.Physical,
                Vector2.zero,
                tags: DamageTags.BasicAttack));

            Assert.That(result.Applied, Is.True);
            Assert.That(target.Status.Has(CombatStatusIds.DefenseDown), Is.True);
            Assert.That(target.Stats.BasePhysicalDefense, Is.EqualTo(100f).Within(0.001f));
            Assert.That(target.Stats.PhysicalDefense, Is.EqualTo(80f).Within(0.001f));
            Assert.That(result.PreMitigationDamage, Is.EqualTo(160f).Within(0.001f));
            Assert.That(result.EffectiveDefense, Is.EqualTo(80f).Within(0.001f));
            Assert.That(result.Damage, Is.EqualTo(80f).Within(0.001f));

            Object.DestroyImmediate(sourceGo);
            Object.DestroyImmediate(targetGo);
        }

    }
}
