using System;
using System.Collections;
using System.Linq;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Skadi
{
    /// <summary>
    /// Gameplay slot 2 maps to Skadi's original S3, 涌潮悲歌.
    /// Official mastery values modify ATK / DEF / MaxHP through the temporary stat layer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class SkadiSkill2 :
        MonoBehaviour,
        IPlayerSkill,
        IPlayerSkillLifecycleState,
        IPlayerSkillInterruptible,
        IOperatorSkillMasteryTarget,
        ILayeredCombatStatModifier,
        IPlayerControlLockSource,
        IPlayerRunResettable
    {
        [SerializeField] private string displayName = "涌潮悲歌";
        [SerializeField, Min(0f)] private float skillPointCost;
        [SerializeField, Min(0f)] private float initialSkillPoints;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [SerializeField, Min(0f)] private float buffDuration;
        [SerializeField, Min(0f)] private float officialAttackBonus;
        [SerializeField, Min(0f)] private float officialDefenseBonus;
        [SerializeField, Min(0f)] private float officialMaxHealthBonus;

        private CombatEntity _entity;
        private OperatorRuntimeStats _runtimeStats;
        private Coroutine _routine;
        private float _skillPoints;
        private float _activeUntil;
        private bool _officialSkillDataApplied;

        public int Slot => 2;
        public int MasterySlot => 2;
        public string DisplayName => displayName;
        public float SkillPointCost => Mathf.Max(0f, skillPointCost);
        public float SkillPoints => Mathf.Clamp(_skillPoints, 0f, SkillPointCost);
        public float SkillPointRatio =>
            SkillPointCost > 0.0001f
                ? Mathf.Clamp01(SkillPoints / SkillPointCost)
                : 0f;
        public float NaturalSkillPointPerSecond => Mathf.Max(0f, naturalSkillPointPerSecond);
        public float CooldownRemaining =>
            NaturalSkillPointPerSecond <= 0f
                ? (SkillPointRatio >= 1f ? 0f : float.PositiveInfinity)
                : Mathf.Max(0f, SkillPointCost - SkillPoints) /
                  Mathf.Max(0.01f, NaturalSkillPointPerSecond);
        public bool IsCasting { get; private set; }
        public bool IsActive { get; private set; }
        public bool HasOfficialSkillData => _officialSkillDataApplied;
        public float OfficialAttackBonus => Mathf.Max(0f, officialAttackBonus);
        public float OfficialDefenseBonus => Mathf.Max(0f, officialDefenseBonus);
        public float OfficialMaxHealthBonus => Mathf.Max(0f, officialMaxHealthBonus);
        public PlayerSkillLifecycleType LifecycleType => PlayerSkillLifecycleType.Duration;
        public float ActiveDurationSeconds => Mathf.Max(0f, buffDuration);
        public float ActiveSecondsRemaining =>
            IsActive ? Mathf.Max(0f, _activeUntil - Time.time) : 0f;
        public int AmmoRemaining => 0;
        public int AmmoCapacity => 0;
        public bool BlocksMovement => IsCasting;
        public bool BlocksDash => IsCasting;
        public bool BlocksBasicAttack => IsCasting;
        public CombatStatModifierLayer ModifierLayer => CombatStatModifierLayer.Temporary;

        public event Action CastStarted;
        public event Action BuffStarted;
        public event Action BuffEnded;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _runtimeStats = GetComponent<OperatorRuntimeStats>();
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        private void OnEnable()
        {
            if (!IsActive)
                return;

            if (Time.time >= _activeUntil)
            {
                EndActiveBuff(stopRoutine: false);
                return;
            }

            IsCasting = false;
            if (_routine == null)
                _routine = StartCoroutine(WaitForBuffEnd());
        }

        private void OnDisable()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            IsCasting = false;
            // Preserve active state and absolute end time for the temporary test-only
            // operator switching path. Formal gameplay does not switch operators in-run.
        }

        public bool TryCast()
        {
            if (!_officialSkillDataApplied ||
                IsCasting ||
                IsActive ||
                SkillPointCost <= 0f ||
                SkillPoints + 0.0001f < SkillPointCost ||
                _entity?.Health == null ||
                _entity.Health.IsDead)
                return false;

            _skillPoints = Mathf.Max(0f, SkillPoints - SkillPointCost);
            _routine = StartCoroutine(CastRoutine());
            return true;
        }

        public void TickSkillPoints(
            float deltaTime,
            float recoveryMultiplier,
            float flatRecoveryPerSecond)
        {
            if (IsCasting || IsActive || deltaTime <= 0f || SkillPointRatio >= 1f)
                return;

            var perSecond =
                NaturalSkillPointPerSecond * Mathf.Max(0f, recoveryMultiplier) +
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

        public void ApplyMasterySnapshot(OperatorSkillMasterySnapshot snapshot)
        {
            _officialSkillDataApplied = false;
            if (snapshot == null)
                return;

            if (!string.IsNullOrWhiteSpace(snapshot.DisplayName))
                displayName = snapshot.DisplayName;

            skillPointCost = snapshot.SkillPointCost;
            initialSkillPoints = Mathf.Clamp(
                snapshot.InitialSkillPoints,
                0f,
                Mathf.Max(0f, skillPointCost));
            buffDuration = Mathf.Max(0f, snapshot.Duration);

            var hasAttack = snapshot.TryGetBlackboard("atk", out var attackBonus);
            var hasDefense = snapshot.TryGetBlackboard("def", out var defenseBonus);
            var hasMaxHealth = snapshot.TryGetBlackboard("max_hp", out var maxHealthBonus);

            officialAttackBonus = hasAttack ? Mathf.Max(0f, attackBonus) : 0f;
            officialDefenseBonus = hasDefense ? Mathf.Max(0f, defenseBonus) : 0f;
            officialMaxHealthBonus = hasMaxHealth ? Mathf.Max(0f, maxHealthBonus) : 0f;

            _officialSkillDataApplied =
                skillPointCost > 0f &&
                buffDuration > 0f &&
                hasAttack &&
                hasDefense &&
                hasMaxHealth;

            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public void AccumulateStatModifiers(
            CombatStatType stat,
            ref float flat,
            ref float additivePercent)
        {
            if (!IsActive)
                return;

            switch (stat)
            {
                case CombatStatType.Attack:
                    additivePercent += officialAttackBonus;
                    break;
                case CombatStatType.PhysicalDefense:
                    additivePercent += officialDefenseBonus;
                    break;
                case CombatStatType.MaxHealth:
                    additivePercent += officialMaxHealthBonus;
                    break;
            }
        }

        public void InterruptCast()
        {
            if (!IsCasting)
                return;

            EndActiveBuff(stopRoutine: true);
        }

        public void ResetForNewRun()
        {
            EndActiveBuff(stopRoutine: true, notify: false);
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        private IEnumerator CastRoutine()
        {
            IsCasting = true;
            IsActive = true;
            _activeUntil = Time.time + ActiveDurationSeconds;
            CastStarted?.Invoke();

            ResolveRuntimeStats()?.RefreshResolvedMaxHealthFromModifierChange(
                healAddedCapacity: true);
            BuffStarted?.Invoke();

            var startup = ResolveSkillAnimationDuration();
            if (startup > 0f)
                yield return new WaitForSeconds(startup);

            IsCasting = false;
            _routine = StartCoroutine(WaitForBuffEnd());
        }

        private IEnumerator WaitForBuffEnd()
        {
            while (IsActive && Time.time < _activeUntil)
                yield return null;

            if (IsActive)
                EndActiveBuff(stopRoutine: false);
        }

        private void EndActiveBuff(bool stopRoutine, bool notify = true)
        {
            if (stopRoutine && _routine != null)
                StopCoroutine(_routine);

            var wasActive = IsActive;
            IsCasting = false;
            IsActive = false;
            _activeUntil = 0f;
            _routine = null;

            if (wasActive)
                ResolveRuntimeStats()?.RefreshResolvedMaxHealthFromModifierChange(
                    healAddedCapacity: false);

            if (notify && wasActive)
                BuffEnded?.Invoke();
        }

        private float ResolveSkillAnimationDuration()
        {
            var presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
            return presentation != null &&
                   presentation.TryGetAnimationDuration("Skill_3", out var duration)
                ? Mathf.Max(0f, duration)
                : 0f;
        }

        private OperatorRuntimeStats ResolveRuntimeStats() =>
            _runtimeStats != null
                ? _runtimeStats
                : (_runtimeStats = GetComponent<OperatorRuntimeStats>());
    }
}
