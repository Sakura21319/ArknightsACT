using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Wisadel
{
    /// <summary>Wisadel action-to-Spine mapping. Gameplay timing and damage stay in gameplay components.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor25D), typeof(PlayerAttackController), typeof(PlayerSkillController))]
    public sealed class WisadelPresentationDriver25D : MonoBehaviour, IPlayerRunResettable
    {
        private static readonly string[] AttackActions = { "Attack_A", "Attack_B", "Attack_C" };

        private PlayerMotor25D _motor;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private WisadelSkill _skill2;
        private WisadelSkill _skill3;
        private WisadelRangedBasicAttack _rangedAttack;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;
        private SpineBoneMotionRetarget2D _retarget;
        private BillboardPresentation25D _billboard;
        private int _activeSkillSlot;
        private string _skillStateAnimation = string.Empty;
        private float _actionLockUntil;
        private float _endLockUntil;
        private bool _skill2AutoAttacking;
        private bool _dead;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor25D>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            var wisadelSkills = GetComponents<WisadelSkill>();
            for (var i = 0; i < wisadelSkills.Length; i++)
            {
                if (wisadelSkills[i] == null)
                    continue;
                if (wisadelSkills[i].Slot == 1)
                    _skill2 = wisadelSkills[i];
                else if (wisadelSkills[i].Slot == 2)
                    _skill3 = wisadelSkills[i];
            }
            _rangedAttack = GetComponent<WisadelRangedBasicAttack>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<SpineCharacterPresentation2D>(true);
            _retarget = GetComponent<SpineBoneMotionRetarget2D>();
            _retarget?.EnableFullSourceVisuals(true);
            _billboard = GetComponentInChildren<BillboardPresentation25D>(true);

            _presentation?.Configure(
                "Idle",
                "Move",
                AttackActions,
                "Skill_3_Begin",
                "Stun",
                "Die");
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCast;
            if (_skill2 != null)
            {
                _skill2.AutoAttackStarted += OnSkill2AutoAttackStarted;
                _skill2.AutoShotStarted += OnSkill2AutoShotStarted;
                _skill2.AutoAttackStopped += OnSkill2AutoAttackStopped;
            }
            if (_entity?.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCast;
            if (_skill2 != null)
            {
                _skill2.AutoAttackStarted -= OnSkill2AutoAttackStarted;
                _skill2.AutoShotStarted -= OnSkill2AutoShotStarted;
                _skill2.AutoAttackStopped -= OnSkill2AutoAttackStopped;
            }
            if (_entity?.Health != null)
                _entity.Health.Died -= OnDied;
            _retarget?.SetMoving(false);
        }

        private void Update()
        {
            if (_dead || _presentation == null || _motor == null)
                return;

            var facing = _motor.FacingSign;
            if (_activeSkillSlot != 0)
            {
                var skill = _activeSkillSlot == 1 ? _skill2 : _skill3;
                var lifecycleActive = skill != null && PlayerSkillLifecycleUtility.IsActive(skill);
                var lifecycleCasting = skill != null && skill.IsCasting;

                if (lifecycleCasting || lifecycleActive)
                {
                    // Both skills use the authored state chain:
                    // Begin -> Idle while no attack -> Loop while attacking -> End.
                    StopLocomotion();
                    _presentation.SetFacingImmediate(facing);
                    _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);

                    // Begin and S3's individual shot animation are one-shots. Do not replace them
                    // until their authored duration has elapsed.
                    if (Time.time < _actionLockUntil)
                        return;

                    if (lifecycleActive)
                    {
                        if (_activeSkillSlot == 1)
                            EnsureSkillLoop(_skill2AutoAttacking ? "Skill_2_Loop" : "Skill_2_Idle");
                        else
                            EnsureSkillLoop("Skill_3_Idle");
                    }

                    return;
                }

                // The final S3 ammo round can end gameplay state in the same frame that its
                // Skill_3_Loop shot starts. Let that shot finish before playing Skill_3_End.
                if (Time.time < _actionLockUntil)
                {
                    StopLocomotion();
                    _presentation.SetFacingImmediate(facing);
                    _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
                    return;
                }

                var endedSlot = _activeSkillSlot;
                _activeSkillSlot = 0;
                _skillStateAnimation = string.Empty;
                var end = endedSlot == 1 ? "Skill_2_End" : "Skill_3_End";
                var duration = ResolveDuration(end, 0.35f);
                if (_presentation.PlayNamedAnimation(end, facing, false, duration))
                {
                    _endLockUntil = Time.time + duration;
                    _actionLockUntil = _endLockUntil;
                    return;
                }
            }

            if (Time.time < _actionLockUntil || Time.time < _endLockUntil)
            {
                StopLocomotion();
                _presentation.SetFacingImmediate(facing);
                _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
                return;
            }

            var useRetarget = _motor.IsMoving && _retarget != null && _retarget.IsCompatible;
            _retarget?.SetMoving(useRetarget);
            _presentation.SetExternalLocomotionActive(useRetarget);
            _presentation.SetLocomotion(_motor.IsMoving, facing);
            _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, _motor.IsMoving ? 0.65f : 0.25f);
        }

        private void OnAttackStarted(int comboIndex)
        {
            if (_dead || _presentation == null)
                return;

            // Wisadel is a ranged auto-target operator. Resolve the nearest valid target before
            // choosing animation facing so basic attacks and S3 do not depend on last move input.
            if (_rangedAttack != null &&
                _rangedAttack.TryGetAimTarget(out var aimTarget) &&
                aimTarget != null &&
                _motor != null)
            {
                var direction = aimTarget.transform.position - transform.position;
                direction.y = 0f;
                _motor.SetPlanarFacing(direction);
            }

            if (_skill3 != null &&
                (PlayerSkillLifecycleUtility.IsActive(_skill3) ||
                 _skill3.LastAmmoConsumedFrame == Time.frameCount))
            {
                // S3 mirrors S2: Begin/Idle are stance states, while each actual attack
                // plays Skill_3_Loop once and then returns to Skill_3_Idle if ammo remains.
                var duration = ResolveDuration("Skill_3_Loop", 0.35f);
                _skillStateAnimation = "Skill_3_Loop";
                _presentation.PlayNamedAnimation(
                    "Skill_3_Loop",
                    _motor != null ? _motor.FacingSign : 1,
                    false,
                    duration);
                _actionLockUntil = Time.time + duration;
                return;
            }

            var action = AttackActions[Mathf.Abs(comboIndex) % AttackActions.Length];
            var normalDuration = ResolveDuration(action, 0.26f);
            _presentation.PlayNamedAnimation(action, _motor != null ? _motor.FacingSign : 1, false, normalDuration);
            _actionLockUntil = Time.time + normalDuration;
        }

        private void OnSkillCast(int slot)
        {
            if (_dead || _presentation == null || slot < 1 || slot > 2)
                return;

            _activeSkillSlot = slot;
            _skill2AutoAttacking = false;
            _endLockUntil = 0f;

            var begin = slot == 1 ? "Skill_2_Begin" : "Skill_3_Begin";
            var duration = ResolveDuration(begin, slot == 1 ? 0.17f : 0.55f);
            _skillStateAnimation = begin;
            _actionLockUntil = Time.time + duration;
            _presentation.PlayNamedAnimation(
                begin,
                _motor != null ? _motor.FacingSign : 1,
                false,
                duration);
        }

        private void OnSkill2AutoAttackStarted(int slot, CombatEntity target)
        {
            if (slot != 1 || target == null || _dead || _presentation == null)
                return;

            _activeSkillSlot = 1;
            _skill2AutoAttacking = true;

            if (_motor != null)
            {
                var direction = target.transform.position - transform.position;
                direction.y = 0f;
                _motor.SetPlanarFacing(direction);
            }

            // AutoAttackStarted is a target-state transition, not a per-projectile event.
            // Keep Skill_2_Loop looping for as long as a valid attack target exists.
            if (Time.time >= _actionLockUntil)
                EnsureSkillLoop("Skill_2_Loop");
        }

        private void OnSkill2AutoShotStarted(int slot, CombatEntity target)
        {
            if (slot != 1 || target == null || _motor == null)
                return;

            // The loop animation is owned by the attack-state transition, but facing must follow
            // every resolved auto-shot because the nearest target can change without an idle gap.
            var direction = target.transform.position - transform.position;
            direction.y = 0f;
            _motor.SetPlanarFacing(direction);
        }

        private void OnSkill2AutoAttackStopped(int slot)
        {
            if (slot != 1)
                return;

            _skill2AutoAttacking = false;
            if (_activeSkillSlot == 1)
                _skillStateAnimation = string.Empty;
        }

        private void EnsureSkillLoop(string animation)
        {
            if (_presentation == null ||
                string.IsNullOrWhiteSpace(animation) ||
                string.Equals(_skillStateAnimation, animation, System.StringComparison.OrdinalIgnoreCase))
                return;

            _skillStateAnimation = animation;
            _presentation.PlayNamedAnimation(
                animation,
                _motor != null ? _motor.FacingSign : 1,
                true);
        }

        private float ResolveDuration(string animation, float fallback)
        {
            return _presentation != null && _presentation.TryGetAnimationDuration(animation, out var duration)
                ? Mathf.Max(0.01f, duration)
                : Mathf.Max(0.01f, fallback);
        }

        private void StopLocomotion()
        {
            _retarget?.SetMoving(false);
            _presentation?.SetExternalLocomotionActive(false);
        }

        private void OnDied()
        {
            _dead = true;
            _activeSkillSlot = 0;
            _skillStateAnimation = string.Empty;
            _skill2AutoAttacking = false;
            _billboard?.ResetDirectionalCue();
            _presentation?.PlayDie();
        }

        public void ResetForNewRun()
        {
            _dead = false;
            _activeSkillSlot = 0;
            _skillStateAnimation = string.Empty;
            _skill2AutoAttacking = false;
            _actionLockUntil = 0f;
            _endLockUntil = 0f;
            _presentation?.SetExternalLocomotionActive(false);
            _presentation?.SetLocomotion(false, _motor != null ? _motor.FacingSign : 1);
        }
    }
}
