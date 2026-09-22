using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
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
        private CollectibleInventory _collectibles;
        private IPlayerSkill _skill1;
        private IPlayerSkill _skill2;

        public IPlayerSkill Skill1 => _skill1;
        public IPlayerSkill Skill2 => _skill2;
        public bool IsCasting => (_skill1?.IsCasting ?? false) || (_skill2?.IsCasting ?? false);

        public event Action<int> SkillCastSucceeded;
        public event Action<int> SkillCancelled;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _input = GetComponent<IPlayerInputSource>();
            _motor = FindLocomotion();
            _attack = GetComponent<PlayerAttackController>();
            _dash = GetComponent<PlayerDashController>();
            _collectibles = GetComponent<CollectibleInventory>();

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

            // The old IMGUI HUD is kept only as an inert compatibility component for old scenes.
            var legacyHud = GetComponent<PlayerSkillPointHUD>();
            if (legacyHud != null)
                legacyHud.enabled = false;
            if (GetComponent<GameplayHUDController>() == null)
                gameObject.AddComponent<GameplayHUDController>();
        }

        private void Update()
        {
            if (_entity != null && _entity.Health != null && _entity.Health.IsDead)
                return;

            var recoveryPercent = _collectibles != null
                ? _collectibles.GetEffectTotal(CollectibleEffectType.SkillPointRecoveryPercent)
                : 0f;
            var recoveryMultiplier = Mathf.Max(0f, 1f + recoveryPercent);
            var flatRecovery = _collectibles != null
                ? Mathf.Max(0f, _collectibles.GetEffectTotal(CollectibleEffectType.SkillPointRecoveryPerSecond))
                : 0f;
            _skill1?.TickSkillPoints(Time.deltaTime, recoveryMultiplier, flatRecovery);
            _skill2?.TickSkillPoints(Time.deltaTime, recoveryMultiplier, flatRecovery);

            if (_input == null || IsCasting || (_dash != null && _dash.IsDashing))
                return;
            if (_motor != null && !_motor.IsGrounded)
                return;

            if (_input.Skill1PressedThisFrame)
                TryCast(_skill1);
            else if (_input.Skill2PressedThisFrame)
                TryCast(_skill2);
        }

        public void GainAllSkillPoints(float amount)
        {
            if (amount <= 0f) return;
            _skill1?.GainSkillPoints(amount);
            _skill2?.GainSkillPoints(amount);
        }

        private void TryCast(IPlayerSkill skill)
        {
            if (skill == null)
                return;

            var activeState = skill as IPlayerSkillActiveState;
            var wasActive = activeState != null && activeState.IsActive;

            if (_attack != null && _attack.IsAttacking)
                _attack.CancelCurrentAttack();

            if (!skill.TryCast())
                return;

            var isActiveAfter = activeState != null && activeState.IsActive;
            if (wasActive && !isActiveAfter)
                SkillCancelled?.Invoke(skill.Slot);
            else
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
