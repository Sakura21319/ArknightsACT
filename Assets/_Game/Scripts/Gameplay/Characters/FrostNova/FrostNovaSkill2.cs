using System;
using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class FrostNovaSkill2 : MonoBehaviour, IPlayerSkill, IPlayerSkillInterruptible, IPlayerRunResettable, IOperatorSkillMasteryTarget
    {
        [Header("Skill points")]
        [SerializeField, Min(0f)] private float skillPointCost;
        [SerializeField, Min(0f)] private float initialSkillPoints;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [SerializeField, Min(0f)] private float startupSeconds = 0.90f;
        [SerializeField, Min(0.1f)] private float radius = 7.0f;
        [SerializeField, Min(0f)] private float officialDamageScale;
        [SerializeField, Min(0f)] private float officialColdDurationSeconds;
        [SerializeField, Min(0f)] private float officialRadiusTiles;
        [SerializeField] private string displayName = "冰爆";
        [SerializeField] private string damageSourceId = "FrostNova_Winter_Skill3_IceBurst";
        [SerializeField] private bool autoTarget;
        [SerializeField, Min(1f)] private float targetSearchRange = 12f;

        private CombatEntity _entity;
        private Coroutine _routine;
        private CombatEntity _lockedTarget;
        private float _skillPoints;
        private bool _officialSkillDataApplied;

        public int Slot => 2;
        public int MasterySlot => 2;
        public string DisplayName => displayName;
        public bool HasOfficialSkillData => _officialSkillDataApplied;
        public float SkillPointCost => Mathf.Max(0f, skillPointCost);
        public float SkillPoints => Mathf.Clamp(_skillPoints, 0f, SkillPointCost);
        public float SkillPointRatio => SkillPointCost > 0.0001f
            ? Mathf.Clamp01(SkillPoints / SkillPointCost)
            : 0f;
        public float NaturalSkillPointPerSecond => Mathf.Max(0f, naturalSkillPointPerSecond);
        public float CooldownRemaining => NaturalSkillPointPerSecond <= 0f
            ? (SkillPointRatio >= 1f ? 0f : float.PositiveInfinity)
            : Mathf.Max(0f, SkillPointCost - SkillPoints) / NaturalSkillPointPerSecond;
        public bool IsCasting { get; private set; }

        public event Action CastStarted;
        public event Action<Vector3, int> Pulse;
        public event Action CastEnded;

        public void ConfigureForSkin(FrostNovaSkinVariant skin)
        {
            displayName = "冰爆";
            damageSourceId = "FrostNova_Winter_Skill3_IceBurst";
            startupSeconds = 0.90f;
            radius = 7.0f;
            autoTarget = false;
            targetSearchRange = 12f;

            skillPointCost = 0f;
            initialSkillPoints = 0f;
            officialDamageScale = 0f;
            officialColdDurationSeconds = 0f;
            officialRadiusTiles = 0f;
            _officialSkillDataApplied = false;
            _skillPoints = 0f;
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

            var hasDamage = snapshot.TryGetBlackboard("damage_scale", out var damageScale);
            var hasCold = snapshot.TryGetBlackboard("cold_duration", out var coldDuration);
            var hasRadius = snapshot.TryGetBlackboard("radius_tiles", out var radiusTiles);
            officialDamageScale = hasDamage ? Mathf.Max(0f, damageScale) : 0f;
            officialColdDurationSeconds = hasCold ? Mathf.Max(0f, coldDuration) : 0f;
            officialRadiusTiles = hasRadius ? Mathf.Max(0f, radiusTiles) : 0f;

            _officialSkillDataApplied =
                skillPointCost > 0f &&
                officialDamageScale > 0f &&
                officialColdDurationSeconds > 0f &&
                officialRadiusTiles > 0f;
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        private void Awake()
        {
            var identity = GetComponent<PlayableOperatorIdentity>();
            if (identity != null)
                ConfigureForSkin(
                    FrostNovaSkinVariantExtensions.FromSkinId(identity.SkinId));

            _entity = GetComponent<CombatEntity>();
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public bool TryCast()
        {
            if (!_officialSkillDataApplied ||
                IsCasting ||
                SkillPoints + 0.0001f < SkillPointCost ||
                _entity?.Health == null ||
                _entity.Health.IsDead)
                return false;

            _lockedTarget = null;
            if (autoTarget)
            {
                FrostNovaCombatUtility.TryFindNearestEnemy(
                    _entity,
                    transform.position,
                    targetSearchRange * ResolveSkillRangeMultiplier(),
                    out _lockedTarget);
            }

            _skillPoints = Mathf.Max(0f, SkillPoints - SkillPointCost);
            _routine = StartCoroutine(CastRoutine());
            return true;
        }

        public void TickSkillPoints(
            float deltaTime,
            float recoveryMultiplier,
            float flatRecoveryPerSecond)
        {
            if (IsCasting || deltaTime <= 0f || SkillPointRatio >= 1f)
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

        public void ResetForNewRun()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            _lockedTarget = null;
            IsCasting = false;
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
            CastEnded?.Invoke();
        }

        public void InterruptCast()
        {
            if (!IsCasting)
                return;
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            _lockedTarget = null;
            IsCasting = false;
            CastEnded?.Invoke();
        }

        private IEnumerator CastRoutine()
        {
            IsCasting = true;
            CastStarted?.Invoke();

            if (startupSeconds > 0f)
                yield return new WaitForSeconds(startupSeconds);

            var center =
                _lockedTarget != null &&
                _lockedTarget.Health != null &&
                !_lockedTarget.Health.IsDead
                    ? _lockedTarget.transform.position
                    : transform.position;

            var runtimeStats = GetComponent<OperatorRuntimeStats>();
            if (runtimeStats != null)
            {
                FrostNovaCombatUtility.DamageEnemiesInRadius(
                    _entity,
                    center,
                    radius * ResolveSkillRangeMultiplier(),
                    runtimeStats.Attack * officialDamageScale,
                    damageSourceId,
                    CombatStatusIds.Cold,
                    officialColdDurationSeconds);
            }
            Pulse?.Invoke(center, 0);

            var presentation = GetComponent<FrostNovaPresentationDriver25D>();
            var remainingVisual = presentation != null
                ? presentation.RemainingActionVisualSeconds
                : 0f;
            if (remainingVisual > 0f)
                yield return new WaitForSeconds(remainingVisual);

            IsCasting = false;
            _routine = null;
            _lockedTarget = null;
            CastEnded?.Invoke();
        }

        private float ResolveSkillRangeMultiplier()
        {
            var runtimeStats = GetComponent<OperatorRuntimeStats>();
            return runtimeStats != null
                ? Mathf.Max(0f, runtimeStats.GetSkillRangeMultiplier(2))
                : 1f;
        }

        private void OnDisable()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            _lockedTarget = null;
            IsCasting = false;
        }
    }
}
