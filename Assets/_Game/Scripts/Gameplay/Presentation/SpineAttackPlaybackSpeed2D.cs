using System;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Attack-speed presentation bridge for Spine characters.
    ///
    /// Arknights communicates higher attack speed by accelerating the actual attack animation,
    /// rather than allowing invisible extra hit pulses while a slower swing is still playing.
    /// Gameplay timing remains authoritative in PlayerAttackController / AttackDefinition, while
    /// this component makes the visible Spine swing keep pace with that cadence.
    /// </summary>
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class SpineAttackPlaybackSpeed2D : MonoBehaviour
    {
        // Texas' prototype battle animation is intentionally shown at 2x by default. Future
        // AttackSpeed stats/builds should scale this value and gameplay cadence together.
        [SerializeField, Range(1f, 4f)] private float attackPlaybackSpeed = 2.0f;
        [SerializeField, Min(0.05f)] private float chainGraceSeconds = 0.30f;

        private PlayerAttackController _attack;
        private PlayerSkillController _skill;
        private object _animationState;
        private PropertyInfo _timeScaleProperty;
        private FieldInfo _timeScaleField;
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

        private void OnAttackStarted(int _)
        {
            if (!TryBind())
                return;

            SetTimeScale(attackPlaybackSpeed);
            _boosted = true;
            _boostUntil = Time.time + chainGraceSeconds;
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

            var skeletonAnimation = combatPresentation.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().FullName == "Spine.Unity.SkeletonAnimation");
            if (skeletonAnimation == null)
                return false;

            var stateProperty = skeletonAnimation.GetType().GetProperty("AnimationState", BindingFlags.Instance | BindingFlags.Public);
            _animationState = stateProperty?.GetValue(skeletonAnimation);
            if (_animationState == null)
                return false;

            var stateType = _animationState.GetType();
            _timeScaleProperty = stateType.GetProperty("TimeScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_timeScaleProperty == null || _timeScaleProperty.PropertyType != typeof(float) || !_timeScaleProperty.CanWrite)
                _timeScaleProperty = null;

            _timeScaleField = stateType.GetField("timeScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_timeScaleField == null || _timeScaleField.FieldType != typeof(float) || _timeScaleField.IsInitOnly)
                _timeScaleField = null;

            return _timeScaleProperty != null || _timeScaleField != null;
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
