using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Keeps the existing authored PRTS Spine presentation usable in the top-down prototype.
    /// Gameplay stays X/Y top-down; the source character art remains the original 2D combat rig.
    /// </summary>
    public sealed class TopDownTexasPresentationDriver2D : MonoBehaviour
    {
        private TopDownPlayerMotor2D _motor;
        private TopDownTexasMeleeController _melee;
        private PlayerSkillController _skillController;
        private Health _health;
        private SpineCharacterPresentation2D _presentation;
        private SpineBoneMotionRetarget2D _retarget;

        private void Awake()
        {
            _motor = GetComponent<TopDownPlayerMotor2D>();
            _melee = GetComponent<TopDownTexasMeleeController>();
            _skillController = GetComponent<PlayerSkillController>();
            _health = GetComponent<Health>();
            _presentation = GetComponentInChildren<SpineCharacterPresentation2D>(true);
            _retarget = GetComponent<SpineBoneMotionRetarget2D>();

            // Presentation helper only: keep fast top-down melee cadence visually aligned with
            // the authored tower-defense attack clip without touching the source Spine asset.
            if (GetComponent<TopDownSpineAttackPlaybackSpeed2D>() == null && _melee != null)
                gameObject.AddComponent<TopDownSpineAttackPlaybackSpeed2D>();
        }

        private void OnEnable()
        {
            if (_melee != null)
                _melee.AttackStarted += OnAttackStarted;
            if (_skillController != null)
                _skillController.SkillCastSucceeded += OnSkillCast;
            if (_health != null)
                _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_melee != null)
                _melee.AttackStarted -= OnAttackStarted;
            if (_skillController != null)
                _skillController.SkillCastSucceeded -= OnSkillCast;
            if (_health != null)
                _health.Died -= OnDied;
            _retarget?.SetMoving(false);
        }

        private void Update()
        {
            if (_presentation == null || _motor == null || _health == null || _health.IsDead)
                return;

            var moving = _motor.IsMoving;
            var attackOwnsPose = _melee != null && _melee.IsAttacking;
            _retarget?.SetMoving(moving && !attackOwnsPose);
            _presentation.SetExternalLocomotionActive(_retarget != null);
            _presentation.SetLocomotion(moving && !attackOwnsPose, _motor.FacingSign);
        }

        private void OnAttackStarted(Vector2 direction)
        {
            if (_presentation == null || _motor == null)
                return;
            _motor.SetFacing(direction);
            _retarget?.SetMoving(false);
            _presentation.PlayAttack(0, _motor.FacingSign);
        }

        private void OnSkillCast()
        {
            if (_presentation == null || _motor == null)
                return;
            _retarget?.SetMoving(false);
            _presentation.PlaySkill(_motor.FacingSign);
        }

        private void OnDied()
        {
            _retarget?.SetMoving(false);
            _presentation?.PlayDie();
        }
    }
}
