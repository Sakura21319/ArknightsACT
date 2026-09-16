using System;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    [DisallowMultipleComponent]
    public sealed class RogueliteRunState : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingIngots = 6;
        [SerializeField, Min(2)] private int maxLevel = 10;
        [SerializeField, Min(1)] private int baseExperienceToNextLevel = 70;
        [SerializeField, Min(0)] private int experienceGrowthPerLevel = 35;

        public static RogueliteRunState Instance { get; private set; }

        public int Ingots { get; private set; }
        public int RouteDepth { get; private set; } = 1;
        public int CombatWinsSinceBoss { get; private set; }
        public int StageIndex { get; private set; } = 1;
        public int ExploredBlocks { get; private set; }
        public int EmergencyClears { get; private set; }
        public int Level { get; private set; } = 1;
        public int CurrentExperience { get; private set; }
        public int MaxLevel => Mathf.Max(2, maxLevel);
        public int ExperienceToNextLevel => Level >= MaxLevel
            ? 0
            : Mathf.Max(1, baseExperienceToNextLevel + (Level - 1) * experienceGrowthPerLevel);

        public event Action Changed;
        public event Action<int> LevelIncreased;

        private void Awake()
        {
            Instance = this;
            Ingots = Mathf.Max(0, startingIngots);
            RouteDepth = 1;
            CombatWinsSinceBoss = 0;
            StageIndex = 1;
            ExploredBlocks = 0;
            EmergencyClears = 0;
            Level = 1;
            CurrentExperience = 0;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void AddIngots(int amount)
        {
            if (amount <= 0)
                return;
            Ingots += amount;
            Changed?.Invoke();
        }

        public bool TrySpendIngots(int amount)
        {
            if (amount <= 0)
                return true;
            if (Ingots < amount)
                return false;

            Ingots -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0 || Level >= MaxLevel)
                return;

            CurrentExperience += amount;
            while (Level < MaxLevel)
            {
                var required = ExperienceToNextLevel;
                if (CurrentExperience < required)
                    break;

                CurrentExperience -= required;
                Level++;
                if (Level >= MaxLevel)
                    CurrentExperience = 0;
                LevelIncreased?.Invoke(Level);
            }
            Changed?.Invoke();
        }

        public void AdvanceRouteDepth()
        {
            RouteDepth++;
            Changed?.Invoke();
        }

        public void SetStageIndex(int stageIndex)
        {
            StageIndex = Mathf.Max(1, stageIndex);
            Changed?.Invoke();
        }

        public void RecordBlockExplored()
        {
            ExploredBlocks++;
            Changed?.Invoke();
        }

        public void RecordCombatClear(bool boss, bool emergency = false)
        {
            CombatWinsSinceBoss = boss ? 0 : CombatWinsSinceBoss + 1;
            if (emergency)
                EmergencyClears++;
            Changed?.Invoke();
        }
    }
}
