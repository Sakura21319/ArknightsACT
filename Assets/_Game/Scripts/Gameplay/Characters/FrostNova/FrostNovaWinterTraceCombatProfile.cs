using System;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    [Serializable]
    public sealed class FrostNovaWinterTraceBaseLevel
    {
        public float maxHealth;
        public float attack;
        public float physicalDefense;
        public float artsResistance;
        public float attackInterval;
        public float attackRadiusTiles;
    }

    [Serializable]
    public sealed class FrostNovaWinterTraceSkillData
    {
        public string sourceSkillId;
        public string displayName;
        public float cooldownSeconds;
        public float damageScale;
        public float radiusTiles;
        public float coldDurationSeconds;
    }

    [Serializable]
    public sealed class FrostNovaWinterTraceCombatData
    {
        public string schema;
        public string sourceId;
        public string sourceLabel;
        public string progressionPolicy;
        public FrostNovaWinterTraceBaseLevel level0;
        public FrostNovaWinterTraceBaseLevel level1;
        public FrostNovaWinterTraceSkillData skill1;
        public FrostNovaWinterTraceSkillData skill2;
        public float basicAttackColdDurationSeconds;
    }

    public static class FrostNovaWinterTraceCombatProfile
    {
        public const string ResourcePath = "Config/FrostNovaWinterTraceCombatProfile";
        public const string ProgressionSourceId = "enemy_1510_frstar2#wintertrace";

        private static FrostNovaWinterTraceCombatData _cached;

        public static bool TryLoad(out FrostNovaWinterTraceCombatData data)
        {
            if (_cached != null)
            {
                data = _cached;
                return true;
            }

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                data = null;
                return false;
            }

            var parsed = JsonUtility.FromJson<FrostNovaWinterTraceCombatData>(asset.text);
            if (!IsValid(parsed))
            {
                data = null;
                return false;
            }

            _cached = parsed;
            data = parsed;
            return true;
        }

        public static OperatorE2Progression BuildProjectProgression(
            FrostNovaWinterTraceCombatData data)
        {
            if (!IsValid(data))
                return new OperatorE2Progression();

            return new OperatorE2Progression(
                90,
                string.Empty,
                new OperatorProgressionSnapshot(1, EvaluateProjectLevel(data, 1)),
                new OperatorProgressionSnapshot(30, EvaluateProjectLevel(data, 30)),
                new OperatorProgressionSnapshot(60, EvaluateProjectLevel(data, 60)),
                new OperatorProgressionSnapshot(90, EvaluateProjectLevel(data, 90)));
        }

        public static OperatorSkillMasterySet BuildSkillMasterySet(
            FrostNovaWinterTraceCombatData data)
        {
            if (!IsValid(data))
                return new OperatorSkillMasterySet();

            return new OperatorSkillMasterySet(
                BuildSkillProfile(1, data.skill1),
                BuildSkillProfile(2, data.skill2));
        }

        public static OperatorBaseStats EvaluateProjectLevel(
            FrostNovaWinterTraceCombatData data,
            int level)
        {
            if (!IsValid(data))
                return new OperatorBaseStats(1f, 0f, 0f, 0f);

            var clamped = Mathf.Clamp(level, 1, 90);
            var t = (clamped - 1f) / 89f;
            return new OperatorBaseStats(
                Mathf.Lerp(data.level0.maxHealth, data.level1.maxHealth, t),
                Mathf.Lerp(data.level0.attack, data.level1.attack, t),
                Mathf.Lerp(data.level0.physicalDefense, data.level1.physicalDefense, t),
                Mathf.Lerp(data.level0.artsResistance, data.level1.artsResistance, t),
                Mathf.Lerp(data.level0.attackInterval, data.level1.attackInterval, t),
                1f,
                1f);
        }

        private static OperatorSkillMasteryProfile BuildSkillProfile(
            int slot,
            FrostNovaWinterTraceSkillData skill)
        {
            var snapshots = new OperatorSkillMasterySnapshot[4];
            for (var mastery = 0; mastery <= 3; mastery++)
            {
                snapshots[mastery] = new OperatorSkillMasterySnapshot(
                    mastery,
                    7 + mastery,
                    skill.sourceSkillId,
                    skill.displayName,
                    string.Empty,
                    "WinterTrace uses the same official enemy value at Skill7/M1/M2/M3 because the source enemy has no mastery tiers.",
                    skill.cooldownSeconds,
                    0f,
                    0f,
                    OperatorSkillPointRecoveryType.Natural,
                    "ENEMY_COOLDOWN_ADAPTED_TO_SP",
                    new OperatorSkillBlackboardValue("damage_scale", skill.damageScale),
                    new OperatorSkillBlackboardValue("radius_tiles", skill.radiusTiles),
                    new OperatorSkillBlackboardValue("cold_duration", skill.coldDurationSeconds));
            }

            return new OperatorSkillMasteryProfile(
                slot,
                slot,
                skill.sourceSkillId,
                snapshots);
        }

        private static bool IsValid(FrostNovaWinterTraceCombatData data)
        {
            return data != null &&
                   data.level0 != null &&
                   data.level1 != null &&
                   data.skill1 != null &&
                   data.skill2 != null &&
                   data.level0.maxHealth > 0f &&
                   data.level1.maxHealth > 0f &&
                   data.level0.attack >= 0f &&
                   data.level1.attack >= 0f &&
                   data.level0.attackInterval > 0f &&
                   data.level1.attackInterval > 0f &&
                   data.skill1.cooldownSeconds > 0f &&
                   data.skill2.cooldownSeconds > 0f &&
                   data.skill1.damageScale > 0f &&
                   data.skill2.damageScale > 0f;
        }
    }
}
