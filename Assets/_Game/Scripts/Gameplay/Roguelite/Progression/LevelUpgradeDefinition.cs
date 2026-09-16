using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Progression
{
    public enum LevelUpgradeEffectType
    {
        AllDamagePercent,
        PhysicalDamagePercent,
        ArtsDamagePercent,
        BurnOnHit,
        ChainLightning
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
        [SerializeField, Min(1)] private int maxStacks = 3;
        [SerializeField, Min(0.01f)] private float rewardWeight = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public CombatFeature RequiredFeatures => requiredFeatures;
        public LevelUpgradeEffectType EffectType => effectType;
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
            float weight = 1f)
        {
            id = upgradeId ?? string.Empty;
            displayName = name ?? string.Empty;
            description = text ?? string.Empty;
            requiredFeatures = requirements;
            effectType = type;
            value = effectValue;
            maxStacks = Mathf.Max(1, stackLimit);
            rewardWeight = Mathf.Max(0.01f, weight);
        }
    }
}
