using System;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Collectibles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class CollectibleInventory : MonoBehaviour, IDamageModifier
    {
        private readonly Dictionary<string, int> _stacks = new();
        private readonly Dictionary<string, CollectibleDefinition> _definitions = new();

        private CombatEntity _entity;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private float _baseMaxHealth;

        public event Action<CollectibleDefinition, int> Collected;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _baseMaxHealth = _entity != null && _entity.Health != null ? _entity.Health.MaxHealth : 100f;
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackHit += OnBasicAttackHit;
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCastSucceeded;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackHit -= OnBasicAttackHit;
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCastSucceeded;
        }

        public int GetStackCount(CollectibleDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                return 0;
            return _stacks.TryGetValue(definition.Id, out var count) ? count : 0;
        }

        public bool CanAcquire(CollectibleDefinition definition) =>
            definition != null &&
            !string.IsNullOrWhiteSpace(definition.Id) &&
            GetStackCount(definition) < definition.MaxStacks;

        public bool Acquire(CollectibleDefinition definition)
        {
            if (!CanAcquire(definition))
                return false;

            var nextStack = GetStackCount(definition) + 1;
            _stacks[definition.Id] = nextStack;
            _definitions[definition.Id] = definition;

            if (definition.EffectType == CollectibleEffectType.MaxHealthPercent)
                RecalculateMaxHealth();

            Collected?.Invoke(definition, nextStack);
            Debug.Log(
                $"[ArknightsACT/Roguelite] Collected '{definition.DisplayName}' stack {nextStack}/{definition.MaxStacks}.",
                this);
            return true;
        }

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage)
        {
            var percent = SumEffect(CollectibleEffectType.AllDamagePercent);
            percent += context.DamageType switch
            {
                DamageType.Physical => SumEffect(CollectibleEffectType.PhysicalDamagePercent),
                DamageType.Arts => SumEffect(CollectibleEffectType.ArtsDamagePercent),
                DamageType.True => SumEffect(CollectibleEffectType.TrueDamagePercent),
                _ => 0f
            };
            return currentDamage * Mathf.Max(0f, 1f + percent);
        }

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) => currentDamage;

        private void OnBasicAttackHit(CombatEntity target)
        {
            var seconds = SumEffect(CollectibleEffectType.CooldownOnBasicHitSeconds);
            if (seconds > 0f)
                ReduceAllSkillCooldowns(seconds);
        }

        private void OnSkillCastSucceeded(int slot)
        {
            if (_entity?.Health != null && !_entity.Health.IsDead)
            {
                var healFraction = SumEffect(CollectibleEffectType.HealOnSkillCastFraction);
                if (healFraction > 0f)
                    _entity.Health.Heal(_entity.Health.MaxHealth * healFraction);
            }

            var cooldownSeconds = SumEffect(CollectibleEffectType.CooldownOnSkillCastSeconds);
            if (cooldownSeconds > 0f)
                ReduceAllSkillCooldowns(cooldownSeconds);
        }

        private void ReduceAllSkillCooldowns(float seconds)
        {
            if (_skills == null || seconds <= 0f)
                return;
            _skills.Skill1?.ReduceCooldown(seconds);
            _skills.Skill2?.ReduceCooldown(seconds);
        }

        private void RecalculateMaxHealth()
        {
            if (_entity?.Health == null)
                return;

            var oldMax = _entity.Health.MaxHealth;
            var newMax = Mathf.Max(1f, _baseMaxHealth * (1f + SumEffect(CollectibleEffectType.MaxHealthPercent)));
            if (Mathf.Approximately(oldMax, newMax))
                return;

            _entity.Health.SetMaxHealth(newMax, refill: false);
            if (newMax > oldMax)
                _entity.Health.Heal(newMax - oldMax);
        }

        private float SumEffect(CollectibleEffectType type)
        {
            var total = 0f;
            foreach (var pair in _definitions)
            {
                var definition = pair.Value;
                if (definition == null || definition.EffectType != type)
                    continue;
                if (!_stacks.TryGetValue(pair.Key, out var stackCount) || stackCount <= 0)
                    continue;
                total += definition.Value * stackCount;
            }
            return total;
        }
    }
}
