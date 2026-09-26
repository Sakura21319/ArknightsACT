using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Developer/runtime shortcuts for authored build_char_* base-motion actions.
    /// The hidden BaseMotion skeleton owns these clips and SpineBoneMotionRetarget2D
    /// transfers the pose to the visible combat skeleton.
    ///
    /// F5 = Interact (one shot)
    /// F6 = Sit (toggle loop)
    /// F7 = Sleep (toggle loop)
    /// F9 = Special (one shot)
    /// Relax is automatic: after at least 3 seconds standing idle it triggers at a randomized time.
    /// Movement/combat immediately cancels the base-motion action.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpineBoneMotionRetarget2D))]
    public sealed class BaseMotionActionShortcutController : MonoBehaviour
    {
        private SpineBoneMotionRetarget2D _retarget;
        private PlayerMotor25D _motor;
        private PlayerAttackController _attack;
        private PlayerDashController _dash;
        private PlayerSkillController _skills;
        private bool _warnedMissingInteract;
        private bool _warnedMissingSit;
        private bool _warnedMissingSleep;
        private bool _warnedMissingSpecial;
        private bool _warnedMissingRelax;
        private bool _idleRelaxScheduled;
        private float _nextIdleRelaxAt;

        private const float IdleRelaxMinimumSeconds = 3f;
        private const float IdleRelaxRandomWindowSeconds = 2f;

        private void Awake()
        {
            _retarget = GetComponent<SpineBoneMotionRetarget2D>();
            _motor = GetComponent<PlayerMotor25D>();
            _attack = GetComponent<PlayerAttackController>();
            _dash = GetComponent<PlayerDashController>();
            _skills = GetComponent<PlayerSkillController>();
            ResetIdleRelaxTimer();
        }

        private void Update()
        {
            if (_retarget == null)
                return;

            var gameplayBusy = IsGameplayBusy();
            if (_retarget.IsPlayingMotionAction && gameplayBusy)
                _retarget.StopMotionAction();

            if (Time.timeScale <= 0f ||
                ArknightsACT.Gameplay.Input.GameplayInputBlocker.IsBlocked ||
                gameplayBusy)
            {
                ResetIdleRelaxTimer();
                return;
            }

            var keyboard = Keyboard.current;
            if (_retarget.IsPlayingMotionAction)
            {
                // Looping Sit/Sleep must still be toggleable with the same shortcut.
                if (keyboard != null &&
                    keyboard.f6Key.wasPressedThisFrame &&
                    string.Equals(_retarget.ActiveMotionAction, "Sit", System.StringComparison.OrdinalIgnoreCase))
                {
                    ToggleLoop("Sit", ref _warnedMissingSit);
                }
                else if (keyboard != null &&
                         keyboard.f7Key.wasPressedThisFrame &&
                         string.Equals(_retarget.ActiveMotionAction, "Sleep", System.StringComparison.OrdinalIgnoreCase))
                {
                    ToggleLoop("Sleep", ref _warnedMissingSleep);
                }

                ResetIdleRelaxTimer();
                return;
            }

            if (keyboard == null)
            {
                TickIdleRelax();
                return;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                PlayOneShot("Interact", ref _warnedMissingInteract);
                ResetIdleRelaxTimer();
                return;
            }
            if (keyboard.f6Key.wasPressedThisFrame)
            {
                ToggleLoop("Sit", ref _warnedMissingSit);
                ResetIdleRelaxTimer();
                return;
            }
            if (keyboard.f7Key.wasPressedThisFrame)
            {
                ToggleLoop("Sleep", ref _warnedMissingSleep);
                ResetIdleRelaxTimer();
                return;
            }
            if (keyboard.f9Key.wasPressedThisFrame)
            {
                PlayOneShot("Special", ref _warnedMissingSpecial);
                ResetIdleRelaxTimer();
                return;
            }

            TickIdleRelax();
        }

        private void TickIdleRelax()
        {
            if (!_idleRelaxScheduled)
            {
                _nextIdleRelaxAt = Time.time + IdleRelaxMinimumSeconds +
                                   Random.Range(0f, IdleRelaxRandomWindowSeconds);
                _idleRelaxScheduled = true;
                return;
            }

            if (Time.time < _nextIdleRelaxAt)
                return;

            _idleRelaxScheduled = false;
            if (!_retarget.HasSourceAnimation("Relax"))
            {
                WarnMissing("Relax", ref _warnedMissingRelax);
                return;
            }

            _retarget.PlayMotionAction("Relax", false);
        }

        private void ResetIdleRelaxTimer()
        {
            _idleRelaxScheduled = false;
            _nextIdleRelaxAt = 0f;
        }

        private bool IsGameplayBusy()
        {
            if (_motor != null && _motor.IsMoving)
                return true;
            if (_attack != null && _attack.IsAttacking)
                return true;
            if (_dash != null && _dash.IsDashing)
                return true;
            if (_skills == null)
                return false;

            return _skills.IsCasting ||
                   PlayerSkillLifecycleUtility.IsActive(_skills.Skill1) ||
                   PlayerSkillLifecycleUtility.IsActive(_skills.Skill2);
        }

        private void PlayOneShot(string animation, ref bool warnedMissing)
        {
            if (!_retarget.HasSourceAnimation(animation))
            {
                WarnMissing(animation, ref warnedMissing);
                return;
            }

            _retarget.PlayMotionAction(animation, false);
        }

        private void ToggleLoop(string animation, ref bool warnedMissing)
        {
            if (_retarget.IsPlayingMotionAction &&
                string.Equals(
                    _retarget.ActiveMotionAction,
                    animation,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                _retarget.StopMotionAction();
                return;
            }

            if (!_retarget.HasSourceAnimation(animation))
            {
                WarnMissing(animation, ref warnedMissing);
                return;
            }

            _retarget.PlayMotionAction(animation, true);
        }

        private void WarnMissing(string animation, ref bool warned)
        {
            if (warned)
                return;

            warned = true;
            Debug.LogWarning(
                $"[ArknightsACT/BaseMotion] '{animation}' is not present on the current skin's build skeleton.",
                this);
        }
    }
}
