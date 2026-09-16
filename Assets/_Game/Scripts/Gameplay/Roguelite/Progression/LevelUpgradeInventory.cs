using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class LevelUpgradeInventory : MonoBehaviour, IDamageModifier
    {
        private readonly Dictionary<string, int> _stacks = new();
        private readonly Dictionary<string, LevelUpgradeDefinition> _definitions = new();
        private readonly Dictionary<string, Coroutine> _burnRoutines = new();

        private CombatEntity _entity;
        private float _baseMaxHealth;
        private int _chainHitCounter;

        public event Action<LevelUpgradeDefinition, int> Upgraded;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _baseMaxHealth = _entity != null && _entity.Health != null ? _entity.Health.MaxHealth : 100f;
        }

        private void OnEnable()
        {
            DamageSystem.DamageApplied += OnDamageApplied;
        }

        private void OnDisable()
        {
            DamageSystem.DamageApplied -= OnDamageApplied;
            foreach (var pair in _burnRoutines)
            {
                if (pair.Value != null)
                    StopCoroutine(pair.Value);
            }
            _burnRoutines.Clear();
        }

        public int GetStackCount(LevelUpgradeDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                return 0;
            return _stacks.TryGetValue(definition.Id, out var count) ? count : 0;
        }

        public bool CanAcquire(LevelUpgradeDefinition definition) =>
            definition != null &&
            !string.IsNullOrWhiteSpace(definition.Id) &&
            GetStackCount(definition) < definition.MaxStacks;

        public bool Acquire(LevelUpgradeDefinition definition)
        {
            if (!CanAcquire(definition))
                return false;

            var nextStack = GetStackCount(definition) + 1;
            _stacks[definition.Id] = nextStack;
            _definitions[definition.Id] = definition;

            if (definition.EffectType == LevelUpgradeEffectType.MaxHealthPercent)
                RecalculateMaxHealth();

            Upgraded?.Invoke(definition, nextStack);
            Debug.Log(
                $"[ArknightsACT/Progression] Upgrade '{definition.DisplayName}' -> {nextStack}/{definition.MaxStacks}.",
                this);
            return true;
        }

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage)
        {
            var percent = SumEffect(LevelUpgradeEffectType.AllDamagePercent);
            percent += context.DamageType switch
            {
                DamageType.Physical => SumEffect(LevelUpgradeEffectType.PhysicalDamagePercent),
                DamageType.Arts => SumEffect(LevelUpgradeEffectType.ArtsDamagePercent),
                _ => 0f
            };
            return currentDamage * Mathf.Max(0f, 1f + percent);
        }

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) => currentDamage;

        private void OnDamageApplied(DamageContext context, DamageResult result)
        {
            if (_entity == null || !result.Applied || context.ProcGeneration > 0)
                return;
            if (context.Source != _entity && context.Owner != _entity)
                return;
            if (context.Target == null || context.Target.Team == _entity.Team)
                return;

            TryApplyBurn(context, result);
            TryApplyChain(context, result);
        }

        private void TryApplyBurn(in DamageContext context, in DamageResult result)
        {
            var stacks = SumStacks(LevelUpgradeEffectType.BurnOnHit);
            if (stacks <= 0 || result.Killed || context.Target.Health == null || context.Target.Health.IsDead)
                return;

            var chance = Mathf.Min(0.60f, 0.20f + (stacks - 1) * 0.12f);
            if (UnityEngine.Random.value > chance)
                return;

            var perTickFraction = Mathf.Max(0f, FirstValue(LevelUpgradeEffectType.BurnOnHit)) + (stacks - 1) * 0.035f;
            var perTickDamage = Mathf.Max(0.5f, result.Damage * perTickFraction);
            var id = context.Target.EntityId;
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (_burnRoutines.TryGetValue(id, out var active) && active != null)
                StopCoroutine(active);
            _burnRoutines[id] = StartCoroutine(BurnRoutine(id, context.Target, perTickDamage, context.ProcGeneration + 1));
        }

        private IEnumerator BurnRoutine(string id, CombatEntity target, float perTickDamage, int procGeneration)
        {
            const int tickCount = 3;
            const float interval = 0.65f;
            for (var i = 0; i < tickCount; i++)
            {
                yield return new WaitForSeconds(interval);
                if (target == null || target.Health == null || target.Health.IsDead || _entity == null || _entity.Health == null || _entity.Health.IsDead)
                    break;

                DamageSystem.Apply(new DamageContext(
                    _entity,
                    _entity,
                    target,
                    perTickDamage,
                    DamageType.Arts,
                    Vector2.zero,
                    procGeneration,
                    "LevelUpgrade_Burn"));
            }
            _burnRoutines.Remove(id);
        }

        private void TryApplyChain(in DamageContext context, in DamageResult result)
        {
            var stacks = SumStacks(LevelUpgradeEffectType.ChainLightning);
            if (stacks <= 0)
                return;

            _chainHitCounter++;
            var triggerEvery = Mathf.Max(3, 6 - stacks);
            if (_chainHitCounter < triggerEvery)
                return;
            _chainHitCounter = 0;

            var secondary = FindNearestEnemy(context.Target, 3.25f);
            if (secondary == null)
                return;

            var baseFraction = Mathf.Max(0f, FirstValue(LevelUpgradeEffectType.ChainLightning));
            var fraction = baseFraction + (stacks - 1) * 0.12f;
            var damage = Mathf.Max(1f, result.Damage * fraction);
            DamageSystem.Apply(new DamageContext(
                _entity,
                _entity,
                secondary,
                damage,
                DamageType.Arts,
                Vector2.zero,
                context.ProcGeneration + 1,
                "LevelUpgrade_Chain"));
        }

        private CombatEntity FindNearestEnemy(CombatEntity primary, float radius)
        {
            if (primary == null)
                return null;

            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            CombatEntity best = null;
            var bestSqr = radius * radius;
            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (candidate == null || candidate == primary || candidate == _entity)
                    continue;
                if (candidate.Team == _entity.Team || candidate.Health == null || candidate.Health.IsDead)
                    continue;

                var delta = candidate.transform.position - primary.transform.position;
                if (Mathf.Abs(delta.y) > 1.15f)
                    continue;
                delta.y = 0f;
                var sqr = delta.sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = candidate;
            }
            return best;
        }

        private void RecalculateMaxHealth()
        {
            if (_entity?.Health == null)
                return;

            var oldMax = _entity.Health.MaxHealth;
            var newMax = Mathf.Max(1f, _baseMaxHealth * (1f + SumEffect(LevelUpgradeEffectType.MaxHealthPercent)));
            if (Mathf.Approximately(oldMax, newMax))
                return;

            _entity.Health.SetMaxHealth(newMax, refill: false);
            if (newMax > oldMax)
                _entity.Health.Heal(newMax - oldMax);
        }

        private float SumEffect(LevelUpgradeEffectType type)
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

        private int SumStacks(LevelUpgradeEffectType type)
        {
            var total = 0;
            foreach (var pair in _definitions)
            {
                if (pair.Value == null || pair.Value.EffectType != type)
                    continue;
                if (_stacks.TryGetValue(pair.Key, out var count))
                    total += count;
            }
            return total;
        }

        private float FirstValue(LevelUpgradeEffectType type)
        {
            foreach (var pair in _definitions)
            {
                if (pair.Value != null && pair.Value.EffectType == type)
                    return pair.Value.Value;
            }
            return 0f;
        }
    }
}
