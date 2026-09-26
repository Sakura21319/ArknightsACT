using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Wisadel
{
    /// <summary>Single-target physical ranged resolver used by Wisadel's prototype basic attack.</summary>
    [DisallowMultipleComponent]
    public sealed class WisadelRangedBasicAttack :
        MonoBehaviour,
        IPlayerBasicAttackResolver,
        IPlayerBasicAttackTargetProvider,
        IPlayerSpecialAttackAudioState
    {
        [SerializeField, Min(1f)] private float range = 10f;
        [SerializeField, Min(0.1f)] private float maximumHeightDifference = 2.5f;
        [SerializeField, Min(0.02f)] private float projectileVisualFlightSeconds = 0.22f;

        private CombatEntity _entity;
        private IPlayerLocomotion _locomotion;
        private PlayerMotor25D _motor25D;
        private PlayerAttackController _attackController;
        private SpineCharacterPresentation2D _presentation;
        private CombatEntity _pendingAimTarget;
        private bool _hasPendingAimSnapshot;
        private bool _pendingWasSkill3;
        private float _nextSkill3AttackAllowedAt = -999f;

        /// <summary>
        /// Raised only after this resolver successfully applies a real ranged hit. The bool records
        /// the attack mode captured at target-acquisition time, so final S3 ammo rounds remain S3
        /// even after their lifecycle ends before impact.
        /// </summary>
        public event Action<CombatEntity, bool> ShotHitResolved;

        public float ProjectileVisualFlightSeconds =>
            Mathf.Max(0.02f, projectileVisualFlightSeconds);

        public float Skill3AttackIntervalSeconds => ResolveSkill3AttackInterval();

        // The generic operator audio runtime uses this to switch the ordinary attack sound
        // to Wisadel's S3 shot sound, including the final ammo round.
        public bool UseSpecialAttackAudio => IsSkill3Active();

        public bool TryGetAimTarget(out CombatEntity target)
        {
            if (_hasPendingAimSnapshot &&
                IsStillValidPendingTarget(_entity, _pendingAimTarget, 1f))
            {
                target = _pendingAimTarget;
                FaceTarget(target);
                return true;
            }

            return TryAcquireBasicAttackTarget(_entity, _locomotion, 1f, out target);
        }

        public bool TryAcquireBasicAttackTarget(
            CombatEntity attacker,
            IPlayerLocomotion locomotion,
            float rangeMultiplier,
            out CombatEntity target)
        {
            if (IsSkill3Active() && Time.time + 0.0001f < _nextSkill3AttackAllowedAt)
            {
                target = null;
                return false;
            }

            target = FindTarget(attacker, rangeMultiplier);
            _pendingAimTarget = target;
            _hasPendingAimSnapshot = true;
            _pendingWasSkill3 = IsSkill3Active();

            if (_pendingWasSkill3)
            {
                Debug.Log(
                    $"[WisadelS3FxDiag/Ranged] Acquire target={DescribeEntity(target)} " +
                    $"frame={Time.frameCount} subscribers={(ShotHitResolved != null ? ShotHitResolved.GetInvocationList().Length : 0)}",
                    this);
            }

            if (target == null)
                return false;

            FaceTarget(target);
            return true;
        }

        public Vector3 GetMissDestination()
        {
            var forward = ResolveForward(_locomotion);
            return transform.position + Vector3.up * 0.82f + forward * Mathf.Max(1f, range);
        }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _locomotion = FindLocomotion();
            _motor25D = GetComponent<PlayerMotor25D>();
            _attackController = GetComponent<PlayerAttackController>();
            _presentation = GetComponentInChildren<SpineCharacterPresentation2D>(true);
        }

        private void OnEnable()
        {
            if (_attackController != null)
                _attackController.AttackStarted += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_attackController != null)
                _attackController.AttackStarted -= OnAttackStarted;
        }

        private void OnAttackStarted(int _)
        {
            if (!IsSkill3Active())
                return;

            _nextSkill3AttackAllowedAt = Time.time + ResolveSkill3AttackInterval();
        }

        private float ResolveSkill3AttackInterval()
        {
            if (_presentation == null)
                _presentation = GetComponentInChildren<SpineCharacterPresentation2D>(true);

            if (_presentation != null &&
                _presentation.TryGetAnimationDuration("Skill_3_Loop", out var duration))
                return Mathf.Max(0.05f, duration);

            // Raw Skill_3_Loop is 5.0s; imported character animations run at the project-wide 2x.
            return 2.5f;
        }

        public bool TryResolveBasicAttack(
            CombatEntity attacker,
            AttackDefinition definition,
            IPlayerLocomotion locomotion,
            float finalDamage,
            float rangeMultiplier,
            out CombatEntity hitTarget)
        {
            var wasSkill3 = _pendingWasSkill3;
            if (_hasPendingAimSnapshot)
            {
                hitTarget = IsStillValidPendingTarget(
                    attacker,
                    _pendingAimTarget,
                    rangeMultiplier)
                    ? _pendingAimTarget
                    : null;
                _pendingAimTarget = null;
                _hasPendingAimSnapshot = false;
                _pendingWasSkill3 = false;
            }
            else
            {
                hitTarget = FindTarget(attacker, rangeMultiplier);
                wasSkill3 = IsSkill3Active();
            }

            if (wasSkill3)
            {
                Debug.Log(
                    $"[WisadelS3FxDiag/Ranged] Resolve begin target={DescribeEntity(hitTarget)} " +
                    $"hadSnapshot={_hasPendingAimSnapshot} frame={Time.frameCount}",
                    this);
            }

            if (hitTarget == null)
                return false;

            FaceTarget(hitTarget);

            var result = DamageSystem.Apply(new DamageContext(
                attacker,
                attacker,
                hitTarget,
                Mathf.Max(0f, finalDamage),
                DamageType.Physical,
                Vector2.zero,
                sourceId: definition != null ? definition.name : "Wisadel_BasicAttack",
                tags: DamageTags.BasicAttack));


            if (wasSkill3)
            {
                Debug.Log(
                    $"[WisadelS3FxDiag/Ranged] Damage result applied={result.Applied} " +
                    $"finalDamage={result.FinalDamage:F2} target={DescribeEntity(hitTarget)} " +
                    $"subscribers={(ShotHitResolved != null ? ShotHitResolved.GetInvocationList().Length : 0)}",
                    this);
            }

            if (result.Applied)
            {
                if (wasSkill3)
                    Debug.Log("[WisadelS3FxDiag/Ranged] Invoking ShotHitResolved(wasSkill3=true)", this);

                ShotHitResolved?.Invoke(hitTarget, wasSkill3);
                return true;
            }

            hitTarget = null;
            return false;
        }

        private bool IsStillValidPendingTarget(
            CombatEntity attacker,
            CombatEntity target,
            float rangeMultiplier)
        {
            if (!RangedBasicAttackTargeting.IsValidEnemy(attacker, target))
                return false;

            var delta = target.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > maximumHeightDifference)
                return false;

            delta.y = 0f;
            var distance = delta.magnitude;
            var maxRange = range * Mathf.Max(0.1f, rangeMultiplier);
            return distance >= 0.001f && distance <= maxRange;
        }

        private CombatEntity FindTarget(
            CombatEntity attacker,
            float rangeMultiplier)
        {
            var maxRange = range * Mathf.Max(0.1f, rangeMultiplier);
            return RangedBasicAttackTargeting.TryFindBestTarget(
                attacker,
                transform.position,
                maxRange,
                maximumHeightDifference,
                ResolveForward(_locomotion),
                minimumForwardDot: -1f,
                forwardPenaltyWeight: 0f,
                extraFilter: null,
                out var target)
                ? target
                : null;
        }

        private bool IsSkill3Active()
        {
            var skills = GetComponents<WisadelSkill>();
            for (var i = 0; i < skills.Length; i++)
            {
                var skill = skills[i];
                if (skill == null || skill.Slot != 2)
                    continue;

                return skill.IsActive || skill.LastAmmoConsumedFrame == Time.frameCount;
            }

            return false;
        }

        private void FaceTarget(CombatEntity target)
        {
            if (target == null)
                return;

            var direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;

            _motor25D?.SetPlanarFacing(direction);
        }

        private static Vector3 ResolveForward(IPlayerLocomotion locomotion)
        {
            var forward = locomotion != null ? locomotion.PlanarForward : Vector3.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;
            return forward.normalized;
        }

        private static string DescribeEntity(CombatEntity entity)
        {
            if (entity == null)
                return "null";

            var health = entity.Health;
            return $"{entity.name}(team={entity.Team},enabled={entity.enabled},active={entity.gameObject.activeInHierarchy}," +
                   $"health={(health != null ? health.CurrentHealth.ToString("F1") : "null")}," +
                   $"dead={(health != null && health.IsDead)},pos={entity.transform.position})";
        }

        private IPlayerLocomotion FindLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            return null;
        }
    }
}
