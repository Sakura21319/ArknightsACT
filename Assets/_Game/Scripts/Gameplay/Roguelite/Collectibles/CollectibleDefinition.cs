using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Collectibles
{
    public enum CollectibleRarity
    {
        Common,
        Rare,
        Epic
    }

    public enum CollectibleEffectType
    {
        AllDamagePercent,
        PhysicalDamagePercent,
        ArtsDamagePercent,
        TrueDamagePercent,
        MaxHealthPercent,
        CooldownOnBasicHitSeconds,
        HealOnSkillCastFraction,
        CooldownOnSkillCastSeconds
    }

    [CreateAssetMenu(menuName = "ArknightsACT/Roguelite/Collectible", fileName = "Collectible_")]
    public sealed class CollectibleDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private CollectibleRarity rarity = CollectibleRarity.Common;
        [SerializeField] private CombatFeature requiredFeatures = CombatFeature.None;
        [SerializeField] private CollectibleEffectType effectType;
        [SerializeField] private float value;
        [SerializeField, Min(1)] private int maxStacks = 3;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public CollectibleRarity Rarity => rarity;
        public CombatFeature RequiredFeatures => requiredFeatures;
        public CollectibleEffectType EffectType => effectType;
        public float Value => value;
        public int MaxStacks => Mathf.Max(1, maxStacks);

        public float RewardWeight => rarity switch
        {
            CollectibleRarity.Epic => 0.20f,
            CollectibleRarity.Rare => 0.50f,
            _ => 1.00f
        };

        public void Configure(
            string collectibleId,
            string name,
            string text,
            CollectibleRarity collectibleRarity,
            CombatFeature requirements,
            CollectibleEffectType type,
            float effectValue,
            int stackLimit = 3)
        {
            id = collectibleId ?? string.Empty;
            displayName = name ?? string.Empty;
            description = text ?? string.Empty;
            rarity = collectibleRarity;
            requiredFeatures = requirements;
            effectType = type;
            value = effectValue;
            maxStacks = Mathf.Max(1, stackLimit);
        }
    }
}
