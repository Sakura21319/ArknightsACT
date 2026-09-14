using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Runtime-facing animation adapter for a Spine SkeletonAnimation component.
    /// This class intentionally uses reflection so Game.Gameplay does not take a hard
    /// compile-time dependency on a particular Spine runtime fork/version.
    /// </summary>
    public sealed class SpineCharacterPresentation2D : MonoBehaviour
    {
        [Header("Resolved animation names")]
        [SerializeField] private string idleAnimation = "Idle";
        [SerializeField] private string moveAnimation = "Move";
        [SerializeField] private string[] attackAnimations = { "Attack" };
        [SerializeField] private string skillAnimation = "Skill";
        [SerializeField] private string hitAnimation = string.Empty;
        [SerializeField] private string dieAnimation = "Die";

        [Header("One-shot locks")]
        [SerializeField, Min(0f)] private float attackLockSeconds = 0.28f;
        [SerializeField, Min(0f)] private float skillLockSeconds = 0.65f;
        [SerializeField, Min(0f)] private float hitLockSeconds = 0.16f;

        [Header("Visual transform")]
        [SerializeField] private Transform visualRoot;

        private Component _skeletonAnimation;
        private object _animationState;
        private MethodInfo _setAnimationMethod;
        private Vector3 _baseScale = Vector3.one;
        private float _lockedUntil;
        private string _currentLoop;
        private bool _bindingAttempted;
        private bool _warned;

        public bool IsBound => _skeletonAnimation != null && _animationState != null && _setAnimationMethod != null;

        private void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;

            _baseScale = visualRoot.localScale;
            TryBind();
        }

        public void Configure(
            string idle,
            string move,
            string[] attacks,
            string skill,
            string hit,
            string die)
        {
            idleAnimation = idle ?? string.Empty;
            moveAnimation = move ?? string.Empty;
            attackAnimations = attacks != null && attacks.Length > 0 ? attacks : new[] { "Attack" };
            skillAnimation = skill ?? string.Empty;
            hitAnimation = hit ?? string.Empty;
            dieAnimation = die ?? string.Empty;
        }

        public void SetVisualRoot(Transform root)
        {
            visualRoot = root != null ? root : transform;
            _baseScale = visualRoot.localScale;
        }

        public bool TryBind()
        {
            if (IsBound)
                return true;

            _bindingAttempted = true;
            var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            _skeletonAnimation = behaviours.FirstOrDefault(x => x != null && x.GetType().FullName == "Spine.Unity.SkeletonAnimation");
            if (_skeletonAnimation == null)
                return false;

            var type = _skeletonAnimation.GetType();
            var initialize = type.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);
            try
            {
                initialize?.Invoke(_skeletonAnimation, new object[] { false });
            }
            catch (Exception exception)
            {
                WarnOnce("Failed to initialize Spine presentation: " + exception.GetBaseException().Message);
            }

            var stateProperty = type.GetProperty("AnimationState", BindingFlags.Instance | BindingFlags.Public);
            _animationState = stateProperty?.GetValue(_skeletonAnimation);
            if (_animationState == null)
                return false;

            _setAnimationMethod = _animationState.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "SetAnimation") return false;
                    var args = method.GetParameters();
                    return args.Length == 3 &&
                           args[0].ParameterType == typeof(int) &&
                           args[1].ParameterType == typeof(string) &&
                           args[2].ParameterType == typeof(bool);
                });

            if (_setAnimationMethod == null)
            {
                WarnOnce("Spine AnimationState.SetAnimation(int,string,bool) was not found.");
                return false;
            }

            return true;
        }

        public void SetLocomotion(bool moving, int facing)
        {
            SetFacing(facing);
            if (Time.time < _lockedUntil)
                return;

            var next = moving && !string.IsNullOrWhiteSpace(moveAnimation)
                ? moveAnimation
                : idleAnimation;

            PlayLoop(next);
        }

        public void PlayAttack(int comboIndex, int facing)
        {
            SetFacing(facing);
            if (attackAnimations == null || attackAnimations.Length == 0)
                return;

            var index = Mathf.Abs(comboIndex) % attackAnimations.Length;
            PlayOneShot(attackAnimations[index], attackLockSeconds);
        }

        public void PlaySkill(int facing)
        {
            SetFacing(facing);
            PlayOneShot(skillAnimation, skillLockSeconds);
        }

        public void PlayHit()
        {
            if (!string.IsNullOrWhiteSpace(hitAnimation))
                PlayOneShot(hitAnimation, hitLockSeconds);
        }

        public void PlayDie()
        {
            _lockedUntil = float.PositiveInfinity;
            Play(dieAnimation, false, force: true);
        }

        private void PlayLoop(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation) || _currentLoop == animation)
                return;

            if (Play(animation, true, force: false))
                _currentLoop = animation;
        }

        private void PlayOneShot(string animation, float lockSeconds)
        {
            if (string.IsNullOrWhiteSpace(animation))
                return;

            _currentLoop = string.Empty;
            _lockedUntil = Mathf.Max(_lockedUntil, Time.time + Mathf.Max(0f, lockSeconds));
            Play(animation, false, force: true);
        }

        private bool Play(string animation, bool loop, bool force)
        {
            if (string.IsNullOrWhiteSpace(animation))
                return false;

            if (!IsBound && !TryBind())
            {
                if (_bindingAttempted)
                    WarnOnce("No compatible Spine.Unity.SkeletonAnimation was found under " + name + ". Placeholder presentation will remain active.");
                return false;
            }

            try
            {
                _setAnimationMethod.Invoke(_animationState, new object[] { 0, animation, loop });
                return true;
            }
            catch (TargetInvocationException exception)
            {
                WarnOnce($"Failed to play Spine animation '{animation}': {exception.GetBaseException().Message}");
                return false;
            }
            catch (Exception exception)
            {
                WarnOnce($"Failed to play Spine animation '{animation}': {exception.Message}");
                return false;
            }
        }

        private void SetFacing(int facing)
        {
            if (visualRoot == null)
                return;

            var sign = facing < 0 ? -1f : 1f;
            var x = Mathf.Abs(_baseScale.x) * sign;
            visualRoot.localScale = new Vector3(x, _baseScale.y, _baseScale.z);
        }

        private void WarnOnce(string message)
        {
            if (_warned)
                return;

            _warned = true;
            Debug.LogWarning("[ArknightsACT/Spine] " + message, this);
        }
    }
}
