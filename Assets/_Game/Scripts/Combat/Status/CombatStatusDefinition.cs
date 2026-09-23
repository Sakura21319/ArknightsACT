using System;
using System.Collections.Generic;

namespace ArknightsACT.Combat.Status
{
    public sealed class CombatStatusDefinition
    {
        private readonly CombatStatModifier[] _statModifiers;
        private readonly StatusDamageModifier[] _damageModifiers;

        public string Id { get; }
        public string DisplayName { get; }
        public CombatStatusTags Tags { get; }
        public float DefaultDuration { get; }
        public CombatStatusStackPolicy StackPolicy { get; }
        public int MaxStacks { get; }
        public CombatActionMask BlockedActions { get; }
        public CombatActionMask InterruptActions { get; }
        public IReadOnlyList<CombatStatModifier> StatModifiers => _statModifiers;
        public IReadOnlyList<StatusDamageModifier> DamageModifiers => _damageModifiers;
        public float TickInterval { get; }
        public DamageType PeriodicDamageType { get; }
        public float PeriodicFlatDamage { get; }
        public bool PeriodicUsesApplicationMagnitude { get; }

        public CombatStatusDefinition(
            string id,
            string displayName,
            CombatStatusTags tags,
            float defaultDuration,
            CombatStatusStackPolicy stackPolicy = CombatStatusStackPolicy.RefreshDuration,
            int maxStacks = 1,
            CombatActionMask blockedActions = CombatActionMask.None,
            CombatActionMask interruptActions = CombatActionMask.None,
            CombatStatModifier[] statModifiers = null,
            StatusDamageModifier[] damageModifiers = null,
            float tickInterval = 0f,
            DamageType periodicDamageType = DamageType.Arts,
            float periodicFlatDamage = 0f,
            bool periodicUsesApplicationMagnitude = false)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? id ?? string.Empty;
            Tags = tags;
            DefaultDuration = Math.Max(0f, defaultDuration);
            StackPolicy = stackPolicy;
            MaxStacks = Math.Max(1, maxStacks);
            BlockedActions = blockedActions;
            InterruptActions = interruptActions;
            _statModifiers = statModifiers ?? Array.Empty<CombatStatModifier>();
            _damageModifiers = damageModifiers ?? Array.Empty<StatusDamageModifier>();
            TickInterval = Math.Max(0f, tickInterval);
            PeriodicDamageType = periodicDamageType;
            PeriodicFlatDamage = Math.Max(0f, periodicFlatDamage);
            PeriodicUsesApplicationMagnitude = periodicUsesApplicationMagnitude;
        }
    }
}
