using System;
using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    /// <summary>
    /// Gameplay slot 2 maps to Schwarz's original S3, 战术的终结. The active window modifies the
    /// generic basic-attack pipeline through IPlayerBasicAttackModifier rather than branching in
    /// PlayerAttackController.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class SchwarzSkill2 : MonoBehaviour, IPlayerSkill, IPlayerBasicAttackModifier, IPlayerSkillLifecycleState, IPlayerControlLockSource, IPlayerSkillInterruptible, IOperatorSkillMasteryTarget, ILayeredCombatStatModifier
    {
        [SerializeField, Min(0f)] private float skillPointCost;
        [SerializeField, Min(0f)] private float initialSkillPoints;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [SerializeField, Min(0f)] private float startupSeconds = 0.38f;
        [SerializeField, Min(0f)] private float buffDuration;
        [SerializeField] private bool debugInfiniteDuration = false;
        [SerializeField, Min(0f)] private float officialAttackMultiplier;
        [SerializeField] private float basicAttackIntervalFlatDelta;

        private CombatEntity _entity;
        private float _skillPoints;
        private float _runtimeDamageMultiplier = 1f;
        private float _runtimeRangeMultiplier = 1f;
        private float _runtimeDurationMultiplier = 1f;
        private float _runtimeCostMultiplier = 1f;
        private Coroutine _routine;
        private SchwarzSkill1 _skill2;
        private float _activeUntil;
        private bool _officialSkillDataApplied;

        public int Slot => 2;
        public int MasterySlot => 2;
        public string DisplayName => "战术的终结";
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
        public PlayerSkillLifecycleType LifecycleType => PlayerSkillLifecycleType.Duration;
        public float ActiveDurationSeconds => debugInfiniteDuration
            ? float.PositiveInfinity
            : Mathf.Max(0f, buffDuration * _runtimeDurationMultiplier);
        public float ActiveSecondsRemaining => !IsBuffActive
            ? 0f
            : debugInfiniteDuration
                ? float.PositiveInfinity
                : Mathf.Max(0f, _activeUntil - Time.time);
        public int AmmoRemaining => 0;
        public int AmmoCapacity => 0;
        public bool BlocksMovement => IsCasting || IsBuffActive;
        public bool BlocksDash => IsCasting || IsBuffActive;
        public bool BlocksBasicAttack => IsBuffActive;
        public CombatStatModifierLayer ModifierLayer => CombatStatModifierLayer.Temporary;

        public float BasicAttackDamageMultiplier =>
            IsBuffActive ? _runtimeDamageMultiplier : 1f;

        public float BasicAttackRangeMultiplier
        {
            get
            {
                if (!IsBuffActive)
                    return 1f;
                var runtimeStats = GetComponent<OperatorRuntimeStats>();
                var pipeline = runtimeStats != null
                    ? runtimeStats.GetSkillRangeMultiplier(MasterySlot)
                    : 1f;
                return OperatorRangeUtility.ResolveSkillRangeMultiplier(
                           this,
                           MasterySlot,
                           1f) *
                       pipeline *
                       _runtimeRangeMultiplier;
            }
        }

        public float BasicAttackTimingMultiplier => 1f;

        public void AccumulateStatModifiers(CombatStatType stat, ref float flat, ref float additivePercent)
        {
            if (!IsBuffActive)
                return;

            switch (stat)
            {
                case CombatStatType.Attack:
                    additivePercent += Mathf.Max(0f, officialAttackMultiplier - 1f);
                    break;
                case CombatStatType.AttackInterval:
                    flat += basicAttackIntervalFlatDelta;
                    break;
            }
        }

        public event Action CastStarted;
        public event Action BuffStarted;
        public event Action BuffEnded;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _skill2 = GetComponent<SchwarzSkill1>();
            if (GetComponent<SchwarzSniperModeController>() == null)
                gameObject.AddComponent<SchwarzSniperModeController>();
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        private void OnEnable()
        {
            if (!IsBuffActive)
                return;

            if (!debugInfiniteDuration && Time.time >= _activeUntil)
            {
                EndActiveBuff();
                return;
            }

            if (_routine == null)
                _routine = StartCoroutine(WaitForBuffEnd());
        }

        public bool TryCast()
        {
            if (!_officialSkillDataApplied || IsBuffActive)
                return false;

            if (IsCasting || (_skill2 != null && _skill2.IsBuffActive) ||
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
            _activeUntil = 0f;
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
            if (amount <= 0f)
                return;
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

        public void ConfigureFormalLifecycle()
        {
            debugInfiniteDuration = false;
            if (IsBuffActive && float.IsPositiveInfinity(_activeUntil))
                _activeUntil = Time.time + ActiveDurationSeconds;
        }

        public void ApplyMasterySnapshot(OperatorSkillMasterySnapshot snapshot)
        {
            if (snapshot == null)
            {
                _officialSkillDataApplied = false;
                return;
            }

            skillPointCost = snapshot.SkillPointCost;
            initialSkillPoints = Mathf.Clamp(
                snapshot.InitialSkillPoints,
                0f,
                Mathf.Max(1f, skillPointCost));
            buffDuration = snapshot.Duration;

            var hasAttackBonus = snapshot.TryGetBlackboard("atk", out var attackBonus);
            var hasAttackTime = snapshot.TryGetBlackboard("base_attack_time", out var baseAttackTimeDelta);
            var hasTalentProc = snapshot.TryGetBlackboard("talent@prob", out var talentProc);
            officialAttackMultiplier = hasAttackBonus
                ? Mathf.Max(1f, 1f + attackBonus)
                : 0f;
            basicAttackIntervalFlatDelta = hasAttackTime ? baseAttackTimeDelta : 0f;
            if (hasTalentProc)
                GetComponent<SchwarzArmorBreakTalent>()?.SetSkill3ProcChance(talentProc);

            _officialSkillDataApplied =
                skillPointCost > 0f &&
                buffDuration > 0f &&
                hasAttackBonus &&
                hasAttackTime &&
                hasTalentProc &&
                !string.IsNullOrWhiteSpace(snapshot.RangeId);
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public void AddDamagePercent(float value) =>
            _runtimeDamageMultiplier = Mathf.Clamp(_runtimeDamageMultiplier + Mathf.Max(0f, value), 1f, 3.5f);

        public void AddRangePercent(float value) =>
            _runtimeRangeMultiplier = Mathf.Clamp(_runtimeRangeMultiplier + Mathf.Max(0f, value), 1f, 2f);

        public void AddDurationPercent(float value) =>
            _runtimeDurationMultiplier = Mathf.Clamp(_runtimeDurationMultiplier + Mathf.Max(0f, value), 1f, 2.5f);

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
            _runtimeRangeMultiplier = 1f;
            _runtimeDurationMultiplier = 1f;
            _runtimeCostMultiplier = 1f;
            _activeUntil = 0f;
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
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
            IsCasting = true;
            CastStarted?.Invoke();
            if (!debugInfiniteDuration && startupSeconds > 0f)
                yield return new WaitForSeconds(startupSeconds);

            IsCasting = false;
            IsBuffActive = true;
            BuffStarted?.Invoke();

            _activeUntil = debugInfiniteDuration
                ? float.PositiveInfinity
                : Time.time + ActiveDurationSeconds;

            yield return WaitForBuffEnd();
        }

        private IEnumerator WaitForBuffEnd()
        {
            if (debugInfiniteDuration)
            {
                while (IsBuffActive)
                    yield return null;
                yield break;
            }

            while (IsBuffActive && Time.time < _activeUntil)
                yield return null;

            if (IsBuffActive)
                EndActiveBuff();
        }

        private void EndActiveBuff()
        {
            IsCasting = false;
            IsBuffActive = false;
            _activeUntil = 0f;
            _routine = null;
            BuffEnded?.Invoke();
        }

        private void OnDisable()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            IsCasting = false;
            // Keep the active lifecycle state when this operator is moved to reserve.
            // Duration is absolute, so it can expire while another operator is active.
        }
    }
}
