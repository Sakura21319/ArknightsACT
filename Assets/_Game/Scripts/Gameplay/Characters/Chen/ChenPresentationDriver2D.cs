using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Character-specific presentation mapping for Ch'en's authored PRTS clips.
    /// Gameplay does not know Spine clip names; this component owns that translation.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor2D), typeof(PlayerAttackController), typeof(PlayerSkillController))]
    public sealed class ChenPresentationDriver2D : MonoBehaviour
    {
        private const float AttackPlaybackSpeed = 2.0f;
        private const float Combo3PlaybackSpeed = 1.80f;
        private const float Skill1PlaybackSpeed = 1.50f;
        private const float Skill2PlaybackSpeed = 1.55f;

        private readonly Dictionary<string, float> _durations = new(StringComparer.OrdinalIgnoreCase);

        private PlayerMotor2D _motor;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private CombatEntity _entity;
        private Rigidbody2D _body;
        private SpineCharacterPresentation2D _presentation;
        private SpineBoneMotionRetarget2D _retarget;

        private Component _skeletonAnimation;
        private object _animationState;
        private MethodInfo _setAnimation;
        private MethodInfo _addAnimation;
        private float _visualLockUntil;
        private bool _actionVisualWasActive;
        private bool _dead;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor2D>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _entity = GetComponent<CombatEntity>();
            _body = GetComponent<Rigidbody2D>();
            _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
            _retarget = GetComponent<SpineBoneMotionRetarget2D>();
            TryBind();
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCast;
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
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCast;
            if (_entity != null)
            {
                _entity.Damaged -= OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died -= OnDied;
            }
            _retarget?.SetMoving(false);
        }

        private void Update()
        {
            if (_dead || _presentation == null || _motor == null || _body == null)
                return;

            var actionVisualActive = Time.time < _visualLockUntil ||
                                     (_attack != null && _attack.IsAttacking) ||
                                     (_skills != null && _skills.IsCasting);
            if (actionVisualActive)
            {
                _actionVisualWasActive = true;
                _retarget?.SetMoving(false);
                _presentation.SetExternalLocomotionActive(false);
                return;
            }

            if (_actionVisualWasActive)
            {
                _actionVisualWasActive = false;
                PlayDirectLoop("Idle");
            }

            var moving = Mathf.Abs(_body.linearVelocity.x) > 0.08f;
            var allowRetarget = moving && _retarget != null && _retarget.IsCompatible;
            _retarget?.SetMoving(allowRetarget);
            _presentation.SetExternalLocomotionActive(allowRetarget);
            _presentation.SetLocomotion(moving, _motor.FacingSign);
        }

        private void OnAttackStarted(int comboIndex)
        {
            if (_dead)
                return;

            StopLocomotionPresentation();
            var facing = _motor != null ? _motor.FacingSign : 1;
            _presentation?.SetLocomotion(false, facing);

            switch (comboIndex)
            {
                case 0:
                    PlaySegment("Attack", 0.00f, 0.50f, AttackPlaybackSpeed);
                    break;
                case 1:
                    PlaySegment("Attack", 0.50f, 1.00f, AttackPlaybackSpeed);
                    break;
                default:
                    PlayOneShot("Skill", string.Empty, Combo3PlaybackSpeed);
                    break;
            }
        }

        private void OnSkillCast(int slot)
        {
            if (_dead)
                return;

            StopLocomotionPresentation();
            var facing = _motor != null ? _motor.FacingSign : 1;
            _presentation?.SetLocomotion(false, facing);

            if (slot == 1)
                PlayOneShot("Skill_2", "Skill_End_2", Skill1PlaybackSpeed);
            else if (slot == 2)
                PlayOneShot("Skill_3", "Skill_End_3", Skill2PlaybackSpeed);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            if (_dead || (_skills != null && _skills.IsCasting))
                return;
            _presentation?.PlayHit();
        }

        private void OnDied()
        {
            _dead = true;
            _retarget?.SetMoving(false);
            _presentation?.SetExternalLocomotionActive(false);
            _presentation?.PlayDie();
        }

        private void StopLocomotionPresentation()
        {
            _retarget?.SetMoving(false);
            _presentation?.SetExternalLocomotionActive(false);
        }

        private void PlaySegment(string clip, float startNormalized, float endNormalized, float speed)
        {
            if (!TryBind() || !_durations.TryGetValue(clip, out var duration) || duration <= 0f)
                return;

            var resolvedStart = Mathf.Clamp01(startNormalized);
            var resolvedEnd = Mathf.Clamp(endNormalized, resolvedStart + 0.01f, 1f);
            var entry = _setAnimation.Invoke(_animationState, new object[] { 0, clip, false });
            if (entry == null)
                return;

            SetFloatMember(entry, "TrackTime", "trackTime", duration * resolvedStart);
            SetFloatMember(entry, "TimeScale", "timeScale", Mathf.Max(0.01f, speed));
            _visualLockUntil = Time.time + duration * (resolvedEnd - resolvedStart) / Mathf.Max(0.01f, speed);
            _actionVisualWasActive = true;
        }

        private void PlayOneShot(string clip, string endClip, float speed)
        {
            if (!TryBind() || !_durations.TryGetValue(clip, out var duration) || duration <= 0f)
                return;

            var entry = _setAnimation.Invoke(_animationState, new object[] { 0, clip, false });
            if (entry == null)
                return;
            SetFloatMember(entry, "TimeScale", "timeScale", Mathf.Max(0.01f, speed));

            var total = duration / Mathf.Max(0.01f, speed);
            if (!string.IsNullOrWhiteSpace(endClip) && _durations.TryGetValue(endClip, out var endDuration) && _addAnimation != null)
            {
                var endEntry = _addAnimation.Invoke(_animationState, new object[] { 0, endClip, false, 0f });
                if (endEntry != null)
                    SetFloatMember(endEntry, "TimeScale", "timeScale", Mathf.Max(0.01f, speed));
                total += endDuration / Mathf.Max(0.01f, speed);
            }

            _visualLockUntil = Time.time + total;
            _actionVisualWasActive = true;
        }

        private void PlayDirectLoop(string clip)
        {
            if (!TryBind())
                return;
            try
            {
                _setAnimation.Invoke(_animationState, new object[] { 0, clip, true });
            }
            catch
            {
                // The generic presentation will recover locomotion on the next frame.
            }
        }

        private bool TryBind()
        {
            if (_animationState != null && _setAnimation != null)
                return true;

            _skeletonAnimation = GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().FullName == "Spine.Unity.SkeletonAnimation");
            if (_skeletonAnimation == null)
                return false;

            try
            {
                var type = _skeletonAnimation.GetType();
                var initialize = type.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);
                initialize?.Invoke(_skeletonAnimation, new object[] { false });
                _animationState = type.GetProperty("AnimationState", BindingFlags.Instance | BindingFlags.Public)?.GetValue(_skeletonAnimation);
                if (_animationState == null)
                    return false;

                var stateType = _animationState.GetType();
                _setAnimation = stateType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method => method.Name == "SetAnimation" && method.GetParameters().Length == 3);
                _addAnimation = stateType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method => method.Name == "AddAnimation" && method.GetParameters().Length == 4);
                DiscoverDurations(type);
                return _setAnimation != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArknightsACT/Chen] Failed to bind Spine action presentation: " + exception.GetBaseException().Message, this);
                return false;
            }
        }

        private void DiscoverDurations(Type skeletonType)
        {
            _durations.Clear();
            var skeleton = skeletonType.GetProperty("Skeleton", BindingFlags.Instance | BindingFlags.Public)?.GetValue(_skeletonAnimation);
            var data = skeleton?.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(skeleton);
            var animations = data?.GetType().GetProperty("Animations", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data) as IEnumerable;
            if (animations == null)
                return;

            foreach (var animation in animations)
            {
                if (animation == null)
                    continue;
                var animationType = animation.GetType();
                var name = animationType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(animation) as string;
                var rawDuration = animationType.GetProperty("Duration", BindingFlags.Instance | BindingFlags.Public)?.GetValue(animation);
                if (!string.IsNullOrWhiteSpace(name) && rawDuration is float duration)
                    _durations[name] = duration;
            }
        }

        private static void SetFloatMember(object target, string propertyName, string fieldName, float value)
        {
            if (target == null)
                return;
            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType == typeof(float))
            {
                property.SetValue(target, value);
                return;
            }
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(float))
                field.SetValue(target, value);
        }
    }
}
