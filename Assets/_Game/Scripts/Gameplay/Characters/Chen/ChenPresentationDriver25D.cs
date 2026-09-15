using System.Linq;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Lightweight 2.5D presentation driver. Gameplay roots move on XZ while the visible Spine
    /// stays camera-facing through BillboardPresentation25D.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor25D), typeof(PlayerAttackController), typeof(PlayerSkillController))]
    public sealed class ChenPresentationDriver25D : MonoBehaviour
    {
        private PlayerMotor25D _motor;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;
        private bool _dead;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor25D>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCast;
            if (_entity?.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCast;
            if (_entity?.Health != null)
                _entity.Health.Died -= OnDied;
        }

        private void Update()
        {
            if (_dead || _presentation == null || _motor == null)
                return;
            if ((_attack != null && _attack.IsAttacking) || (_skills != null && _skills.IsCasting))
                return;
            _presentation.SetLocomotion(_motor.IsMoving, _motor.FacingSign);
        }

        private void OnAttackStarted(int comboIndex)
        {
            if (_dead || _presentation == null)
                return;
            _presentation.PlayAttack(comboIndex, _motor != null ? _motor.FacingSign : 1);
        }

        private void OnSkillCast(int slot)
        {
            if (_dead || _presentation == null)
                return;
            _presentation.PlaySkill(_motor != null ? _motor.FacingSign : 1);
        }

        private void OnDied()
        {
            _dead = true;
            _presentation?.PlayDie();
        }
    }
}
