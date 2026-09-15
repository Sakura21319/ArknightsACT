using System;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Top-down-only presentation bridge. The authored Arknights attack clip is sped up while
    /// melee is active so gameplay cadence can be faster without editing the source Spine asset.
    /// </summary>
    [RequireComponent(typeof(TopDownTexasMeleeController))]
    public sealed class TopDownSpineAttackPlaybackSpeed2D : MonoBehaviour
    {
        [SerializeField, Range(1f, 4f)] private float attackPlaybackSpeed = 2.35f;
        [SerializeField, Min(0.05f)] private float speedHoldSeconds = 0.46f;

        private TopDownTexasMeleeController _melee;
        private PlayerSkillController _skill;
        private object _animationState;
        private PropertyInfo _timeScaleProperty;
        private FieldInfo _timeScaleField;
        private bool _bindAttempted;
        private bool _boosted;
        private float _boostUntil;

        private void Awake()
        {
            _melee = GetComponent<TopDownTexasMeleeController>();
            _skill = GetComponent<PlayerSkillController>();
        }

        private void OnEnable()
        {
            if (_melee != null)
                _melee.AttackStarted += OnAttackStarted;
            if (_skill != null)
                _skill.SkillCastSucceeded += OnSkillCast;
        }

        private void OnDisable()
        {
            if (_melee != null)
                _melee.AttackStarted -= OnAttackStarted;
            if (_skill != null)
                _skill.SkillCastSucceeded -= OnSkillCast;
            Restore();
        }

        private void Update()
        {
            if (_boosted && Time.time >= _boostUntil)
                Restore();
        }

        private void OnAttackStarted(Vector2 _)
        {
            if (!TryBind())
                return;
            SetTimeScale(attackPlaybackSpeed);
            _boosted = true;
            _boostUntil = Time.time + speedHoldSeconds;
        }

        private void OnSkillCast() => Restore();

        private bool TryBind()
        {
            if (_animationState != null && (_timeScaleProperty != null || _timeScaleField != null))
                return true;
            if (_bindAttempted)
                return false;

            _bindAttempted = true;
            var presentation = GetComponentInChildren<SpineCharacterPresentation2D>(true);
            if (presentation == null)
                return false;

            var skeletonAnimation = presentation.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().FullName == "Spine.Unity.SkeletonAnimation");
            if (skeletonAnimation == null)
                return false;

            var stateProperty = skeletonAnimation.GetType().GetProperty("AnimationState", BindingFlags.Instance | BindingFlags.Public);
            _animationState = stateProperty?.GetValue(skeletonAnimation);
            if (_animationState == null)
                return false;

            var stateType = _animationState.GetType();
            _timeScaleProperty = stateType.GetProperty("TimeScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_timeScaleProperty == null || !_timeScaleProperty.CanWrite || _timeScaleProperty.PropertyType != typeof(float))
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
                Debug.LogWarning("[ArknightsACT/TopDown] Failed to change Spine time scale: " + exception.GetBaseException().Message, this);
            }
        }

        private void Restore()
        {
            if (!_boosted)
                return;
            SetTimeScale(1f);
            _boosted = false;
            _boostUntil = 0f;
        }
    }
}
