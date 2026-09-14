using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Abilities
{
    public sealed class PlayerSkillController : MonoBehaviour
    {
        private CombatEntity _entity;
        private IPlayerInputSource _input;
        private IPlayerSkill _skill;

        public IPlayerSkill Skill => _skill;
        public event Action SkillCastSucceeded;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _input = GetComponent<IPlayerInputSource>();
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerSkill skill)
                {
                    _skill = skill;
                    break;
                }
            }
        }

        private void Update()
        {
            if (_entity != null && _entity.Health != null && _entity.Health.IsDead)
                return;

            if (_input == null || !_input.SkillPressedThisFrame || _skill == null)
                return;

            if (_skill.TryCast())
                SkillCastSucceeded?.Invoke();
        }
    }
}
