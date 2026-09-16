using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Abilities
{
    public sealed class PlayerSkillController : MonoBehaviour
    {
        private CombatEntity _entity;
        private IPlayerInputSource _input;
        private IPlayerLocomotion _motor;
        private PlayerAttackController _attack;
        private PlayerDashController _dash;
        private IPlayerSkill _skill1;
        private IPlayerSkill _skill2;

        public IPlayerSkill Skill1 => _skill1;
        public IPlayerSkill Skill2 => _skill2;
        public bool IsCasting => (_skill1?.IsCasting ?? false) || (_skill2?.IsCasting ?? false);

        public event Action<int> SkillCastSucceeded;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _input = GetComponent<IPlayerInputSource>();
            _motor = FindLocomotion();
            _attack = GetComponent<PlayerAttackController>();
            _dash = GetComponent<PlayerDashController>();

            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IPlayerSkill skill)
                    continue;

                if (skill.Slot == 1 && _skill1 == null)
                    _skill1 = skill;
                else if (skill.Slot == 2 && _skill2 == null)
                    _skill2 = skill;
            }
        }

        private void Update()
        {
            if (_entity != null && _entity.Health != null && _entity.Health.IsDead)
                return;
            if (_input == null || IsCasting || (_dash != null && _dash.IsDashing))
                return;
            if (_motor != null && !_motor.IsGrounded)
                return;

            if (_input.Skill1PressedThisFrame)
                TryCast(_skill1);
            else if (_input.Skill2PressedThisFrame)
                TryCast(_skill2);
        }

        private void TryCast(IPlayerSkill skill)
        {
            if (skill == null)
                return;

            if (_attack != null && _attack.IsAttacking)
                _attack.CancelCurrentAttack();

            if (!skill.TryCast())
                return;

            SkillCastSucceeded?.Invoke(skill.Slot);
        }

        private IPlayerLocomotion FindLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            }
            return null;
        }
    }
}
