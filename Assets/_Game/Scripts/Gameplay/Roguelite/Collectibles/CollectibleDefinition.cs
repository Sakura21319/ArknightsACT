using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Collectibles
{
    public enum CollectibleRarity
    {
        Common,
        Rare,
        Epic
    }

    public enum SalvageRarity
    {
        Common,
        Rare,
        Precious,
        Mythic
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
        CooldownOnSkillCastSeconds,
        // Appended values preserve serialized IDs. Consumers opt in through ICollectibleEffectConsumer.
        AttackSpeedPercent,
        ShieldCapacity,
        HealingPowerPercent,
        SummonDamagePercent,
        RedeployCooldownPercent,
        PushForce,
        ResourceOnKill,
        AuraDamagePercent,
        IncomingDamagePercent,
        InitialSkillPoints,
        SkillPointRecoveryPerSecond,
        SkillPointRecoveryPercent,
        SkillPointOnBasicHit,
        SkillPointOnSkillCast,
        IngotOnAcquire,
        EnemyMaxHealthPercent,
        PhysicalDefensePercent,
        PhysicalDefenseFlat,
        ArtsResistancePercent,
        ArtsResistanceFlat,
        EnemyPhysicalDefensePercent
    }

    [System.Serializable]
    public sealed class CollectibleEffectModifier
    {
        [SerializeField] private CollectibleEffectType type;
        [SerializeField] private float value;
        [SerializeField] private OperatorProfession profession;
        [SerializeField, Min(0f)] private float durationSeconds;

        public CollectibleEffectType Type => type;
        public float Value => value;
        public OperatorProfession Profession => profession;
        public float DurationSeconds => Mathf.Max(0f, durationSeconds);

        public CollectibleEffectModifier(CollectibleEffectType effectType, float effectValue,
            OperatorProfession targetProfession = OperatorProfession.Unspecified, float duration = 0f)
        {
            type = effectType;
            value = effectValue;
            profession = targetProfession;
            durationSeconds = Mathf.Max(0f, duration);
        }
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
        [SerializeField] private CollectibleEffectModifier[] effects;
        [TextArea(1, 4)] [SerializeField] private string effectDescription;
        [SerializeField, Min(1)] private int maxStacks = 3;
        [SerializeField] private OperatorProfession profession;
        [Header("Scavenging metadata")]
        [SerializeField] private bool salvageCommodity;
        [SerializeField] private SalvageRarity salvageRarity = SalvageRarity.Common;
        [SerializeField] private Vector2Int salvageGridSize = Vector2Int.one;
        [SerializeField] private string salvageIconResource;
        [SerializeField, Min(0)] private int collectionValue;
        private Sprite _salvageIcon;
        private Texture2D _salvageTexture;
        public OperatorProfession Profession => profession;
        public void SetProfession(OperatorProfession target) => profession = target;
        public IReadOnlyList<CollectibleEffectModifier> Effects => effects ?? System.Array.Empty<CollectibleEffectModifier>();
        public string EffectDescription => effectDescription ?? string.Empty;
        public bool HasBuiltInEffect
        {
            get
            {
                var list = Effects;
                if (list.Count == 0)
                    return Mathf.Abs(value) > 0.000001f && IsBuiltInEffect(effectType);
                for (var i = 0; i < list.Count; i++)
                    if (IsBuiltInEffect(list[i].Type)) return true;
                return false;
            }
        }

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public CollectibleRarity Rarity => rarity;
        public CombatFeature RequiredFeatures => requiredFeatures;
        public CollectibleEffectType EffectType => effectType;
        public float Value => value;
        public int MaxStacks => Mathf.Max(1, maxStacks);
        public bool IsSalvageCommodity => salvageCommodity;
        public SalvageRarity SalvageRarity => salvageRarity;
        public Vector2Int SalvageGridSize => new Vector2Int(Mathf.Max(1, salvageGridSize.x), Mathf.Max(1, salvageGridSize.y));
        public string SalvageIconResource => salvageIconResource ?? string.Empty;
        public Sprite SalvageIcon
        {
            get
            {
                if (_salvageIcon == null && !string.IsNullOrWhiteSpace(salvageIconResource))
                    _salvageIcon = Resources.Load<Sprite>(salvageIconResource);
                return _salvageIcon;
            }
        }
        public Texture2D SalvageIconTexture
        {
            get
            {
                var sprite = SalvageIcon;
                if (sprite != null) return sprite.texture;
                if (_salvageTexture == null && !string.IsNullOrWhiteSpace(salvageIconResource))
                    _salvageTexture = Resources.Load<Texture2D>(salvageIconResource);
                return _salvageTexture;
            }
        }
        public int CollectionValue => Mathf.Max(0, collectionValue);

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
            effects = new[] { new CollectibleEffectModifier(type, effectValue, profession) };
            effectDescription = text ?? string.Empty;
            maxStacks = Mathf.Max(1, stackLimit);
        }

        public void ConfigureEffects(CollectibleEffectModifier[] modifiers, string rawEffect)
        {
            effects = modifiers ?? System.Array.Empty<CollectibleEffectModifier>();
            effectDescription = rawEffect ?? string.Empty;
            if (effects.Length > 0)
            {
                effectType = effects[0].Type;
                value = effects[0].Value;
            }
        }

        public static bool IsBuiltInEffect(CollectibleEffectType type) => type switch
        {
            CollectibleEffectType.AllDamagePercent => true,
            CollectibleEffectType.PhysicalDamagePercent => true,
            CollectibleEffectType.ArtsDamagePercent => true,
            CollectibleEffectType.TrueDamagePercent => true,
            CollectibleEffectType.MaxHealthPercent => true,
            CollectibleEffectType.CooldownOnBasicHitSeconds => true,
            CollectibleEffectType.HealOnSkillCastFraction => true,
            CollectibleEffectType.CooldownOnSkillCastSeconds => true,
            CollectibleEffectType.AttackSpeedPercent => true,
            CollectibleEffectType.IncomingDamagePercent => true,
            CollectibleEffectType.InitialSkillPoints => true,
            CollectibleEffectType.SkillPointRecoveryPerSecond => true,
            CollectibleEffectType.SkillPointRecoveryPercent => true,
            CollectibleEffectType.SkillPointOnBasicHit => true,
            CollectibleEffectType.SkillPointOnSkillCast => true,
            CollectibleEffectType.IngotOnAcquire => true,
            CollectibleEffectType.EnemyMaxHealthPercent => true,
            CollectibleEffectType.PhysicalDefensePercent => true,
            CollectibleEffectType.PhysicalDefenseFlat => true,
            CollectibleEffectType.ArtsResistancePercent => true,
            CollectibleEffectType.ArtsResistanceFlat => true,
            CollectibleEffectType.EnemyPhysicalDefensePercent => true,
            _ => false
        };

        public void ConfigureScavengingMetadata(
            bool commodity,
            SalvageRarity lootRarity,
            Vector2Int gridSize,
            string iconResource,
            int collectibleValue)
        {
            salvageCommodity = commodity;
            salvageRarity = lootRarity;
            salvageGridSize = new Vector2Int(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y));
            salvageIconResource = iconResource ?? string.Empty;
            collectionValue = Mathf.Max(0, collectibleValue);
            _salvageIcon = null;
            _salvageTexture = null;
        }
    }
}
