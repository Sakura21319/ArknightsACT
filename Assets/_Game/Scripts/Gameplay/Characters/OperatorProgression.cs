using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    [Serializable]
    public struct OperatorProgressionSnapshot
    {
        [SerializeField, Min(1)] private int level;
        [SerializeField] private OperatorBaseStats stats;

        public int Level => Mathf.Max(1, level);
        public OperatorBaseStats Stats => stats;

        public OperatorProgressionSnapshot(int level, OperatorBaseStats stats)
        {
            this.level = Mathf.Max(1, level);
            this.stats = stats;
        }
    }

    [Serializable]
    public sealed class OperatorE2Progression
    {
        [SerializeField, Min(1)] private int maxLevel = 90;
        [SerializeField] private string rangeId = string.Empty;
        [SerializeField] private OperatorProgressionSnapshot[] snapshots = Array.Empty<OperatorProgressionSnapshot>();

        public int MaxLevel => Mathf.Max(1, maxLevel);
        public string RangeId => rangeId ?? string.Empty;
        public IReadOnlyList<OperatorProgressionSnapshot> Snapshots => snapshots;
        public bool HasData => snapshots != null && snapshots.Length > 0;

        public OperatorE2Progression() { }

        public OperatorE2Progression(
            int maxLevel,
            string rangeId,
            params OperatorProgressionSnapshot[] snapshots)
        {
            this.maxLevel = Mathf.Max(1, maxLevel);
            this.rangeId = rangeId ?? string.Empty;
            this.snapshots = snapshots ?? Array.Empty<OperatorProgressionSnapshot>();
            Array.Sort(this.snapshots, (a, b) => a.Level.CompareTo(b.Level));
        }

        public int ClampLevel(int level) => Mathf.Clamp(level, 1, MaxLevel);

        public OperatorBaseStats Evaluate(int level)
        {
            if (!HasData)
                return new OperatorBaseStats(100f, 10f, 0f, 0f);

            var clamped = ClampLevel(level);
            if (snapshots.Length == 1 || clamped <= snapshots[0].Level)
                return snapshots[0].Stats;

            for (var i = 1; i < snapshots.Length; i++)
            {
                var upper = snapshots[i];
                if (clamped > upper.Level)
                    continue;

                var lower = snapshots[i - 1];
                var span = Mathf.Max(1, upper.Level - lower.Level);
                var t = Mathf.Clamp01((clamped - lower.Level) / (float)span);
                return OperatorBaseStats.Lerp(lower.Stats, upper.Stats, t);
            }

            return snapshots[snapshots.Length - 1].Stats;
        }

        public bool HasCheckpoint(int level)
        {
            if (!HasData)
                return false;
            for (var i = 0; i < snapshots.Length; i++)
                if (snapshots[i].Level == level)
                    return true;
            return false;
        }
    }

    [Serializable]
    public sealed class OperatorUpgradeMaterialCost
    {
        [SerializeField] private string itemId = string.Empty;
        [SerializeField, Min(1)] private int amount = 1;

        public string ItemId => itemId ?? string.Empty;
        public int Amount => Mathf.Max(1, amount);

        public OperatorUpgradeMaterialCost(string itemId, int amount)
        {
            this.itemId = itemId ?? string.Empty;
            this.amount = Mathf.Max(1, amount);
        }
    }

    [Serializable]
    public sealed class OperatorLevelUpgradeStep
    {
        [SerializeField, Min(1)] private int fromLevel = 1;
        [SerializeField, Min(1)] private int targetLevel = 30;
        [SerializeField, Min(0)] private int lmdCost = 20000;
        [SerializeField] private OperatorUpgradeMaterialCost[] materials = Array.Empty<OperatorUpgradeMaterialCost>();

        public int FromLevel => Mathf.Max(1, fromLevel);
        public int TargetLevel => Mathf.Max(FromLevel, targetLevel);
        public int LmdCost => Mathf.Max(0, lmdCost);
        public IReadOnlyList<OperatorUpgradeMaterialCost> Materials =>
            materials ?? Array.Empty<OperatorUpgradeMaterialCost>();

        public OperatorLevelUpgradeStep(
            int fromLevel,
            int targetLevel,
            int lmdCost,
            params OperatorUpgradeMaterialCost[] materials)
        {
            this.fromLevel = Mathf.Max(1, fromLevel);
            this.targetLevel = Mathf.Max(this.fromLevel, targetLevel);
            this.lmdCost = Mathf.Max(0, lmdCost);
            this.materials = materials ?? Array.Empty<OperatorUpgradeMaterialCost>();
        }
    }

    [Serializable]
    public sealed class OperatorMetaProgressionPlan
    {
        [SerializeField] private OperatorLevelUpgradeStep[] levelSteps =
            Array.Empty<OperatorLevelUpgradeStep>();

        private static readonly OperatorMetaProgressionPlan Recommended =
            CreateRecommendedDefault();

        public IReadOnlyList<OperatorLevelUpgradeStep> LevelSteps =>
            levelSteps ?? Array.Empty<OperatorLevelUpgradeStep>();
        public bool HasData => levelSteps != null && levelSteps.Length > 0;
        public static OperatorMetaProgressionPlan RecommendedDefault => Recommended;

        public OperatorMetaProgressionPlan() { }

        public OperatorMetaProgressionPlan(params OperatorLevelUpgradeStep[] steps)
        {
            levelSteps = steps ?? Array.Empty<OperatorLevelUpgradeStep>();
            Array.Sort(levelSteps, (a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return a.FromLevel.CompareTo(b.FromLevel);
            });
        }

        public OperatorLevelUpgradeStep GetNextStep(int currentLevel)
        {
            if (!HasData)
                return null;

            var level = Mathf.Max(1, currentLevel);
            for (var i = 0; i < levelSteps.Length; i++)
            {
                var step = levelSteps[i];
                if (step != null && step.FromLevel == level)
                    return step;
            }
            return null;
        }

        public static OperatorMetaProgressionPlan CreateRecommendedDefault()
        {
            return new OperatorMetaProgressionPlan(
                new OperatorLevelUpgradeStep(
                    1,
                    30,
                    20000,
                    new OperatorUpgradeMaterialCost("rogue_3_relic_legacy_54", 2)),
                new OperatorLevelUpgradeStep(
                    30,
                    60,
                    45000,
                    new OperatorUpgradeMaterialCost("rogue_3_relic_legacy_55", 2)),
                new OperatorLevelUpgradeStep(
                    60,
                    90,
                    90000,
                    new OperatorUpgradeMaterialCost("rogue_4_relic_legacy_187", 1)));
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(OperatorRuntimeStats))]
    public sealed class OperatorProgressionController : MonoBehaviour
    {
        [SerializeField] private string sourceCharacterId = string.Empty;
        [SerializeField] private OperatorE2Progression e2Progression = new();
        [SerializeField] private OperatorMetaProgressionPlan metaProgressionPlan = OperatorMetaProgressionPlan.CreateRecommendedDefault();
        [SerializeField, Min(1)] private int eliteLevel = 1;
        [SerializeField] private bool applyProgressionStatsOnStart;

        private OperatorRuntimeStats _runtimeStats;

        public string SourceCharacterId => sourceCharacterId;
        public OperatorE2Progression E2Progression => e2Progression;
        public OperatorMetaProgressionPlan MetaProgressionPlan =>
            metaProgressionPlan != null && metaProgressionPlan.HasData
                ? metaProgressionPlan
                : OperatorMetaProgressionPlan.RecommendedDefault;
        public int EliteLevel => eliteLevel;
        public bool HasProgression => e2Progression != null && e2Progression.HasData;

        private void Awake()
        {
            _runtimeStats = GetComponent<OperatorRuntimeStats>();
        }

        private void Start()
        {
            if (applyProgressionStatsOnStart)
                ApplyCurrentLevel();
        }

        public void Configure(
            string sourceId,
            OperatorE2Progression progression,
            int initialEliteLevel = 1,
            bool applyImmediately = false)
        {
            sourceCharacterId = sourceId ?? string.Empty;
            e2Progression = progression ?? new OperatorE2Progression();
            eliteLevel = e2Progression.HasData
                ? e2Progression.ClampLevel(initialEliteLevel)
                : Mathf.Max(1, initialEliteLevel);
            applyProgressionStatsOnStart = applyImmediately;

            if (applyImmediately)
                ApplyCurrentLevel();
        }

        public void ConfigureMetaProgression(OperatorMetaProgressionPlan plan)
        {
            metaProgressionPlan =
                plan != null && plan.HasData
                    ? plan
                    : OperatorMetaProgressionPlan.RecommendedDefault;
        }

        public bool SetEliteLevel(int level, bool applyStats = true)
        {
            if (!HasProgression)
                return false;

            eliteLevel = e2Progression.ClampLevel(level);
            if (applyStats)
                ApplyCurrentLevel();
            return true;
        }

        public bool ApplyCurrentLevel()
        {
            if (!HasProgression)
                return false;

            if (_runtimeStats == null)
                _runtimeStats = GetComponent<OperatorRuntimeStats>();
            if (_runtimeStats == null)
                return false;

            var evaluated = e2Progression.Evaluate(eliteLevel);
            // Official rangeId is consumed by OperatorRangeUtility as a tile shape.
            // Keep the authored ACT scalar ranges as the world-space calibration so P5 changes
            // shape/coverage without abruptly rescaling the whole combat scene.
            evaluated = new OperatorBaseStats(
                evaluated.MaxHealth,
                evaluated.Attack,
                evaluated.PhysicalDefense,
                evaluated.ArtsResistance,
                evaluated.AttackInterval,
                _runtimeStats.BaseBasicAttackRange,
                _runtimeStats.BaseSkillRange);

            _runtimeStats.ConfigureBasePreserveHealthRatio(evaluated);
            return true;
        }
    }
}
