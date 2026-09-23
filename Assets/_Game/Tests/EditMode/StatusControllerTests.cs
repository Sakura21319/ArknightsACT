using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    public sealed class StatusControllerTests
    {
        [Test]
        public void LegacyApply_MakesStatusImmediatelyAvailable()
        {
            var entity = CreateEntity("LegacyStatus", Team.Player);

            entity.Status.Apply(CombatStatusType.Shock, 2f);

            Assert.That(entity.Status.Has(CombatStatusType.Shock), Is.True);
            Assert.That(entity.Status.Remaining(CombatStatusType.Shock), Is.GreaterThan(0f));
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void Cold_ModifiesMoveAndAttackSpeed()
        {
            var entity = CreateEntity("ColdTarget", Team.Enemy);

            Assert.That(entity.Status.Apply(CombatStatusIds.Cold, 4f), Is.True);
            Assert.That(entity.Stats.MoveSpeedMultiplier, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(entity.Stats.AttackSpeedMultiplier, Is.EqualTo(0.7f).Within(0.001f));
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void ColdAppliedTwice_ReactsIntoFreeze()
        {
            var entity = CreateEntity("FreezeTarget", Team.Enemy);

            entity.Status.Apply(CombatStatusIds.Cold, 4f);
            entity.Status.Apply(CombatStatusIds.Cold, 4f);

            Assert.That(entity.Status.Has(CombatStatusIds.Cold), Is.False);
            Assert.That(entity.Status.Has(CombatStatusIds.Freeze), Is.True);
            Assert.That(CombatActionUtility.IsBlocked(entity, CombatActionMask.Movement), Is.True);
            Assert.That(CombatActionUtility.IsBlocked(entity, CombatActionMask.BasicAttack), Is.True);
            Assert.That(CombatActionUtility.IsBlocked(entity, CombatActionMask.Skill), Is.True);
            Assert.That(CombatActionUtility.IsBlocked(entity, CombatActionMask.Interaction), Is.True);
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void Disarm_BlocksOnlyBasicAttackAmongCoreCombatActions()
        {
            var entity = CreateEntity("DisarmTarget", Team.Player);

            entity.Status.Apply(CombatStatusIds.Disarm, 4f);

            Assert.That(CombatActionUtility.IsBlocked(entity, CombatActionMask.BasicAttack), Is.True);
            Assert.That(CombatActionUtility.IsBlocked(entity, CombatActionMask.Movement), Is.False);
            Assert.That(CombatActionUtility.IsBlocked(entity, CombatActionMask.Skill), Is.False);
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void DefenseDown_UsesApplicationMagnitude()
        {
            var entity = CreateEntity("DefenseDownTarget", Team.Enemy);
            entity.Stats.SetBasePhysicalDefense(100f);

            entity.Status.Apply(CombatStatusIds.DefenseDown, duration: 5f, magnitude: 0.35f);

            Assert.That(entity.Stats.PhysicalDefense, Is.EqualTo(65f).Within(0.001f));
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void ResistanceDown_UsesFlatMagnitude()
        {
            var entity = CreateEntity("ResistanceDownTarget", Team.Enemy);
            entity.Stats.SetBaseArtsResistance(30f);

            entity.Status.Apply(CombatStatusIds.ResistanceDown, duration: 5f, magnitude: 20f);

            Assert.That(entity.Stats.ArtsResistance, Is.EqualTo(10f).Within(0.001f));
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void Fragile_IncreasesIncomingDamage()
        {
            var source = CreateEntity("FragileSource", Team.Player);
            var target = CreateEntity("FragileTarget", Team.Enemy);
            target.Health.SetMaxHealth(200f);

            target.Status.Apply(CombatStatusIds.Fragile, duration: 5f, magnitude: 0.25f);

            var result = DamageSystem.Apply(new DamageContext(
                source, source, target, 100f, DamageType.True, Vector2.zero));

            Assert.That(result.Damage, Is.EqualTo(125f).Within(0.001f));
            Object.DestroyImmediate(source.gameObject);
            Object.DestroyImmediate(target.gameObject);
        }

        [Test]
        public void ActiveStatuses_CanBeCopiedAcrossPlayerSwitch()
        {
            var source = CreateEntity("StatusSourcePlayer", Team.Player);
            var destination = CreateEntity("StatusDestinationPlayer", Team.Player);

            source.Status.Apply(CombatStatusIds.Cold, 4f);
            source.Status.Apply(
                CombatStatusIds.Burn,
                duration: 2.05f,
                source: source,
                owner: source,
                magnitude: 3f,
                periodicDamageType: DamageType.Arts,
                sourceId: "TestBurn");

            source.Status.CopyActiveTo(destination.Status);

            Assert.That(destination.Status.Has(CombatStatusIds.Cold), Is.True);
            Assert.That(destination.Status.Has(CombatStatusIds.Burn), Is.True);

            Object.DestroyImmediate(source.gameObject);
            Object.DestroyImmediate(destination.gameObject);
        }

        [Test]
        public void CopyActiveTo_DoesNotReapplyDestinationResistance()
        {
            var source = CreateEntity("FrozenSourcePlayer", Team.Player);
            var destination = CreateEntity("FreezeImmuneDestination", Team.Player);
            var resistance = destination.gameObject.AddComponent<StatusResistanceProfile>();
            resistance.ConfigureIdRule(CombatStatusIds.Freeze, immune: true);

            source.Status.Apply(CombatStatusIds.Freeze, 2.5f);
            source.Status.CopyActiveTo(destination.Status);

            Assert.That(destination.Status.Has(CombatStatusIds.Freeze), Is.True);
            Assert.That(destination.Status.Remaining(CombatStatusIds.Freeze), Is.GreaterThan(0f));

            Object.DestroyImmediate(source.gameObject);
            Object.DestroyImmediate(destination.gameObject);
        }

        [Test]
        public void Freeze_InvokesInterruptHandlers()
        {
            var entity = CreateEntity("InterruptTarget", Team.Player);
            var handler = entity.gameObject.AddComponent<TestCombatInterruptHandler>();

            entity.Status.Apply(CombatStatusIds.Freeze, 2.5f);

            Assert.That((handler.LastActions & CombatActionMask.Movement) != 0, Is.True);
            Assert.That((handler.LastActions & CombatActionMask.Dash) != 0, Is.True);
            Assert.That((handler.LastActions & CombatActionMask.BasicAttack) != 0, Is.True);
            Assert.That((handler.LastActions & CombatActionMask.Skill) != 0, Is.True);
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void FreezeImmuneTarget_KeepsColdInsteadOfConsumingIt()
        {
            var entity = CreateEntity("FreezeImmuneColdTarget", Team.Enemy);
            var resistance = entity.gameObject.AddComponent<StatusResistanceProfile>();
            resistance.ConfigureIdRule(CombatStatusIds.Freeze, immune: true);

            entity.Status.Apply(CombatStatusIds.Cold, 4f);
            var secondApplied = entity.Status.Apply(CombatStatusIds.Cold, 4f);

            Assert.That(secondApplied, Is.True);
            Assert.That(entity.Status.Has(CombatStatusIds.Cold), Is.True);
            Assert.That(entity.Status.Has(CombatStatusIds.Freeze), Is.False);
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void StatusResistance_IdRuleCanMakeStatusImmune()
        {
            var entity = CreateEntity("ImmuneTarget", Team.Enemy);
            var resistance = entity.gameObject.AddComponent<StatusResistanceProfile>();
            resistance.ConfigureIdRule(CombatStatusIds.Freeze, immune: true);

            var applied = entity.Status.Apply(CombatStatusIds.Freeze, 2.5f);

            Assert.That(applied, Is.False);
            Assert.That(entity.Status.Has(CombatStatusIds.Freeze), Is.False);
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void StatusResistance_TagRuleScalesDuration()
        {
            var entity = CreateEntity("ResistantTarget", Team.Enemy);
            var resistance = entity.gameObject.AddComponent<StatusResistanceProfile>();
            resistance.ConfigureTagRule(
                CombatStatusTags.HardCrowdControl,
                durationMultiplier: 0.4f);

            var applied = entity.Status.Apply(CombatStatusIds.Freeze, 5f);

            Assert.That(applied, Is.True);
            Assert.That(entity.Status.Remaining(CombatStatusIds.Freeze), Is.EqualTo(2f).Within(0.05f));
            Object.DestroyImmediate(entity.gameObject);
        }

        [Test]
        public void BurnDefinition_IsPeriodicArtsDamage()
        {
            Assert.That(CombatStatusCatalog.TryGet(CombatStatusIds.Burn, out var burn), Is.True);
            Assert.That(burn.TickInterval, Is.EqualTo(0.65f).Within(0.001f));
            Assert.That(burn.DefaultDuration, Is.EqualTo(2.05f).Within(0.001f));
            Assert.That(burn.PeriodicDamageType, Is.EqualTo(DamageType.Arts));
            Assert.That(burn.PeriodicUsesApplicationMagnitude, Is.True);
            Assert.That((burn.Tags & CombatStatusTags.DamageOverTime) != 0, Is.True);
        }

        private static CombatEntity CreateEntity(string name, Team team)
        {
            var go = new GameObject(name);
            go.AddComponent<Health>().SetMaxHealth(100f);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(team);
            return entity;
        }
    }

    public sealed class TestCombatInterruptHandler : MonoBehaviour, ICombatActionInterruptHandler
    {
        public CombatActionMask LastActions { get; private set; }

        public void InterruptCombatActions(CombatActionMask actions)
        {
            LastActions |= actions;
        }
    }
}
