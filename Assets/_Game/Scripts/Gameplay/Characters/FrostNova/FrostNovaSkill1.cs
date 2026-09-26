using System;
using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Abilities;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class FrostNovaSkill1 : MonoBehaviour, IPlayerSkill, IPlayerSkillInterruptible, IPlayerRunResettable
    {
        [Header("Skill points")]
        [SerializeField, Min(1f)] private float skillPointCost = 20f;
        [SerializeField, Min(0f)] private float initialSkillPoints = 10f;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [SerializeField, Min(0f)] private float startupSeconds = 0.62f;
        [SerializeField, Min(0.1f)] private float impactRadius = 4.4f;
        [SerializeField, Min(0f)] private float forwardOffset = 3.1f;
        [SerializeField, Min(0f)] private float artsDamage = 58f;
        [SerializeField] private string displayName = "冰锥";
        [SerializeField] private bool centeredOnPlayer;
        [SerializeField] private bool autoTarget;
        [SerializeField, Min(1f)] private float targetSearchRange = 12f;

        private CombatEntity _entity;
        private IPlayerLocomotion _locomotion;
        private Coroutine _routine;
        private CombatEntity _lockedTarget;
        private float _skillPoints;

        public int Slot => 1;
        public string DisplayName => displayName;
        public float SkillPointCost => Mathf.Max(1f, skillPointCost);
        public float SkillPoints => Mathf.Clamp(_skillPoints, 0f, SkillPointCost);
        public float SkillPointRatio => Mathf.Clamp01(SkillPoints / SkillPointCost);
        public float NaturalSkillPointPerSecond => Mathf.Max(0f, naturalSkillPointPerSecond);
        public float CooldownRemaining => NaturalSkillPointPerSecond <= 0f
            ? (SkillPointRatio >= 1f ? 0f : float.PositiveInfinity)
            : Mathf.Max(0f, SkillPointCost - SkillPoints) / NaturalSkillPointPerSecond;
        public bool IsCasting { get; private set; }
        public CombatEntity LockedTarget => _lockedTarget;

        public event Action CastStarted;
        public event Action<Vector3> Impact;

        public void ConfigureForSkin(FrostNovaSkinVariant skin)
        {
            if (skin.UsesWinterSkillSet())
            {
                displayName = "冰环";
                startupSeconds = 0.72f;
                impactRadius = 5.4f;
                forwardOffset = 0f;
                artsDamage = 62f;
                centeredOnPlayer = false;
                autoTarget = true;
                targetSearchRange = 12f;
                return;
            }

            displayName = "冰锥";
            startupSeconds = 0.62f;
            impactRadius = 4.4f;
            forwardOffset = 3.1f;
            artsDamage = 58f;
            centeredOnPlayer = false;
            autoTarget = false;
        }

        private void Awake()
        {
            var identity = GetComponent<PlayableOperatorIdentity>();
            if (identity != null)
                ConfigureForSkin(
                    FrostNovaSkinVariantExtensions.FromSkinId(identity.SkinId));

            _entity = GetComponent<CombatEntity>();
            _locomotion = FindLocomotion();
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
        }

        private IEnumerator CastRoutine()
        {
            IsCasting = true;
            CastStarted?.Invoke();

            if (startupSeconds > 0f)
                yield return new WaitForSeconds(startupSeconds);

            var hasLockedTarget =
                _lockedTarget != null &&
                _lockedTarget.Health != null &&
                !_lockedTarget.Health.IsDead;

            // Winter ice ring is allowed to empty-cast for animation/feel,
            // but without a valid target it must not create hit FX or deal impact damage.
            if (autoTarget && !hasLockedTarget)
            {
                var emptyCastPresentation =
                    GetComponent<FrostNovaPresentationDriver25D>();
                var emptyCastRemaining = emptyCastPresentation != null
                    ? emptyCastPresentation.RemainingActionVisualSeconds
                    : 0f;
                if (emptyCastRemaining > 0f)
                    yield return new WaitForSeconds(emptyCastRemaining);

                IsCasting = false;
                _routine = null;
                _lockedTarget = null;
                yield break;
            }

            var forward = FrostNovaCombatUtility.ResolveForward(_locomotion);
            var center = hasLockedTarget
                ? _lockedTarget.transform.position
                : centeredOnPlayer
                    ? transform.position
                    : transform.position + forward * forwardOffset;

            FrostNovaCombatUtility.DamageEnemiesInRadius(
                _entity,
                center,
                impactRadius,
                artsDamage,
                "FrostNova_Skill1_IceCone",
                CombatStatusIds.Cold,
                4f);
            Impact?.Invoke(center);

            var presentation = GetComponent<FrostNovaPresentationDriver25D>();
            var remainingVisual = presentation != null
                ? presentation.RemainingActionVisualSeconds
                : 0f;
            if (remainingVisual > 0f)
                yield return new WaitForSeconds(remainingVisual);

            IsCasting = false;
            _routine = null;
            _lockedTarget = null;
        }

        private IPlayerLocomotion FindLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            return null;
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
