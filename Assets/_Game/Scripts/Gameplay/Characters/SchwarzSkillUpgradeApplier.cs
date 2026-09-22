using ArknightsACT.Gameplay.Roguelite.SkillUpgrades;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    [DisallowMultipleComponent]
    public sealed class SchwarzSkillUpgradeApplier : MonoBehaviour, ICharacterSkillUpgradeApplier
    {
        public const string CharacterKey = "Schwarz";

        public const string OriginalSkill2Damage = "schwarz_s2_damage";
        public const string OriginalSkill2Duration = "schwarz_s2_duration";
        public const string OriginalSkill2Cycle = "schwarz_s2_cycle";

        public const string OriginalSkill3Damage = "schwarz_s3_damage";
        public const string OriginalSkill3Range = "schwarz_s3_range";
        public const string OriginalSkill3Duration = "schwarz_s3_duration";

        private SchwarzSkill1 _skill2;
        private SchwarzSkill2 _skill3;

        public string CharacterId => CharacterKey;

        private void Awake()
        {
            // Class names follow gameplay slots for compatibility:
            // SchwarzSkill1 = gameplay slot 1 = original S2
            // SchwarzSkill2 = gameplay slot 2 = original S3
            _skill2 = GetComponent<SchwarzSkill1>();
            _skill3 = GetComponent<SchwarzSkill2>();
        }

        public bool Supports(string effectId) =>
            effectId == OriginalSkill2Damage ||
            effectId == OriginalSkill2Duration ||
            effectId == OriginalSkill2Cycle ||
            effectId == OriginalSkill3Damage ||
            effectId == OriginalSkill3Range ||
            effectId == OriginalSkill3Duration;

        public void Apply(string effectId, float value, int newStack)
        {
            switch (effectId)
            {
                case OriginalSkill2Damage:
                    _skill2?.AddDamagePercent(value);
                    break;
                case OriginalSkill2Duration:
                    _skill2?.AddDurationPercent(value);
                    break;
                case OriginalSkill2Cycle:
                    _skill2?.AddCostReductionPercent(value);
                    break;
                case OriginalSkill3Damage:
                    _skill3?.AddDamagePercent(value);
                    break;
                case OriginalSkill3Range:
                    _skill3?.AddRangePercent(value);
                    break;
                case OriginalSkill3Duration:
                    _skill3?.AddDurationPercent(value);
                    break;
            }
        }

        public void ResetRun()
        {
            _skill2?.ResetRunModifiers();
            _skill3?.ResetRunModifiers();
        }
    }
}
