using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Roguelite.Progression;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    public enum EnemyRank
    {
        Normal = 0,
        Elite = 1,
        Boss = 2
    }

    /// <summary>
    /// Orthogonal enemy threat tier. Archetype controls how an enemy fights; Rank controls
    /// how dangerous/rewarding that enemy is. This keeps normal/elite/boss tuning out of
    /// individual enemy art or combat-archetype templates.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity), typeof(Health))]
    public sealed class EnemyThreatProfile25D : MonoBehaviour
    {
        [SerializeField] private EnemyRank rank = EnemyRank.Normal;

        private bool _applied;

        public EnemyRank Rank => rank;

        public void Configure(EnemyRank value)
        {
            rank = value;
            if (isActiveAndEnabled)
                ApplyIfNeeded();
        }

        private void OnEnable()
        {
            ApplyIfNeeded();
        }

        private void ApplyIfNeeded()
        {
            if (_applied)
                return;

            // Rank is intentionally orthogonal to official enemy combat stats.
            // It may change awareness/presentation/reward value, but it must not
            // multiply HP / ATK / DEF / RES / attack interval.
            var experienceMultiplier = rank switch
            {
                EnemyRank.Elite => 1.75f,
                EnemyRank.Boss => 2.00f,
                _ => 1f
            };

            var experience = GetComponent<EnemyExperienceReward>();
            if (experience != null)
                experience.SetRewardMultiplier(experience.RewardMultiplier * experienceMultiplier);

            var brain = GetComponent<PrototypeEnemyCombatBrain25D>();
            if (brain != null)
                brain.ApplyThreatProfile(rank);

            _applied = true;
        }
    }
}
