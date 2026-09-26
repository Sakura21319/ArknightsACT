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
    public sealed class FrostNovaSkill2 : MonoBehaviour, IPlayerSkill, IPlayerSkillInterruptible, IPlayerRunResettable
    {
        [Header("Skill points")]
        [SerializeField, Min(1f)] private float skillPointCost = 35f;
        [SerializeField, Min(0f)] private float initialSkillPoints = 15f;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [SerializeField, Min(0f)] private float startupSeconds = 0.82f;
        [SerializeField, Min(1)] private int pulseCount = 3;
        [SerializeField, Min(0.05f)] private float pulseInterval = 0.48f;
        [SerializeField, Min(0.1f)] private float radius = 6.0f;
        [SerializeField, Min(0f)] private float artsDamagePerPulse = 34f;
        [SerializeField] private string displayName = "冰霜领域";
        [SerializeField] private string damageSourceId = "FrostNova_Skill2_FrostField";
        [SerializeField] private bool autoTarget = true;
        [SerializeField, Min(1f)] private float targetSearchRange = 12f;

        private CombatEntity _entity;
        private Coroutine _routine;
        private CombatEntity _lockedTarget;
        private float _skillPoints;

        public int Slot => 2;
        public string DisplayName => displayName;
        public float SkillPointCost => Mathf.Max(1f, skillPointCost);
        public float SkillPoints => Mathf.Clamp(_skillPoints, 0f, SkillPointCost);
        public float SkillPointRatio => Mathf.Clamp01(SkillPoints / SkillPointCost);
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
            if (skin.UsesWinterSkillSet())
            {
                displayName = "冰爆";
                damageSourceId = "FrostNova_Winter_Skill3_IceBurst";
                startupSeconds = 0.90f;
                pulseCount = 2;
                pulseInterval = 0.42f;
                radius = 7.0f;
                artsDamagePerPulse = 52f;
                autoTarget = false;
                return;
            }

            displayName = "冰霜领域";
            damageSourceId = "FrostNova_Skill2_FrostField";
            startupSeconds = 0.82f;
            pulseCount = 3;
            pulseInterval = 0.48f;
            radius = 6.0f;
            artsDamagePerPulse = 34f;
            autoTarget = true;
            targetSearchRange = 12f;
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
            if (IsCasting ||
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
                    targetSearchRange,
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

            var count = Mathf.Max(1, pulseCount);
            for (var i = 0; i < count; i++)
            {
                var center =
                    _lockedTarget != null &&
                    _lockedTarget.Health != null &&
                    !_lockedTarget.Health.IsDead
                        ? _lockedTarget.transform.position
                        : transform.position;

                FrostNovaCombatUtility.DamageEnemiesInRadius(
                    _entity,
                    center,
                    radius,
                    artsDamagePerPulse,
                    damageSourceId,
                    CombatStatusIds.Cold,
                    4f);
                Pulse?.Invoke(center, i);

                if (i + 1 < count)
                    yield return new WaitForSeconds(pulseInterval);
            }

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
