using ArknightsACT.Gameplay.Abilities;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    internal sealed class PlayerSkillLifecycleTestSkill : IPlayerSkill, IPlayerSkillAmmoConsumer
    {
        private float _points;
        private bool _active;
        private int _ammo;

        public int Slot { get; set; } = 1;
        public string DisplayName { get; set; } = "Lifecycle Test";
        public float CooldownRemaining => 0f;
        public float SkillPoints => _points;
        public float SkillPointCost { get; set; } = 10f;
        public float SkillPointRatio => SkillPointCost > 0f
            ? Mathf.Clamp01(_points / SkillPointCost)
            : 0f;
        public float NaturalSkillPointPerSecond => 1f;
        public bool IsCasting { get; set; }
        public bool IsActive => _active;
        public PlayerSkillLifecycleType LifecycleType { get; set; }
        public float ActiveDurationSeconds { get; set; }
        public float ActiveSecondsRemaining { get; set; }
        public int AmmoRemaining => _ammo;
        public int AmmoCapacity { get; set; }
        public bool ConsumeAmmoOnBasicAttackStarted { get; set; } = true;

        public void ConfigureActive(bool active, int ammo = 0)
        {
            _active = active;
            _ammo = Mathf.Max(0, ammo);
        }

        public bool TryCast() => false;
        public void TickSkillPoints(float deltaTime, float recoveryMultiplier, float flatRecoveryPerSecond) { }
        public void GainSkillPoints(float amount) => SetSkillPoints(_points + amount);
        public void SetSkillPoints(float amount) => _points = Mathf.Clamp(amount, 0f, SkillPointCost);
        public void ReduceCooldown(float seconds) => GainSkillPoints(seconds);

        public bool TryConsumeAmmo()
        {
            if (!_active || LifecycleType != PlayerSkillLifecycleType.Ammo || _ammo <= 0)
                return false;

            _ammo--;
            if (_ammo == 0)
                _active = false;
            return true;
        }
    }

    public sealed class PlayerSkillLifecycleTests
    {
        [Test]
        public void DetachedSkill_DefaultsToDetachedAndUsesSpReadiness()
        {
            var skill = new PlayerSkillLifecycleTestSkill
            {
                LifecycleType = PlayerSkillLifecycleType.Detached,
                SkillPointCost = 20f
            };
            skill.SetSkillPoints(20f);

            Assert.That(PlayerSkillLifecycleUtility.GetLifecycleType(skill),
                Is.EqualTo(PlayerSkillLifecycleType.Detached));
            Assert.That(PlayerSkillLifecycleUtility.IsReady(skill), Is.True);
            Assert.That(PlayerSkillLifecycleUtility.CanReceiveSkillPoints(skill), Is.True);
        }

        [Test]
        public void PermanentSkill_StaysActiveAndCannotReceiveSp()
        {
            var skill = new PlayerSkillLifecycleTestSkill
            {
                LifecycleType = PlayerSkillLifecycleType.Permanent,
                SkillPointCost = 20f
            };
            skill.SetSkillPoints(20f);
            skill.ConfigureActive(true);

            Assert.That(PlayerSkillLifecycleUtility.IsReady(skill), Is.False);
            Assert.That(PlayerSkillLifecycleUtility.CanReceiveSkillPoints(skill), Is.False);
            Assert.That(PlayerSkillLifecycleUtility.GetActiveHudValue(skill), Is.EqualTo("ACTIVE"));
            Assert.That(PlayerSkillLifecycleUtility.GetLifecycleLabel(skill), Is.EqualTo("永久"));
        }

        [Test]
        public void DurationSkill_ExposesRemainingTimeForHud()
        {
            var skill = new PlayerSkillLifecycleTestSkill
            {
                LifecycleType = PlayerSkillLifecycleType.Duration,
                ActiveDurationSeconds = 20f,
                ActiveSecondsRemaining = 7.5f
            };
            skill.ConfigureActive(true);

            Assert.That(PlayerSkillLifecycleUtility.GetActiveHudValue(skill), Is.EqualTo("7.5s"));
            Assert.That(PlayerSkillLifecycleUtility.GetLifecycleLabel(skill), Is.EqualTo("持续"));
        }

        [Test]
        public void AmmoSkill_EndsOnlyWhenLastRoundIsConsumed()
        {
            var skill = new PlayerSkillLifecycleTestSkill
            {
                LifecycleType = PlayerSkillLifecycleType.Ammo,
                AmmoCapacity = 3
            };
            skill.ConfigureActive(true, ammo: 3);

            Assert.That(PlayerSkillLifecycleUtility.GetActiveHudValue(skill), Is.EqualTo("3 / 3"));
            Assert.That(skill.TryConsumeAmmo(), Is.True);
            Assert.That(skill.IsActive, Is.True);
            Assert.That(skill.AmmoRemaining, Is.EqualTo(2));
            Assert.That(skill.TryConsumeAmmo(), Is.True);
            Assert.That(skill.IsActive, Is.True);
            Assert.That(skill.TryConsumeAmmo(), Is.True);
            Assert.That(skill.IsActive, Is.False);
            Assert.That(skill.AmmoRemaining, Is.Zero);
        }
    }
}
