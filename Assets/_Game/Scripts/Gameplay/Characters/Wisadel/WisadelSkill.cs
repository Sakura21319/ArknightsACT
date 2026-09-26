using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Wisadel
{
    /// <summary>
    /// Wisadel's playable S2/S3 lifecycle.
    /// Slot 1 maps to original S2: a timed, continuously firing auto-target skill.
    /// Slot 2 maps to original S3: an ammo state consumed by player basic attacks.
    /// </summary>
    public sealed class WisadelSkill : MonoBehaviour,
        IPlayerSkill,
        IPlayerSkillInterruptible,
        IPlayerRunResettable,
        IPlayerSkillAmmoConsumer,
        IPlayerControlLockSource
    {
        [SerializeField, Range(1, 2)] private int slot = 1;
        [SerializeField] private string displayName = "Wisadel Skill";
        [SerializeField, Min(1f)] private float skillPointCost = 25f;
        [SerializeField, Min(0f)] private float initialSkillPoints = 10f;
        [SerializeField, Min(0f)] private float naturalSkillPointPerSecond = 1f;
        [SerializeField, Min(0f)] private float startupSeconds = 0.55f;

        [Header("Skill 2 - continuous auto target")]
        [SerializeField, Min(0.5f)] private float activeDurationSeconds = 25f;
        // PRTS / game data at M3: base interval 2.1s + S2 base_attack_time(-0.7) = 1.4s.
        [SerializeField, Min(0.1f)] private float autoAttackIntervalSeconds = 1.4f;
        // Overdrive keeps the 1.4s attack cycle, but each attack becomes a 4-shot burst.
        // The imported Skill_2_Loop is 1.4s raw / 2x project playback = 0.7s, so 0.175s
        // spaces four visible shots across that effective loop.
        [SerializeField, Range(1, 4)] private int overloadBurstCount = 4;
        [SerializeField, Min(0.01f)] private float overloadBurstShotIntervalSeconds = 0.175f;
        [SerializeField, Min(1f)] private float autoTargetRange = 12f;
        [SerializeField, Min(0.1f)] private float autoImpactRadius = 3.8f;
        [SerializeField, Min(0f)] private float autoDamagePerShot = 94f;

        [Header("Skill 3 - ammo")]
        [SerializeField, Min(1)] private int ammoCapacity = 6;

        private CombatEntity _entity;
        private Coroutine _routine;
        private float _skillPoints;
        private float _activeUntil;
        private float _overloadStartsAt;
        private int _ammoRemaining;

        public int Slot => slot;
        public string DisplayName => displayName;
        public float CooldownRemaining => NaturalSkillPointPerSecond <= 0f
            ? (SkillPointRatio >= 1f ? 0f : float.PositiveInfinity)
            : Mathf.Max(0f, SkillPointCost - SkillPoints) / NaturalSkillPointPerSecond;
        public float SkillPoints => Mathf.Clamp(_skillPoints, 0f, SkillPointCost);
        public float SkillPointCost => Mathf.Max(1f, skillPointCost);
        public float SkillPointRatio => Mathf.Clamp01(SkillPoints / SkillPointCost);
        public float NaturalSkillPointPerSecond => Mathf.Max(0f, naturalSkillPointPerSecond);
        public bool IsCasting { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsAutoAttacking { get; private set; }
        public bool IsOverloaded { get; private set; }

        public PlayerSkillLifecycleType LifecycleType =>
            slot == 2 ? PlayerSkillLifecycleType.Ammo : PlayerSkillLifecycleType.Duration;

        public float ActiveDurationSeconds =>
            slot == 1 ? Mathf.Max(0.5f, activeDurationSeconds) : 0f;

        public float Skill2StageDurationSeconds =>
            slot == 1 ? ActiveDurationSeconds * 0.5f : 0f;

        public float ActiveSecondsRemaining =>
            slot == 1 && IsActive
                ? Mathf.Max(0f, _activeUntil - Time.time)
                : 0f;

        public int AmmoRemaining =>
            slot == 2 && IsActive ? Mathf.Max(0, _ammoRemaining) : 0;

        public int AmmoCapacity =>
            slot == 2 ? Mathf.Max(1, ammoCapacity) : 0;

        public bool ConsumeAmmoOnBasicAttackStarted => slot == 2;
        public int LastAmmoConsumedFrame { get; private set; } = -1;

        // S2 owns attacks but allows movement/dash. S3 is an ammo stance: it roots the player
        // while active but still allows basic-attack input so each shot can consume ammunition.
        public bool BlocksMovement => slot == 2 && (IsCasting || IsActive);
        public bool BlocksDash => slot == 2 && (IsCasting || IsActive);
        public bool BlocksBasicAttack => slot == 1 && (IsCasting || IsActive);

        /// <summary>Raised after startup when the duration/ammo state actually begins.</summary>
        public event Action<int> ActiveStarted;

        /// <summary>Raised once when S2 transitions from no target to actively attacking.</summary>
        public event Action<int, CombatEntity> AutoAttackStarted;

        /// <summary>Raised once when S2 no longer has a valid target.</summary>
        public event Action<int> AutoAttackStopped;

        /// <summary>Raised once when S2 reaches the midpoint and enters its overdrive phase.</summary>
        public event Action<int> OverloadStarted;

        /// <summary>
        /// Raised immediately before every S2 automatic shot. Projectile/action FX such as
        /// skill_02_start bind here; the Spine Begin/Idle/Loop/End state machine binds to the
        /// higher-level cast and auto-attack state transitions instead.
        /// </summary>
        public event Action<int, CombatEntity> AutoShotStarted;

        /// <summary>Raised for each S2 automatic shot that has a valid locked target.</summary>
        public event Action<int, CombatEntity> AutoTargetImpact;

        /// <summary>Raised for every S3 round consumed. remaining is the post-consumption ammo count.</summary>
        public event Action<int, int> AmmoConsumed;

        /// <summary>Raised when S2 expires or the last S3 round is consumed.</summary>
        public event Action<int> CastFinished;

        public void ConfigurePrototype(
            int skillSlot,
            string name,
            float pointCost,
            float initialPoints,
            float startup,
            float durationSeconds,
            int maximumAmmo)
        {
            slot = Mathf.Clamp(skillSlot, 1, 2);
            displayName = name ?? string.Empty;
            skillPointCost = Mathf.Max(1f, pointCost);
            initialSkillPoints = Mathf.Clamp(initialPoints, 0f, skillPointCost);
            startupSeconds = Mathf.Max(0f, startup);
            activeDurationSeconds = Mathf.Max(0.5f, durationSeconds);
            ammoCapacity = Mathf.Max(1, maximumAmmo);

            // Current Wisadel prototype is authored against S2 M3 timing. Keep this explicit so
            // old serialized scene instances do not retain the pre-PRTS 0.35s prototype cadence.
            if (slot == 1)
            {
                autoAttackIntervalSeconds = 1.4f;
                overloadBurstCount = 4;
                overloadBurstShotIntervalSeconds = 0.175f;
            }
        }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        private void OnEnable()
        {
            if (slot != 1 || !IsActive)
                return;

            if (Time.time >= _activeUntil)
            {
                EndActiveState(false);
                return;
            }

            if (_routine == null)
                _routine = StartCoroutine(RunSkill2ActiveRoutine());
        }

        private void OnDisable()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            IsCasting = false;
            IsAutoAttacking = false;
            // Lifecycle state survives reserve-operator deactivation.
            // S2 uses an absolute expiry timestamp; S3 preserves remaining ammunition.
        }

        public bool TryCast()
        {
            if (IsCasting || IsActive || IsSiblingBusy() ||
                SkillPoints + 0.0001f < SkillPointCost ||
                _entity?.Health == null || _entity.Health.IsDead)
                return false;

            _skillPoints = Mathf.Max(0f, SkillPoints - SkillPointCost);
            _routine = StartCoroutine(ActivationRoutine());
            return true;
        }

        public void TickSkillPoints(float deltaTime, float recoveryMultiplier, float flatRecoveryPerSecond)
        {
            if (IsCasting || IsActive || deltaTime <= 0f || SkillPointRatio >= 1f)
                return;

            GainSkillPoints(
                (NaturalSkillPointPerSecond * Mathf.Max(0f, recoveryMultiplier) +
                 Mathf.Max(0f, flatRecoveryPerSecond)) * deltaTime);
        }

        public void GainSkillPoints(float amount)
        {
            if (amount > 0f && !IsActive)
                _skillPoints = Mathf.Clamp(SkillPoints + amount, 0f, SkillPointCost);
        }

        public void SetSkillPoints(float amount) =>
            _skillPoints = Mathf.Clamp(amount, 0f, SkillPointCost);

        public void ReduceCooldown(float seconds)
        {
            if (seconds > 0f && !IsActive)
                GainSkillPoints(seconds * Mathf.Max(0.01f, NaturalSkillPointPerSecond));
        }

        public bool TryConsumeAmmo()
        {
            if (slot != 2 || !IsActive || _ammoRemaining <= 0)
                return false;

            _ammoRemaining = Mathf.Max(0, _ammoRemaining - 1);
            LastAmmoConsumedFrame = Time.frameCount;
            AmmoConsumed?.Invoke(slot, _ammoRemaining);

            if (_ammoRemaining <= 0)
                EndActiveState(false);

            return true;
        }

        public void ResetForNewRun()
        {
            StopAllRuntimeState(false);
            _skillPoints = Mathf.Clamp(initialSkillPoints, 0f, SkillPointCost);
        }

        public void InterruptCast()
        {
            if (!IsCasting && !IsActive)
                return;

            EndActiveState(true);
        }

        private IEnumerator ActivationRoutine()
        {
            if (slot == 1)
            {
                // S2 must remain mobile from the moment it is triggered. Mark the lifecycle active
                // immediately so it cannot be recast, but do not expose IsCasting to the shared
                // motor/dash systems (they treat any casting skill as movement-locked).
                IsCasting = false;
                IsActive = true;
                IsOverloaded = false;
                var activeStartsAt = Time.time + Mathf.Max(0f, startupSeconds);
                _overloadStartsAt = activeStartsAt + Skill2StageDurationSeconds;
                _activeUntil = activeStartsAt + ActiveDurationSeconds;

                if (startupSeconds > 0f)
                    yield return new WaitForSeconds(startupSeconds);

                if (_entity?.Health == null || _entity.Health.IsDead)
                {
                    EndActiveState(false);
                    yield break;
                }

                ActiveStarted?.Invoke(slot);
                yield return RunSkill2ActiveRoutine();
                yield break;
            }

            IsCasting = true;
            if (startupSeconds > 0f)
                yield return new WaitForSeconds(startupSeconds);

            if (_entity?.Health == null || _entity.Health.IsDead)
            {
                IsCasting = false;
                _routine = null;
                yield break;
            }

            // S3 becomes a rooted ammo stance after the begin animation. Basic attacks remain
            // available and spend one round each; movement/dash stay locked until ammo is empty.
            IsCasting = false;
            IsActive = true;
            _ammoRemaining = AmmoCapacity;
            ActiveStarted?.Invoke(slot);
            _routine = null;
        }

        private IEnumerator RunSkill2ActiveRoutine()
        {
            IsCasting = false;

            // S2 has two equal phases inside one authored duration. PRTS/game data keeps the
            // M3 attack cycle at 1.4s (2.1 base - 0.7); overdrive changes one cycle into a four-shot
            // burst instead of shortening the whole cycle to the old prototype's 0.175s cadence.
            var nextAttackAt = Time.time;
            while (IsActive && Time.time < _activeUntil)
            {
                if (!IsOverloaded && _overloadStartsAt > 0f && Time.time >= _overloadStartsAt)
                {
                    IsOverloaded = true;
                    OverloadStarted?.Invoke(slot);
                    nextAttackAt = Mathf.Min(nextAttackAt, Time.time);
                }

                if (Time.time >= nextAttackAt)
                {
                    var cycleStartedAt = Time.time;
                    if (TryFindNearestEnemy(out var target))
                    {
                        if (!IsAutoAttacking)
                        {
                            IsAutoAttacking = true;
                            AutoAttackStarted?.Invoke(slot, target);
                        }

                        if (IsOverloaded)
                        {
                            yield return FireSkill2OverloadBurst(target);
                        }
                        else
                        {
                            AutoShotStarted?.Invoke(slot, target);
                            FireSkill2AutoShot(target);
                        }
                    }
                    else if (IsAutoAttacking)
                    {
                        IsAutoAttacking = false;
                        AutoAttackStopped?.Invoke(slot);
                    }

                    nextAttackAt = cycleStartedAt + Mathf.Max(0.1f, autoAttackIntervalSeconds);
                }

                yield return null;
            }

            if (IsActive)
                EndActiveState(false);
        }

        private IEnumerator FireSkill2OverloadBurst(CombatEntity firstTarget)
        {
            var target = firstTarget;
            var burstCount = Mathf.Clamp(overloadBurstCount, 1, 4);
            var shotInterval = Mathf.Max(0.01f, overloadBurstShotIntervalSeconds);

            for (var shot = 0; shot < burstCount; shot++)
            {
                if (!IsActive || !IsOverloaded || Time.time >= _activeUntil)
                    yield break;

                if (shot > 0 && !TryFindNearestEnemy(out target))
                    yield break;

                if (target != null)
                {
                    AutoShotStarted?.Invoke(slot, target);
                    FireSkill2AutoShot(target);
                }

                if (shot + 1 < burstCount)
                    yield return new WaitForSeconds(shotInterval);
            }
        }

        private void FireSkill2AutoShot(CombatEntity target)
        {
            if (target == null)
                return;

            var center = target.transform.position;
            var colliders = Physics.OverlapSphere(
                center,
                Mathf.Max(0.1f, autoImpactRadius),
                ~0,
                QueryTriggerInteraction.Ignore);
            var processed = new HashSet<CombatEntity>();

            for (var i = 0; i < colliders.Length; i++)
            {
                var candidate = colliders[i] != null
                    ? colliders[i].GetComponentInParent<CombatEntity>()
                    : null;
                if (candidate == null ||
                    candidate == _entity ||
                    candidate.Team == _entity.Team ||
                    candidate.Health == null ||
                    candidate.Health.IsDead ||
                    !processed.Add(candidate))
                    continue;

                DamageSystem.Apply(new DamageContext(
                    _entity,
                    _entity,
                    candidate,
                    Mathf.Max(0f, autoDamagePerShot),
                    DamageType.Physical,
                    Vector2.zero,
                    sourceId: "Wisadel_Skill2_Auto",
                    tags: DamageTags.Skill));
            }

            AutoTargetImpact?.Invoke(slot, target);
        }

        private bool TryFindNearestEnemy(out CombatEntity target)
        {
            target = null;
            if (_entity == null)
                return false;

            var colliders = Physics.OverlapSphere(
                transform.position,
                Mathf.Max(1f, autoTargetRange),
                ~0,
                QueryTriggerInteraction.Ignore);
            var processed = new HashSet<CombatEntity>();
            var bestDistanceSq = float.PositiveInfinity;

            for (var i = 0; i < colliders.Length; i++)
            {
                var candidate = colliders[i] != null
                    ? colliders[i].GetComponentInParent<CombatEntity>()
                    : null;
                if (candidate == null ||
                    candidate == _entity ||
                    candidate.Team == _entity.Team ||
                    candidate.Health == null ||
                    candidate.Health.IsDead ||
                    !processed.Add(candidate))
                    continue;

                var delta = candidate.transform.position - transform.position;
                var distanceSq = delta.sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        private bool IsSiblingBusy()
        {
            var skills = GetComponents<WisadelSkill>();
            for (var i = 0; i < skills.Length; i++)
            {
                var other = skills[i];
                if (other == null || other == this)
                    continue;
                if (other.IsCasting || other.IsActive)
                    return true;
            }

            return false;
        }

        private void EndActiveState(bool stopRoutine)
        {
            if (stopRoutine && _routine != null)
                StopCoroutine(_routine);

            var wasActive = IsActive;
            var wasAutoAttacking = IsAutoAttacking;
            IsCasting = false;
            IsActive = false;
            IsAutoAttacking = false;
            _routine = null;
            _activeUntil = 0f;
            _overloadStartsAt = 0f;
            _ammoRemaining = 0;
            IsOverloaded = false;

            if (wasAutoAttacking)
                AutoAttackStopped?.Invoke(slot);
            if (wasActive)
                CastFinished?.Invoke(slot);
        }

        private void StopAllRuntimeState(bool notifyFinished)
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;

            var wasActive = IsActive;
            IsCasting = false;
            IsActive = false;
            IsAutoAttacking = false;
            _activeUntil = 0f;
            _overloadStartsAt = 0f;
            _ammoRemaining = 0;
            IsOverloaded = false;
            LastAmmoConsumedFrame = -1;

            if (notifyFinished && wasActive)
                CastFinished?.Invoke(slot);
        }
    }
}
