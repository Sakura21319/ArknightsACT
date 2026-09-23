using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    internal sealed class PlayerRuntimeSwitchStateProbe : MonoBehaviour, IPlayerSwitchStateTransfer
    {
        public int Value;

        public void CopySwitchStateTo(Transform destination)
        {
            var target = destination != null ? destination.GetComponent<PlayerRuntimeSwitchStateProbe>() : null;
            if (target != null)
                target.Value = Value;
        }
    }

    internal sealed class PlayerRuntimeTestSkill : MonoBehaviour, IPlayerSkill
    {
        private int _slot;
        private float _points;
        private float _cost = 100f;

        public int Slot => _slot;
        public string DisplayName => "Test";
        public float CooldownRemaining => Mathf.Max(0f, _cost - _points);
        public float SkillPoints => Mathf.Clamp(_points, 0f, _cost);
        public float SkillPointCost => _cost;
        public float SkillPointRatio => _cost > 0f ? Mathf.Clamp01(SkillPoints / _cost) : 0f;
        public float NaturalSkillPointPerSecond => 1f;
        public bool IsCasting => false;

        public void Configure(int slot, float cost, float points)
        {
            _slot = slot;
            _cost = Mathf.Max(1f, cost);
            SetSkillPoints(points);
        }

        public bool TryCast() => false;
        public void TickSkillPoints(float deltaTime, float recoveryMultiplier, float flatRecoveryPerSecond) { }
        public void GainSkillPoints(float amount) => SetSkillPoints(SkillPoints + amount);
        public void SetSkillPoints(float amount) => _points = Mathf.Clamp(amount, 0f, _cost);
        public void ReduceCooldown(float seconds) => GainSkillPoints(seconds);
    }

    public sealed class PlayerRuntimeContextTests
    {
        [Test]
        public void SwitchTo_TransfersRunStateAndPreservesHealthRatio()
        {
            var contextObject = new GameObject("PlayerRuntimeContextTest");
            var context = contextObject.AddComponent<PlayerRuntimeContext>();
            var first = CreatePlayer("Player_A", "Chen", "default", 100f);
            var second = CreatePlayer("Player_B", "Schwarz", "default", 200f);

            try
            {
                var firstHealth = first.GetComponent<Health>();
                firstHealth.SetCurrentHealth(40f);
                first.AddComponent<PlayerRuntimeSwitchStateProbe>().Value = 17;
                second.AddComponent<PlayerRuntimeSwitchStateProbe>().Value = 0;

                context.Configure(first.transform);
                context.RegisterPlayer(second.transform);

                Assert.That(context.SwitchTo(second.transform), Is.True);
                Assert.That(context.ActivePlayer, Is.EqualTo(second.transform));
                Assert.That(first.activeSelf, Is.False);
                Assert.That(second.activeSelf, Is.True);
                Assert.That(second.GetComponent<Health>().CurrentHealth, Is.EqualTo(80f).Within(0.001f));
                Assert.That(second.GetComponent<PlayerRuntimeSwitchStateProbe>().Value, Is.EqualTo(17));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(contextObject);
            }
        }

        [Test]
        public void TrySwitchTo_UsesOperatorAndSkinIdentity()
        {
            var contextObject = new GameObject("PlayerRuntimeContextIdentityTest");
            var first = CreatePlayer("Player_A", "Chen", "default", 100f);
            var second = CreatePlayer("Player_B", "Schwarz", "snow#1", 100f);
            var context = contextObject.AddComponent<PlayerRuntimeContext>();

            try
            {
                context.Configure(first.transform);
                context.RegisterPlayer(second.transform);

                Assert.That(context.TrySwitchTo("Schwarz", "default"), Is.False);
                Assert.That(context.TrySwitchTo("Schwarz", "snow#1"), Is.True);
                Assert.That(context.ActivePlayer, Is.EqualTo(second.transform));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(contextObject);
            }
        }

        [Test]
        public void SwitchTo_SameOperatorSkin_PreservesSkillPointRatio()
        {
            var contextObject = new GameObject("PlayerRuntimeContextSkinStateTest");
            var defaultSkin = CreatePlayer("Schwarz_Default", "Schwarz", "default", 100f);
            var snowSkin = CreatePlayer("Schwarz_Snow", "Schwarz", "snow#1", 100f);

            var sourceSkill1 = defaultSkin.AddComponent<PlayerRuntimeTestSkill>();
            sourceSkill1.Configure(1, 100f, 25f);
            var sourceSkill2 = defaultSkin.AddComponent<PlayerRuntimeTestSkill>();
            sourceSkill2.Configure(2, 80f, 60f);
            defaultSkin.AddComponent<PlayerSkillController>();

            var targetSkill1 = snowSkin.AddComponent<PlayerRuntimeTestSkill>();
            targetSkill1.Configure(1, 200f, 0f);
            var targetSkill2 = snowSkin.AddComponent<PlayerRuntimeTestSkill>();
            targetSkill2.Configure(2, 40f, 0f);
            snowSkin.AddComponent<PlayerSkillController>();

            var context = contextObject.AddComponent<PlayerRuntimeContext>();

            try
            {
                context.Configure(defaultSkin.transform);
                context.RegisterPlayer(snowSkin.transform);

                Assert.That(context.SwitchTo(snowSkin.transform), Is.True);
                Assert.That(targetSkill1.SkillPoints, Is.EqualTo(50f).Within(0.001f));
                Assert.That(targetSkill2.SkillPoints, Is.EqualTo(30f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(defaultSkin);
                Object.DestroyImmediate(snowSkin);
                Object.DestroyImmediate(contextObject);
            }
        }

        [Test]
        public void TrySwitchTo_DistinguishesSkinsOfSameOperator()
        {
            var contextObject = new GameObject("PlayerRuntimeContextSkinTest");
            var defaultSkin = CreatePlayer("Schwarz_Default", "Schwarz", "default", 100f);
            var snowSkin = CreatePlayer("Schwarz_Snow", "Schwarz", "snow#1", 100f);
            var context = contextObject.AddComponent<PlayerRuntimeContext>();

            try
            {
                context.Configure(defaultSkin.transform);
                context.RegisterPlayer(snowSkin.transform);

                Assert.That(context.TrySwitchTo("Schwarz", "snow#1"), Is.True);
                Assert.That(context.ActivePlayer, Is.EqualTo(snowSkin.transform));
                Assert.That(context.ActivePlayer.GetComponent<PlayableOperatorIdentity>().SkinId, Is.EqualTo("snow#1"));
            }
            finally
            {
                Object.DestroyImmediate(defaultSkin);
                Object.DestroyImmediate(snowSkin);
                Object.DestroyImmediate(contextObject);
            }
        }

        private static GameObject CreatePlayer(
            string name,
            string operatorId,
            string skinId,
            float maxHealth)
        {
            var go = new GameObject(name);
            var health = go.AddComponent<Health>();
            health.SetMaxHealth(maxHealth);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);
            var identity = go.AddComponent<PlayableOperatorIdentity>();
            identity.Configure(operatorId, operatorId, skinId, string.Empty, string.Empty, string.Empty);
            return go;
        }
    }
}
