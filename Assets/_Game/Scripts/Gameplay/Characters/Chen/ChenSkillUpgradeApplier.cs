using ArknightsACT.Gameplay.Roguelite.SkillUpgrades;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    [DisallowMultipleComponent]
    public sealed class ChenSkillUpgradeApplier : MonoBehaviour, ICharacterSkillUpgradeApplier
    {
        public const string CharacterKey = "Chen";
        public const string Skill1Range = "chen_skill1_range";
        public const string Skill1Damage = "chen_skill1_damage";
        public const string Skill1Cooldown = "chen_skill1_cooldown";
        public const string Skill2ExtraStrikes = "chen_skill2_extra_strikes";
        public const string Skill2FinalDamage = "chen_skill2_final_damage";
        public const string Skill2Radius = "chen_skill2_radius";

        private ChenSkill1 _skill1;
        private ChenSkill2 _skill2;

        public string CharacterId => CharacterKey;

        private void Awake()
        {
            _skill1 = GetComponent<ChenSkill1>();
            _skill2 = GetComponent<ChenSkill2>();
        }

        public bool Supports(string effectId) => effectId == Skill1Range ||
                                                 effectId == Skill1Damage ||
                                                 effectId == Skill1Cooldown ||
                                                 effectId == Skill2ExtraStrikes ||
                                                 effectId == Skill2FinalDamage ||
                                                 effectId == Skill2Radius;

        public void Apply(string effectId, float value, int newStack)
        {
            switch (effectId)
            {
                case Skill1Range:
                    _skill1?.AddRangePercent(value);
                    break;
                case Skill1Damage:
                    _skill1?.AddDamagePercent(value);
                    break;
                case Skill1Cooldown:
                    _skill1?.AddCooldownReductionPercent(value);
                    break;
                case Skill2ExtraStrikes:
                    _skill2?.AddStrikeCount(Mathf.Max(1, Mathf.RoundToInt(value)));
                    break;
                case Skill2FinalDamage:
                    _skill2?.AddFinalDamagePercent(value);
                    break;
                case Skill2Radius:
                    _skill2?.AddTargetingRadiusPercent(value);
                    break;
            }
        }
    }
}
