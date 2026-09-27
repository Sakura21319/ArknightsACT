using System;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Authoritative runtime-facing base stats for a playable operator.
    /// PRTS progression data should configure this component; run/status effects should flow
    /// through CombatStats/ICombatStatModifier instead of mutating authored base values.
    /// </summary>
    [Serializable]
    public struct OperatorBaseStats
    {
        [SerializeField, Min(1f)] private float maxHealth;
        [SerializeField, Min(0f)] private float attack;
        [SerializeField, Min(0f)] private float physicalDefense;
        [SerializeField] private float artsResistance;
        [SerializeField, Min(0.05f)] private float attackInterval;
        [SerializeField, Min(0f)] private float basicAttackRange;
        [SerializeField, Min(0f)] private float skillRange;

        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float Attack => Mathf.Max(0f, attack);
        public float PhysicalDefense => Mathf.Max(0f, physicalDefense);
        public float ArtsResistance => artsResistance;
        public float AttackInterval => Mathf.Max(0.05f, attackInterval);
        public float BasicAttackRange => Mathf.Max(0f, basicAttackRange);
        public float SkillRange => Mathf.Max(0f, skillRange);

        public OperatorBaseStats(
            float maxHealth,
            float attack,
            float physicalDefense,
            float artsResistance,
            float attackInterval = 1f,
            float basicAttackRange = 1f,
            float skillRange = 1f)
        {
            this.maxHealth = Mathf.Max(1f, maxHealth);
            this.attack = Mathf.Max(0f, attack);
            this.physicalDefense = Mathf.Max(0f, physicalDefense);
            this.artsResistance = artsResistance;
            this.attackInterval = Mathf.Max(0.05f, attackInterval);
            this.basicAttackRange = Mathf.Max(0f, basicAttackRange);
            this.skillRange = Mathf.Max(0f, skillRange);
        }

        public static OperatorBaseStats Lerp(OperatorBaseStats from, OperatorBaseStats to, float t)
        {
            t = Mathf.Clamp01(t);
            return new OperatorBaseStats(
                Mathf.Lerp(from.MaxHealth, to.MaxHealth, t),
                Mathf.Lerp(from.Attack, to.Attack, t),
                Mathf.Lerp(from.PhysicalDefense, to.PhysicalDefense, t),
                Mathf.Lerp(from.ArtsResistance, to.ArtsResistance, t),
                Mathf.Lerp(from.AttackInterval, to.AttackInterval, t),
                Mathf.Lerp(from.BasicAttackRange, to.BasicAttackRange, t),
                Mathf.Lerp(from.SkillRange, to.SkillRange, t));
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class OperatorRuntimeStats : MonoBehaviour
    {
        [SerializeField] private OperatorBaseStats baseStats =
            new(100f, 10f, 0f, 0f, 1f, 1f, 1f);

        private CombatEntity _entity;

        public OperatorBaseStats BaseStats => baseStats;
        public float BaseMaxHealth => baseStats.MaxHealth;
        public float BaseAttack => baseStats.Attack;
        public float BasePhysicalDefense => baseStats.PhysicalDefense;
        public float BaseArtsResistance => baseStats.ArtsResistance;
        public float BaseAttackInterval => baseStats.AttackInterval;
        public float BaseBasicAttackRange => baseStats.BasicAttackRange;
        public float BaseSkillRange => baseStats.SkillRange;

        public float MaxHealth => Mathf.Max(1f, Resolve(CombatStatType.MaxHealth, BaseMaxHealth));
        public float Attack => Mathf.Max(0f, Resolve(CombatStatType.Attack, BaseAttack));
        public float PhysicalDefense => Stats != null ? Stats.PhysicalDefense : BasePhysicalDefense;
        public float ArtsResistance => Stats != null ? Stats.ArtsResistance : BaseArtsResistance;
        public float AttackSpeedMultiplier => Stats != null ? Stats.AttackSpeedMultiplier : 1f;
        public float AttackInterval =>
            Mathf.Max(0.05f, Resolve(CombatStatType.AttackInterval, BaseAttackInterval)) /
            Mathf.Max(0.05f, AttackSpeedMultiplier);
        public float BasicAttackRange =>
            Mathf.Max(0f, Resolve(CombatStatType.BasicAttackRange, BaseBasicAttackRange));
        public float SkillRange =>
            Mathf.Max(0f, Resolve(CombatStatType.SkillRange, BaseSkillRange));

        public float BasicAttackRangeMultiplier =>
            BaseBasicAttackRange > 0.0001f
                ? Mathf.Max(0f, BasicAttackRange / BaseBasicAttackRange)
                : 1f;

        public float GetSkillRangeMultiplier(int slot)
        {
            var global = BaseSkillRange > 0.0001f
                ? Mathf.Max(0f, SkillRange / BaseSkillRange)
                : 1f;
            var slotStat = Mathf.Clamp(slot, 1, 2) == 1
                ? CombatStatType.Skill1RangeMultiplier
                : CombatStatType.Skill2RangeMultiplier;
            return global * Mathf.Max(0f, Resolve(slotStat, 1f));
        }

        private CombatEntity Entity => _entity != null ? _entity : (_entity = GetComponent<CombatEntity>());
        private CombatStats Stats => Entity != null ? Entity.Stats : GetComponent<CombatStats>();
        private Health Health => Entity != null ? Entity.Health : GetComponent<Health>();

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            ApplyBaseToCoreComponents(refillHealth: false);
        }

        public void ConfigureBase(
            float maxHealth,
            float attack,
            float physicalDefense,
            float artsResistance,
            float attackInterval = 1f,
            float basicAttackRange = 1f,
            float skillRange = 1f,
            bool refillHealth = true)
        {
            baseStats = new OperatorBaseStats(
                maxHealth,
                attack,
                physicalDefense,
                artsResistance,
                attackInterval,
                basicAttackRange,
                skillRange);
            ApplyBaseToCoreComponents(refillHealth);
        }

        public void ConfigureBase(OperatorBaseStats value, bool refillHealth = true)
        {
            baseStats = value;
            ApplyBaseToCoreComponents(refillHealth);
        }

        public void ConfigureBasePreserveHealthRatio(OperatorBaseStats value)
        {
            var health = Health;
            var ratio = health != null && health.MaxHealth > 0f
                ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth)
                : 1f;

            baseStats = value;
            ApplyBaseToCoreComponents(refillHealth: false);

            if (health != null)
                health.SetCurrentHealth(health.MaxHealth * ratio);
        }

        public void ConfigureCore(
            float maxHealth,
            float attack,
            float physicalDefense,
            float artsResistance,
            bool refillHealth)
        {
            baseStats = new OperatorBaseStats(
                maxHealth,
                attack,
                physicalDefense,
                artsResistance,
                BaseAttackInterval,
                BaseBasicAttackRange,
                BaseSkillRange);
            ApplyBaseToCoreComponents(refillHealth);
        }

        public void SetBaseAttack(float value)
        {
            baseStats = new OperatorBaseStats(
                BaseMaxHealth,
                value,
                BasePhysicalDefense,
                BaseArtsResistance,
                BaseAttackInterval,
                BaseBasicAttackRange,
                BaseSkillRange);
        }

        public void ApplyBaseToCoreComponents(bool refillHealth)
        {
            var health = Health;
            if (health != null)
                health.SetMaxHealth(MaxHealth, refillHealth);

            var stats = Stats;
            if (stats != null)
            {
                stats.SetBasePhysicalDefense(BasePhysicalDefense);
                stats.SetBaseArtsResistance(BaseArtsResistance);
            }
        }

        public void RefreshResolvedMaxHealth(bool preserveHealthRatio = true)
        {
            var health = Health;
            if (health == null)
                return;

            var ratio = health.MaxHealth > 0f
                ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth)
                : 1f;
            health.SetMaxHealth(MaxHealth, refill: !preserveHealthRatio);
            if (preserveHealthRatio)
                health.SetCurrentHealth(health.MaxHealth * ratio);
        }

        public void RefreshResolvedMaxHealthFromModifierChange(bool healAddedCapacity)
        {
            var health = Health;
            if (health == null)
                return;

            var oldMax = health.MaxHealth;
            var oldCurrent = health.CurrentHealth;
            var newMax = MaxHealth;
            health.SetMaxHealth(newMax, refill: false);
            if (healAddedCapacity && newMax > oldMax)
                health.SetCurrentHealth(Mathf.Min(newMax, oldCurrent + (newMax - oldMax)));
            else
                health.SetCurrentHealth(Mathf.Min(newMax, oldCurrent));
        }

        private float Resolve(CombatStatType stat, float baseValue)
        {
            var stats = Stats;
            return stats != null ? stats.Resolve(stat, baseValue) : baseValue;
        }
    }
}
