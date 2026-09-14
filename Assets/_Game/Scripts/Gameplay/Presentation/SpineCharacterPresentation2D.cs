using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Runtime-facing animation adapter for a Spine SkeletonAnimation component.
    /// Reflection keeps gameplay independent from a specific Spine runtime fork/version.
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
        [SerializeField, Min(0f)] private float attackLockSeconds = 0.24f;
        [SerializeField, Min(0f)] private float skillLockSeconds = 0.55f;
        [SerializeField, Min(0f)] private float hitLockSeconds = 0.14f;

        [Header("Combat locomotion fallback")]
        [SerializeField, Min(0f)] private float proceduralMoveBob = 0.035f;
        [SerializeField, Min(0f)] private float proceduralMoveTiltDegrees = 1.5f;
        [SerializeField, Min(0.1f)] private float proceduralMoveFrequency = 10f;

        [Header("Visual transform")]
        [SerializeField] private Transform visualRoot;

        private readonly List<string> _availableAnimations = new();
        private readonly Dictionary<string, float> _animationDurations = new(StringComparer.OrdinalIgnoreCase);
        private Component _skeletonAnimation;
        private object _animationState;
        private MethodInfo _setAnimationMethod;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _baseLocalPosition = Vector3.zero;
        private Quaternion _baseLocalRotation = Quaternion.identity;
        private float _lockedUntil;
        private string _currentLoop;
        private bool _bindingAttempted;
        private bool _warned;
        private bool _bindingLogged;
        private bool _hasDedicatedMoveAnimation;

        public bool IsBound => _skeletonAnimation != null && _animationState != null && _setAnimationMethod != null;
        public IReadOnlyList<string> AvailableAnimations => _availableAnimations;

        private void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;

            CaptureVisualBasePose();
            TryBind();
        }

        private void Start()
        {
            if (TryBind() && !string.IsNullOrWhiteSpace(idleAnimation))
                PlayLoop(idleAnimation);
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
            CaptureVisualBasePose();
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
            TryInitialize(type, false);

            var stateProperty = type.GetProperty("AnimationState", BindingFlags.Instance | BindingFlags.Public);
            _animationState = stateProperty?.GetValue(_skeletonAnimation);
            if (_animationState == null)
            {
                TryInitialize(type, true);
                _animationState = stateProperty?.GetValue(_skeletonAnimation);
            }

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

            DiscoverAnimations(type);
            ResolveConfiguredNames();
            LogBindingOnce();
            return true;
        }

        public void SetLocomotion(bool moving, int facing)
        {
            SetFacing(facing);
            if (Time.time < _lockedUntil)
                return;

            if (!moving)
            {
                ResetProceduralLocomotion();
                PlayLoop(idleAnimation);
                return;
            }

            if (_hasDedicatedMoveAnimation)
            {
                ResetProceduralLocomotion();
                PlayLoop(moveAnimation);
                return;
            }

            // Combat operator skeletons such as Texas do not ship a weapon-preserving Move clip.
            // Keep the combat Idle skeleton (and its weapons) and add a subtle world-motion cue
            // rather than swapping to the Base/Dorm model where the weapon disappears.
            PlayLoop(idleAnimation);
            ApplyProceduralLocomotion();
        }

        public void PlayAttack(int comboIndex, int facing)
        {
            SetFacing(facing);
            ResetProceduralLocomotion();
            if (attackAnimations == null || attackAnimations.Length == 0)
                return;

            var index = attackAnimations.Length == 1
                ? 0
                : Mathf.Abs(comboIndex) % attackAnimations.Length;
            var animation = attackAnimations[index];
            PlayOneShot(animation, Mathf.Max(attackLockSeconds, DurationOrZero(animation) * 0.72f));
        }

        public void PlaySkill(int facing)
        {
            SetFacing(facing);
            ResetProceduralLocomotion();
            PlayOneShot(skillAnimation, Mathf.Max(skillLockSeconds, DurationOrZero(skillAnimation) * 0.72f));
        }

        public void PlayHit()
        {
            ResetProceduralLocomotion();
            if (!string.IsNullOrWhiteSpace(hitAnimation))
                PlayOneShot(hitAnimation, Mathf.Max(hitLockSeconds, DurationOrZero(hitAnimation) * 0.55f));
        }

        public void PlayDie()
        {
            ResetProceduralLocomotion();
            _lockedUntil = float.PositiveInfinity;
            Play(dieAnimation, false, true);
        }

        private void PlayLoop(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation) || string.Equals(_currentLoop, animation, StringComparison.OrdinalIgnoreCase))
                return;

            if (Play(animation, true, false))
                _currentLoop = animation;
        }

        private void PlayOneShot(string animation, float lockSeconds)
        {
            if (string.IsNullOrWhiteSpace(animation))
                return;

            _currentLoop = string.Empty;
            _lockedUntil = Mathf.Max(_lockedUntil, Time.time + Mathf.Max(0f, lockSeconds));
            Play(animation, false, true);
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

            var resolved = ResolveExact(animation);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                WarnOnce($"Animation '{animation}' is not present on {name}. Available: {string.Join(", ", _availableAnimations)}");
                return false;
            }

            try
            {
                _setAnimationMethod.Invoke(_animationState, new object[] { 0, resolved, loop });
                return true;
            }
            catch (TargetInvocationException exception)
            {
                WarnOnce($"Failed to play Spine animation '{resolved}': {exception.GetBaseException().Message}");
                return false;
            }
            catch (Exception exception)
            {
                WarnOnce($"Failed to play Spine animation '{resolved}': {exception.Message}");
                return false;
            }
        }

        private void TryInitialize(Type type, bool overwrite)
        {
            var initialize = type.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);
            try
            {
                initialize?.Invoke(_skeletonAnimation, new object[] { overwrite });
            }
            catch (Exception exception)
            {
                WarnOnce("Failed to initialize Spine presentation: " + exception.GetBaseException().Message);
            }
        }

        private void DiscoverAnimations(Type skeletonAnimationType)
        {
            _availableAnimations.Clear();
            _animationDurations.Clear();

            object skeleton = skeletonAnimationType.GetProperty("Skeleton", BindingFlags.Instance | BindingFlags.Public)?.GetValue(_skeletonAnimation);
            object skeletonData = skeleton?.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(skeleton);

            if (skeletonData == null)
            {
                var dataAsset = GetMemberValue(_skeletonAnimation, "SkeletonDataAsset", "skeletonDataAsset");
                var getSkeletonData = dataAsset?.GetType().GetMethod("GetSkeletonData", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);
                skeletonData = getSkeletonData?.Invoke(dataAsset, new object[] { false });
            }

            var animations = skeletonData?.GetType().GetProperty("Animations", BindingFlags.Instance | BindingFlags.Public)?.GetValue(skeletonData) as IEnumerable;
            if (animations == null)
                return;

            foreach (var animation in animations)
            {
                if (animation == null)
                    continue;

                var name = animation.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(animation) as string;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!_availableAnimations.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                    _availableAnimations.Add(name);

                var durationValue = animation.GetType().GetProperty("Duration", BindingFlags.Instance | BindingFlags.Public)?.GetValue(animation);
                if (durationValue is float duration)
                    _animationDurations[name] = duration;
            }

            _availableAnimations.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private void ResolveConfiguredNames()
        {
            if (_availableAnimations.Count == 0)
                return;

            idleAnimation = ResolveIdle()
                            ?? ResolveExact(idleAnimation)
                            ?? _availableAnimations[0];

            var persistentMove = ResolvePersistentMove();
            _hasDedicatedMoveAnimation = !string.IsNullOrWhiteSpace(persistentMove);
            moveAnimation = _hasDedicatedMoveAnimation ? persistentMove : idleAnimation;

            attackAnimations = ResolveBasicAttacks();
            if (attackAnimations.Length == 0)
                attackAnimations = new[] { idleAnimation };

            skillAnimation = ResolveExact("Skill")
                             ?? ResolveRole(skillAnimation, "skill", "ability", "special")
                             ?? attackAnimations[0];
            hitAnimation = ResolveExact("Hit")
                           ?? ResolveRole(hitAnimation, "hit", "hurt", "stun", "damage")
                           ?? string.Empty;
            dieAnimation = ResolveExact("Die")
                           ?? ResolveRole(dieAnimation, "die", "death", "dead")
                           ?? idleAnimation;
        }

        private string ResolveIdle()
        {
            return ResolveExact("Idle")
                   ?? _availableAnimations.FirstOrDefault(name => StartsWithAny(name, "idle") && IsLoopLike(name))
                   ?? _availableAnimations.FirstOrDefault(name => StartsWithAny(name, "idle", "relax"))
                   ?? ResolveExact("Default")
                   ?? _availableAnimations.FirstOrDefault(name => StartsWithAny(name, "default", "stand"));
        }

        private string ResolvePersistentMove()
        {
            return ResolveExact("Move")
                   ?? ResolveExact("Move_Loop")
                   ?? ResolveExact("Run_Loop")
                   ?? ResolveExact("Walk_Loop")
                   ?? _availableAnimations.FirstOrDefault(name =>
                       StartsWithAny(name, "move", "run", "walk") && IsLoopLike(name))
                   ?? _availableAnimations.FirstOrDefault(name =>
                       StartsWithAny(name, "move", "run", "walk") && !IsTransitionClip(name));
        }

        private string[] ResolveBasicAttacks()
        {
            // Attack_Start / Attack_Loop / Attack_End are one phased attack state in
            // Arknights operator data, not three different combo attacks.
            var loop = ResolveExact("Attack_Loop");
            if (!string.IsNullOrWhiteSpace(loop))
                return new[] { loop };

            var exact = ResolveExact("Attack") ?? ResolveExact("Combat");
            if (!string.IsNullOrWhiteSpace(exact))
                return new[] { exact };

            var configured = attackAnimations?
                .Select(ResolveExact)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !IsTransitionClip(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();
            if (configured.Length > 0)
                return configured;

            return _availableAnimations
                .Where(name => StartsWithAny(name, "attack", "combat", "atk"))
                .Where(name => !IsTransitionClip(name))
                .OrderByDescending(IsLoopLike)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToArray();
        }

        private void LogBindingOnce()
        {
            if (_bindingLogged)
                return;

            _bindingLogged = true;
            Debug.Log(
                $"[ArknightsACT/Spine] Bound {name}: " +
                $"Idle='{idleAnimation}', Move='{moveAnimation}', DedicatedMove={_hasDedicatedMoveAnimation}, " +
                $"Attack=[{string.Join(", ", attackAnimations ?? Array.Empty<string>())}], " +
                $"Skill='{skillAnimation}', Hit='{hitAnimation}', Die='{dieAnimation}'. " +
                $"Available=[{string.Join(", ", _availableAnimations)}]",
                this);
        }

        private string ResolveRole(string configured, params string[] prefixes)
        {
            var exact = ResolveExact(configured);
            if (!string.IsNullOrWhiteSpace(exact))
                return exact;

            return _availableAnimations.FirstOrDefault(name => StartsWithAny(name, prefixes));
        }

        private string ResolveExact(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation))
                return null;

            return _availableAnimations.FirstOrDefault(name => string.Equals(name, animation, StringComparison.OrdinalIgnoreCase));
        }

        private static bool StartsWithAny(string name, params string[] prefixes)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var normalized = name.Trim().Replace('-', '_');
            for (var i = 0; i < prefixes.Length; i++)
            {
                if (normalized.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsTransitionClip(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var normalized = name.Replace('-', '_').ToLowerInvariant();
            return normalized.Contains("_start") || normalized.Contains("_begin") ||
                   normalized.Contains("_end") || normalized.Contains("_finish") ||
                   normalized.Contains("_up") || normalized.Contains("_down");
        }

        private static bool IsLoopLike(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   name.IndexOf("loop", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private float DurationOrZero(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation))
                return 0f;

            return _animationDurations.TryGetValue(animation, out var duration) ? duration : 0f;
        }

        private static object GetMemberValue(object target, string propertyName, string fieldName)
        {
            if (target == null)
                return null;

            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(target);

            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(target);
        }

        private void CaptureVisualBasePose()
        {
            if (visualRoot == null)
                return;

            _baseScale = visualRoot.localScale;
            _baseLocalPosition = visualRoot.localPosition;
            _baseLocalRotation = visualRoot.localRotation;
        }

        private void ApplyProceduralLocomotion()
        {
            if (visualRoot == null)
                return;

            var phase = Time.time * proceduralMoveFrequency;
            var bob = Mathf.Abs(Mathf.Sin(phase)) * proceduralMoveBob;
            var tilt = Mathf.Sin(phase * 0.5f) * proceduralMoveTiltDegrees;
            visualRoot.localPosition = _baseLocalPosition + new Vector3(0f, bob, 0f);
            visualRoot.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, tilt);
        }

        private void ResetProceduralLocomotion()
        {
            if (visualRoot == null)
                return;

            visualRoot.localPosition = _baseLocalPosition;
            visualRoot.localRotation = _baseLocalRotation;
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
