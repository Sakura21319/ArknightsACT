using System;
using System.Linq;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    /// <summary>
    /// Schwarz-specific presentation driver.
    /// S3 is treated as one continuous authored state:
    /// Skill_Begin -> Skill_Loop (or another skill-specific loop/attack clip) -> Skill_End.
    /// Individual S3 shots never fall back to the normal Attack_Loop.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor25D), typeof(PlayerAttackController), typeof(PlayerSkillController))]
    public sealed class SchwarzPresentationDriver25D : MonoBehaviour, IPlayerRunResettable
    {
        private PlayerMotor25D _motor;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private SchwarzSkill2 _skill3;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;
        private SpineBoneMotionRetarget2D _retarget;
        private BillboardPresentation25D _billboard;

        private float _skillVisualLockUntil;
        private string _s3LoopAnimation;
        private string _s3ShotAnimation;
        private string _s3EndAnimation;
        private bool _s3AnimationsResolved;
        private bool _s3LoopApplied;
        private bool _dead;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor25D>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _skill3 = GetComponent<SchwarzSkill2>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
            _retarget = GetComponent<SpineBoneMotionRetarget2D>();
            _billboard = GetComponentInChildren<BillboardPresentation25D>(true);
            ResolveS3Animations();
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
            if (_skills != null)
            {
                _skills.SkillCastSucceeded += OnSkillCast;
                _skills.SkillCancelled += OnSkillCancelled;
            }
            if (_skill3 != null)
            {
                _skill3.BuffStarted += OnSkill3BuffStarted;
                _skill3.BuffEnded += OnSkill3BuffEnded;
            }
            if (_entity?.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;
            if (_skills != null)
            {
                _skills.SkillCastSucceeded -= OnSkillCast;
                _skills.SkillCancelled -= OnSkillCancelled;
            }
            if (_skill3 != null)
            {
                _skill3.BuffStarted -= OnSkill3BuffStarted;
                _skill3.BuffEnded -= OnSkill3BuffEnded;
            }
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

            if (_skill3 != null && _skill3.IsBuffActive)
            {
                StopLocomotion();
                _presentation.SetFacingImmediate(facing);
                _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);

                if ((_skills != null && _skills.IsCasting) || Time.time < _skillVisualLockUntil)
                    return;

                EnsureS3Loop();
                return;
            }

            var actionActive = (_attack != null && _attack.IsAttacking) ||
                               (_skills != null && _skills.IsCasting) ||
                               Time.time < _skillVisualLockUntil;
            if (actionActive)
            {
                StopLocomotion();
                _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
                return;
            }

            var moving = _motor.IsMoving;
            var useRetarget = moving && _retarget != null && _retarget.IsCompatible;
            _retarget?.SetMoving(useRetarget);
            _presentation.SetExternalLocomotionActive(useRetarget);
            _presentation.SetLocomotion(moving, facing);
            _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, moving ? 0.65f : 0.25f);
        }

        private void OnAttackStarted(int comboIndex)
        {
            if (_dead)
                return;

            StopLocomotion();
            var facing = _motor != null ? _motor.FacingSign : 1;

            if (_skill3 != null && _skill3.IsBuffActive)
            {
                ResolveS3Animations();

                // Use only an authored skill-shot animation here. Never fall back to the normal
                // Attack_Loop, otherwise Schwarz visibly stands up between aimed S3 shots.
                if (_presentation != null && !string.IsNullOrWhiteSpace(_s3ShotAnimation))
                {
                    _presentation.PlayNamedAnimation(_s3ShotAnimation, facing, false, 0.22f);
                    _skillVisualLockUntil = Time.time + 0.22f;
                    _s3LoopApplied = false;
                }
                else if (_presentation != null)
                {
                    // Final fallback: the generated Schwarz presentation always binds Skill_Begin.
                    // It is permitted here only because this path is reached from a real aimed shot.
                    _presentation.PlaySkill(facing);
                    _skillVisualLockUntil = Time.time + 0.30f;
                    _s3LoopApplied = false;
                }

                if (_motor != null)
                    _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
                return;
            }

            _presentation?.PlayAttack(comboIndex, facing);
            if (_motor != null)
                _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
        }

        private void OnSkillCast(int slot)
        {
            if (_dead)
                return;

            // Gameplay slot 1 is Schwarz S2. The authored character has no dedicated activation
            // action for this buff, so never reuse S3's Skill_Begin here.
            if (slot == 1)
            {
                _skillVisualLockUntil = 0f;
                RestoreLocomotionPose();
                return;
            }

            // Entering S3 must not play a firing animation. The sniper controller owns the
            // actual shot input; only a dedicated authored stance is allowed to play here.
            StopLocomotion();
            _s3LoopApplied = false;
            _skillVisualLockUntil = 0f;
            ResolveS3Animations();
            EnsureS3Loop();

            var facing = _motor != null ? _motor.FacingSign : 1;
            if (_motor != null)
                _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
        }

        private void OnSkillCancelled(int slot)
        {
            // S3 BuffEnded is raised synchronously before SkillCancelled and owns its end pose.
            if (slot == 2)
                return;

            _skillVisualLockUntil = 0f;
            RestoreLocomotionPose();
        }

        private void OnSkill3BuffStarted()
        {
            if (_dead)
                return;

            ResolveS3Animations();
            StopLocomotion();
            _s3LoopApplied = false;
            _skillVisualLockUntil = 0f;
            EnsureS3Loop();
        }

        private void OnSkill3BuffEnded()
        {
            _s3LoopApplied = false;
            ResolveS3Animations();

            var facing = _motor != null ? _motor.FacingSign : 1;
            if (_presentation != null && !string.IsNullOrWhiteSpace(_s3EndAnimation))
            {
                _presentation.PlayNamedAnimation(_s3EndAnimation, facing, false, 0.30f);
                _skillVisualLockUntil = Time.time + 0.30f;
                return;
            }

            _skillVisualLockUntil = 0f;
            RestoreLocomotionPose();
        }

        private void EnsureS3Loop()
        {
            if (_s3LoopApplied || _presentation == null)
                return;

            ResolveS3Animations();
            _s3LoopApplied = true;

            if (string.IsNullOrWhiteSpace(_s3LoopAnimation))
                return;

            var facing = _motor != null ? _motor.FacingSign : 1;
            _presentation.PlayNamedAnimation(_s3LoopAnimation, facing, true);
        }

        private void ResolveS3Animations()
        {
            if (_s3AnimationsResolved || _presentation == null)
                return;

            _presentation.TryBind();
            var names = _presentation.AvailableAnimations;
            if (names == null || names.Count == 0)
                return;

            _s3LoopAnimation =
                FindExact(
                    names,
                    "Skill_Aim",
                    "Skill_3_Aim",
                    "Skill_03_Aim",
                    "Skill_Aim_Loop",
                    "Skill_Ready",
                    "Skill_Stance",
                    "Skill_Idle") ??
                names.FirstOrDefault(name =>
                {
                    var normalized = Normalize(name);
                    if (!normalized.Contains("skill") ||
                        normalized.Contains("attack") ||
                        normalized.Contains("atk") ||
                        normalized.Contains("shoot") ||
                        normalized.Contains("shot") ||
                        normalized.Contains("fire"))
                        return false;

                    return normalized.Contains("aim") ||
                           normalized.Contains("ready") ||
                           normalized.Contains("stance") ||
                           normalized.Contains("idle");
                });

            _s3ShotAnimation =
                FindExact(
                    names,
                    "Skill_Attack",
                    "Skill_3_Attack",
                    "Skill_03_Attack",
                    "Skill3_Attack",
                    "Skill_Shoot",
                    "Skill_3_Shoot",
                    "Skill_03_Shoot",
                    "Skill_Shot",
                    "Skill_Loop",
                    "Skill_3_Loop",
                    "Skill_03_Loop",
                    "Skill3_Loop") ??
                names.FirstOrDefault(name =>
                {
                    var normalized = Normalize(name);
                    return normalized.Contains("skill") &&
                           !normalized.Contains("begin") &&
                           !normalized.Contains("end") &&
                           !normalized.Contains("loop") &&
                           (normalized.Contains("attack") ||
                            normalized.Contains("atk") ||
                            normalized.Contains("shoot") ||
                            normalized.Contains("shot"));
                });

            _s3EndAnimation =
                FindExact(names, "Skill_End", "Skill_3_End", "Skill_03_End", "Skill3_End") ??
                names.FirstOrDefault(name =>
                {
                    var normalized = Normalize(name);
                    return normalized.Contains("skill") && normalized.Contains("end");
                });

            _s3AnimationsResolved = true;
            Debug.Log(
                $"[ArknightsACT/Schwarz] S3 authored chain: Entry=<none>, " +
                $"Stance='{_s3LoopAnimation ?? "<none>"}', Shot='{_s3ShotAnimation ?? "<none>"}', " +
                $"End='{_s3EndAnimation ?? "<none>"}'. " +
                $"Available=[{string.Join(", ", names)}]",
                this);
        }

        private static string FindExact(System.Collections.Generic.IReadOnlyList<string> names, params string[] candidates)
        {
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                var exact = names.FirstOrDefault(name =>
                    string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact))
                    return exact;
            }
            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private void RestoreLocomotionPose()
        {
            if (_dead || _presentation == null)
                return;

            _retarget?.SetMoving(false);
            _presentation.SetExternalLocomotionActive(false);
            var facing = _motor != null ? _motor.FacingSign : 1;
            _presentation.SetLocomotion(_motor != null && _motor.IsMoving, facing);
            _billboard?.ResetDirectionalCue();
        }

        private void OnDied()
        {
            _dead = true;
            _s3LoopApplied = false;
            StopLocomotion();
            _billboard?.ResetDirectionalCue();
            _presentation?.PlayDie();
        }

        public void ResetForNewRun()
        {
            _dead = false;
            _s3LoopApplied = false;
            _skillVisualLockUntil = 0f;
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
