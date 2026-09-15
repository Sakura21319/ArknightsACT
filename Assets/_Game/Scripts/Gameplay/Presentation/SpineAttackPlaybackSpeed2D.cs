using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Bridges attack speed between gameplay and Spine presentation.
    /// One complete visible Attack_Loop equals one authoritative gameplay attack.
    /// </summary>
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class SpineAttackPlaybackSpeed2D : MonoBehaviour, IAttackTimingProvider
    {
        [SerializeField, Range(1f, 4f)] private float attackPlaybackSpeed = 1.6f;
        [SerializeField, Range(0.1f, 0.9f)] private float impactNormalizedTime = 0.58f;
        [SerializeField, Min(0.05f)] private float chainGraceSeconds = 0.20f;
        [SerializeField, Min(0.05f)] private float minimumCycleSeconds = 0.14f;
        [SerializeField, Min(0.05f)] private float maximumCycleSeconds = 0.70f;

        private PlayerAttackController _attack;
        private PlayerSkillController _skill;
        private Component _skeletonAnimation;
        private object _animationState;
        private PropertyInfo _timeScaleProperty;
        private FieldInfo _timeScaleField;
        private float _attackLoopDuration;
        private bool _boosted;
        private float _boostUntil;
        private bool _bindAttempted;

        public float AttackPlaybackSpeed => attackPlaybackSpeed;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _skill = GetComponent<PlayerSkillController>();
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
            if (_skill != null)
                _skill.SkillCastSucceeded += OnSkillCast;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;
            if (_skill != null)
                _skill.SkillCastSucceeded -= OnSkillCast;
            RestoreNormalSpeed();
        }

        private void Update()
        {
            if (_boosted && Time.time >= _boostUntil)
                RestoreNormalSpeed();
        }

        public bool TryGetBasicAttackTiming(out float impactSeconds, out float cycleSeconds)
        {
            impactSeconds = 0f;
            cycleSeconds = 0f;

            if (!TryBind() || _attackLoopDuration <= 0.001f)
                return false;

            cycleSeconds = Mathf.Clamp(
                _attackLoopDuration / Mathf.Max(0.01f, attackPlaybackSpeed),
                minimumCycleSeconds,
                maximumCycleSeconds);
            impactSeconds = Mathf.Clamp(cycleSeconds * impactNormalizedTime, 0.01f, cycleSeconds - 0.01f);
            return true;
        }

        private void OnAttackStarted(int comboIndex)
        {
            if (!TryBind())
                return;

            SetTimeScale(attackPlaybackSpeed);
            _boosted = true;

            var hold = chainGraceSeconds;
            if (TryGetBasicAttackTiming(out var ignoredImpact, out var cycle))
                hold = Mathf.Max(hold, cycle + 0.05f);
            _boostUntil = Time.time + hold;
        }

        private void OnSkillCast()
        {
            RestoreNormalSpeed();
        }

        private bool TryBind()
        {
            if (_animationState != null && (_timeScaleProperty != null || _timeScaleField != null))
                return true;
            if (_bindAttempted)
                return false;

            _bindAttempted = true;
            var presentations = GetComponentsInChildren<SpineCharacterPresentation2D>(true);
            var combatPresentation = presentations.FirstOrDefault(item => item != null && item.enabled);
            if (combatPresentation == null)
                return false;

            _skeletonAnimation = combatPresentation.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().FullName == "Spine.Unity.SkeletonAnimation");
            if (_skeletonAnimation == null)
                return false;

            var skeletonType = _skeletonAnimation.GetType();
            var stateProperty = skeletonType.GetProperty("AnimationState", BindingFlags.Instance | BindingFlags.Public);
            _animationState = stateProperty?.GetValue(_skeletonAnimation);
            if (_animationState == null)
                return false;

            var stateType = _animationState.GetType();
            _timeScaleProperty = stateType.GetProperty("TimeScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_timeScaleProperty == null || _timeScaleProperty.PropertyType != typeof(float) || !_timeScaleProperty.CanWrite)
                _timeScaleProperty = null;

            _timeScaleField = stateType.GetField("timeScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_timeScaleField == null || _timeScaleField.FieldType != typeof(float) || _timeScaleField.IsInitOnly)
                _timeScaleField = null;

            _attackLoopDuration = DiscoverAnimationDuration(skeletonType, "Attack_Loop");
            if (_attackLoopDuration <= 0.001f)
                _attackLoopDuration = DiscoverAnimationDuration(skeletonType, "Attack");

            if (_attackLoopDuration > 0.001f)
            {
                Debug.Log(
                    $"[ArknightsACT/AttackTiming] Spine cycle={_attackLoopDuration:0.###}s raw, " +
                    $"speed={attackPlaybackSpeed:0.##}x, impact={impactNormalizedTime:P0}.",
                    this);
            }

            return _timeScaleProperty != null || _timeScaleField != null;
        }

        private float DiscoverAnimationDuration(Type skeletonAnimationType, string wantedName)
        {
            try
            {
                var skeleton = skeletonAnimationType.GetProperty("Skeleton", BindingFlags.Instance | BindingFlags.Public)?.GetValue(_skeletonAnimation);
                var data = skeleton?.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(skeleton);
                var animations = data?.GetType().GetProperty("Animations", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data) as IEnumerable;
                if (animations == null)
                    return 0f;

                foreach (var animation in animations)
                {
                    if (animation == null)
                        continue;
                    var type = animation.GetType();
                    var name = type.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(animation) as string;
                    if (!string.Equals(name, wantedName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var rawDuration = type.GetProperty("Duration", BindingFlags.Instance | BindingFlags.Public)?.GetValue(animation);
                    return rawDuration is float duration ? duration : 0f;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArknightsACT/AttackTiming] Failed to read Spine animation duration: " + exception.GetBaseException().Message, this);
            }
            return 0f;
        }

        private void SetTimeScale(float value)
        {
            if (_animationState == null)
                return;

            try
            {
                if (_timeScaleProperty != null)
                    _timeScaleProperty.SetValue(_animationState, value);
                else
                    _timeScaleField?.SetValue(_animationState, value);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArknightsACT/Spine] Failed to set attack playback speed: " + exception.GetBaseException().Message, this);
            }
        }

        private void RestoreNormalSpeed()
        {
            if (!_boosted)
                return;

            SetTimeScale(1f);
            _boosted = false;
            _boostUntil = 0f;
        }
    }
}
