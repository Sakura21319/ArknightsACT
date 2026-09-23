using System;
using System.Collections.Generic;

namespace ArknightsACT.Combat.Status
{
    public static class CombatStatusIds
    {
        public const string Shock = "shock";
        public const string Burn = "burn";
        public const string Ink = "ink";
        public const string Cold = "cold";
        public const string Freeze = "freeze";
        public const string Disarm = "disarm";
        public const string Silence = "silence";
        public const string Stun = "stun";
        public const string Root = "root";
        public const string Slow = "slow";
        public const string AttackSlow = "attack_slow";
        public const string DefenseDown = "defense_down";
        public const string ResistanceDown = "resistance_down";
        public const string Fragile = "fragile";
        public const string Weaken = "weaken";
    }

    public readonly struct StatusReactionRule
    {
        public string ExistingId { get; }
        public string IncomingId { get; }
        public string ResultId { get; }
        public bool ConsumeExisting { get; }

        public StatusReactionRule(string existingId, string incomingId, string resultId, bool consumeExisting = true)
        {
            ExistingId = existingId;
            IncomingId = incomingId;
            ResultId = resultId;
            ConsumeExisting = consumeExisting;
        }
    }

    public static class CombatStatusCatalog
    {
        private static readonly Dictionary<string, CombatStatusDefinition> Definitions =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly List<StatusReactionRule> Reactions = new();

