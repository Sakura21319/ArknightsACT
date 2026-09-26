using System.Linq;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Skadi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor25D), typeof(PlayerAttackController))]
    public sealed class SkadiPresentationDriver25D : MonoBehaviour, IPlayerRunResettable
    {
        private PlayerMotor25D _motor;
        private PlayerAttackController _attack;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;
        private SpineBoneMotionRetarget2D _retarget;
        private BillboardPresentation25D _billboard;
        private bool _dead;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor25D>();
            _attack = GetComponent<PlayerAttackController>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
            _retarget = GetComponent<SpineBoneMotionRetarget2D>();
            _billboard = GetComponentInChildren<BillboardPresentation25D>(true);

            _presentation?.Configure(
                "Idle",
                "Move",
                new[] { "Attack" },
                "Attack",
                string.Empty,
                "Die");
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
            if (_entity?.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;
            if (_entity?.Health != null)
                _entity.Health.Died -= OnDied;

            _retarget?.SetMoving(false);
            _billboard?.ResetDirectionalCue();
        }

        private void Update()
        {
            if (_dead || _presentation == null || _motor == null)
                return;

            var facing = _motor.FacingSign;
            if (_attack != null && _attack.IsAttacking)
            {
                StopLocomotion();
                _presentation.SetFacingImmediate(facing);
                _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
                return;
            }

            var moving = _motor.IsMoving;
            var useRetarget = moving && _retarget != null && _retarget.IsCompatible;
            _retarget?.SetMoving(useRetarget);
            _presentation.SetExternalLocomotionActive(useRetarget);
            _presentation.SetLocomotion(moving, facing);
            _billboard?.SetPlanarDirection(
                _motor.PlanarForward,
                facing,
                moving ? 0.65f : 0.25f);
        }

        private void OnAttackStarted(int comboIndex)
        {
            if (_dead || _presentation == null)
                return;

            StopLocomotion();
            var facing = _motor != null ? _motor.FacingSign : 1;
            if (!_presentation.PlayNamedAnimation("Attack", facing, false))
                _presentation.PlayAttack(comboIndex, facing);

            if (_motor != null)
                _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
        }

        private void OnDied()
        {
            _dead = true;
            StopLocomotion();
            _billboard?.ResetDirectionalCue();
            _presentation?.PlayDie();
        }

        public void ResetForNewRun()
        {
            _dead = false;
            _retarget?.SetMoving(false);
            _presentation?.SetExternalLocomotionActive(false);
            _billboard?.ResetDirectionalCue();
            _presentation?.SetLocomotion(false, _motor != null ? _motor.FacingSign : 1);
        }

        private void StopLocomotion()
        {
            _retarget?.SetMoving(false);
            _presentation?.SetExternalLocomotionActive(false);
        }
    }
}
