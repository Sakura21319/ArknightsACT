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
        private bool _attackRecoveryCancelledToMove;
        private bool _dead;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _attack = GetComponent<PlayerAttackController>();
            _skill = GetComponent<PlayerSkillController>();
            _body = GetComponent<Rigidbody2D>();
            _entity = GetComponent<CombatEntity>();
            _spine = FindEnabledPresentation();
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
            if (_spine == null || !_spine.enabled)
                _spine = FindEnabledPresentation();
            if (_motionRetarget == null)
                _motionRetarget = GetComponent<SpineBoneMotionRetarget2D>();

            if (_dead)
            {
                _motionRetarget?.SetMoving(false);
                _spine?.SetExternalLocomotionActive(false);
                return;
            }

            if (_spine == null || _body == null || _motor == null)
                return;

            var moving = Mathf.Abs(_body.linearVelocity.x) > movingThreshold;
            var attackMovementLocked = _attack != null && _attack.IsMovementLocked;
            var attackOwnsAirPose = _attack != null &&
                                    _attack.IsAttacking &&
                                    (_attack.CurrentActionType == PlayerAttackActionType.AirSlash ||
                                     _attack.CurrentActionType == PlayerAttackActionType.Plunge);

            // Air actions are full movement states, not recovery tails. Never let horizontal
            // velocity swap them back to walking/retargeted locomotion before the action ends.
            if (attackOwnsAirPose)
            {
                _motionRetarget?.SetMoving(false);
                _spine.SetExternalLocomotionActive(false);
                return;
            }

            // Grounded attacks may cancel only their visual recovery tail after the authoritative
            // impact has landed. Gameplay cadence is unchanged, so this remains feel-only.
            if (moving &&
                _attack != null &&
                _attack.IsAttacking &&
                !attackMovementLocked &&
                !_attackRecoveryCancelledToMove)
            {
                _attackRecoveryCancelledToMove = true;
                _externalMotionBlockedUntil = Time.time;
                _spine.CancelBasicAttackPresentation();
            }

            var allowRetarget = moving &&
                                !attackMovementLocked &&
                                Time.time >= _externalMotionBlockedUntil &&
                                _motionRetarget != null &&
                                _motionRetarget.IsCompatible;

            _motionRetarget?.SetMoving(allowRetarget);
            _spine.SetExternalLocomotionActive(allowRetarget);
            _spine.SetLocomotion(moving, _motor.FacingSign);
        }

        private void OnAttackStarted(int comboIndex)
        {
            if (_dead)
                return;
            _attackRecoveryCancelledToMove = false;
            _externalMotionBlockedUntil = Mathf.Max(_externalMotionBlockedUntil, Time.time + attackPresentationGrace);
            _motionRetarget?.SetMoving(false);
            _spine?.SetExternalLocomotionActive(false);
            _spine?.PlayAttack(comboIndex, _motor != null ? _motor.FacingSign : 1);
        }

        private void OnSkillCast()
        {
            if (_dead)
                return;
            _attackRecoveryCancelledToMove = false;
            _externalMotionBlockedUntil = Mathf.Max(_externalMotionBlockedUntil, Time.time + skillPresentationLock);
            _motionRetarget?.SetMoving(false);
            _spine?.SetExternalLocomotionActive(false);
            _spine?.PlaySkill(_motor != null ? _motor.FacingSign : 1);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            if (_dead)
                return;
            _attackRecoveryCancelledToMove = false;
            _externalMotionBlockedUntil = Mathf.Max(_externalMotionBlockedUntil, Time.time + hitPresentationLock);
            _motionRetarget?.SetMoving(false);
            _spine?.SetExternalLocomotionActive(false);
            _spine?.PlayHit();
        }

        private void OnDied()
        {
            _dead = true;
            _attackRecoveryCancelledToMove = false;
            _motionRetarget?.SetMoving(false);
            _spine?.SetExternalLocomotionActive(false);
            _spine?.PlayDie();
        }

        private SpineCharacterPresentation2D FindEnabledPresentation()
        {
            var presentations = GetComponentsInChildren<SpineCharacterPresentation2D>(true);
            for (var i = 0; i < presentations.Length; i++)
            {
                if (presentations[i] != null && presentations[i].enabled)
                    return presentations[i];
            }
            return null;
        }
    }
}