        static CombatStatusCatalog()
        {
            Register(new CombatStatusDefinition(
                CombatStatusIds.Shock, "Shock",
                CombatStatusTags.Debuff | CombatStatusTags.Dispellable,
                2f));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Ink, "Ink",
                CombatStatusTags.Debuff | CombatStatusTags.Dispellable,
                3f));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Cold, "Cold",
                CombatStatusTags.Debuff | CombatStatusTags.Elemental |
                CombatStatusTags.MovementImpair | CombatStatusTags.AttackImpair |
                CombatStatusTags.Dispellable,
                4f,
                statModifiers: new[]
                {
                    new CombatStatModifier(CombatStatType.MoveSpeedMultiplier, additivePercent: -0.20f),
                    new CombatStatModifier(CombatStatType.AttackSpeedMultiplier, additivePercent: -0.30f)
                }));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Freeze, "Freeze",
                CombatStatusTags.Debuff | CombatStatusTags.Elemental |
                CombatStatusTags.CrowdControl | CombatStatusTags.HardCrowdControl |
                CombatStatusTags.MovementImpair | CombatStatusTags.AttackImpair |
                CombatStatusTags.SkillImpair | CombatStatusTags.Dispellable,
                2.5f,
                blockedActions: CombatActionMask.Movement | CombatActionMask.Dash |
                                CombatActionMask.BasicAttack | CombatActionMask.Skill |
                                CombatActionMask.Interaction,
                interruptActions: CombatActionMask.Movement | CombatActionMask.Dash |
                                  CombatActionMask.BasicAttack | CombatActionMask.Skill));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Disarm, "Disarm",
                CombatStatusTags.Debuff | CombatStatusTags.AttackImpair | CombatStatusTags.Dispellable,
                4f,
                blockedActions: CombatActionMask.BasicAttack,
                interruptActions: CombatActionMask.BasicAttack));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Silence, "Silence",
                CombatStatusTags.Debuff | CombatStatusTags.SkillImpair | CombatStatusTags.Dispellable,
                4f,
                blockedActions: CombatActionMask.Skill));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Stun, "Stun",
                CombatStatusTags.Debuff | CombatStatusTags.CrowdControl |
                CombatStatusTags.HardCrowdControl | CombatStatusTags.Dispellable,
                2f,
                blockedActions: CombatActionMask.Movement | CombatActionMask.Dash |
                                CombatActionMask.BasicAttack | CombatActionMask.Skill |
                                CombatActionMask.Interaction | CombatActionMask.Targeting,
                interruptActions: CombatActionMask.Movement | CombatActionMask.Dash |
                                  CombatActionMask.BasicAttack | CombatActionMask.Skill));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Root, "Root",
                CombatStatusTags.Debuff | CombatStatusTags.CrowdControl |
                CombatStatusTags.MovementImpair | CombatStatusTags.Dispellable,
                3f,
                blockedActions: CombatActionMask.Movement | CombatActionMask.Dash,
                interruptActions: CombatActionMask.Dash));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Slow, "Slow",
                CombatStatusTags.Debuff | CombatStatusTags.MovementImpair | CombatStatusTags.Dispellable,
                4f,
                statModifiers: new[]
                {
                    new CombatStatModifier(
                        CombatStatType.MoveSpeedMultiplier,
                        additivePercent: -1f,
                        scaleByApplicationMagnitude: true)
                }));

            Register(new CombatStatusDefinition(
                CombatStatusIds.AttackSlow, "Attack Slow",
                CombatStatusTags.Debuff | CombatStatusTags.AttackImpair | CombatStatusTags.Dispellable,
                4f,
                statModifiers: new[]
                {
                    new CombatStatModifier(
                        CombatStatType.AttackSpeedMultiplier,
                        additivePercent: -1f,
                        scaleByApplicationMagnitude: true)
                }));

            Register(new CombatStatusDefinition(
                CombatStatusIds.DefenseDown, "Defense Down",
                CombatStatusTags.Debuff | CombatStatusTags.Dispellable,
                5f,
                stackPolicy: CombatStatusStackPolicy.ReplaceIfStronger,
                statModifiers: new[]
                {
                    new CombatStatModifier(
                        CombatStatType.PhysicalDefense,
                        additivePercent: -1f,
                        scaleByApplicationMagnitude: true)
                }));

            Register(new CombatStatusDefinition(
                CombatStatusIds.ResistanceDown, "Resistance Down",
                CombatStatusTags.Debuff | CombatStatusTags.Dispellable,
                5f,
                stackPolicy: CombatStatusStackPolicy.ReplaceIfStronger,
                statModifiers: new[]
                {
                    new CombatStatModifier(
                        CombatStatType.ArtsResistance,
                        flat: -1f,
                        scaleByApplicationMagnitude: true)
                }));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Fragile, "Fragile",
                CombatStatusTags.Debuff | CombatStatusTags.Dispellable,
                5f,
                stackPolicy: CombatStatusStackPolicy.ReplaceIfStronger,
                damageModifiers: new[]
                {
                    new StatusDamageModifier(
                        StatusDamageModifierDirection.Incoming,
                        1f,
                        scaleByApplicationMagnitude: true)
                }));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Weaken, "Weaken",
                CombatStatusTags.Debuff | CombatStatusTags.Dispellable,
                5f,
                stackPolicy: CombatStatusStackPolicy.ReplaceIfStronger,
                damageModifiers: new[]
                {
                    new StatusDamageModifier(
                        StatusDamageModifierDirection.Outgoing,
                        -1f,
                        scaleByApplicationMagnitude: true)
                }));

            Register(new CombatStatusDefinition(
                CombatStatusIds.Burn, "Burn",
                CombatStatusTags.Debuff | CombatStatusTags.Elemental |
                CombatStatusTags.DamageOverTime | CombatStatusTags.Dispellable,
                2.05f,
                stackPolicy: CombatStatusStackPolicy.ReplaceIfStronger,
                tickInterval: 0.65f,
                periodicDamageType: DamageType.Arts,
                periodicUsesApplicationMagnitude: true));

            Reactions.Add(new StatusReactionRule(
                CombatStatusIds.Cold,
                CombatStatusIds.Cold,
                CombatStatusIds.Freeze,
                consumeExisting: true));
        }

        public static void Register(CombatStatusDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                return;
            Definitions[definition.Id] = definition;
        }

        public static bool TryGet(string id, out CombatStatusDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(id) &&
                   Definitions.TryGetValue(id, out definition);
        }

        public static bool TryResolveReaction(string existingId, string incomingId, out StatusReactionRule reaction)
        {
            for (var i = 0; i < Reactions.Count; i++)
            {
                var candidate = Reactions[i];
                if (string.Equals(candidate.ExistingId, existingId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate.IncomingId, incomingId, StringComparison.OrdinalIgnoreCase))
                {
                    reaction = candidate;
                    return true;
                }
            }

            reaction = default;
            return false;
        }

        public static string LegacyId(CombatStatusType type) => type switch
        {
            CombatStatusType.Shock => CombatStatusIds.Shock,
            CombatStatusType.Burn => CombatStatusIds.Burn,
            CombatStatusType.Ink => CombatStatusIds.Ink,
            _ => string.Empty
        };
    }
}
