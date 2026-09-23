using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController), typeof(FrostNovaSkill1), typeof(FrostNovaSkill2))]
    public sealed class FrostNovaExtractedFxController : MonoBehaviour
    {
        [Header("Basic attack")]
        [SerializeField] private GameObject basicStart;
        [SerializeField] private GameObject basicTrail;
        [SerializeField] private GameObject basicHit;

        [Header("Skill 1 / ice cone")]
        [SerializeField] private GameObject skill1Range;
        [SerializeField] private GameObject skill1Buff;

        [Header("Skill 2 / frost field")]
        [SerializeField] private GameObject skill2Start;
        [SerializeField] private GameObject skill2Trail;
        [SerializeField] private GameObject skill2Range;
        [SerializeField] private GameObject skill2Range2;

        [Header("Winter actor-attached FX")]
        [SerializeField] private GameObject winterBuff03;
        [SerializeField] private GameObject winterBuff04;
        [SerializeField] private GameObject winterBuff05;

        [Header("Base placement")]
        [SerializeField] private Vector3 actorOffset = new(0f, 0.82f, 0f);
        [SerializeField] private Vector3 targetOffset = new(0f, 0.72f, 0f);
        [SerializeField, Min(0.1f)] private float actorScale = 3.0f;
        [SerializeField, Min(0.1f)] private float rangeScale = 4.5f;
        [SerializeField, Min(0.02f)] private float fallbackProjectileSeconds = 0.22f;
        [SerializeField, Min(0.1f)] private float fallbackLifetime = 3f;

        private readonly List<GameObject> _active = new();
        private PlayerAttackController _attacks;
        private FrostNovaRangedBasicAttack _ranged;
        private FrostNovaSkill1 _skill1;
        private FrostNovaSkill2 _skill2;
        private PlayerMotor25D _motor;
        private FrostNovaTuningProfile _tuning;
        [SerializeField] private FrostNovaSkinVariant _skin;
        private GameObject _persistentWinterBackFx;
        private Coroutine _persistentWinterBackFxLoop;

        public void Configure(
            FrostNovaSkinVariant skin,
            GameObject basicStartValue,
            GameObject basicTrailValue,
            GameObject basicHitValue,
            GameObject skill1RangeValue,
            GameObject skill1BuffValue,
            GameObject skill2StartValue,
            GameObject skill2TrailValue,
            GameObject skill2RangeValue,
            GameObject skill2Range2Value,
            GameObject winterBuff03Value = null,
            GameObject winterBuff04Value = null,
            GameObject winterBuff05Value = null)
        {
            _skin = skin;
            basicStart = basicStartValue;
            basicTrail = basicTrailValue;
            basicHit = basicHitValue;
            skill1Range = skill1RangeValue;
            skill1Buff = skill1BuffValue;
            skill2Start = skill2StartValue;
            skill2Trail = skill2TrailValue;
            skill2Range = skill2RangeValue;
            skill2Range2 = skill2Range2Value;
            winterBuff03 = winterBuff03Value;
            winterBuff04 = winterBuff04Value;
            winterBuff05 = winterBuff05Value;

            if (Application.isPlaying && isActiveAndEnabled)
                EnsurePersistentWinterBackFx();
        }

        private void Awake()
        {
            SyncSkinFromIdentity();
            _attacks = GetComponent<PlayerAttackController>();
            _ranged = GetComponent<FrostNovaRangedBasicAttack>();
            _skill1 = GetComponent<FrostNovaSkill1>();
            _skill2 = GetComponent<FrostNovaSkill2>();
            _motor = GetComponent<PlayerMotor25D>();
            RefreshTuning();
        }

        private void SyncSkinFromIdentity()
        {
            var identity = GetComponent<PlayableOperatorIdentity>();
            if (identity == null ||
                !string.Equals(
                    identity.OperatorId,
                    "FrostNova",
                    System.StringComparison.OrdinalIgnoreCase))
                return;

            _skin = FrostNovaSkinVariantExtensions.FromSkinId(identity.SkinId);
        }

        public void RefreshTuning()
        {
            _tuning = Resources.Load<FrostNovaTuningProfile>(
                FrostNovaTuningProfile.ResourcePath);

            if (Application.isPlaying && isActiveAndEnabled)
                EnsurePersistentWinterBackFx();
        }

        private void OnEnable()
        {
            SyncSkinFromIdentity();
            RefreshTuning();

            if (_attacks != null)
            {
                _attacks.AttackStarted += OnAttackStarted;
                _attacks.AttackHit += OnAttackHit;
            }

            if (_skill1 != null)
            {
                _skill1.CastStarted += OnSkill1Started;
                _skill1.Impact += OnSkill1Impact;
            }

            if (_skill2 != null)
            {
                _skill2.CastStarted += OnSkill2Started;
                _skill2.Pulse += OnSkill2Pulse;
                _skill2.CastEnded += OnSkill2Ended;
            }

            EnsurePersistentWinterBackFx();
        }

        private void Update()
        {
            if (_persistentWinterBackFx != null)
                ApplyPersistentWinterBackFxTransform();
        }

        private void OnDisable()
        {
            if (_attacks != null)
            {
                _attacks.AttackStarted -= OnAttackStarted;
                _attacks.AttackHit -= OnAttackHit;
            }

            if (_skill1 != null)
            {
                _skill1.CastStarted -= OnSkill1Started;
                _skill1.Impact -= OnSkill1Impact;
            }

            if (_skill2 != null)
            {
                _skill2.CastStarted -= OnSkill2Started;
                _skill2.Pulse -= OnSkill2Pulse;
                _skill2.CastEnded -= OnSkill2Ended;
            }

            StopAllCoroutines();
            DestroyPersistentWinterBackFx();
            ClearActive();
        }

        private void OnAttackStarted(int _)
        {
            ScheduleAttached(
                basicStart,
                transform,
                actorOffset,
                actorScale,
                FrostNovaFxSlot.BasicStart);

            var origin = transform.TransformPoint(actorOffset);
            if (_ranged != null &&
                _ranged.TryGetAimTarget(out var target) &&
                target != null)
            {
                StartCoroutine(FlyTuned(
                    basicTrail,
                    origin,
                    target.transform,
                    target.transform.TransformPoint(targetOffset),
                    actorScale,
                    FrostNovaFxSlot.BasicTrail));
                return;
            }

            var destination = _ranged != null
                ? _ranged.GetMissDestination()
                : transform.position + Vector3.right * 8f;
            StartCoroutine(FlyTuned(
                basicTrail,
                origin,
                null,
                destination,
                actorScale,
                FrostNovaFxSlot.BasicTrail));
        }

        private void OnAttackHit(CombatEntity target)
        {
            if (target == null)
                return;

            ScheduleWorld(
                basicHit,
                target.transform.TransformPoint(targetOffset),
                actorScale,
                FrostNovaFxSlot.BasicHit);
        }

        private void OnSkill1Started()
        {
            if (_skin.UsesWinterSkillSet())
            {
                SpawnEnabledWinterActorFxForSkill2();
                return;
            }

            ScheduleAttached(
                skill1Buff,
                transform,
                actorOffset,
                actorScale,
                FrostNovaFxSlot.DefaultSkill1Buff);
        }

        private void OnSkill1Impact(Vector3 center)
        {
            if (_skin.UsesWinterSkillSet())
            {
                var target = _skill1 != null ? _skill1.LockedTarget : null;
                if (target != null &&
                    target.Health != null &&
                    !target.Health.IsDead)
                {
                    ScheduleAttached(
                        skill1Range,
                        target.transform,
                        Vector3.zero,
                        rangeScale,
                        FrostNovaFxSlot.WinterSkill2Range);
                }
                else
                {
                    // Impact is raised only for a real hit. If this hit killed the target,
                    // its Health is already dead by the time presentation receives the event.
                    // Spawn at the captured impact center instead of dropping the hit FX.
                    ScheduleWorld(
                        skill1Range,
                        center,
                        rangeScale,
                        FrostNovaFxSlot.WinterSkill2Range);
                }

                return;
            }

            ScheduleWorld(
                skill1Range,
                center,
                rangeScale,
                FrostNovaFxSlot.DefaultSkill1Range);
        }

        private void OnSkill2Started()
        {
            if (_skin.UsesWinterSkillSet())
            {
                SetPersistentWinterBackFxVisible(false);
                ScheduleAttached(
                    skill2Start,
                    transform,
                    actorOffset,
                    actorScale,
                    FrostNovaFxSlot.WinterSkill3Start);
                SpawnEnabledWinterActorFxForSkill3();
                return;
            }

            ScheduleAttached(
                skill2Start,
                transform,
                actorOffset,
                actorScale,
                FrostNovaFxSlot.DefaultSkill2Start);
            ScheduleAttached(
                skill2Trail,
                transform,
                actorOffset,
                actorScale,
                FrostNovaFxSlot.DefaultSkill2Trail);
        }

        private void OnSkill2Ended()
        {
            if (_skin.UsesWinterSkillSet())
                SetPersistentWinterBackFxVisible(true);
        }

        private void OnSkill2Pulse(Vector3 center, int pulseIndex)
        {
            if (_skin.UsesWinterSkillSet())
            {
                // Winter Skill_3 visual sequence:
                // start FX is spawned on CastStarted;
                // first damage pulse schedules charge + the delayed second-stage burst.
                // Later damage pulses must not replay the charge FX.
                if (pulseIndex == 0)
                {
                    ScheduleWorld(
                        skill2Range,
                        center,
                        rangeScale,
                        FrostNovaFxSlot.WinterSkill3Range);
                    ScheduleWorld(
                        skill2Range2,
                        center,
                        rangeScale,
                        FrostNovaFxSlot.WinterSkill3Range2);
                }

                return;
            }

            ScheduleWorld(
                skill2Range,
                center,
                rangeScale,
                FrostNovaFxSlot.DefaultSkill2Range);

            if (pulseIndex == 0)
            {
                ScheduleWorld(
                    skill2Range2,
                    center,
                    rangeScale,
                    FrostNovaFxSlot.DefaultSkill2Range2);
            }
        }

        private void SpawnEnabledWinterActorFxForSkill2()
        {
            SpawnWinterActorFx(
                winterBuff03,
                FrostNovaFxSlot.WinterActorBuff03,
                2);
            SpawnWinterActorFx(
                winterBuff04,
                FrostNovaFxSlot.WinterActorBuff04,
                2);
        }

        private void SpawnEnabledWinterActorFxForSkill3()
        {
            SpawnWinterActorFx(
                winterBuff03,
                FrostNovaFxSlot.WinterActorBuff03,
                3);
            SpawnWinterActorFx(
                winterBuff04,
                FrostNovaFxSlot.WinterActorBuff04,
                3);
        }

        private void EnsurePersistentWinterBackFx()
        {
            if (!_skin.UsesWinterSkillSet() || winterBuff05 == null)
            {
                DestroyPersistentWinterBackFx();
                return;
            }

            var setting = GetTuning(FrostNovaFxSlot.WinterActorBuff05);
            if (_persistentWinterBackFx == null)
            {
                _persistentWinterBackFx = Instantiate(winterBuff05, transform, false);
                _persistentWinterBackFx.name = "FrostNova_Winter_BackFx_Persistent";
            }

            ApplyPersistentWinterBackFxTransform();

            var playback =
                _persistentWinterBackFx.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            if (playback == null)
                return;

            var animator =
                _persistentWinterBackFx.GetComponentInChildren<Animator>(true);
            var speed = setting != null
                ? Mathf.Max(0.05f, setting.playbackSpeed)
                : ExtractedFrameFxPlayback.DefaultPlaybackSpeed;
            playback.Configure(
                animator,
                playback.Duration,
                false,
                speed);
            playback.PlayFromStart();
            SetPersistentWinterBackFxVisible(!(_skill2?.IsCasting ?? false));

            if (_persistentWinterBackFxLoop != null)
                StopCoroutine(_persistentWinterBackFxLoop);
            _persistentWinterBackFxLoop = StartCoroutine(
                LoopPersistentWinterBackFx(
                    _persistentWinterBackFx,
                    playback));
        }

        private void ApplyPersistentWinterBackFxTransform()
        {
            if (_persistentWinterBackFx == null)
                return;

            var setting = GetTuning(FrostNovaFxSlot.WinterActorBuff05);
            var left = _motor != null && _motor.FacingSign < 0;
            var offset = setting != null
                ? left ? setting.leftOffset : setting.offset
                : Vector2.zero;
            var angle = setting != null
                ? left ? setting.leftAngleDegrees : setting.angleDegrees
                : 0f;
            var scale = setting != null
                ? Mathf.Max(0.05f, setting.scale)
                : 1f;

            _persistentWinterBackFx.transform.localPosition =
                actorOffset + new Vector3(offset.x, offset.y, 0f);
            _persistentWinterBackFx.transform.localScale =
                Vector3.one * actorScale * scale;
            _persistentWinterBackFx.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle);

            foreach (var renderer in
                     _persistentWinterBackFx.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.flipX = left;
        }

        private void SetPersistentWinterBackFxVisible(bool visible)
        {
            if (_persistentWinterBackFx == null)
                return;

            if (_persistentWinterBackFx.activeSelf != visible)
                _persistentWinterBackFx.SetActive(visible);

            if (!visible)
                return;

            ApplyPersistentWinterBackFxTransform();
            var playback =
                _persistentWinterBackFx.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            playback?.PlayFromStart();
        }

        private IEnumerator LoopPersistentWinterBackFx(
            GameObject instance,
            ExtractedFrameFxPlayback playback)
        {
            while (instance != null && playback != null)
            {
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(0.05f, playback.EffectiveDuration));
                if (instance == null || playback == null)
                    break;
                playback.PlayFromStart();
            }

            _persistentWinterBackFxLoop = null;
        }

        private void DestroyPersistentWinterBackFx()
        {
            if (_persistentWinterBackFxLoop != null)
                StopCoroutine(_persistentWinterBackFxLoop);
            _persistentWinterBackFxLoop = null;

            if (_persistentWinterBackFx == null)
                return;

            Destroy(_persistentWinterBackFx);
            _persistentWinterBackFx = null;
        }

        private void SpawnWinterActorFx(
            GameObject prefab,
            FrostNovaFxSlot slot,
            int skillNumber)
        {
            var setting = GetTuning(slot);
            if (setting == null ||
                !setting.enabled ||
                (setting.triggerSkill != 0 && setting.triggerSkill != skillNumber))
                return;

            ScheduleAttached(
                prefab,
                transform,
                actorOffset,
                actorScale,
                slot);
        }

        private void ScheduleAttached(
            GameObject prefab,
            Transform parent,
            Vector3 localOffset,
            float baseScale,
            FrostNovaFxSlot slot)
        {
            if (prefab == null || parent == null)
                return;

            StartCoroutine(SpawnAttachedDelayed(
                prefab,
                parent,
                localOffset,
                baseScale,
                slot));
        }

        private IEnumerator SpawnAttachedDelayed(
            GameObject prefab,
            Transform parent,
            Vector3 localOffset,
            float baseScale,
            FrostNovaFxSlot slot)
        {
            var setting = GetTuning(slot);
            var delay = setting != null ? Mathf.Max(0f, setting.delaySeconds) : 0f;
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (prefab == null || parent == null)
                yield break;

            var instance = Instantiate(prefab, parent, false);
            var offset = setting != null ? setting.offset : Vector2.zero;
            instance.transform.localPosition =
                localOffset + new Vector3(offset.x, offset.y, 0f);
            ApplyTunedTransform(instance, baseScale, setting);
            ApplyFacing(instance);
            Track(instance, setting);
        }

        private void ScheduleWorld(
            GameObject prefab,
            Vector3 position,
            float baseScale,
            FrostNovaFxSlot slot)
        {
            if (prefab == null)
                return;

            StartCoroutine(SpawnWorldDelayed(prefab, position, baseScale, slot));
        }

        private IEnumerator SpawnWorldDelayed(
            GameObject prefab,
            Vector3 position,
            float baseScale,
            FrostNovaFxSlot slot)
        {
            var setting = GetTuning(slot);
            var delay = setting != null ? Mathf.Max(0f, setting.delaySeconds) : 0f;
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (prefab == null)
                yield break;

            var instance = Instantiate(prefab);
            instance.transform.position =
                position + ToWorldOffset(setting != null ? setting.offset : Vector2.zero);
            ApplyTunedTransform(instance, baseScale, setting);
            ApplyFacing(instance);
            Track(instance, setting);
        }

        private IEnumerator FlyTuned(
            GameObject prefab,
            Vector3 origin,
            Transform target,
            Vector3 destination,
            float baseScale,
            FrostNovaFxSlot slot)
        {
            if (prefab == null)
                yield break;

            var setting = GetTuning(slot);
            var delay = setting != null ? Mathf.Max(0f, setting.delaySeconds) : 0f;
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (prefab == null)
                yield break;

            var left = _motor != null && _motor.FacingSign < 0;
            var configuredOffset = setting != null
                ? left ? setting.leftOffset : setting.offset
                : Vector2.zero;
            var visualOffset = ToWorldOffset(configuredOffset);
            var launchPosition = origin + visualOffset;

            var instance = Instantiate(prefab);
            instance.name = "FrostNova_BasicTrail";
            instance.transform.position = launchPosition;
            ApplyTunedTransform(instance, baseScale, setting);
            if (setting != null)
            {
                instance.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    left ? setting.leftAngleDegrees : setting.angleDegrees);
            }
            ApplyFacing(instance);
            Prepare(instance, setting);
            _active.Add(instance);

            var elapsed = 0f;
            var duration = _tuning != null
                ? _tuning.BasicProjectileFlightSeconds
                : Mathf.Max(0.02f, fallbackProjectileSeconds);

            while (instance != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (target != null)
                    destination = target.TransformPoint(targetOffset);

                // Offset is a muzzle/start-point correction only.
                // The end point remains the real target position so the projectile
                // cannot overshoot by the configured offset.
                instance.transform.position =
                    Vector3.Lerp(
                        launchPosition,
                        destination,
                        Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (instance != null)
            {
                _active.Remove(instance);
                Destroy(instance);
            }
        }

        private void ApplyTunedTransform(
            GameObject instance,
            float baseScale,
            FrostNovaFxTuningSetting setting)
        {
            if (instance == null)
                return;

            var scale = setting != null ? Mathf.Max(0.05f, setting.scale) : 1f;
            var angle = setting != null ? setting.angleDegrees : 0f;
            instance.transform.localScale = Vector3.one * baseScale * scale;
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private Vector3 ToWorldOffset(Vector2 offset)
        {
            var camera = Camera.main;
            return camera != null
                ? camera.transform.right * offset.x + camera.transform.up * offset.y
                : new Vector3(offset.x, offset.y, 0f);
        }

        private FrostNovaFxTuningSetting GetTuning(FrostNovaFxSlot slot)
        {
            return _tuning != null ? _tuning.Get(slot) : null;
        }

        private void Track(
            GameObject instance,
            FrostNovaFxTuningSetting setting)
        {
            if (instance == null)
                return;

            _active.Add(instance);
            var seconds = Prepare(instance, setting);
            StartCoroutine(RecycleAfter(instance, seconds));
        }

        private float Prepare(
            GameObject instance,
            FrostNovaFxTuningSetting setting)
        {
            var playback = instance != null
                ? instance.GetComponentInChildren<ExtractedFrameFxPlayback>(true)
                : null;
            if (playback == null)
                return fallbackLifetime;

            var speed = setting != null
                ? Mathf.Max(0.05f, setting.playbackSpeed)
                : ExtractedFrameFxPlayback.DefaultPlaybackSpeed;
            playback.SetPlaybackSpeed(speed);
            playback.PlayFromStart();
            return playback.Loop
                ? fallbackLifetime
                : Mathf.Max(0.05f, playback.EffectiveDuration);
        }

        private void ApplyFacing(GameObject instance)
        {
            if (instance == null)
                return;

            var left = _motor != null && _motor.FacingSign < 0;
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.flipX = left;
        }

        private IEnumerator RecycleAfter(GameObject instance, float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, seconds));
            if (instance == null)
                yield break;

            _active.Remove(instance);
            Destroy(instance);
        }

        private void ClearActive()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i] != null)
                    Destroy(_active[i]);
            }
            _active.Clear();
        }
    }
}
