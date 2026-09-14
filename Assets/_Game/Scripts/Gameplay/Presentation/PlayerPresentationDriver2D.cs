using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    [RequireComponent(typeof(PlayerMotor2D), typeof(PlayerAttackController), typeof(Rigidbody2D))]
    public sealed class PlayerPresentationDriver2D : MonoBehaviour
    {
        [SerializeField] private float movingThreshold = 0.08f;

        private PlayerMotor2D _motor;
        private PlayerAttackController _attack;
        private PlayerSkillController _skill;
        private Rigidbody2D _body;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _spine;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _attack = GetComponent<PlayerAttackController>();
            _skill = GetComponent<PlayerSkillController>();
            _body = GetComponent<Rigidbody2D>();
            _entity = GetComponent<CombatEntity>();
            _spine = GetComponentInChildren<SpineCharacterPresentation2D>(true);
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;

            if (_skill != null)
                _skill.SkillCastSucceeded += OnSkillCast;

            if (_entity != null)
            {
                _entity.Damaged += OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;

            if (_skill != null)
                _skill.SkillCastSucceeded -= OnSkillCast;

            if (_entity != null)
            {
                _entity.Damaged -= OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (_spine == null)
                _spine = GetComponentInChildren<SpineCharacterPresentation2D>(true);

            if (_spine == null || _body == null || _motor == null)
                return;

            var moving = Mathf.Abs(_body.linearVelocity.x) > movingThreshold;
            _spine.SetLocomotion(moving, _motor.FacingSign);
        }

        private void OnAttackStarted(int comboIndex)
        {
            _spine?.PlayAttack(comboIndex, _motor != null ? _motor.FacingSign : 1);
        }

        private void OnSkillCast()
        {
            _spine?.PlaySkill(_motor != null ? _motor.FacingSign : 1);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            _spine?.PlayHit();
        }

        private void OnDied()
        {
            _spine?.PlayDie();
        }
    }
}
