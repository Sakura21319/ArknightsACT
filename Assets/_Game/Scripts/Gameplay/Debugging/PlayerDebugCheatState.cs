using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    /// <summary>
    /// Runtime-only cheat state for the currently controlled operator.
    /// It plugs into the normal player damage gate instead of bypassing DamageSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDebugCheatState : MonoBehaviour, IPlayerInvulnerabilitySource
    {
        [SerializeField] private bool invincible;
        [SerializeField] private bool infiniteSkillPoints;

        private PlayerSkillController _skills;

        public bool IsInvulnerable => invincible;
        public bool Invincible => invincible;
        public bool InfiniteSkillPoints => infiniteSkillPoints;

        private void Awake()
        {
            _skills = GetComponent<PlayerSkillController>();
        }

        private void Update()
        {
            if (!infiniteSkillPoints)
                return;

            _skills ??= GetComponent<PlayerSkillController>();
            if (_skills == null)
                return;

            Refill(_skills.Skill1);
            Refill(_skills.Skill2);
        }

        public void Configure(bool invincibleValue, bool infiniteSkillPointsValue)
        {
            invincible = invincibleValue;
            infiniteSkillPoints = infiniteSkillPointsValue;

            if (infiniteSkillPoints)
            {
                _skills ??= GetComponent<PlayerSkillController>();
                if (_skills != null)
                {
                    Refill(_skills.Skill1);
                    Refill(_skills.Skill2);
                }
            }
        }

        public void Clear()
        {
            invincible = false;
            infiniteSkillPoints = false;
        }

        private static void Refill(IPlayerSkill skill)
        {
            if (skill == null ||
                skill.IsCasting ||
                PlayerSkillLifecycleUtility.IsActive(skill))
                return;

            skill.SetSkillPoints(skill.SkillPointCost);
        }
    }
}
