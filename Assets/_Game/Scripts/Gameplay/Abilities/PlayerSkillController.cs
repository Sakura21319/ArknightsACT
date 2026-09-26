using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEngine;

namespace ArknightsACT.Gameplay.Abilities
{
    public sealed class PlayerSkillController : MonoBehaviour, ICombatActionInterruptHandler
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

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnBasicAttackStarted;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnBasicAttackStarted;
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
            TickSkillPoints(_skill1, Time.deltaTime, recoveryMultiplier, flatRecovery);
            TickSkillPoints(_skill2, Time.deltaTime, recoveryMultiplier, flatRecovery);

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
            if (amount <= 0f)
                return;

            if (PlayerSkillLifecycleUtility.CanReceiveSkillPoints(_skill1))
                _skill1.GainSkillPoints(amount);
            if (PlayerSkillLifecycleUtility.CanReceiveSkillPoints(_skill2))
                _skill2.GainSkillPoints(amount);
        }

        private void TryCast(IPlayerSkill skill)
        {
            if (skill == null || PlayerSkillLifecycleUtility.IsActive(skill))
                return;

            if (_attack != null && _attack.IsAttacking)
                _attack.CancelCurrentAttack();

            if (!skill.TryCast())
                return;

            SkillCastSucceeded?.Invoke(skill.Slot);
        }

        private static void TickSkillPoints(
            IPlayerSkill skill,
            float deltaTime,
            float recoveryMultiplier,
            float flatRecoveryPerSecond)
        {
            if (!PlayerSkillLifecycleUtility.CanReceiveSkillPoints(skill))
                return;

            skill.TickSkillPoints(deltaTime, recoveryMultiplier, flatRecoveryPerSecond);
        }

        private void OnBasicAttackStarted(int _)
        {
            ConsumeBasicAttackAmmo(_skill1);
            ConsumeBasicAttackAmmo(_skill2);
        }

        private void ConsumeBasicAttackAmmo(IPlayerSkill skill)
        {
            if (skill is not IPlayerSkillAmmoConsumer ammo ||
                !ammo.IsActive ||
                !ammo.ConsumeAmmoOnBasicAttackStarted)
                return;

            if (ammo.TryConsumeAmmo() && !ammo.IsActive)
                SkillCancelled?.Invoke(skill.Slot);
        }

        public void InterruptCombatActions(CombatActionMask actions)
        {
            if ((actions & CombatActionMask.Skill) == 0)
                return;

            if (_skill1 is IPlayerSkillInterruptible first && _skill1.IsCasting)
                first.InterruptCast();
            if (_skill2 is IPlayerSkillInterruptible second && _skill2.IsCasting)
                second.InterruptCast();
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
