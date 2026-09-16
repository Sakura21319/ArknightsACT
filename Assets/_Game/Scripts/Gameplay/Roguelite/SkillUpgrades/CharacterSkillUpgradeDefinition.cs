using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.SkillUpgrades
{
    [CreateAssetMenu(menuName = "ArknightsACT/Roguelite/Character Skill Upgrade", fileName = "SkillUpgrade_")]
    public sealed class CharacterSkillUpgradeDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string characterId;
        [SerializeField] private string effectId;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private float value;
        [SerializeField, Min(1)] private int maxStacks = 3;
        [SerializeField, Min(0.01f)] private float rewardWeight = 1f;

        public string Id => id;
        public string CharacterId => characterId;
        public string EffectId => effectId;
        public string DisplayName => displayName;
        public string Description => description;
        public float Value => value;
        public int MaxStacks => Mathf.Max(1, maxStacks);
        public float RewardWeight => Mathf.Max(0.01f, rewardWeight);

        public void Configure(
            string upgradeId,
            string targetCharacterId,
            string targetEffectId,
            string name,
            string text,
            float effectValue,
            int stackLimit = 3,
            float weight = 1f)
        {
            id = upgradeId ?? string.Empty;
            characterId = targetCharacterId ?? string.Empty;
            effectId = targetEffectId ?? string.Empty;
            displayName = name ?? string.Empty;
            description = text ?? string.Empty;
            value = effectValue;
            maxStacks = Mathf.Max(1, stackLimit);
            rewardWeight = Mathf.Max(0.01f, weight);
        }
    }
}
