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
        [SerializeField] private float attackPresentationGrace = 0.28f;
        [SerializeField] private float skillPresentationLock = 0.55f;
        [SerializeField] private float hitPresentationLock = 0.14f;

        private PlayerMotor2D _motor;
        private PlayerAttackController _attack;
        private PlayerSkillController _skill;
        private Rigidbody2D _body;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _spine;
        private SpineBoneMotionRetarget2D _motionRetarget;
        private float _externalMotionBlockedUntil;
        private bool _dead;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _attack = GetComponent<PlayerAttackController>();
            _skill = GetComponent<PlayerSkillController>();
            _body = GetComponent<Rigidbody2D>();
            _entity = GetComponent<CombatEntity>();
            _spine = GetComponentInChildren<SpineCharacterPresentation2D>(true);
            _motionRetarget = GetComponent<SpineBoneMotionRetarget2D>();
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
            if (_motionRetarget == null)
                _motionRetarget = GetComponent<SpineBoneMotionRetarget2D>();

            if (_spine == null || _body == null || _motor == null)
                return;

            var moving = Mathf.Abs(_body.linearVelocity.x) > movingThreshold;
            var allowRetarget = !_dead &&
                                moving &&
                                (_attack == null || !_attack.IsAttacking) &&
                                Time.time >= _externalMotionBlockedUntil &&
                                _motionRetarget != null &&
                                _motionRetarget.IsCompatible;

            _motionRetarget?.SetMoving(allowRetarget);
            _spine.SetExternalLocomotionActive(allowRetarget);
            _spine.SetLocomotion(moving, _motor.FacingSign);
        }

        private void OnAttackStarted(int comboIndex)
        {
            _externalMotionBlockedUntil = Mathf.Max(_externalMotionBlockedUntil, Time.time + attackPresentationGrace);
            _motionRetarget?.SetMoving(false);
            _spine?.SetExternalLocomotionActive(false);
            _spine?.PlayAttack(comboIndex, _motor != null ? _motor.FacingSign : 1);
        }

        private void OnSkillCast()
        {
            _externalMotionBlockedUntil = Mathf.Max(_externalMotionBlockedUntil, Time.time + skillPresentationLock);
            _motionRetarget?.SetMoving(false);
            _spine?.SetExternalLocomotionActive(false);
            _spine?.PlaySkill(_motor != null ? _motor.FacingSign : 1);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            _externalMotionBlockedUntil = Mathf.Max(_externalMotionBlockedUntil, Time.time + hitPresentationLock);
            _motionRetarget?.SetMoving(false);
            _spine?.SetExternalLocomotionActive(false);
            _spine?.PlayHit();
        }

        private void OnDied()
        {
            _dead = true;
            _motionRetarget?.SetMoving(false);
            _spine?.SetExternalLocomotionActive(false);
            _spine?.PlayDie();
        }
    }
}
