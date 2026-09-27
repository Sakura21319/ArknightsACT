using ArknightsACT.Combat;
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
        public const string Skill1EchoSlash = "chen_skill1_echo_slash";
        public const string Skill2ExtraStrikes = "chen_skill2_extra_strikes";
        public const string Skill2FinalDamage = "chen_skill2_final_damage";
        public const string Skill2Radius = "chen_skill2_radius";
        public const string Skill2KillRefresh = "chen_skill2_kill_refresh";

        private ChenSkill1 _skill1;
        private ChenSkill2 _skill2;
        private CombatStatModifierBucket _runStats;

        public string CharacterId => CharacterKey;

        private void Awake()
        {
            _skill1 = GetComponent<ChenSkill1>();
            _skill2 = GetComponent<ChenSkill2>();
            _runStats = CombatStatModifierBucket.GetOrCreate(gameObject, CombatStatModifierLayer.RunPermanent);
        }

        public bool Supports(string effectId) => effectId == Skill1Range ||
                                                 effectId == Skill1Damage ||
                                                 effectId == Skill1Cooldown ||
                                                 effectId == Skill1EchoSlash ||
                                                 effectId == Skill2ExtraStrikes ||
                                                 effectId == Skill2FinalDamage ||
                                                 effectId == Skill2Radius ||
                                                 effectId == Skill2KillRefresh;

        public void Apply(string effectId, float value, int newStack)
        {
            switch (effectId)
            {
                case Skill1Range:
                    _runStats?.Set(
                        CombatStatType.Skill1RangeMultiplier,
                        Skill1Range,
                        additivePercent: Mathf.Max(0f, value) * Mathf.Max(1, newStack));
                    break;
                case Skill1Damage:
                    _skill1?.AddDamagePercent(value);
                    break;
                case Skill1Cooldown:
                    _skill1?.AddCooldownReductionPercent(value);
                    break;
                case Skill1EchoSlash:
                    _skill1?.EnableEchoSlash(value);
                    break;
                case Skill2ExtraStrikes:
                    _skill2?.AddStrikeCount(Mathf.Max(1, Mathf.RoundToInt(value)));
                    break;
                case Skill2FinalDamage:
                    _skill2?.AddFinalDamagePercent(value);
                    break;
                case Skill2Radius:
                    _runStats?.Set(
                        CombatStatType.Skill2RangeMultiplier,
                        Skill2Radius,
                        additivePercent: Mathf.Max(0f, value) * Mathf.Max(1, newStack));
                    break;
                case Skill2KillRefresh:
                    _skill2?.EnableKillRefresh(Mathf.Max(1, Mathf.RoundToInt(value)));
                    break;
            }
        }

        public void ResetRun()
        {
            _runStats?.ClearSource(Skill1Range);
            _runStats?.ClearSource(Skill2Radius);
            _skill1?.ResetRunModifiers();
            _skill2?.ResetRunModifiers();
        }
    }
}
