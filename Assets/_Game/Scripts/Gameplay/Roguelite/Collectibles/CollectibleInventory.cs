using System;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Collectibles
{
    /// <summary>Future character systems consume GetEffectTotal and react to Collected.</summary>
    public interface ICollectibleEffectConsumer
    {
        bool SupportsCollectibleEffect(CollectibleEffectType effect);
    }
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class CollectibleInventory : MonoBehaviour, IDamageModifier, IPlayerSwitchStateTransfer, ICombatStatModifier, ICombatTargetStatModifier
    {
        private readonly Dictionary<string, int> _stacks = new();
        private readonly Dictionary<string, CollectibleDefinition> _definitions = new();

        private readonly Dictionary<Health, float> _enemyBaseMaxHealth = new();
        private CombatEntity _entity;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private PlayerCombatProfile _profile;
        private float _baseMaxHealth;
        private float _runStartedAt;
        private float _nextEnemyHealthSyncAt;

        public event Action<CollectibleDefinition, int> Collected;
        public event Action<CollectibleDefinition, int> Removed;
        public event Action<int, string> SkillPointFeedback;
        public IReadOnlyDictionary<string, CollectibleDefinition> Definitions => _definitions;

        public bool IsApplicable(CollectibleDefinition definition)
        {
            if (definition == null) return false;
            if (definition.RequiredFeatures != CombatFeature.None &&
                (_profile == null || !_profile.Supports(definition.RequiredFeatures)))
                return false;
            if (definition.IsSalvageCommodity) return true;

            var effects = definition.Effects;
            if (effects.Count == 0)
                return definition.Profession == OperatorProfession.Unspecified ||
                       (_profile != null && _profile.Profession == definition.Profession);

            for (var i = 0; i < effects.Count; i++)
                if (EffectAppliesToCurrentOperator(effects[i])) return true;
            return false;
        }

        public bool IsEffectActive(CollectibleDefinition definition)
        {
            if (!IsApplicable(definition)) return false;
            var effects = definition.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!EffectAppliesToCurrentOperator(effect)) continue;
                if (CollectibleDefinition.IsBuiltInEffect(effect.Type)) return true;
                foreach (var component in GetComponents<MonoBehaviour>())
                    if (component is ICollectibleEffectConsumer consumer && consumer.SupportsCollectibleEffect(effect.Type))
                        return true;
            }
            return effects.Count == 0 && definition.HasBuiltInEffect;
        }

        // Stable extension point: future attack-speed, shield, summon and healing systems query here.
        public float GetEffectTotal(CollectibleEffectType type) => SumEffect(type);

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _profile = GetComponent<PlayerCombatProfile>();
            _baseMaxHealth = _entity != null && _entity.Health != null ? _entity.Health.MaxHealth : 100f;
            _runStartedAt = Time.time;
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

        private void Update()
        {
            if (Time.time < _nextEnemyHealthSyncAt) return;
            _nextEnemyHealthSyncAt = Time.time + 0.5f;
            if (Mathf.Abs(SumEffect(CollectibleEffectType.EnemyMaxHealthPercent)) > 0.0001f)
                SyncEnemyMaxHealth();
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

        public bool Acquire(CollectibleDefinition definition) =>
            AcquireInternal(definition, applyOneShotEffects: true);

        /// <summary>
        /// Re-activates a relic dropped earlier in the same run without granting one-shot pickup rewards twice.
        /// </summary>
        public bool ReacquireDropped(CollectibleDefinition definition) =>
            AcquireInternal(definition, applyOneShotEffects: false);

        private bool AcquireInternal(CollectibleDefinition definition, bool applyOneShotEffects)
        {
            if (!CanAcquire(definition))
                return false;

            var nextStack = GetStackCount(definition) + 1;
            _stacks[definition.Id] = nextStack;
            _definitions[definition.Id] = definition;

            if (DefinitionHasEffect(definition, CollectibleEffectType.MaxHealthPercent))
                RecalculateMaxHealth();
            if (applyOneShotEffects)
                ApplyOneShotEffects(definition);
            if (DefinitionHasEffect(definition, CollectibleEffectType.EnemyMaxHealthPercent))
                SyncEnemyMaxHealth();

            Collected?.Invoke(definition, nextStack);
            Debug.Log(
                $"[ArknightsACT/Roguelite] Collected '{definition.DisplayName}' stack {nextStack}/{definition.MaxStacks}.",
                this);
            return true;
        }

        /// <summary>
        /// Removes one active stack when an unsecured relic is discarded or lost.
        /// Ongoing/stat effects are recalculated immediately. One-shot pickup effects already consumed
        /// (for example initial skill points or ingots-on-acquire) are intentionally not rewound.
        /// </summary>
        public bool Release(CollectibleDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                !_stacks.TryGetValue(definition.Id, out var currentStack) || currentStack <= 0)
                return false;

            var nextStack = currentStack - 1;
            if (nextStack <= 0)
            {
                _stacks.Remove(definition.Id);
                _definitions.Remove(definition.Id);
                nextStack = 0;
            }
            else
            {
                _stacks[definition.Id] = nextStack;
            }

            if (DefinitionHasEffect(definition, CollectibleEffectType.MaxHealthPercent))
                RecalculateMaxHealth();
            if (DefinitionHasEffect(definition, CollectibleEffectType.EnemyMaxHealthPercent))
                SyncEnemyMaxHealth();

            Removed?.Invoke(definition, nextStack);
            Debug.Log(
                $"[ArknightsACT/Roguelite] Released '{definition.DisplayName}', remaining stack {nextStack}/{definition.MaxStacks}.",
                this);
            return true;
        }

        public void ClearRunCollectibles()
        {
            if (_definitions.Count == 0)
            {
                _runStartedAt = Time.time;
                return;
            }

            var snapshot = new List<CollectibleDefinition>(_definitions.Values);
            for (var i = 0; i < snapshot.Count; i++)
            {
                var definition = snapshot[i];
                while (definition != null && GetStackCount(definition) > 0)
                    Release(definition);
            }

            _enemyBaseMaxHealth.Clear();
            _runStartedAt = Time.time;
        }

        public void CopySwitchStateTo(Transform destination)
        {
            if (destination == null)
                return;
            var target = destination.GetComponent<CollectibleInventory>();
            if (target == null || target == this)
                return;

            target._stacks.Clear();
            target._definitions.Clear();
            target._enemyBaseMaxHealth.Clear();

            foreach (var pair in _stacks)
                target._stacks[pair.Key] = pair.Value;
            foreach (var pair in _definitions)
                target._definitions[pair.Key] = pair.Value;
            foreach (var pair in _enemyBaseMaxHealth)
                if (pair.Key != null)
                    target._enemyBaseMaxHealth[pair.Key] = pair.Value;

            target._runStartedAt = _runStartedAt;
            target.RecalculateMaxHealth();
            // Always resync: switching from a profession-specific enemy-health relic to an
            // incompatible operator must also restore enemies to their recorded base max health.
            target.SyncEnemyMaxHealth();
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

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) =>
            currentDamage * Mathf.Max(0f, 1f + SumEffect(CollectibleEffectType.IncomingDamagePercent));

        public void AccumulateStatModifiers(CombatStatType stat, ref float flat, ref float additivePercent)
        {
            switch (stat)
            {
                case CombatStatType.PhysicalDefense:
                    flat += SumEffect(CollectibleEffectType.PhysicalDefenseFlat);
                    additivePercent += SumEffect(CollectibleEffectType.PhysicalDefensePercent);
                    break;
                case CombatStatType.ArtsResistance:
                    flat += SumEffect(CollectibleEffectType.ArtsResistanceFlat);
                    additivePercent += SumEffect(CollectibleEffectType.ArtsResistancePercent);
                    break;
            }
        }

        public float ModifyTargetStat(in DamageContext context, CombatStatType stat, float currentValue)
        {
            if (_entity == null ||
                context.Target == null ||
                context.Target.Team == _entity.Team)
                return currentValue;

            if (stat == CombatStatType.PhysicalDefense)
            {
                var percent = SumEffect(CollectibleEffectType.EnemyPhysicalDefensePercent);
                return Mathf.Max(0f, currentValue * Mathf.Max(0f, 1f + percent));
            }

            return currentValue;
        }

        private void OnBasicAttackHit(CombatEntity target)
        {
            var seconds = SumEffect(CollectibleEffectType.CooldownOnBasicHitSeconds);
            if (seconds > 0f)
                ReduceAllSkillCooldowns(seconds);
            var skillPoints = SumEffect(CollectibleEffectType.SkillPointOnBasicHit);
            if (skillPoints > 0f)
                EmitSkillPointFeedback(skillPoints, "普攻触发");
            if (skillPoints > 0f)
                _skills?.GainAllSkillPoints(skillPoints);
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
            var skillPoints = SumEffect(CollectibleEffectType.SkillPointOnSkillCast);
            if (skillPoints > 0f)
                EmitSkillPointFeedback(skillPoints, "技能触发");
            if (skillPoints > 0f)
                _skills?.GainAllSkillPoints(skillPoints);
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
            var elapsed = Mathf.Max(0f, Time.time - _runStartedAt);
            foreach (var pair in _definitions)
            {
                var definition = pair.Value;
                if (definition == null || !_stacks.TryGetValue(pair.Key, out var stackCount) || stackCount <= 0)
                    continue;

                var effects = definition.Effects;
                if (effects.Count == 0)
                {
                    if (definition.EffectType == type && IsApplicable(definition))
                        total += definition.Value * stackCount;
                    continue;
                }

                for (var i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];
                    if (effect.Type != type || !EffectAppliesToCurrentOperator(effect)) continue;
                    if (effect.DurationSeconds > 0f && elapsed > effect.DurationSeconds) continue;
                    total += effect.Value * stackCount;
                }
            }
            return total;
        }

        private bool EffectAppliesToCurrentOperator(CollectibleEffectModifier effect) =>
            effect != null && (effect.Profession == OperatorProfession.Unspecified ||
                               (_profile != null && _profile.Profession == effect.Profession));

        private bool DefinitionHasEffect(CollectibleDefinition definition, CollectibleEffectType type)
        {
            if (definition == null) return false;
            var effects = definition.Effects;
            if (effects.Count == 0) return definition.EffectType == type;
            for (var i = 0; i < effects.Count; i++)
                if (effects[i].Type == type && EffectAppliesToCurrentOperator(effects[i])) return true;
            return false;
        }

        private void ApplyOneShotEffects(CollectibleDefinition definition)
        {
            if (definition == null || _skills == null) return;
            var effects = definition.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!EffectAppliesToCurrentOperator(effect)) continue;
                if (effect.Type == CollectibleEffectType.InitialSkillPoints && effect.Value > 0f)
                    _skills.GainAllSkillPoints(effect.Value);
                else if (effect.Type == CollectibleEffectType.IngotOnAcquire && effect.Value > 0f)
                    RogueliteRunState.Instance?.AddIngots(Mathf.RoundToInt(effect.Value));
            }
        }

        private void EmitSkillPointFeedback(float amount, string source)
        {
            if (amount <= 0f)
                return;
            SkillPointFeedback?.Invoke(Mathf.Max(1, Mathf.RoundToInt(amount)), source);
        }

        private void SyncEnemyMaxHealth()
        {
            if (_entity == null) return;
            var modifier = Mathf.Clamp(SumEffect(CollectibleEffectType.EnemyMaxHealthPercent), -0.9f, 5f);
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            foreach (var candidate in entities)
            {
                if (candidate == null || candidate == _entity || candidate.Health == null || candidate.Health.IsDead) continue;
                if (candidate.Team == Team.Neutral || candidate.Team == _entity.Team) continue;
                var health = candidate.Health;
                if (!_enemyBaseMaxHealth.TryGetValue(health, out var baseMax))
                {
                    baseMax = health.MaxHealth;
                    _enemyBaseMaxHealth[health] = baseMax;
                }
                var desired = Mathf.Max(1f, baseMax * (1f + modifier));
                if (!Mathf.Approximately(health.MaxHealth, desired))
                    health.SetMaxHealth(desired, refill: false);
            }
        }
    }
}
