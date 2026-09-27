using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    public enum OperatorSkillPointRecoveryType
    {
        Natural = 0,
        Attack = 1,
        Defensive = 2,
        None = 3
    }

    [Serializable]
    public struct OperatorSkillBlackboardValue
    {
        [SerializeField] private string key;
        [SerializeField] private float value;
        [SerializeField] private string valueString;

        public string Key => key ?? string.Empty;
        public float Value => value;
        public string ValueString => valueString ?? string.Empty;

        public OperatorSkillBlackboardValue(string key, float value, string valueString = null)
        {
            this.key = key ?? string.Empty;
            this.value = value;
            this.valueString = valueString ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class OperatorSkillMasterySnapshot
    {
        [SerializeField, Range(0, 3)] private int masteryLevel;
        [SerializeField, Min(1)] private int sourceLevel = 7;
        [SerializeField] private string sourceSkillId;
        [SerializeField] private string displayName;
        [SerializeField] private string rangeId;
        [SerializeField, TextArea] private string description;
        [SerializeField, Min(0f)] private float skillPointCost;
        [SerializeField, Min(0f)] private float initialSkillPoints;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField] private OperatorSkillPointRecoveryType skillPointRecoveryType;
        [SerializeField] private string sourceSkillPointRecoveryType;
        [SerializeField] private OperatorSkillBlackboardValue[] blackboard =
            Array.Empty<OperatorSkillBlackboardValue>();

        public int MasteryLevel => Mathf.Clamp(masteryLevel, 0, 3);
        public int SourceLevel => Mathf.Max(1, sourceLevel);
        public string SourceSkillId => sourceSkillId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string RangeId => rangeId ?? string.Empty;
        public string Description => description ?? string.Empty;
        public float SkillPointCost => Mathf.Max(0f, skillPointCost);
        public float InitialSkillPoints => Mathf.Max(0f, initialSkillPoints);
        public float Duration => Mathf.Max(0f, duration);
        public OperatorSkillPointRecoveryType SkillPointRecoveryType => skillPointRecoveryType;
        public string SourceSkillPointRecoveryType => sourceSkillPointRecoveryType ?? string.Empty;
        public IReadOnlyList<OperatorSkillBlackboardValue> Blackboard =>
            blackboard ?? Array.Empty<OperatorSkillBlackboardValue>();

        public OperatorSkillMasterySnapshot(
            int masteryLevel,
            int sourceLevel,
            string sourceSkillId,
            string displayName,
            string rangeId,
            string description,
            float skillPointCost,
            float initialSkillPoints,
            float duration,
            OperatorSkillPointRecoveryType skillPointRecoveryType,
            string sourceSkillPointRecoveryType,
            params OperatorSkillBlackboardValue[] blackboard)
        {
            this.masteryLevel = Mathf.Clamp(masteryLevel, 0, 3);
            this.sourceLevel = Mathf.Max(1, sourceLevel);
            this.sourceSkillId = sourceSkillId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.rangeId = rangeId ?? string.Empty;
            this.description = description ?? string.Empty;
            this.skillPointCost = Mathf.Max(0f, skillPointCost);
            this.initialSkillPoints = Mathf.Max(0f, initialSkillPoints);
            this.duration = Mathf.Max(0f, duration);
            this.skillPointRecoveryType = skillPointRecoveryType;
            this.sourceSkillPointRecoveryType = sourceSkillPointRecoveryType ?? string.Empty;
            this.blackboard = blackboard ?? Array.Empty<OperatorSkillBlackboardValue>();
        }

        public bool TryGetBlackboard(string key, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(key) || blackboard == null)
                return false;

            for (var i = 0; i < blackboard.Length; i++)
            {
                var entry = blackboard[i];
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }
            return false;
        }

        public float GetFirstBlackboard(float fallback, params string[] keys)
        {
            if (keys == null)
                return fallback;
            for (var i = 0; i < keys.Length; i++)
                if (TryGetBlackboard(keys[i], out var value))
                    return value;
            return fallback;
        }

        public int GetFirstBlackboardInt(int fallback, params string[] keys)
        {
            var value = GetFirstBlackboard(float.NaN, keys);
            return float.IsNaN(value) ? fallback : Mathf.RoundToInt(value);
        }
    }

    [Serializable]
    public sealed class OperatorSkillMasteryProfile
    {
        [SerializeField, Range(1, 2)] private int gameplaySlot = 1;
        [SerializeField, Min(1)] private int sourceSkillIndex = 2;
        [SerializeField] private string sourceSkillId;
        [SerializeField] private OperatorSkillMasterySnapshot[] snapshots =
            Array.Empty<OperatorSkillMasterySnapshot>();

        public int GameplaySlot => Mathf.Clamp(gameplaySlot, 1, 2);
        public int SourceSkillIndex => Mathf.Max(1, sourceSkillIndex);
        public string SourceSkillId => sourceSkillId ?? string.Empty;
        public IReadOnlyList<OperatorSkillMasterySnapshot> Snapshots =>
            snapshots ?? Array.Empty<OperatorSkillMasterySnapshot>();
        public bool HasData => snapshots != null && snapshots.Length > 0;

        public OperatorSkillMasteryProfile(
            int gameplaySlot,
            int sourceSkillIndex,
            string sourceSkillId,
            params OperatorSkillMasterySnapshot[] snapshots)
        {
            this.gameplaySlot = Mathf.Clamp(gameplaySlot, 1, 2);
            this.sourceSkillIndex = Mathf.Max(1, sourceSkillIndex);
            this.sourceSkillId = sourceSkillId ?? string.Empty;
            this.snapshots = snapshots ?? Array.Empty<OperatorSkillMasterySnapshot>();
            Array.Sort(this.snapshots, (a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return a.MasteryLevel.CompareTo(b.MasteryLevel);
            });
        }

        public OperatorSkillMasterySnapshot GetSnapshot(int masteryLevel)
        {
            if (!HasData)
                return null;

            var mastery = Mathf.Clamp(masteryLevel, 0, 3);
            OperatorSkillMasterySnapshot best = null;
            for (var i = 0; i < snapshots.Length; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot == null)
                    continue;
                if (snapshot.MasteryLevel == mastery)
                    return snapshot;
                if (snapshot.MasteryLevel <= mastery &&
                    (best == null || snapshot.MasteryLevel > best.MasteryLevel))
                    best = snapshot;
            }
            return best ?? snapshots[0];
        }
    }

    [Serializable]
    public sealed class OperatorSkillMasterySet
    {
        [SerializeField] private OperatorSkillMasteryProfile[] skills =
            Array.Empty<OperatorSkillMasteryProfile>();

        public IReadOnlyList<OperatorSkillMasteryProfile> Skills =>
            skills ?? Array.Empty<OperatorSkillMasteryProfile>();
        public bool HasData => skills != null && skills.Length > 0;

        public OperatorSkillMasterySet(params OperatorSkillMasteryProfile[] skills)
        {
            this.skills = skills ?? Array.Empty<OperatorSkillMasteryProfile>();
        }

        public OperatorSkillMasteryProfile FindSlot(int gameplaySlot)
        {
            if (skills == null)
                return null;
            var slot = Mathf.Clamp(gameplaySlot, 1, 2);
            for (var i = 0; i < skills.Length; i++)
            {
                var profile = skills[i];
                if (profile != null && profile.GameplaySlot == slot)
                    return profile;
            }
            return null;
        }
    }

    [Serializable]
    public sealed class OperatorSkillMasteryCostStep
    {
        [SerializeField, Range(0, 2)] private int fromMastery;
        [SerializeField, Range(1, 3)] private int targetMastery = 1;
        [SerializeField, Min(0)] private int lmdCost = 30000;
        [SerializeField] private OperatorUpgradeMaterialCost[] materials =
            Array.Empty<OperatorUpgradeMaterialCost>();

        public int FromMastery => Mathf.Clamp(fromMastery, 0, 2);
        public int TargetMastery => Mathf.Clamp(targetMastery, 1, 3);
        public int LmdCost => Mathf.Max(0, lmdCost);
        public IReadOnlyList<OperatorUpgradeMaterialCost> Materials =>
            materials ?? Array.Empty<OperatorUpgradeMaterialCost>();

        public OperatorSkillMasteryCostStep(
            int fromMastery,
            int targetMastery,
            int lmdCost,
            params OperatorUpgradeMaterialCost[] materials)
        {
            this.fromMastery = Mathf.Clamp(fromMastery, 0, 2);
            this.targetMastery = Mathf.Clamp(targetMastery, 1, 3);
            this.lmdCost = Mathf.Max(0, lmdCost);
            this.materials = materials ?? Array.Empty<OperatorUpgradeMaterialCost>();
        }
    }

    [Serializable]
    public sealed class OperatorSkillMasteryCostPlan
    {
        [SerializeField] private OperatorSkillMasteryCostStep[] steps =
            Array.Empty<OperatorSkillMasteryCostStep>();

        private static readonly OperatorSkillMasteryCostPlan Recommended =
            CreateRecommendedDefault();

        public IReadOnlyList<OperatorSkillMasteryCostStep> Steps =>
            steps ?? Array.Empty<OperatorSkillMasteryCostStep>();
        public bool HasData => steps != null && steps.Length > 0;
        public static OperatorSkillMasteryCostPlan RecommendedDefault => Recommended;

        public OperatorSkillMasteryCostPlan() { }

        public OperatorSkillMasteryCostPlan(params OperatorSkillMasteryCostStep[] steps)
        {
            this.steps = steps ?? Array.Empty<OperatorSkillMasteryCostStep>();
        }

        public OperatorSkillMasteryCostStep GetNextStep(int currentMastery)
        {
            var mastery = Mathf.Clamp(currentMastery, 0, 3);
            if (!HasData || mastery >= 3)
                return null;
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step != null && step.FromMastery == mastery)
                    return step;
            }
            return null;
        }

        public static OperatorSkillMasteryCostPlan CreateRecommendedDefault()
        {
            return new OperatorSkillMasteryCostPlan(
                new OperatorSkillMasteryCostStep(
                    0,
                    1,
                    30000,
                    new OperatorUpgradeMaterialCost("rogue_5_relic_legacy_11", 2)),
                new OperatorSkillMasteryCostStep(
                    1,
                    2,
                    60000,
                    new OperatorUpgradeMaterialCost("rogue_3_relic_legacy_57", 2)),
                new OperatorSkillMasteryCostStep(
                    2,
                    3,
                    120000,
                    new OperatorUpgradeMaterialCost("relic_24", 1)));
        }
    }

    public interface IOperatorSkillMasteryTarget
    {
        int MasterySlot { get; }
        void ApplyMasterySnapshot(OperatorSkillMasterySnapshot snapshot);
    }

    [DisallowMultipleComponent]
    public sealed class OperatorSkillMasteryController : MonoBehaviour
    {
        [SerializeField] private OperatorSkillMasterySet masterySet = new();
        [SerializeField] private OperatorSkillMasteryCostPlan masteryCosts =
            OperatorSkillMasteryCostPlan.CreateRecommendedDefault();

        private int _appliedSkill1 = -1;
        private int _appliedSkill2 = -1;
        private OperatorSkillPointRecoveryType _skill1RecoveryType = OperatorSkillPointRecoveryType.Natural;
        private OperatorSkillPointRecoveryType _skill2RecoveryType = OperatorSkillPointRecoveryType.Natural;
        private string _skill1RangeId = string.Empty;
        private string _skill2RangeId = string.Empty;

        public OperatorSkillMasterySet MasterySet => masterySet;
        public OperatorSkillMasteryCostPlan MasteryCosts =>
            masteryCosts != null && masteryCosts.HasData
                ? masteryCosts
                : OperatorSkillMasteryCostPlan.RecommendedDefault;
        public bool HasData => masterySet != null && masterySet.HasData;

        private void Awake()
        {
            // Skill implementations keep their "official data applied" guard as runtime state.
            // Re-apply the serialized Level 7 snapshot after a scene/domain load so skills are
            // immediately usable; meta progression may then replace it with M1-M3.
            _appliedSkill1 = -1;
            _appliedSkill2 = -1;
            _skill1RecoveryType = OperatorSkillPointRecoveryType.Natural;
            _skill2RecoveryType = OperatorSkillPointRecoveryType.Natural;
            _skill1RangeId = string.Empty;
            _skill2RangeId = string.Empty;
            if (HasData)
                ApplyAll(0, 0);
        }

        public OperatorSkillPointRecoveryType GetRecoveryType(int slot)
        {
            return Mathf.Clamp(slot, 1, 2) == 1
                ? _skill1RecoveryType
                : _skill2RecoveryType;
        }

        public string GetRangeId(int slot)
        {
            return Mathf.Clamp(slot, 1, 2) == 1
                ? _skill1RangeId ?? string.Empty
                : _skill2RangeId ?? string.Empty;
        }

        public void Configure(
            OperatorSkillMasterySet set,
            OperatorSkillMasteryCostPlan costs,
            bool applyLevel7 = false)
        {
            masterySet = set ?? new OperatorSkillMasterySet();
            masteryCosts = costs != null && costs.HasData
                ? costs
                : OperatorSkillMasteryCostPlan.CreateRecommendedDefault();
            _appliedSkill1 = -1;
            _appliedSkill2 = -1;
            _skill1RecoveryType = OperatorSkillPointRecoveryType.Natural;
            _skill2RecoveryType = OperatorSkillPointRecoveryType.Natural;
            _skill1RangeId = string.Empty;
            _skill2RangeId = string.Empty;
            if (applyLevel7)
                ApplyAll(0, 0);
        }

        public void ApplyAll(int skill1Mastery, int skill2Mastery)
        {
            ApplySlot(1, skill1Mastery);
            ApplySlot(2, skill2Mastery);
        }

        public bool ApplySlot(int slot, int masteryLevel)
        {
            if (!HasData)
                return false;

            var normalizedSlot = Mathf.Clamp(slot, 1, 2);
            var normalizedMastery = Mathf.Clamp(masteryLevel, 0, 3);
            var alreadyApplied = normalizedSlot == 1
                ? _appliedSkill1 == normalizedMastery
                : _appliedSkill2 == normalizedMastery;
            if (alreadyApplied)
                return true;

            var profile = masterySet.FindSlot(normalizedSlot);
            var snapshot = profile?.GetSnapshot(normalizedMastery);
            if (snapshot == null)
                return false;

            var behaviours = GetComponents<MonoBehaviour>();
            var applied = false;
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IOperatorSkillMasteryTarget target ||
                    target.MasterySlot != slot)
                    continue;
                target.ApplyMasterySnapshot(snapshot);
                applied = true;
            }

            if (applied)
            {
                if (normalizedSlot == 1)
                {
                    _appliedSkill1 = normalizedMastery;
                    _skill1RecoveryType = snapshot.SkillPointRecoveryType;
                    _skill1RangeId = snapshot.RangeId;
                }
                else
                {
                    _appliedSkill2 = normalizedMastery;
                    _skill2RecoveryType = snapshot.SkillPointRecoveryType;
                    _skill2RangeId = snapshot.RangeId;
                }
            }
            return applied;
        }
    }
}
