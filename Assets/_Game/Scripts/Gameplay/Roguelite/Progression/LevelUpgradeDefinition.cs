using System;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Progression
{
    public enum LevelUpgradeEffectType
    {
        // Keep existing numeric values stable for already-authored assets.
        AllDamagePercent = 0,
        PhysicalDamagePercent = 1,
        ArtsDamagePercent = 2,
        BurnOnHit = 3,
        ChainLightning = 4,

        PhysicalDefensePercent = 5,
        ArtsResistanceFlat = 6,
        MoveSpeedPercent = 7,
        AttackSpeedPercent = 8,

        // B: build-synergy effects.
        OverloadExplosion = 9,

        // C: high-upside upgrades with explicit costs.
        OriginiumOverclock = 10,
        BloodDebt = 11
    }

    [Flags]
    public enum RunBuildTag
    {
        None = 0,
        Burn = 1 << 0,
        Chain = 1 << 1,
        Overload = 1 << 2,
        Skill = 1 << 3,
        Hunt = 1 << 4,
        Risk = 1 << 5
    }

    public enum LevelUpgradeArchetype
    {
        Foundation = 0,
        Synergy = 1,
        DangerousProtocol = 2
    }

    public enum LevelUpgradeCategory
    {
        Offense = 0,
        Defense = 1,
        Mobility = 2,
        Proc = 3
    }

    [CreateAssetMenu(menuName = "ArknightsACT/Roguelite/Level Upgrade", fileName = "Upgrade_")]
    public sealed class LevelUpgradeDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private CombatFeature requiredFeatures = CombatFeature.None;
        [SerializeField] private LevelUpgradeEffectType effectType;
        [SerializeField] private float value;
        [SerializeField] private LevelUpgradeArchetype archetype = LevelUpgradeArchetype.Foundation;
        [SerializeField] private RunBuildTag requiredTags = RunBuildTag.None;
        [SerializeField] private RunBuildTag grantedTags = RunBuildTag.None;
        [SerializeField, Min(1)] private int maxStacks = 3;
        [SerializeField, Min(0.01f)] private float rewardWeight = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public CombatFeature RequiredFeatures => requiredFeatures;
        public LevelUpgradeEffectType EffectType => effectType;
        public LevelUpgradeArchetype Archetype => archetype;
        public RunBuildTag RequiredTags => requiredTags;
        public RunBuildTag GrantedTags
        {
            get
            {
                if (grantedTags != RunBuildTag.None)
                    return grantedTags;
                return effectType switch
                {
                    LevelUpgradeEffectType.BurnOnHit => RunBuildTag.Burn,
                    LevelUpgradeEffectType.ChainLightning => RunBuildTag.Chain,
                    LevelUpgradeEffectType.OverloadExplosion => RunBuildTag.Overload,
                    LevelUpgradeEffectType.OriginiumOverclock or
                    LevelUpgradeEffectType.BloodDebt => RunBuildTag.Risk,
                    _ => RunBuildTag.None
                };
            }
        }
        public LevelUpgradeCategory Category => archetype == LevelUpgradeArchetype.DangerousProtocol
            ? LevelUpgradeCategory.Proc
            : effectType switch
            {
                LevelUpgradeEffectType.PhysicalDefensePercent or
                LevelUpgradeEffectType.ArtsResistanceFlat => LevelUpgradeCategory.Defense,

                LevelUpgradeEffectType.MoveSpeedPercent or
                LevelUpgradeEffectType.AttackSpeedPercent => LevelUpgradeCategory.Mobility,

                LevelUpgradeEffectType.BurnOnHit or
                LevelUpgradeEffectType.ChainLightning or
                LevelUpgradeEffectType.OverloadExplosion => LevelUpgradeCategory.Proc,

                _ => LevelUpgradeCategory.Offense
            };
        public float Value => value;
        public int MaxStacks => Mathf.Max(1, maxStacks);
        public float RewardWeight => Mathf.Max(0.01f, rewardWeight);

        public void Configure(
            string upgradeId,
            string name,
            string text,
            CombatFeature requirements,
            LevelUpgradeEffectType type,
            float effectValue,
            int stackLimit = 3,
            float weight = 1f,
            LevelUpgradeArchetype upgradeArchetype = LevelUpgradeArchetype.Foundation,
            RunBuildTag requires = RunBuildTag.None,
            RunBuildTag grants = RunBuildTag.None)
        {
            id = upgradeId ?? string.Empty;
            displayName = name ?? string.Empty;
            description = text ?? string.Empty;
            requiredFeatures = requirements;
            effectType = type;
            value = effectValue;
            archetype = upgradeArchetype;
            requiredTags = requires;
            grantedTags = grants;
            maxStacks = Mathf.Max(1, stackLimit);
            rewardWeight = Mathf.Max(0.01f, weight);
        }
    }
}
