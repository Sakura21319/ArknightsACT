using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Wisadel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController), typeof(PlayerSkillController))]
    public sealed class WisadelExtractedFxController : MonoBehaviour, IPlayerRuntimeActivationHandler
    {
        [Header("Basic attack")]
        [SerializeField] private GameObject[] basicStarts = new GameObject[3];
        [SerializeField] private GameObject[] basicDownStarts = new GameObject[3];
        [SerializeField] private GameObject basicTrail;
        [SerializeField] private GameObject basicHit;
        [SerializeField] private GameObject basicHit02;

        [Header("Skill 2")]
        [SerializeField] private GameObject skill2Start;
        [SerializeField] private GameObject skill2Buff;
        [SerializeField] private GameObject skill2Buff02;
        [SerializeField] private GameObject skill2Hit;
        [SerializeField] private GameObject skill2Hit02;
        [SerializeField] private GameObject skill2OverloadStart;

        [Header("Skill 3")]
        [SerializeField] private GameObject skill3Start;
        [SerializeField] private GameObject skill3UpStart;
        [SerializeField] private GameObject skill3DownStart;
        [SerializeField] private GameObject skill3Trail;
        [SerializeField] private GameObject skill3Hit;
        [SerializeField] private GameObject skill3Hit02;
        [SerializeField] private GameObject skill3Hit03;
        [SerializeField] private GameObject skill3BuffBack;
        [SerializeField] private GameObject skill3BuffFront;
        [SerializeField] private GameObject skill3Buff02Back;
        [SerializeField] private GameObject skill3Buff02Front;

        [Header("Base placement")]
        [SerializeField] private Transform actorMount;
        [SerializeField] private Vector3 actorLocalOffset = new(0f, 0.82f, 0f);
        [SerializeField] private Vector3 targetLocalOffset = new(0f, 0.70f, 0f);
        [SerializeField, Min(0.1f)] private float basicScale = 2.8f;
        [SerializeField, Min(0.1f)] private float skill2Scale = 3.2f;
        [SerializeField, Min(0.1f)] private float skill3Scale = 3.4f;
        [SerializeField, Min(0.1f)] private float fallbackLifetime = 1.6f;

        private readonly List<GameObject> _active = new();
        private readonly List<GameObject> _skill2Persistent = new();
        private readonly List<GameObject> _skill3Persistent = new();
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private PlayerMotor25D _motor;
        private WisadelRangedBasicAttack _rangedAttack;
        private WisadelFxTuningProfile _tuning;
        private WisadelSkill[] _wisadelSkills;
        private WisadelSkill _skill2;
        private WisadelSkill _skill3;
        private int _currentCombo;

        public void Configure(
            GameObject[] basicStartValues,
            GameObject[] basicDownStartValues,
            GameObject basicTrailValue,
            GameObject basicHitValue,
            GameObject basicHit02Value,
            GameObject skill2StartValue,
            GameObject skill2BuffValue,
            GameObject skill2Buff02Value,
            GameObject skill2HitValue,
            GameObject skill2Hit02Value,
            GameObject skill2OverloadStartValue,
            GameObject skill3StartValue,
            GameObject skill3UpStartValue,
            GameObject skill3DownStartValue,
            GameObject skill3TrailValue,
            GameObject skill3HitValue,
            GameObject skill3Hit02Value,
            GameObject skill3Hit03Value,
            GameObject skill3BuffBackValue,
            GameObject skill3BuffFrontValue,
            GameObject skill3Buff02BackValue,
            GameObject skill3Buff02FrontValue)
        {
            basicStarts = basicStartValues ?? new GameObject[3];
            basicDownStarts = basicDownStartValues ?? new GameObject[3];
            basicTrail = basicTrailValue;
            basicHit = basicHitValue;
            basicHit02 = basicHit02Value;

            skill2Start = skill2StartValue;
            skill2Buff = skill2BuffValue;
            skill2Buff02 = skill2Buff02Value;
            skill2Hit = skill2HitValue;
            skill2Hit02 = skill2Hit02Value;
            skill2OverloadStart = skill2OverloadStartValue;

            skill3Start = skill3StartValue;
            skill3UpStart = skill3UpStartValue;
            skill3DownStart = skill3DownStartValue;
            skill3Trail = skill3TrailValue;
            skill3Hit = skill3HitValue;
            skill3Hit02 = skill3Hit02Value;
            skill3Hit03 = skill3Hit03Value;
            skill3BuffBack = skill3BuffBackValue;
            skill3BuffFront = skill3BuffFrontValue;
            skill3Buff02Back = skill3Buff02BackValue;
            skill3Buff02Front = skill3Buff02FrontValue;
        }

        public void RefreshTuning()
        {
            _tuning = Resources.Load<WisadelFxTuningProfile>(WisadelFxTuningProfile.ResourcePath);
        }

        public void ApplySavedTuning()
        {
            RefreshTuning();
            ReconcilePersistentFx();
            RefreshPersistentTransforms();
        }

        /// <summary>
        /// S2 overload has its own authored transition FX. The gameplay lifecycle raises the
        /// overload event at the midpoint of the 25-second skill and this method plays it once.
        /// </summary>
        public void PlaySkill2OverloadFx()
        {
            SpawnOnActor(
                skill2OverloadStart,
                WisadelFxSlot.Skill2OverloadStart,
                ResolveActorBaseOffset(),
                skill2Scale);
        }

        private void Awake()
        {
            RefreshRuntimeBindings();
            RefreshTuning();
        }

        private void OnEnable()
        {
            RefreshRuntimeState();

            var identity = GetComponent<PlayableOperatorIdentity>();
            Debug.Log(
                $"[ArknightsACT/WisadelFX] enabled skin={(identity != null ? identity.SkinId : "?")} " +
                $"attack={(_attack != null)} skills={(_skills != null)} ranged={(_rangedAttack != null)} " +
                $"s2={(_skill2 != null)} s3={(_skill3 != null)} mount={(actorMount != null ? actorMount.name : "null")} " +
                $"basic0={PrefabName(basicStarts != null && basicStarts.Length > 0 ? basicStarts[0] : null)} " +
                $"s3start={PrefabName(skill3Start)} s3trail={PrefabName(skill3Trail)}",
                this);

        }

        public void OnPlayerRuntimeActivated()
        {
            RefreshRuntimeState();
        }

        private void RefreshRuntimeState()
        {
            // Shared runtime entry used by normal OnEnable and PlayerRuntimeContext Tab switching.
            // Menu/editor refresh is allowed to rebuild serialized assets, but once Play Mode starts
            // every activation path must converge here so FX bindings cannot diverge by switch route.
            RefreshRuntimeBindings();
            RefreshTuning();
            RebindEventSubscriptions();

            // Reserve/operator switching can disable this controller while the skill lifecycle
            // itself remains active. Restore only the persistent FX that are still enabled.
            ReconcilePersistentFx();
        }

        private void Start()
        {
            // Inactive reserve operators run Awake/OnEnable only when first switched in. Rebind
            // once more in Start so every sibling component has completed initialization before
            // this controller owns the runtime event subscriptions.
            if (!isActiveAndEnabled)
                return;

            RefreshRuntimeBindings();
            RebindEventSubscriptions();
        }

        private void Update()
        {
            RefreshPersistentTransforms();
        }

        private void RefreshRuntimeBindings()
        {
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _motor = GetComponent<PlayerMotor25D>();
            _rangedAttack = GetComponent<WisadelRangedBasicAttack>();

            if (actorMount == null || !actorMount.IsChildOf(transform))
                actorMount = transform.Find("CustomFxMountPoint");
            actorMount = actorMount != null ? actorMount : transform;

            _skill2 = null;
            _skill3 = null;
            _wisadelSkills = GetComponents<WisadelSkill>();
            if (_wisadelSkills == null)
                return;

            for (var i = 0; i < _wisadelSkills.Length; i++)
            {
                var skill = _wisadelSkills[i];
                if (skill == null)
                    continue;
                if (skill.Slot == 1)
                    _skill2 = skill;
                else if (skill.Slot == 2)
                    _skill3 = skill;
            }
        }

        private void RebindEventSubscriptions()
        {
            UnsubscribeEventSubscriptions();

            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;

            if (_rangedAttack != null)
                _rangedAttack.ShotHitResolved += OnRangedShotHitResolved;

            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCast;

            if (_wisadelSkills == null)
                return;

            for (var i = 0; i < _wisadelSkills.Length; i++)
            {
                var skill = _wisadelSkills[i];
                if (skill == null)
                    continue;
                skill.ActiveStarted += OnSkillActiveStarted;
                skill.AutoShotStarted += OnSkillAutoShotStarted;
                skill.AutoTargetImpact += OnSkillAutoTargetImpact;
                skill.OverloadStarted += OnSkillOverloadStarted;
                skill.AmmoConsumed += OnSkillAmmoConsumed;
                skill.CastFinished += OnSkillFinished;
            }
        }

        private void UnsubscribeEventSubscriptions()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;

            if (_rangedAttack != null)
                _rangedAttack.ShotHitResolved -= OnRangedShotHitResolved;

            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCast;

            if (_wisadelSkills == null)
                return;

            for (var i = 0; i < _wisadelSkills.Length; i++)
            {
                var skill = _wisadelSkills[i];
                if (skill == null)
                    continue;
                skill.ActiveStarted -= OnSkillActiveStarted;
                skill.AutoShotStarted -= OnSkillAutoShotStarted;
                skill.AutoTargetImpact -= OnSkillAutoTargetImpact;
                skill.OverloadStarted -= OnSkillOverloadStarted;
                skill.AmmoConsumed -= OnSkillAmmoConsumed;
                skill.CastFinished -= OnSkillFinished;
            }
        }

        private void OnDisable()
        {
            UnsubscribeEventSubscriptions();

            ClearActive();
            ClearSkill2Persistent();
            ClearSkill3Persistent();
        }

        private void OnAttackStarted(int comboIndex)
        {
            _currentCombo = Mathf.Abs(comboIndex) % 3;

            var isSkill3Round =
                _skill3 != null &&
                (_skill3.IsActive || _skill3.LastAmmoConsumedFrame == Time.frameCount);
            if (isSkill3Round)
                return;

            var useDown = UseDownAttackVariant();
            var starts = useDown ? basicDownStarts : basicStarts;
            var start = starts != null && _currentCombo < starts.Length
                ? starts[_currentCombo]
                : null;

            if (start == null && basicStarts != null && _currentCombo < basicStarts.Length)
                start = basicStarts[_currentCombo];

            // The authored A/B/C start is still the weapon muzzle/action layer. During S3 only
            // the projectile/hit layers are replaced by skill_03_trail + skill_03_hit*.
            SpawnOnActor(
                start,
                useDown ? WisadelFxSlot.BasicDownStart : WisadelFxSlot.BasicStart,
                ResolveActorBaseOffset(),
                basicScale);

            // *_trail is projectile flight, never an actor-attached start effect.
            Transform target = null;
            Vector3 destination;
            if (_rangedAttack != null &&
                _rangedAttack.TryGetAimTarget(out var aimTarget) &&
                aimTarget != null)
            {
                target = aimTarget.transform;
                destination = target.TransformPoint(targetLocalOffset);
            }
            else
            {
                destination = _rangedAttack != null
                    ? _rangedAttack.GetMissDestination()
                    : ResolveForwardMissDestination();
            }

            StartTrailFlight(
                basicTrail,
                target,
                destination,
                WisadelFxSlot.BasicTrail,
                basicScale,
                _rangedAttack != null
                    ? _rangedAttack.ProjectileVisualFlightSeconds
                    : 0.22f);
        }

        private void OnRangedShotHitResolved(CombatEntity target, bool wasSkill3)
        {
            Debug.Log(
                $"[WisadelS3FxDiag/FX] ShotHitResolved received target={(target != null ? target.name : "null")} " +
                $"wasSkill3={wasSkill3} frame={Time.frameCount}",
                this);

            // This event is emitted by WisadelRangedBasicAttack only after DamageSystem accepts
            // the exact cached target. FX therefore use the same target and the same captured
            // attack mode as gameplay; no event-order inference is involved.
            if (target == null)
                return;

            var anchor = target.transform;
            var baseOffset = targetLocalOffset;
            if (wasSkill3)
            {
                Debug.Log(
                    $"[WisadelS3FxDiag/FX] S3 impact bindings " +
                    $"hit={PrefabName(skill3Hit)} enabled={IsFxEnabled(WisadelFxSlot.Skill3Hit)}; " +
                    $"hit02={PrefabName(skill3Hit02)} enabled={IsFxEnabled(WisadelFxSlot.Skill3Hit02)}; " +
                    $"hit03={PrefabName(skill3Hit03)} enabled={IsFxEnabled(WisadelFxSlot.Skill3Hit03)}; " +
                    $"targetPos={anchor.position} localOffset={baseOffset}",
                    this);

                // S3 impact prefabs are authored as fixed/static-offset explosion layers. Spawn
                // them in world space at the resolved impact point instead of parenting them to
                // the enemy hierarchy; otherwise target rotation/depth can hide the billboard.
                var impactPoint = anchor.TransformPoint(baseOffset);
                SpawnSkill3ImpactAtWorld(skill3Hit, impactPoint, WisadelFxSlot.Skill3Hit, skill3Scale, 0.120f);
                SpawnSkill3ImpactAtWorld(skill3Hit02, impactPoint, WisadelFxSlot.Skill3Hit02, skill3Scale, 0.124f);
                SpawnSkill3ImpactAtWorld(skill3Hit03, impactPoint, WisadelFxSlot.Skill3Hit03, skill3Scale, 0.128f);
                return;
            }

            SpawnAttached(basicHit, anchor, baseOffset, WisadelFxSlot.BasicHit, basicScale);
            SpawnAttached(basicHit02, anchor, baseOffset, WisadelFxSlot.BasicHit02, basicScale);
        }

        private void OnSkillCast(int slot)
        {
            if (slot == 2)
            {
                Debug.Log(
                    $"[WisadelS3FxDiag/FX] SkillCast S3 start={PrefabName(skill3Start)} " +
                    $"up={PrefabName(skill3UpStart)} down={PrefabName(skill3DownStart)} " +
                    $"enabledStart={IsFxEnabled(WisadelFxSlot.Skill3Start)}",
                    this);
            }

            if (slot == 1)
            {
                ClearSkill2Persistent();
                return;
            }

            if (slot != 2)
                return;

            ClearSkill3Persistent();

            // Keep the existing directional interpretation for the exported S3 start variants.
            var verticalDirection = ResolveScreenVerticalDirection();
            var directionalStart = verticalDirection > 0
                ? skill3UpStart
                : verticalDirection < 0
                    ? skill3DownStart
                    : skill3Start;
            var directionalSlot = verticalDirection > 0
                ? WisadelFxSlot.Skill3UpStart
                : verticalDirection < 0
                    ? WisadelFxSlot.Skill3DownStart
                    : WisadelFxSlot.Skill3Start;
            if (directionalStart == null)
            {
                directionalStart = skill3Start;
                directionalSlot = WisadelFxSlot.Skill3Start;
            }

            SpawnOnActor(
                directionalStart,
                directionalSlot,
                ResolveActorBaseOffset(),
                skill3Scale);
        }

        private void OnSkillActiveStarted(int slot)
        {

            if (slot == 1)
            {
                ClearSkill2Persistent();
                AddSkill2Persistent(skill2Buff, WisadelFxSlot.Skill2Buff);
                AddSkill2Persistent(skill2Buff02, WisadelFxSlot.Skill2Buff02);
                return;
            }

            if (slot != 2)
                return;

            Debug.Log(
                $"[WisadelS3FxDiag/FX] S3 ActiveStarted " +
                $"buffB={PrefabName(skill3BuffBack)}/{IsFxEnabled(WisadelFxSlot.Skill3BuffBack)} " +
                $"buffF={PrefabName(skill3BuffFront)}/{IsFxEnabled(WisadelFxSlot.Skill3BuffFront)} " +
                $"buff02B={PrefabName(skill3Buff02Back)}/{IsFxEnabled(WisadelFxSlot.Skill3Buff02Back)} " +
                $"buff02F={PrefabName(skill3Buff02Front)}/{IsFxEnabled(WisadelFxSlot.Skill3Buff02Front)}",
                this);

            ClearSkill3Persistent();
            AddSkill3Persistent(skill3BuffBack, WisadelFxSlot.Skill3BuffBack);
            AddSkill3Persistent(skill3BuffFront, WisadelFxSlot.Skill3BuffFront);
            AddSkill3Persistent(skill3Buff02Back, WisadelFxSlot.Skill3Buff02Back);
            AddSkill3Persistent(skill3Buff02Front, WisadelFxSlot.Skill3Buff02Front);
        }

        private void OnSkillAutoShotStarted(int slot, CombatEntity target)
        {
            if (slot != 1 || target == null)
                return;


            // skill_02_start is an attack-action FX. It must restart for every real S2 shot,
            // exactly when the corresponding attack animation is requested.
            SpawnOnActor(skill2Start, WisadelFxSlot.Skill2Start, ResolveActorBaseOffset(), skill2Scale);
        }

        private void OnSkillOverloadStarted(int slot)
        {
            if (slot == 1)
                PlaySkill2OverloadFx();
        }

        private void OnSkillAutoTargetImpact(int slot, CombatEntity target)
        {
            if (slot != 1 || target == null)
                return;


            var anchor = target.transform;
            SpawnAttached(skill2Hit, anchor, targetLocalOffset, WisadelFxSlot.Skill2Hit, skill2Scale);
            SpawnAttached(skill2Hit02, anchor, targetLocalOffset, WisadelFxSlot.Skill2Hit02, skill2Scale);
        }

        private void OnSkillAmmoConsumed(int slot, int remaining)
        {
            if (slot != 2)
                return;

            Debug.Log(
                $"[WisadelS3FxDiag/FX] AmmoConsumed remaining={remaining} trail={PrefabName(skill3Trail)} " +
                $"trailEnabled={IsFxEnabled(WisadelFxSlot.Skill3Trail)} frame={Time.frameCount}",
                this);

            Transform target = null;
            Vector3 destination;
            if (_rangedAttack != null &&
                _rangedAttack.TryGetAimTarget(out var aimTarget) &&
                aimTarget != null)
            {
                target = aimTarget.transform;
                destination = target.TransformPoint(targetLocalOffset);
            }
            else
            {
                destination = _rangedAttack != null
                    ? _rangedAttack.GetMissDestination()
                    : ResolveForwardMissDestination();
            }

            StartTrailFlight(
                skill3Trail,
                target,
                destination,
                WisadelFxSlot.Skill3Trail,
                skill3Scale,
                _rangedAttack != null
                    ? _rangedAttack.ProjectileVisualFlightSeconds
                    : 0.22f);
        }

        private void OnSkillFinished(int slot)
        {
            if (slot == 1)
            {
                ClearSkill2Persistent();
                return;
            }

            if (slot != 2)
                return;

            ClearSkill3Persistent();
        }

        private bool UseDownAttackVariant() =>
            ResolveScreenVerticalDirection() < 0;

        private int ResolveScreenVerticalDirection()
        {
            if (_motor == null)
                return 0;

            var camera = Camera.main;
            if (camera == null)
                return 0;

            var screenUpOnGround = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up);
            if (screenUpOnGround.sqrMagnitude < 0.001f)
                return 0;

            screenUpOnGround.Normalize();
            var forward = _motor.PlanarForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                return 0;

            var dot = Vector3.Dot(forward.normalized, screenUpOnGround);
            if (dot > 0.35f)
                return 1;
            if (dot < -0.35f)
                return -1;
            return 0;
        }

        private void AddSkill2Persistent(GameObject prefab, WisadelFxSlot slot) =>
            AddPersistent(
                _skill2Persistent,
                "WisadelSkill2Persistent_",
                prefab,
                slot,
                skill2Scale);

        private void AddSkill3Persistent(GameObject prefab, WisadelFxSlot slot) =>
            AddPersistent(
                _skill3Persistent,
                "WisadelSkill3Persistent_",
                prefab,
                slot,
                skill3Scale);

        private void AddPersistent(
            List<GameObject> collection,
            string namePrefix,
            GameObject prefab,
            WisadelFxSlot slot,
            float scale)
        {
            var enabled = IsFxEnabled(slot);
            if (prefab == null || actorMount == null || !enabled)
            {
                return;
            }

            var instance = Instantiate(prefab, actorMount, false);
            instance.name = namePrefix + slot;
            collection.Add(instance);
            ApplyActorTransform(instance, ResolveActorBaseOffset(), slot, scale);
            ApplyPersistentLayering(instance, slot);

            var playback = instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            if (playback == null)
                return;

            var animator = playback.GetComponent<Animator>() ??
                           playback.GetComponentInChildren<Animator>(true);
            playback.Configure(
                animator,
                playback.Duration,
                true,
                ExtractedFrameFxPlayback.DefaultPlaybackSpeed);
            playback.PlayFromStart();
        }

        private void RefreshPersistentTransforms()
        {
            RefreshPersistentTransforms(
                _skill2Persistent,
                "WisadelSkill2Persistent_",
                skill2Scale);
            RefreshPersistentTransforms(
                _skill3Persistent,
                "WisadelSkill3Persistent_",
                skill3Scale);
        }

        private void RefreshPersistentTransforms(
            List<GameObject> collection,
            string namePrefix,
            float scale)
        {
            for (var i = 0; i < collection.Count; i++)
            {
                var instance = collection[i];
                if (instance == null)
                    continue;

                if (!System.Enum.TryParse(
                        instance.name.Replace(namePrefix, string.Empty),
                        out WisadelFxSlot slot))
                    continue;

                ApplyActorTransform(instance, ResolveActorBaseOffset(), slot, scale);
            }
        }

        private void ClearSkill2Persistent() =>
            ClearPersistent(_skill2Persistent);

        private void ClearSkill3Persistent() =>
            ClearPersistent(_skill3Persistent);

        private void ClearPersistent(List<GameObject> collection)
        {
            for (var i = collection.Count - 1; i >= 0; i--)
            {
                if (collection[i] != null)
                    Destroy(collection[i]);
            }

            collection.Clear();
        }

        private void StartTrailFlight(
            GameObject prefab,
            Transform movingTarget,
            Vector3 destination,
            WisadelFxSlot slot,
            float scale,
            float flightSeconds)
        {
            var enabled = IsFxEnabled(slot);
            if (prefab == null || !enabled)
            {
                return;
            }


            StartCoroutine(FlyTrail(
                prefab,
                movingTarget,
                destination,
                slot,
                scale,
                Mathf.Max(0.02f, flightSeconds),
                GetStartDelaySeconds(slot)));
        }

        private IEnumerator FlyTrail(
            GameObject prefab,
            Transform movingTarget,
            Vector3 destination,
            WisadelFxSlot slot,
            float scale,
            float flightSeconds,
            float startDelaySeconds)
        {
            var delay = Mathf.Clamp(startDelaySeconds, 0f, Mathf.Max(0f, flightSeconds - 0.02f));
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (movingTarget != null)
                destination = movingTarget.TransformPoint(targetLocalOffset);

            // Delay only shifts the visible launch. Keep the authored/gameplay impact moment aligned
            // by consuming the delay from the remaining visual flight time.
            flightSeconds = Mathf.Max(0.02f, flightSeconds - delay);
            var origin = ResolveTrailOrigin(slot);
            var instance = Instantiate(prefab, origin, Quaternion.identity);
            instance.name = "WisadelProjectile_" + slot;
            instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale * GetScaleMultiplier(slot));
            _active.Add(instance);

            // Projectile orientation is driven by its real world flight vector. Keeping the
            // billboard component enabled would continually overwrite that rotation.
            var billboards = instance.GetComponentsInChildren<BillboardPresentation25D>(true);
            for (var i = 0; i < billboards.Length; i++)
                billboards[i].enabled = false;

            var playback = instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            if (playback != null)
            {
                // timing.json marks the trail particle stack as looping. The frame capture is
                // longer than one projectile flight because it records that loop; it must not be
                // time-compressed to fit the travel time. Keep authored FPS and recycle on impact.
                playback.SetPlaybackSpeed(ExtractedFrameFxPlayback.DefaultPlaybackSpeed);
                playback.PlayFromStart();
            }

            var elapsed = 0f;
            while (instance != null && elapsed < flightSeconds)
            {
                elapsed += Time.deltaTime;
                if (movingTarget != null)
                    destination = movingTarget.TransformPoint(targetLocalOffset);

                var progress = Mathf.Clamp01(elapsed / flightSeconds);
                var current = Vector3.Lerp(origin, destination, progress);
                instance.transform.position = current;
                OrientTrail(instance.transform, destination - origin);
                yield return null;
            }

            if (instance != null)
            {
                instance.transform.position = destination;
                _active.Remove(instance);
                Destroy(instance);
            }
        }

        private Vector3 ResolveTrailOrigin(WisadelFxSlot slot)
        {
            var anchor = actorMount != null ? actorMount : transform;
            var offset = GetOffset(slot);
            var camera = Camera.main;
            var worldOffset = camera != null
                ? camera.transform.right * offset.x + camera.transform.up * offset.y
                : new Vector3(offset.x, offset.y, 0f);
            return anchor.position + worldOffset;
        }

        private Vector3 ResolveForwardMissDestination()
        {
            var forward = _motor != null ? _motor.PlanarForward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;
            return (actorMount != null ? actorMount.position : transform.position) +
                   forward.normalized * 10f;
        }

        private static void OrientTrail(Transform visual, Vector3 worldDirection)
        {
            if (visual == null || worldDirection.sqrMagnitude < 0.0001f)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            var normal = -camera.transform.forward.normalized;
            var along = Vector3.ProjectOnPlane(worldDirection, normal);
            if (along.sqrMagnitude < 0.0001f)
                along = camera.transform.right;
            along.Normalize();

            var up = Vector3.Cross(normal, along);
            if (up.sqrMagnitude < 0.0001f)
                up = camera.transform.up;
            visual.rotation = Quaternion.LookRotation(normal, up.normalized);
        }

        private void SpawnOnActor(
            GameObject prefab,
            WisadelFxSlot slot,
            Vector3 baseOffset,
            float scale) =>
            SpawnAttached(prefab, actorMount, baseOffset, slot, scale);

        private void SpawnAttached(
            GameObject prefab,
            Transform anchor,
            Vector3 baseOffset,
            WisadelFxSlot slot,
            float scale)
        {
            var enabled = IsFxEnabled(slot);
            if (prefab == null || anchor == null || !enabled)
            {
                return;
            }

            var instance = Instantiate(prefab, anchor, false);
            ApplyActorTransform(instance, baseOffset, slot, scale);
            PlayAndRecycle(instance);
        }

        private void ApplyActorTransform(
            GameObject instance,
            Vector3 baseOffset,
            WisadelFxSlot slot,
            float scale)
        {
            if (instance == null)
                return;

            instance.transform.localPosition = baseOffset + GetOffset(slot);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale * GetScaleMultiplier(slot));
            ApplyFacing(instance);
        }

        private void SpawnSkill3ImpactAtWorld(
            GameObject prefab,
            Vector3 impactPoint,
            WisadelFxSlot slot,
            float scale,
            float cameraDepthBias)
        {
            var enabled = IsFxEnabled(slot);
            if (prefab == null || !enabled)
            {
                Debug.Log(
                    $"[WisadelS3FxDiag/Spawn] SKIP slot={slot} prefab={PrefabName(prefab)} enabled={enabled}",
                    this);
                return;
            }

            var camera = Camera.main;
            var offset = GetOffset(slot);
            var worldOffset = camera != null
                ? camera.transform.right * offset.x + camera.transform.up * offset.y
                : new Vector3(offset.x, offset.y, 0f);
            var depthOffset = camera != null
                ? -camera.transform.forward * Mathf.Max(0f, cameraDepthBias)
                : Vector3.zero;
            var spawnPosition = impactPoint + worldOffset + depthOffset;

            Debug.Log(
                $"[WisadelS3FxDiag/Spawn] REQUEST slot={slot} prefab={prefab.name} impact={impactPoint} " +
                $"offset={offset} depthBias={cameraDepthBias:F3} spawn={spawnPosition} " +
                $"camera={(camera != null ? camera.name : "null")} " +
                $"viewport={(camera != null ? camera.WorldToViewportPoint(spawnPosition).ToString() : "n/a")}",
                this);

            var instance = Instantiate(prefab, spawnPosition, Quaternion.identity);
            instance.name = $"WisadelS3Impact_{slot}";
            instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale * GetScaleMultiplier(slot));

            // These are flattened captures of an originally additive particle stack. The generic
            // billboard's directional yaw plus pure One/One blending can make the dark red/blue
            // capture nearly disappear against the bright 2.5D stage. Face it exactly to the
            // gameplay camera and use a premultiplied-style blend that preserves contrast.
            var billboards = instance.GetComponentsInChildren<BillboardPresentation25D>(true);
            for (var i = 0; i < billboards.Length; i++)
                billboards[i].enabled = false;

            if (camera != null)
                instance.transform.rotation = Quaternion.LookRotation(
                    -camera.transform.forward,
                    camera.transform.up);

            ApplyFacing(instance);

            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 140);
                renderer.color = Color.white;
            }

            var playback = instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            var animator = instance.GetComponentInChildren<Animator>(true);
            Debug.Log(
                $"[WisadelS3FxDiag/Spawn] CREATED slot={slot} active={instance.activeInHierarchy} " +
                $"renderers={renderers.Length} playback={(playback != null)} " +
                $"playbackDuration={(playback != null ? playback.Duration.ToString("F3") : "n/a")} " +
                $"animator={(animator != null)} animatorEnabled={(animator != null && animator.enabled)} " +
                $"billboards={billboards.Length} scale={instance.transform.lossyScale}",
                instance);

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                Debug.Log(
                    $"[WisadelS3FxDiag/Renderer] slot={slot} i={i} enabled={renderer.enabled} " +
                    $"active={renderer.gameObject.activeInHierarchy} sprite={(renderer.sprite != null ? renderer.sprite.name : "NULL")} " +
                    $"sortingLayer={renderer.sortingLayerName} order={renderer.sortingOrder} " +
                    $"alpha={renderer.color.a:F2} bounds={renderer.bounds}",
                    renderer);
            }

            PlayAndRecycle(instance);
            StartCoroutine(LogSkill3ImpactNextFrame(instance, slot));
        }

        private IEnumerator LogSkill3ImpactNextFrame(GameObject instance, WisadelFxSlot slot)
        {
            yield return null;
            if (instance == null)
            {
                Debug.Log($"[WisadelS3FxDiag/NextFrame] slot={slot} instance DESTROYED after one frame", this);
                yield break;
            }

            var camera = Camera.main;
            var viewport = camera != null
                ? camera.WorldToViewportPoint(instance.transform.position).ToString()
                : "n/a";
            var playback = instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            var animator = instance.GetComponentInChildren<Animator>(true);
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);

            Debug.Log(
                $"[WisadelS3FxDiag/NextFrame] slot={slot} active={instance.activeInHierarchy} pos={instance.transform.position} " +
                $"viewport={viewport} renderers={renderers.Length} playback={(playback != null)} " +
                $"animator={(animator != null)} animatorEnabled={(animator != null && animator.enabled)} " +
                $"animatorSpeed={(animator != null ? animator.speed.ToString("F2") : "n/a")}",
                instance);

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                Debug.Log(
                    $"[WisadelS3FxDiag/NextFrameRenderer] slot={slot} i={i} enabled={renderer.enabled} " +
                    $"sprite={(renderer.sprite != null ? renderer.sprite.name : "NULL")} alpha={renderer.color.a:F2} " +
                    $"bounds={renderer.bounds}",
                    renderer);
            }
        }

        private static string PrefabName(GameObject prefab) =>
            prefab != null ? prefab.name : "NULL";

        private void SpawnAtWorld(
            GameObject prefab,
            Vector3 center,
            WisadelFxSlot slot,
            float scale)
        {
            if (prefab == null || !IsFxEnabled(slot))
                return;

            var camera = Camera.main;
            var offset = GetOffset(slot);
            var worldOffset = camera != null
                ? camera.transform.right * offset.x + camera.transform.up * offset.y
                : new Vector3(offset.x, offset.y, 0f);
            var instance = Instantiate(prefab, center + worldOffset, Quaternion.identity);
            instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale * GetScaleMultiplier(slot));
            ApplyFacing(instance);
            PlayAndRecycle(instance);
        }

        private void ReconcilePersistentFx()
        {
            ReconcilePersistentSlot(
                _skill2Persistent,
                "WisadelSkill2Persistent_",
                skill2Buff,
                WisadelFxSlot.Skill2Buff,
                skill2Scale,
                _skill2 != null && _skill2.IsActive);
            ReconcilePersistentSlot(
                _skill2Persistent,
                "WisadelSkill2Persistent_",
                skill2Buff02,
                WisadelFxSlot.Skill2Buff02,
                skill2Scale,
                _skill2 != null && _skill2.IsActive);

            var skill3Active = _skill3 != null && _skill3.IsActive;
            ReconcilePersistentSlot(
                _skill3Persistent,
                "WisadelSkill3Persistent_",
                skill3BuffBack,
                WisadelFxSlot.Skill3BuffBack,
                skill3Scale,
                skill3Active);
            ReconcilePersistentSlot(
                _skill3Persistent,
                "WisadelSkill3Persistent_",
                skill3BuffFront,
                WisadelFxSlot.Skill3BuffFront,
                skill3Scale,
                skill3Active);
            ReconcilePersistentSlot(
                _skill3Persistent,
                "WisadelSkill3Persistent_",
                skill3Buff02Back,
                WisadelFxSlot.Skill3Buff02Back,
                skill3Scale,
                skill3Active);
            ReconcilePersistentSlot(
                _skill3Persistent,
                "WisadelSkill3Persistent_",
                skill3Buff02Front,
                WisadelFxSlot.Skill3Buff02Front,
                skill3Scale,
                skill3Active);
        }

        private void ReconcilePersistentSlot(
            List<GameObject> collection,
            string namePrefix,
            GameObject prefab,
            WisadelFxSlot slot,
            float scale,
            bool lifecycleActive)
        {
            var expectedName = namePrefix + slot;
            GameObject existing = null;
            for (var i = collection.Count - 1; i >= 0; i--)
            {
                var instance = collection[i];
                if (instance == null)
                {
                    collection.RemoveAt(i);
                    continue;
                }

                if (instance.name == expectedName)
                    existing = instance;
            }

            if (!lifecycleActive || !IsFxEnabled(slot))
            {
                if (existing != null)
                {
                    collection.Remove(existing);
                    Destroy(existing);
                }
                return;
            }

            if (existing == null)
                AddPersistent(collection, namePrefix, prefab, slot, scale);
        }

        private Vector3 ResolveActorBaseOffset()
        {
            // Prototype factory places CustomFxMountPoint at the actor's authored body height.
            // Applying actorLocalOffset again would shift starts/buffs upward a second time.
            return actorMount != null && actorMount != transform
                ? Vector3.zero
                : actorLocalOffset;
        }

        private static void ApplyPersistentLayering(GameObject instance, WisadelFxSlot slot)
        {
            if (instance == null)
                return;

            var sortingOrder = slot switch
            {
                WisadelFxSlot.Skill3BuffBack => 25,
                WisadelFxSlot.Skill3Buff02Back => 25,
                WisadelFxSlot.Skill3BuffFront => 35,
                WisadelFxSlot.Skill3Buff02Front => 35,
                _ => 90
            };

            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder = sortingOrder;
        }

        private bool IsFxEnabled(WisadelFxSlot slot)
        {
            if (_tuning == null)
                RefreshTuning();

            var setting = _tuning != null ? _tuning.Get(slot) : null;
            return setting == null || setting.Enabled;
        }

        private Vector3 GetOffset(WisadelFxSlot slot)
        {
            if (_tuning == null)
                RefreshTuning();
            var setting = _tuning != null ? _tuning.Get(slot) : null;
            var facing = _motor != null ? _motor.FacingSign : 1;
            return setting != null ? (Vector3)setting.GetOffset(facing) : Vector3.zero;
        }

        private float GetScaleMultiplier(WisadelFxSlot slot)
        {
            if (_tuning == null)
                RefreshTuning();
            var setting = _tuning != null ? _tuning.Get(slot) : null;
            return setting != null ? setting.ScaleMultiplier : 1f;
        }

        private float GetStartDelaySeconds(WisadelFxSlot slot)
        {
            if (_tuning == null)
                RefreshTuning();
            var setting = _tuning != null ? _tuning.Get(slot) : null;
            return setting != null ? setting.StartDelaySeconds : 0f;
        }

        private void ApplyFacing(GameObject instance)
        {
            var flip = _motor != null && _motor.FacingSign < 0;
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                renderers[i].flipX = flip;
        }

        private void PlayAndRecycle(GameObject instance)
        {
            _active.Add(instance);
            var playback = instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            if (instance != null && instance.name.StartsWith("WisadelS3Impact_"))
            {
                Debug.Log(
                    $"[WisadelS3FxDiag/Playback] instance={instance.name} playback={(playback != null)} " +
                    $"loop={(playback != null && playback.Loop)} effectiveDuration={(playback != null ? playback.EffectiveDuration.ToString("F3") : "n/a")}",
                    instance);
            }

            if (playback != null)
            {
                playback.PlayFromStart();
                StartCoroutine(RecycleAfter(instance, playback.Loop
                    ? fallbackLifetime
                    : Mathf.Max(0.05f, playback.EffectiveDuration)));
            }
            else
            {
                StartCoroutine(RecycleAfter(instance, fallbackLifetime));
            }
        }

        private IEnumerator RecycleAfter(GameObject instance, float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, seconds));
            if (instance != null)
            {
                _active.Remove(instance);
                Destroy(instance);
            }
        }

        private void ClearActive()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
                if (_active[i] != null)
                    Destroy(_active[i]);
            _active.Clear();
        }
    }
}
