using System;
using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    /// <summary>
    /// Gameplay slot 1 maps to Schwarz's original S2, 暮眼锐瞳.
    /// Its attack buff remains character-local while armor break is handled by the shared
    /// SchwarzArmorBreakTalent + CombatStats/Status pipeline.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class SchwarzSkill1 : MonoBehaviour, IPlayerSkill, IPlayerBasicAttackModifier, IPlayerSkillActiveState, IPlayerSpecialAttackAudioState, IPlayerSkillInterruptible
    {
        [SerializeField, Min(1f)] private float skillPointCost = 30f;
        [SerializeField, Min(0f)] private float initialSkillPoints = 20f;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [SerializeField, HideInInspector] private float startupSeconds = 0f;
        [SerializeField, Min(0.5f)] private float buffDuration = 40f;
        [SerializeField] private bool debugInfiniteDuration = true;
        [Tooltip("专三暮眼锐瞳：攻击力 +130%，即最终基础攻击倍率约 2.3x。")]
        [SerializeField, Min(1f)] private float basicAttackDamageMultiplier = 2.30f;

        private CombatEntity _entity;
        private float _skillPoints;
        private float _runtimeDamageMultiplier = 1f;
        private float _runtimeDurationMultiplier = 1f;
        private float _runtimeCostMultiplier = 1f;
        private Coroutine _routine;
        private SchwarzSkill2 _skill3;

        public int Slot => 1;
        public string DisplayName => "暮眼锐瞳";
        public float SkillPointCost => Mathf.Max(1f, skillPointCost * _runtimeCostMultiplier);
        public float SkillPoints => Mathf.Clamp(_skillPoints, 0f, SkillPointCost);
        public float SkillPointRatio => Mathf.Clamp01(SkillPoints / SkillPointCost);
        public float NaturalSkillPointPerSecond => Mathf.Max(0f, naturalSkillPointPerSecond);
        public float CooldownRemaining => NaturalSkillPointPerSecond <= 0f
            ? (SkillPointRatio >= 1f ? 0f : float.PositiveInfinity)
            : Mathf.Max(0f, SkillPointCost - SkillPoints) / NaturalSkillPointPerSecond;
        public bool IsCasting { get; private set; }
        public bool IsBuffActive { get; private set; }
        public bool IsActive => IsBuffActive;
        public bool UseSpecialAttackAudio => IsBuffActive;

        public float BasicAttackDamageMultiplier =>
            IsBuffActive ? basicAttackDamageMultiplier * _runtimeDamageMultiplier : 1f;
        public float BasicAttackRangeMultiplier => 1f;
        public float BasicAttackTimingMultiplier => 1f;

        public event Action CastStarted;
        public event Action BuffStarted;
        public event Action BuffEnded;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _skill3 = GetComponent<SchwarzSkill2>();
            if (GetComponent<SchwarzArmorBreakTalent>() == null)
                gameObject.AddComponent<SchwarzArmorBreakTalent>();
            _skillPoints = debugInfiniteDuration
                ? SkillPointCost
                : Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public bool TryCast()
        {
            if (IsBuffActive)
            {
                CancelActiveBuff();
                return true;
            }

            if (IsCasting || (_skill3 != null && _skill3.IsBuffActive) ||
                SkillPoints + 0.0001f < SkillPointCost || _entity?.Health == null || _entity.Health.IsDead)
                return false;

            _skillPoints = Mathf.Max(0f, SkillPoints - SkillPointCost);
            _routine = StartCoroutine(BuffRoutine());
            return true;
        }

        public void CancelActiveBuff()
        {
            if (!IsBuffActive)
                return;

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            IsCasting = false;
            IsBuffActive = false;
            if (debugInfiniteDuration)
                _skillPoints = SkillPointCost;
            BuffEnded?.Invoke();
        }

        public void TickSkillPoints(float deltaTime, float recoveryMultiplier, float flatRecoveryPerSecond)
        {
            if (IsCasting || IsBuffActive || deltaTime <= 0f || SkillPointRatio >= 1f)
                return;
            var perSecond = NaturalSkillPointPerSecond * Mathf.Max(0f, recoveryMultiplier) +
                            Mathf.Max(0f, flatRecoveryPerSecond);
            GainSkillPoints(perSecond * deltaTime);
        }

        public void GainSkillPoints(float amount)
        {
            if (amount > 0f)
                _skillPoints = Mathf.Clamp(SkillPoints + amount, 0f, SkillPointCost);
        }

        public void SetSkillPoints(float amount)
        {
            _skillPoints = Mathf.Clamp(amount, 0f, SkillPointCost);
        }

        public void ReduceCooldown(float seconds)
        {
            if (seconds > 0f)
                GainSkillPoints(seconds * Mathf.Max(0.01f, NaturalSkillPointPerSecond));
        }

        public void AddDamagePercent(float value) =>
            _runtimeDamageMultiplier = Mathf.Clamp(
                _runtimeDamageMultiplier + Mathf.Max(0f, value), 1f, 3.5f);

        public void AddDurationPercent(float value) =>
            _runtimeDurationMultiplier = Mathf.Clamp(
                _runtimeDurationMultiplier + Mathf.Max(0f, value), 1f, 2.5f);

        public void AddCostReductionPercent(float value) =>
            _runtimeCostMultiplier = Mathf.Clamp(
                _runtimeCostMultiplier * (1f - Mathf.Max(0f, value)), 0.45f, 1f);

        public void ResetRunModifiers()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            var wasActive = IsBuffActive;
            IsCasting = false;
            IsBuffActive = false;
            _runtimeDamageMultiplier = 1f;
            _runtimeDurationMultiplier = 1f;
            _runtimeCostMultiplier = 1f;
            _skillPoints = debugInfiniteDuration
                ? SkillPointCost
                : Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
            if (wasActive)
                BuffEnded?.Invoke();
        }

        public void InterruptCast()
        {
            if (!IsCasting)
                return;
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            IsCasting = false;
        }

        private IEnumerator BuffRoutine()
        {
            // S2 has no dedicated character animation/startup action. Activate immediately.
            IsCasting = false;
            CastStarted?.Invoke();
            IsBuffActive = true;
            BuffStarted?.Invoke();

            if (debugInfiniteDuration)
            {
                while (IsBuffActive)
                    yield return null;
                yield break;
            }

            yield return new WaitForSeconds(buffDuration * _runtimeDurationMultiplier);

            IsBuffActive = false;
            _routine = null;
            BuffEnded?.Invoke();
        }

        private void OnDisable()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            IsCasting = false;
            if (IsBuffActive)
            {
                IsBuffActive = false;
                BuffEnded?.Invoke();
            }
        }
    }
}
