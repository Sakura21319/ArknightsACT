using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.SkillUpgrades
{
    public interface ICharacterSkillUpgradeApplier
    {
        string CharacterId { get; }
        bool Supports(string effectId);
        void Apply(string effectId, float value, int newStack);
        void ResetRun();
    }

    [DisallowMultipleComponent]
    public sealed class CharacterSkillUpgradeInventory : MonoBehaviour, IPlayerSwitchStateTransfer
    {
        private readonly Dictionary<string, int> _stacks = new();
        private readonly Dictionary<string, CharacterSkillUpgradeDefinition> _definitions = new();
        private readonly List<ICharacterSkillUpgradeApplier> _appliers = new();

        public event Action<CharacterSkillUpgradeDefinition, int> Upgraded;

        private void Awake()
        {
            RefreshAppliers();
        }

        public int GetStackCount(CharacterSkillUpgradeDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                return 0;
            return _stacks.TryGetValue(definition.Id, out var count) ? count : 0;
        }

        public bool CanAcquire(CharacterSkillUpgradeDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                return false;
            if (GetStackCount(definition) >= definition.MaxStacks)
                return false;

            for (var i = 0; i < _appliers.Count; i++)
            {
                var applier = _appliers[i];
                if (applier != null &&
                    string.Equals(applier.CharacterId, definition.CharacterId, StringComparison.OrdinalIgnoreCase) &&
                    applier.Supports(definition.EffectId))
                    return true;
            }
            return false;
        }

        public bool Acquire(CharacterSkillUpgradeDefinition definition)
        {
            if (!CanAcquire(definition))
                return false;

            var nextStack = GetStackCount(definition) + 1;
            _stacks[definition.Id] = nextStack;
            _definitions[definition.Id] = definition;

            for (var i = 0; i < _appliers.Count; i++)
            {
                var applier = _appliers[i];
                if (applier == null ||
                    !string.Equals(applier.CharacterId, definition.CharacterId, StringComparison.OrdinalIgnoreCase) ||
                    !applier.Supports(definition.EffectId))
                    continue;
                applier.Apply(definition.EffectId, definition.Value, nextStack);
            }

            Upgraded?.Invoke(definition, nextStack);
            Debug.Log($"[ArknightsACT/SkillUpgrade] {definition.DisplayName} -> {nextStack}/{definition.MaxStacks}", this);
            return true;
        }

        public void ResetRun()
        {
            _stacks.Clear();
            _definitions.Clear();
            for (var i = 0; i < _appliers.Count; i++)
                _appliers[i]?.ResetRun();
        }

        public void CopySwitchStateTo(Transform destination)
        {
            if (destination == null)
                return;
            var target = destination.GetComponent<CharacterSkillUpgradeInventory>();
            if (target == null || target == this)
                return;

            target.RefreshAppliers();
            target._stacks.Clear();
            target._definitions.Clear();
            foreach (var pair in _stacks)
                target._stacks[pair.Key] = pair.Value;
            foreach (var pair in _definitions)
                target._definitions[pair.Key] = pair.Value;
            target.RestoreCurrentCharacterEffects();
        }

        private void RefreshAppliers()
        {
            _appliers.Clear();
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICharacterSkillUpgradeApplier applier)
                    _appliers.Add(applier);
            }
        }

        private void RestoreCurrentCharacterEffects()
        {
            for (var i = 0; i < _appliers.Count; i++)
                _appliers[i]?.ResetRun();

            foreach (var pair in _definitions)
            {
                var definition = pair.Value;
                if (definition == null || !_stacks.TryGetValue(pair.Key, out var stackCount) || stackCount <= 0)
                    continue;

                for (var i = 0; i < _appliers.Count; i++)
                {
                    var applier = _appliers[i];
                    if (applier == null ||
                        !string.Equals(applier.CharacterId, definition.CharacterId, StringComparison.OrdinalIgnoreCase) ||
                        !applier.Supports(definition.EffectId))
                        continue;

                    for (var stack = 1; stack <= stackCount; stack++)
                        applier.Apply(definition.EffectId, definition.Value, stack);
                }
            }
        }
    }
}
