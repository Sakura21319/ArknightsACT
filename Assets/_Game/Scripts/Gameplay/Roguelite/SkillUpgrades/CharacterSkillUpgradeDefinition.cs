using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.SkillUpgrades
{
    public enum CharacterSkillUpgradeKind
    {
        Tuning = 0,
        Mutation = 1
    }

    [CreateAssetMenu(menuName = "ArknightsACT/Roguelite/Character Skill Upgrade", fileName = "SkillUpgrade_")]
    public sealed class CharacterSkillUpgradeDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string characterId;
        [SerializeField] private string effectId;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private float value;
        [SerializeField] private CharacterSkillUpgradeKind kind = CharacterSkillUpgradeKind.Tuning;
        [SerializeField, Min(1)] private int maxStacks = 3;
        [SerializeField, Min(0.01f)] private float rewardWeight = 1f;

        public string Id => id;
        public string CharacterId => characterId;
        public string EffectId => effectId;
        public string DisplayName => displayName;
        public string Description => description;
        public float Value => value;
        public CharacterSkillUpgradeKind Kind => kind;
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
            float weight = 1f,
            CharacterSkillUpgradeKind upgradeKind = CharacterSkillUpgradeKind.Tuning)
        {
            id = upgradeId ?? string.Empty;
            characterId = targetCharacterId ?? string.Empty;
            effectId = targetEffectId ?? string.Empty;
            displayName = name ?? string.Empty;
            description = text ?? string.Empty;
            value = effectValue;
            kind = upgradeKind;
            maxStacks = Mathf.Max(1, stackLimit);
            rewardWeight = Mathf.Max(0.01f, weight);
        }
    }
}
