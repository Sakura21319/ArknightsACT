using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController), typeof(SchwarzSkill1), typeof(SchwarzSkill2))]
    public sealed class SchwarzExtractedFxController : MonoBehaviour
    {
        [Header("Basic attack")]
        [SerializeField] private GameObject basicStart;
        [SerializeField] private GameObject basicTrail;
        [SerializeField] private GameObject basicHit;

        [Header("S2 composite common buff")]
        [SerializeField] private GameObject skill2IgniteRed;
        [SerializeField] private GameObject skill2Combustion;

        [Header("S3 / gameplay slot 2")]
        [SerializeField] private GameObject skill3Start;
        [SerializeField] private GameObject skill3Trail;
        [SerializeField] private GameObject skill3Hit;
        [SerializeField] private GameObject skill3Buff02;
        [SerializeField] private GameObject skill3Buff03;

        [Header("Base placement")]
        [SerializeField] private Transform actorMount;
        [SerializeField] private Vector3 actorLocalOffset = new(0f, 0.55f, 0f);
        [SerializeField] private Vector3 projectileLocalOffset = new(0.22f, -0.04f, 0f);
        [SerializeField] private Vector3 basicProjectileLocalOffset = new(0.22f, 0.04f, 0f);
        [SerializeField] private Vector3 targetLocalOffset = new(0f, 0.75f, 0f);
        [SerializeField, Min(0.1f)] private float basicScale = 3.2f;
        [SerializeField, Min(0.1f)] private float skill3Scale = 3.2f;
        [SerializeField, Min(1f)] private float basicProjectileSpeed = 22f;
        [SerializeField, Min(1f)] private float skill3ProjectileSpeed = 30f;
        [SerializeField, Min(0.02f)] private float minimumProjectileSeconds = 0.08f;
        [SerializeField, Min(0.02f)] private float maximumProjectileSeconds = 0.38f;
        [SerializeField, Min(0.1f)] private float fallbackLifetime = 2f;
        [SerializeField, Range(0.05f, 0.9f)] private float skill3Buff03LoopStartNormalized = 0.20f;
        [SerializeField, Range(0.1f, 0.995f)] private float skill3Buff03LoopEndNormalized = 0.98f;
        [SerializeField, Min(0.1f)] private float basicTracerLength = 0.72f;
        [SerializeField, Min(0.1f)] private float skill2TracerLength = 0.88f;
        [SerializeField, Min(0.1f)] private float skill3TracerLength = 1.55f;
        [SerializeField, Min(0.01f)] private float basicTracerWidth = 0.040f;
        [SerializeField, Min(0.01f)] private float skill2TracerWidth = 0.055f;
        [SerializeField, Min(0.01f)] private float skill3TracerWidth = 0.120f;
        [SerializeField] private Color basicTracerTailColor = new(0.55f, 0.72f, 1f, 0.24f);
        [SerializeField] private Color basicTracerTipColor = new(0.92f, 0.97f, 1f, 1f);
        [SerializeField] private Color skill2TracerTailColor = new(1f, 0.20f, 0.06f, 0.28f);
        [SerializeField] private Color skill2TracerTipColor = new(1f, 0.72f, 0.22f, 1f);
        [SerializeField] private Color skill3TracerTailColor = new(0.58f, 0.68f, 1f, 0.62f);
        [SerializeField] private Color skill3TracerTipColor = new(1f, 0.94f, 1f, 1f);

        private readonly List<GameObject> _active = new();
        private PlayerAttackController _attacks;
        private SchwarzSkill1 _skill2;
        private SchwarzSkill2 _skill3;
        private SchwarzRangedBasicAttack _ranged;
        private SchwarzSniperModeController _sniper;
        private PlayerMotor25D _motor25D;
        private PlayerMotor2D _motor2D;
        private SchwarzFxTuningProfile _tuning;

        private GameObject _skill2IgniteInstance;
        private GameObject _skill2CombustionInstance;
        private GameObject _skill3Buff02Instance;
        private GameObject _skill3Buff03Instance;
        private bool _sniperSubscribed;

        public void Configure(
            GameObject basicStartValue,
            GameObject basicTrailValue,
            GameObject basicHitValue,
            GameObject skill2IgniteRedValue,
            GameObject skill2CombustionValue,
            GameObject skill3StartValue,
            GameObject skill3TrailValue,
            GameObject skill3HitValue,
            GameObject skill3Buff02Value,
            GameObject skill3Buff03Value)
        {
            basicStart = basicStartValue;
            basicTrail = basicTrailValue;
            basicHit = basicHitValue;
            skill2IgniteRed = skill2IgniteRedValue;
            skill2Combustion = skill2CombustionValue;
            skill3Start = skill3StartValue;
            skill3Trail = skill3TrailValue;
            skill3Hit = skill3HitValue;
            skill3Buff02 = skill3Buff02Value;
            skill3Buff03 = skill3Buff03Value;
        }

        public void ReloadTuning()
        {
            _tuning = Resources.Load<SchwarzFxTuningProfile>(SchwarzFxTuningProfile.ResourcePath);
        }

        public void ApplySavedTuning()
        {
            ReloadTuning();

            if (!Application.isPlaying)
                return;

            if (_skill2 != null && _skill2.IsBuffActive)
            {
                StopSkill2Visuals();
                OnSkill2BuffStarted();
            }

            if (_skill3 != null && _skill3.IsBuffActive)
            {
                StopSkill3PersistentVisuals();
                _skill3Buff02Instance = ActivatePersistent(
                    _skill3Buff02Instance,
                    skill3Buff02,
                    skill3Scale,
                    SchwarzFxSlot.Skill3Buff02);
                _skill3Buff03Instance = ActivatePersistent(
                    _skill3Buff03Instance,
                    skill3Buff03,
                    skill3Scale,
                    SchwarzFxSlot.Skill3Buff03);
                if (_skill3Buff03Instance != null)
                    StartCoroutine(LoopPersistentVisibleRange(
                        _skill3Buff03Instance,
                        skill3Buff03LoopStartNormalized,
                        skill3Buff03LoopEndNormalized));
            }
        }

        private void Awake()
        {
            _attacks = GetComponent<PlayerAttackController>();
            _skill2 = GetComponent<SchwarzSkill1>();
            _skill3 = GetComponent<SchwarzSkill2>();
            _ranged = GetComponent<SchwarzRangedBasicAttack>();
            _sniper = GetComponent<SchwarzSniperModeController>();
            _motor25D = GetComponent<PlayerMotor25D>();
            _motor2D = GetComponent<PlayerMotor2D>();
            ReloadTuning();

            actorMount = actorMount != null ? actorMount : transform.Find("CustomFxMountPoint");
            if (actorMount == null)
                actorMount = transform;

            PrewarmPersistentVisuals();

        }

        private void OnEnable()
        {
            if (_attacks != null)
            {
                _attacks.AttackStarted += OnAttackStarted;
                _attacks.AttackHit += OnAttackHit;
            }

            if (_skill2 != null)
            {
                _skill2.CastStarted += OnSkill2Started;
                _skill2.BuffStarted += OnSkill2BuffStarted;
                _skill2.BuffEnded += OnSkill2BuffEnded;
            }

            if (_skill3 != null)
            {
                _skill3.CastStarted += OnSkill3Started;
                _skill3.BuffStarted += OnSkill3BuffStarted;
                _skill3.BuffEnded += OnSkill3BuffEnded;
            }

            BindSniper();
        }

        private void Update()
        {
            RefreshPersistentFacing();
        }

        private void OnDisable()
        {
            if (_attacks != null)
            {
                _attacks.AttackStarted -= OnAttackStarted;
                _attacks.AttackHit -= OnAttackHit;
            }

            if (_skill2 != null)
            {
                _skill2.CastStarted -= OnSkill2Started;
                _skill2.BuffStarted -= OnSkill2BuffStarted;
                _skill2.BuffEnded -= OnSkill2BuffEnded;
            }

            if (_skill3 != null)
            {
                _skill3.CastStarted -= OnSkill3Started;
                _skill3.BuffStarted -= OnSkill3BuffStarted;
                _skill3.BuffEnded -= OnSkill3BuffEnded;
            }

            UnbindSniper();
            StopSkill2Visuals();
            StopSkill3PersistentVisuals();
            ClearInstances();
        }

        private void BindSniper()
        {
            if (_sniperSubscribed)
                return;

            if (_sniper == null)
                _sniper = GetComponent<SchwarzSniperModeController>();
            if (_sniper == null)
                return;

            _sniper.ShotFired += OnSniperShotFired;
            _sniperSubscribed = true;
        }

        private void UnbindSniper()
        {
            if (!_sniperSubscribed || _sniper == null)
                return;
            _sniper.ShotFired -= OnSniperShotFired;
            _sniperSubscribed = false;
        }

        private void OnAttackStarted(int _)
        {
            BindSniper();

            // During S3, generic AttackStarted is deliberately visual-silent. The projectile
            // is spawned only by SchwarzSniperModeController.ShotFired after a real mouse click.
            if (_skill3 != null && _skill3.IsBuffActive)
                return;

            SpawnOnActor(basicStart, basicScale, SchwarzFxSlot.BasicStart);

            var projectileSlot = _skill2 != null && _skill2.IsBuffActive
                ? SchwarzFxSlot.Skill2Trail
                : SchwarzFxSlot.BasicTrail;

            CombatEntity target = null;
            if (_ranged != null)
                _ranged.TryGetAimTarget(out target);

            if (target != null)
            {
                StartCoroutine(FlyProjectile(
                    basicTrail,
                    target.transform,
                    target.transform.TransformPoint(targetLocalOffset),
                    basicScale,
                    basicProjectileSpeed,
                    projectileSlot));
                return;
            }

            var missDestination = _ranged != null
                ? _ranged.GetMissDestination()
                : transform.position + Vector3.up * 0.76f +
                  (_motor25D != null ? _motor25D.PlanarForward : Vector3.right) * 12f;
            StartCoroutine(FlyProjectile(
                basicTrail,
                null,
                missDestination,
                basicScale,
                basicProjectileSpeed,
                projectileSlot));
        }

        private void OnSniperShotFired(CombatEntity target)
        {
            if (target == null || _skill3 == null || !_skill3.IsBuffActive)
                return;

            // skill_03_start_* is the authored S3 shot/muzzle effect. It must only play
            // after an actual aimed click succeeds; entering S3 never plays an attack FX.
            SpawnOnActor(skill3Start, skill3Scale, SchwarzFxSlot.Skill3Start);

            StartCoroutine(FlyProjectile(
                skill3Trail,
                target.transform,
                target.transform.TransformPoint(targetLocalOffset),
                skill3Scale,
                skill3ProjectileSpeed,
                SchwarzFxSlot.Skill3Trail));
        }

        private void OnAttackHit(CombatEntity target)
        {
            if (target == null)
                return;

            if (_skill3 != null && _skill3.IsBuffActive)
            {
                // S3 reuses the authored skill_01_hit variant for impact presentation.
                SpawnAtTarget(skill3Hit, target.transform, skill3Scale, SchwarzFxSlot.Skill3Hit);
                return;
            }

            SpawnAtTarget(basicHit, target.transform, basicScale, SchwarzFxSlot.BasicHit);
        }

        private void OnSkill2Started()
        {
            // S2 visual starts when the buff becomes active, not during the startup frame.
        }

        private void OnSkill2BuffStarted()
        {
            StopSkill2Visuals();
            _skill2IgniteInstance = ActivatePersistent(
                _skill2IgniteInstance,
                skill2IgniteRed,
                basicScale,
                SchwarzFxSlot.Skill2IgniteRed);
            _skill2CombustionInstance = ActivatePersistent(
                _skill2CombustionInstance,
                skill2Combustion,
                basicScale,
                SchwarzFxSlot.Skill2Combustion);
        }

        private void OnSkill2BuffEnded()
        {
            StopSkill2Visuals();
        }

        private void OnSkill3Started()
        {
            // Entering S3 only enters sniper stance. No attack FX is allowed here.
        }

        private void OnSkill3BuffStarted()
        {
            StopSkill3PersistentVisuals();

            // Both skill_03_buff_02_* and skill_03_buff_03_* are sustained S3 layers.
            // They loop for the full sniper stance and are removed only when S3 ends.
            _skill3Buff02Instance = ActivatePersistent(
                _skill3Buff02Instance,
                skill3Buff02,
                skill3Scale,
                SchwarzFxSlot.Skill3Buff02);
            _skill3Buff03Instance = ActivatePersistent(
                _skill3Buff03Instance,
                skill3Buff03,
                skill3Scale,
                SchwarzFxSlot.Skill3Buff03);
            if (_skill3Buff03Instance != null)
                StartCoroutine(LoopPersistentVisibleRange(
                    _skill3Buff03Instance,
                    skill3Buff03LoopStartNormalized,
                    skill3Buff03LoopEndNormalized));
        }

        private void OnSkill3BuffEnded()
        {
            StopSkill3PersistentVisuals();
        }

        private IEnumerator FlyProjectile(
            GameObject prefab,
            Transform target,
            Vector3 destination,
            float baseScale,
            float speed,
            SchwarzFxSlot slot)
        {
            var setting = GetSetting(slot);
            var facing = GetFacingSign();
            // S3 uses its own slightly lower muzzle point; basic/S2 keep their existing anchor.
            var originOffset = slot == SchwarzFxSlot.Skill3Trail
                ? projectileLocalOffset
                : basicProjectileLocalOffset;
            originOffset.x *= facing;

            // Projectile offsets are also directional. This is especially important for S3:
            // right-facing values can remain untouched while left-facing muzzle alignment is
            // tuned independently in the Schwarz FX window.
            var tunedOffset = setting.GetOffset(facing);
            originOffset += new Vector3(tunedOffset.x, tunedOffset.y, 0f);

            var origin = actorMount != null
                ? actorMount.TransformPoint(originOffset)
                : transform.TransformPoint(originOffset);

            var flightRoot = new GameObject("SchwarzTracer_" + slot);
            flightRoot.transform.position = origin;
            _active.Add(flightRoot);

            var line = flightRoot.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.positionCount = 2;
            line.sortingOrder = 96;

            var directionalScale = setting.GetScale(facing);
            var isSkill2 = slot == SchwarzFxSlot.Skill2Trail;
            var isSkill3 = slot == SchwarzFxSlot.Skill3Trail;
            var hasAuthoredSkill3Arrow = isSkill3 && prefab != null;
            var widthScale = Mathf.Sqrt(Mathf.Max(0.05f, directionalScale));
            var widthBase = isSkill3
                ? skill3TracerWidth
                : isSkill2
                    ? skill2TracerWidth
                    : basicTracerWidth;
            var width = widthBase * widthScale;
            line.startWidth = width * 0.22f;
            line.endWidth = width;
            line.startColor = isSkill3
                ? skill3TracerTailColor
                : isSkill2
                    ? skill2TracerTailColor
                    : basicTracerTailColor;
            line.endColor = isSkill3
                ? skill3TracerTipColor
                : isSkill2
                    ? skill2TracerTipColor
                    : basicTracerTipColor;

            // skill_03_trail is the S3 arrow. Do not layer the generic/basic tracer underneath it.
            line.enabled = !hasAuthoredSkill3Arrow;

            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            Material tracerMaterial = null;
            if (shader != null)
            {
                tracerMaterial = new Material(shader)
                {
                    name = "Schwarz_Tracer_Runtime"
                };
                if (tracerMaterial.HasProperty("_Color"))
                    tracerMaterial.SetColor("_Color", Color.white);
                if (tracerMaterial.HasProperty("_BaseColor"))
                    tracerMaterial.SetColor("_BaseColor", Color.white);
                line.sharedMaterial = tracerMaterial;
            }

            var distance = Vector3.Distance(origin, destination);
            var travelSeconds = Mathf.Clamp(
                distance / Mathf.Max(1f, speed),
                minimumProjectileSeconds,
                maximumProjectileSeconds);
            var elapsed = 0f;
            var tracerLengthBase = isSkill3
                ? skill3TracerLength
                : isSkill2
                    ? skill2TracerLength
                    : basicTracerLength;
            var tracerLength = tracerLengthBase * Mathf.Max(0.35f, directionalScale);

            // skill_03_trail is the complete authored S3 projectile visual.
            GameObject authoredProjectile = null;
            if (hasAuthoredSkill3Arrow)
            {
                authoredProjectile = CreateProjectileVisualLayer(
                    prefab,
                    flightRoot.transform,
                    "Schwarz_S3_PrimaryTrail",
                    97,
                    tracerLength,
                    setting,
                    facing,
                    Vector2.zero);
            }

            while (flightRoot != null && elapsed < travelSeconds)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / travelSeconds);
                if (target != null)
                    destination = target.TransformPoint(targetLocalOffset);

                var current = Vector3.Lerp(origin, destination, progress);
                var direction = destination - current;
                if (direction.sqrMagnitude < 0.0001f)
                    direction = destination - origin;
                if (direction.sqrMagnitude < 0.0001f)
                    direction = Vector3.right;
                direction.Normalize();

                // Tail -> tip. This is mathematically aligned to the real world-space shot path,
                // so left/right/up/down shots cannot inherit a wrong sprite-axis rotation.
                line.SetPosition(0, current - direction * tracerLength);
                line.SetPosition(1, current + direction * 0.08f);
                flightRoot.transform.position = current;

                if (authoredProjectile != null)
                    OrientProjectileVisual(
                        authoredProjectile.transform,
                        direction,
                        setting.GetAngle(facing));

                yield return null;
            }

            if (tracerMaterial != null)
                Destroy(tracerMaterial);

            if (flightRoot != null)
            {
                _active.Remove(flightRoot);
                Destroy(flightRoot);
            }
        }

        private static GameObject CreateProjectileVisualLayer(
            GameObject prefab,
            Transform parent,
            string objectName,
            int sortingOrder,
            float visualLength,
            SchwarzFxTuningSetting setting,
            int facing,
            Vector2 localOffset)
        {
            if (prefab == null || parent == null)
                return null;

            var instance = Instantiate(prefab, parent, false);
            instance.name = objectName;
            instance.transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            instance.transform.localRotation = Quaternion.identity;

            foreach (var billboard in instance.GetComponentsInChildren<BillboardPresentation25D>(true))
                billboard.enabled = false;
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, sortingOrder);
                renderer.color = new Color(1.65f, 1.65f, 1.65f, 1f);
            }

            var playback = instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            if (playback != null)
            {
                playback.SetPlaybackSpeed(ExtractedFrameFxPlayback.DefaultPlaybackSpeed);
                playback.PlayFromStart();
            }

            var spriteRenderer = instance.GetComponentInChildren<SpriteRenderer>(true);
            var spriteWidth = spriteRenderer != null && spriteRenderer.sprite != null
                ? Mathf.Max(0.01f, spriteRenderer.sprite.bounds.size.x)
                : 1f;
            var visualScale = Mathf.Clamp(
                (visualLength * 2.45f) / spriteWidth,
                0.35f,
                6.0f);
            instance.transform.localScale = Vector3.one * visualScale;
            return instance;
        }

        private static void OrientProjectileVisual(
            Transform visual,
            Vector3 worldDirection,
            float angleOffset)
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

            // Local +X follows the actual shot vector while local Z faces the camera.
            var up = Vector3.Cross(normal, along).normalized;
            var rotation = Quaternion.LookRotation(normal, up);
            if (Mathf.Abs(angleOffset) > 0.001f)
                rotation *= Quaternion.AngleAxis(angleOffset, Vector3.forward);
            visual.rotation = rotation;
        }

        private IEnumerator LoopPersistentVisibleRange(
            GameObject instance,
            float startNormalized,
            float endNormalized)
        {
            // buff_03 has an authored intro at the beginning. Repeating the entire 75-frame clip
            // causes the visible layer to vanish whenever it wraps back to that intro. Keep the
            // animation alive, but loop only the stable animated section after the intro.
            if (instance == null)
                yield break;

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
                yield break;

            startNormalized = Mathf.Clamp(startNormalized, 0.02f, 0.90f);
            endNormalized = Mathf.Clamp(endNormalized, startNormalized + 0.05f, 0.995f);

            // Let the authored entry play once.
            var playback = instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            var duration = playback != null
                ? Mathf.Max(0.1f, playback.EffectiveDuration)
                : 2.5f;
            yield return new WaitForSecondsRealtime(duration * startNormalized);

            if (instance == null || animator == null)
                yield break;

            animator.speed = playback != null
                ? playback.PlaybackSpeed
                : 1f;
            animator.Play(0, 0, startNormalized);
            animator.Update(0f);

            var previous = startNormalized;
            while (instance != null && animator != null && animator.enabled)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                var normalized = Mathf.Repeat(state.normalizedTime, 1f);

                // Reset just before the clip reaches the authored fade/intro boundary. Also catch
                // a wrap that happened between two frames.
                if (normalized >= endNormalized ||
                    (previous > endNormalized - 0.08f && normalized < startNormalized))
                {
                    animator.Play(state.fullPathHash, 0, startNormalized);
                    animator.Update(0f);
                    normalized = startNormalized;
                }

                previous = normalized;
                yield return null;
            }
        }

        private void SpawnOnActor(GameObject prefab, float baseScale, SchwarzFxSlot slot)
        {
            Spawn(prefab, actorMount, actorLocalOffset, baseScale, true, false, slot);
        }

        private void SpawnAtTarget(
            GameObject prefab,
            Transform target,
            float baseScale,
            SchwarzFxSlot slot)
        {
            Spawn(
                prefab,
                target != null ? target : actorMount,
                target != null ? targetLocalOffset : actorLocalOffset,
                baseScale,
                true,
                false,
                slot);
        }

        private void PrewarmPersistentVisuals()
        {
            _skill2IgniteInstance = PrewarmPersistent(
                _skill2IgniteInstance, skill2IgniteRed, basicScale, SchwarzFxSlot.Skill2IgniteRed);
            _skill2CombustionInstance = PrewarmPersistent(
                _skill2CombustionInstance, skill2Combustion, basicScale, SchwarzFxSlot.Skill2Combustion);
            _skill3Buff02Instance = PrewarmPersistent(
                _skill3Buff02Instance, skill3Buff02, skill3Scale, SchwarzFxSlot.Skill3Buff02);
            _skill3Buff03Instance = PrewarmPersistent(
                _skill3Buff03Instance, skill3Buff03, skill3Scale, SchwarzFxSlot.Skill3Buff03);
        }

        private GameObject PrewarmPersistent(
            GameObject instance,
            GameObject prefab,
            float baseScale,
            SchwarzFxSlot slot)
        {
            if (instance != null || prefab == null || actorMount == null)
                return instance;

            instance = Instantiate(prefab, actorMount, false);
            ApplyActorFxTransform(instance, actorLocalOffset, baseScale, slot, true, GetFacingSign());
            PreparePlayback(instance, prefab, persistent: true);
            instance.SetActive(false);
            return instance;
        }

        private GameObject ActivatePersistent(
            GameObject instance,
            GameObject prefab,
            float baseScale,
            SchwarzFxSlot slot)
        {
            instance = PrewarmPersistent(instance, prefab, baseScale, slot);
            if (instance == null)
                return null;

            if (!instance.activeSelf)
                instance.SetActive(true);
            ApplyActorFxTransform(instance, actorLocalOffset, baseScale, slot, true, GetFacingSign());
            PreparePlayback(instance, prefab, persistent: true);
            return instance;
        }

        private GameObject Spawn(
            GameObject prefab,
            Transform anchor,
            Vector3 baseOffset,
            float baseScale,
            bool mirror,
            bool persistent,
            SchwarzFxSlot slot)
        {
            if (prefab == null || anchor == null)
                return null;

            var instance = Instantiate(prefab, anchor, false);
            var sign = mirror ? GetFacingSign() : 1;
            ApplyActorFxTransform(instance, baseOffset, baseScale, slot, mirror, sign);

            _active.Add(instance);
            PreparePlayback(instance, prefab, persistent);
            return instance;
        }

        private void RefreshPersistentFacing()
        {
            var sign = GetFacingSign();

            ApplyActorFxTransform(
                _skill2IgniteInstance,
                actorLocalOffset,
                basicScale,
                SchwarzFxSlot.Skill2IgniteRed,
                true,
                sign);
            ApplyActorFxTransform(
                _skill2CombustionInstance,
                actorLocalOffset,
                basicScale,
                SchwarzFxSlot.Skill2Combustion,
                true,
                sign);
            ApplyActorFxTransform(
                _skill3Buff02Instance,
                actorLocalOffset,
                skill3Scale,
                SchwarzFxSlot.Skill3Buff02,
                true,
                sign);
            ApplyActorFxTransform(
                _skill3Buff03Instance,
                actorLocalOffset,
                skill3Scale,
                SchwarzFxSlot.Skill3Buff03,
                true,
                sign);
        }

        private void ApplyActorFxTransform(
            GameObject instance,
            Vector3 baseOffset,
            float baseScale,
            SchwarzFxSlot slot,
            bool directional,
            int facingSign)
        {
            if (instance == null)
                return;

            var sign = directional && facingSign < 0 ? -1 : 1;
            var setting = GetSetting(slot);
            var tuningOffset = setting.GetOffset(sign);

            var offset = baseOffset;
            if (directional)
                offset.x *= sign;
            offset += new Vector3(tuningOffset.x, tuningOffset.y, 0f);

            instance.transform.localPosition = offset;
            instance.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                setting.GetAngle(sign));

            var finalScale = Mathf.Max(0.05f, baseScale * setting.GetScale(sign));
            instance.transform.localScale = new Vector3(finalScale, finalScale, 1f);

            if (!directional)
                return;

            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.flipX = sign < 0;
        }

        private SchwarzFxTuningSetting GetSetting(SchwarzFxSlot slot)
        {
            if (_tuning == null)
                ReloadTuning();
            return _tuning != null
                ? _tuning.Get(slot)
                : new SchwarzFxTuningSetting();
        }

        private void PreparePlayback(GameObject instance, GameObject sourcePrefab, bool persistent)
        {
            if (instance == null)
                return;

            var playback = instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            if (playback == null)
            {
                if (!persistent)
                    StartCoroutine(RecycleAfter(instance, fallbackLifetime));
                return;
            }

            var speed =
                sourcePrefab == basicStart || sourcePrefab == basicTrail || sourcePrefab == basicHit
                    ? ExtractedFrameFxPlayback.BasicAttackPlaybackSpeed
                    : ExtractedFrameFxPlayback.DefaultPlaybackSpeed;

            if (persistent)
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                playback.Configure(animator, playback.Duration, true, speed);
            }
            else
            {
                playback.SetPlaybackSpeed(speed);
            }

            playback.PlayFromStart();
            if (!persistent && !playback.Loop)
                StartCoroutine(RecycleAfter(instance, playback.EffectiveDuration));
        }

        private void StopSkill2Visuals()
        {
            DeactivatePersistent(_skill2IgniteInstance);
            DeactivatePersistent(_skill2CombustionInstance);
        }

        private void StopSkill3PersistentVisuals()
        {
            DeactivatePersistent(_skill3Buff02Instance);
            DeactivatePersistent(_skill3Buff03Instance);
        }

        private static void DeactivatePersistent(GameObject instance)
        {
            if (instance != null && instance.activeSelf)
                instance.SetActive(false);
        }

        private void DestroyTracked(ref GameObject instance)
        {
            if (instance == null)
                return;
            _active.Remove(instance);
            Destroy(instance);
            instance = null;
        }

        private int GetFacingSign()
        {
            if (_motor25D != null)
                return _motor25D.FacingSign < 0 ? -1 : 1;
            if (_motor2D != null)
                return _motor2D.FacingSign < 0 ? -1 : 1;
            return 1;
        }

        private IEnumerator RecycleAfter(GameObject instance, float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, seconds));
            if (instance == null)
                yield break;

            _active.Remove(instance);
            Destroy(instance);
        }

        private void ClearInstances()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
                if (_active[i] != null)
                    Destroy(_active[i]);
            _active.Clear();

            // Persistent buff layers are pooled children. Keep them allocated across operator
            // enable/disable cycles so entering S2/S3 never pays prefab/Animator setup cost.
            DeactivatePersistent(_skill2IgniteInstance);
            DeactivatePersistent(_skill2CombustionInstance);
            DeactivatePersistent(_skill3Buff02Instance);
            DeactivatePersistent(_skill3Buff03Instance);
        }
    }
}
