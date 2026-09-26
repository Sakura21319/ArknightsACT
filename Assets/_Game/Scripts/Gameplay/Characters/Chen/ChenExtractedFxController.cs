using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Applies frame-sequence FX imported from the extraction tool to Ch'en's authored gameplay
    /// events. It never changes damage, targeting, cooldowns or skill state.
    ///
    /// The game skill slots intentionally map to the extracted client names as follows:
    /// slot 1 / ChenSkill1 -> chen_skill_02_*
    /// slot 2 / ChenSkill2 -> chen_skill_03_*
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSkillController), typeof(ChenSkill1), typeof(ChenSkill2))]
    public sealed class ChenExtractedFxController : MonoBehaviour, IGameplayPresentationLock
    {
        [Header("Game skill 1 -> client skill 02")]
        [SerializeField] private GameObject skill02Start;
        [SerializeField] private GameObject skill02Buff;
        [SerializeField] private GameObject skill02Hit;

        [Header("Game skill 2 -> client skill 03")]
        [SerializeField] private GameObject skill03Start;
        [SerializeField] private GameObject skill03Start02;
        [SerializeField] private GameObject skill03Start03;
        [SerializeField] private GameObject[] skill03Hits = Array.Empty<GameObject>();

        [Header("Basic attack -> client attack_01")]
        [SerializeField] private GameObject basicAttackStart;
        [SerializeField] private GameObject basicAttackHit;

        [Header("Basic attack combo 3 -> client skill_01")]
        [SerializeField] private GameObject basicCombo3Start;
        [SerializeField] private GameObject basicCombo3Hit;

        [Header("Placement")]
        [SerializeField] private Transform actorMount;
        [Tooltip("额外挂在角色挂点上的局部偏移。提取帧本身以画布中心为原点，角色特效通常需要抬到胸口/武器高度。")]
        [SerializeField] private Vector3 actorLocalOffset = new(0f, 0.45f, 0f);
        [Tooltip("命中特效相对于目标根节点的局部偏移。目标根节点在脚底，因此默认抬到身体中段。")]
        [SerializeField] private Vector3 targetLocalOffset = new(0f, 0.75f, 0f);
        [Tooltip("旧版统一缩放字段，保留用于兼容已有场景和 PlayerPrefs。")]
        [SerializeField, Min(0.1f)] private float skill02Scale = 3f;
        [SerializeField, Min(0.1f)] private float skill02SlashScale = 16f;
        [SerializeField, Min(0.1f)] private float skill03Scale = 2.5f;
        [Header("Per-effect placement and uniform scale")]
        [SerializeField] private Vector2 skill02StartOffset;
        [SerializeField] private Vector2 skill02BuffOffset;
        [SerializeField] private Vector2 skill02HitOffset;
        [SerializeField] private float skill02StartScale = 8f;
        [SerializeField] private float skill02BuffScale = 8f;
        [SerializeField] private float skill02HitScale = 8f;
        [SerializeField] private Vector2 skill03StartOffset;
        [SerializeField] private Vector2 skill03Start02Offset;
        [SerializeField] private Vector2 dragonOffset;
        [SerializeField] private Vector2 skill03HitOffset;
        [SerializeField] private float skill03StartScale = 5.25f;
        [SerializeField] private float skill03Start02Scale = 5.25f;
        [SerializeField] private float dragonScale = 5.25f;
        [SerializeField] private float skill03HitScale = 2.5f;
        [SerializeField] private Vector2 basicAttackOffset;
        [SerializeField] private Vector2 basicHitOffset;
        [SerializeField] private float basicAttackScale = 3f;
        [SerializeField] private float basicHitScale = 3f;
        [Tooltip("绝影起手龙沿摄像机水平前方向移动的距离。正值表示远离摄像机，也就是角色后方。")]
        [SerializeField] private float dragonBackDepth = 0.45f;
        [SerializeField, Min(0f)] private float skill03StartGap = 0.08f;
        [SerializeField, Min(0f)] private float fallbackLifetime = 1.25f;

        private readonly List<GameObject> _active = new();
        private readonly List<Coroutine> _sequences = new();
        private PlayerSkillController _skills;
        private ChenSkill1 _skill1;
        private ChenSkill2 _skill2;
        private PlayerMotor25D _motor25D;
        private PlayerMotor2D _motor2D;
        private int _currentBasicComboIndex;
        private float _skill3PresentationUntil;

        public bool IsConfigured => skill02Start != null || skill02Hit != null || skill03Start != null || basicAttackStart != null || basicCombo3Start != null;
        public bool IsPresentationBusy => Time.unscaledTime < _skill3PresentationUntil;
        public float ActorLocalY => actorLocalOffset.y;
        public float ActorLocalX => actorLocalOffset.x;
        public float TargetLocalY => targetLocalOffset.y;
        public float TargetLocalX => targetLocalOffset.x;
        public float Skill02Scale => skill02Scale;
        public float Skill02SlashScale => skill02SlashScale;
        public float Skill03Scale => skill03Scale;
        public float DragonBackDepth => dragonBackDepth;
        public Vector2 Skill02StartOffset => skill02StartOffset;
        public Vector2 Skill02BuffOffset => skill02BuffOffset;
        public Vector2 Skill02HitOffset => skill02HitOffset;
        public float Skill02StartScale => skill02StartScale;
        public float Skill02BuffScale => skill02BuffScale;
        public float Skill02HitScale => skill02HitScale;
        public Vector2 Skill03StartOffset => skill03StartOffset;
        public Vector2 Skill03Start02Offset => skill03Start02Offset;
        public Vector2 DragonOffset => dragonOffset;
        public Vector2 Skill03HitOffset => skill03HitOffset;
        public float Skill03StartScale => skill03StartScale;
        public float Skill03Start02Scale => skill03Start02Scale;
        public float DragonScale => dragonScale;
        public float Skill03HitScale => skill03HitScale;
        public Vector2 BasicAttackOffset => basicAttackOffset;
        public Vector2 BasicHitOffset => basicHitOffset;
        public float BasicAttackScale => basicAttackScale;
        public float BasicHitScale => basicHitScale;

        public void Configure(
            GameObject skill02StartValue,
            GameObject skill02BuffValue,
            GameObject skill02HitValue,
            GameObject skill03StartValue,
            GameObject skill03Start02Value,
            GameObject skill03Start03Value,
            GameObject[] skill03HitValues,
            GameObject basicAttackStartValue = null,
            GameObject basicAttackHitValue = null,
            GameObject basicCombo3StartValue = null,
            GameObject basicCombo3HitValue = null)
        {
            skill02Start = skill02StartValue;
            skill02Buff = skill02BuffValue;
            skill02Hit = skill02HitValue;
            skill03Start = skill03StartValue;
            skill03Start02 = skill03Start02Value;
            skill03Start03 = skill03Start03Value;
            skill03Hits = skill03HitValues ?? Array.Empty<GameObject>();
            basicAttackStart = basicAttackStartValue;
            basicAttackHit = basicAttackHitValue;
            basicCombo3Start = basicCombo3StartValue;
            basicCombo3Hit = basicCombo3HitValue;
        }

        private void Awake()
        {
            _skills = GetComponent<PlayerSkillController>();
            _skill1 = GetComponent<ChenSkill1>();
            _skill2 = GetComponent<ChenSkill2>();
            _motor25D = GetComponent<PlayerMotor25D>();
            _motor2D = GetComponent<PlayerMotor2D>();
            actorMount = actorMount != null ? actorMount : transform.Find("CustomFxMountPoint");
            if (actorMount == null)
                actorMount = transform;

            LoadSavedTuning();
            if (GetComponent<ChenFxTuningOverlay>() == null)
                gameObject.AddComponent<ChenFxTuningOverlay>();
            if (_skills == null || _skill1 == null || _skill2 == null)
            {
                Debug.LogError(
                    "[ArknightsACT/ChenFX] 找不到技能事件源。请确认 ChenExtractedFxController、PlayerSkillController、ChenSkill1、ChenSkill2 都挂在同一个 Player_Chen 根对象上。",
                    this);
            }

            LogMissingPrefab("skill02Start", skill02Start);
            LogMissingPrefab("skill02Buff", skill02Buff);
            LogMissingPrefab("skill02Hit", skill02Hit);
            LogMissingPrefab("skill03Start", skill03Start);
            LogMissingPrefab("skill03Start02", skill03Start02);
            LogMissingPrefab("skill03Start03", skill03Start03);
            if (skill03Hits == null || skill03Hits.Length == 0)
                Debug.LogError("[ArknightsACT/ChenFX] skill03Hits 为空，绝影命中特效不会播放。", this);
            LogMissingPrefab("basicAttackStart", basicAttackStart);
            LogMissingPrefab("basicAttackHit", basicAttackHit);
            LogMissingPrefab("basicCombo3Start", basicCombo3Start);
            LogMissingPrefab("basicCombo3Hit", basicCombo3Hit);
        }

        public void SetActorLocalY(float value)
        {
            actorLocalOffset.y = Mathf.Clamp(value, -2f, 3f);
        }

        public void SetActorLocalX(float value)
        {
            actorLocalOffset.x = Mathf.Clamp(value, -3f, 3f);
        }

        public void SetTargetLocalY(float value)
        {
            targetLocalOffset.y = Mathf.Clamp(value, -2f, 3f);
        }

        public void SetTargetLocalX(float value)
        {
            targetLocalOffset.x = Mathf.Clamp(value, -3f, 3f);
        }

        public void SetSkill02Scale(float value)
        {
            skill02Scale = Mathf.Clamp(value, 0.1f, 32f);
        }

        public void SetSkill02SlashScale(float value)
        {
            skill02SlashScale = Mathf.Clamp(value, 0.1f, 64f);
        }

        public void SetSkill03Scale(float value)
        {
            skill03Scale = Mathf.Clamp(value, 0.1f, 32f);
        }

        public void SetDragonBackDepth(float value)
        {
            dragonBackDepth = Mathf.Clamp(value, -2f, 2f);
        }

        public void SetSkill02StartOffsetX(float value) => skill02StartOffset.x = ClampOffset(value);
        public void SetSkill02StartOffsetY(float value) => skill02StartOffset.y = ClampOffset(value);
        public void SetSkill02StartScale(float value) => skill02StartScale = ClampScale(value);
        public void SetSkill02BuffOffsetX(float value) => skill02BuffOffset.x = ClampOffset(value);
        public void SetSkill02BuffOffsetY(float value) => skill02BuffOffset.y = ClampOffset(value);
        public void SetSkill02BuffScale(float value) => skill02BuffScale = ClampScale(value);
        public void SetSkill02HitOffsetX(float value) => skill02HitOffset.x = ClampOffset(value);
        public void SetSkill02HitOffsetY(float value) => skill02HitOffset.y = ClampOffset(value);
        public void SetSkill02HitScale(float value) => skill02HitScale = ClampScale(value);
        public void SetSkill03StartOffsetX(float value) => skill03StartOffset.x = ClampOffset(value);
        public void SetSkill03StartOffsetY(float value) => skill03StartOffset.y = ClampOffset(value);
        public void SetSkill03StartScale(float value) => skill03StartScale = ClampScale(value);
        public void SetSkill03Start02OffsetX(float value) => skill03Start02Offset.x = ClampOffset(value);
        public void SetSkill03Start02OffsetY(float value) => skill03Start02Offset.y = ClampOffset(value);
        public void SetSkill03Start02Scale(float value) => skill03Start02Scale = ClampScale(value);
        public void SetDragonOffsetX(float value) => dragonOffset.x = ClampOffset(value);
        public void SetDragonOffsetY(float value) => dragonOffset.y = ClampOffset(value);
        public void SetDragonScale(float value) => dragonScale = ClampScale(value);
        public void SetSkill03HitOffsetX(float value) => skill03HitOffset.x = ClampOffset(value);
        public void SetSkill03HitOffsetY(float value) => skill03HitOffset.y = ClampOffset(value);
        public void SetSkill03HitScale(float value) => skill03HitScale = ClampScale(value);
        public void SetBasicAttackOffsetX(float value) => basicAttackOffset.x = ClampOffset(value);
        public void SetBasicAttackOffsetY(float value) => basicAttackOffset.y = ClampOffset(value);
        public void SetBasicAttackScale(float value) => basicAttackScale = ClampScale(value);
        public void SetBasicHitOffsetX(float value) => basicHitOffset.x = ClampOffset(value);
        public void SetBasicHitOffsetY(float value) => basicHitOffset.y = ClampOffset(value);
        public void SetBasicHitScale(float value) => basicHitScale = ClampScale(value);

        public void SaveTuning()
        {
            PlayerPrefs.SetFloat("ArknightsACT.ChenFx.ActorLocalY", actorLocalOffset.y);
            PlayerPrefs.SetFloat("ArknightsACT.ChenFx.ActorLocalX", actorLocalOffset.x);
            PlayerPrefs.SetFloat("ArknightsACT.ChenFx.TargetLocalY", targetLocalOffset.y);
            PlayerPrefs.SetFloat("ArknightsACT.ChenFx.TargetLocalX", targetLocalOffset.x);
            PlayerPrefs.SetFloat("ArknightsACT.ChenFx.Skill02Scale", skill02Scale);
            PlayerPrefs.SetFloat("ArknightsACT.ChenFx.Skill02SlashScale", skill02SlashScale);
            PlayerPrefs.SetFloat("ArknightsACT.ChenFx.Skill03Scale", skill03Scale);
            PlayerPrefs.SetFloat("ArknightsACT.ChenFx.DragonBackDepth", dragonBackDepth);
            SaveEffectTuning("S2Start", skill02StartOffset, skill02StartScale);
            SaveEffectTuning("S2Buff", skill02BuffOffset, skill02BuffScale);
            SaveEffectTuning("S2Hit", skill02HitOffset, skill02HitScale);
            SaveEffectTuning("S3Start", skill03StartOffset, skill03StartScale);
            SaveEffectTuning("S3Start02", skill03Start02Offset, skill03Start02Scale);
            SaveEffectTuning("Dragon", dragonOffset, dragonScale);
            SaveEffectTuning("S3Hit", skill03HitOffset, skill03HitScale);
            SaveEffectTuning("BasicAttack", basicAttackOffset, basicAttackScale);
            SaveEffectTuning("BasicHit", basicHitOffset, basicHitScale);
            PlayerPrefs.Save();
            Debug.Log(
                $"[ArknightsACT/ChenFX] 已保存调参：actor=({actorLocalOffset.x:0.00},{actorLocalOffset.y:0.00}), " +
                $"target=({targetLocalOffset.x:0.00},{targetLocalOffset.y:0.00}), " +
                $"skill02Scale={skill02Scale:0.00}, skill02SlashScale={skill02SlashScale:0.00}, " +
                $"skill03Scale={skill03Scale:0.00}, dragonOffset=({dragonOffset.x:0.00},{dragonOffset.y:0.00}), dragonScale={dragonScale:0.00}, " +
                $"basicAttackOffset=({basicAttackOffset.x:0.00},{basicAttackOffset.y:0.00}), basicAttackScale={basicAttackScale:0.00}, " +
                $"dragonBackDepth={dragonBackDepth:0.00}。",
                this);
        }

        public void ResetTuning()
        {
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.ActorLocalY");
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.ActorLocalX");
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.TargetLocalY");
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.TargetLocalX");
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.Skill02Scale");
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.Skill02SlashScale");
            // Remove the short-lived name used by the first version of the tuning panel.
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.Skill02HitScale");
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.Skill03Scale");
            PlayerPrefs.DeleteKey("ArknightsACT.ChenFx.DragonBackDepth");
            DeleteEffectTuning("S2Start");
            DeleteEffectTuning("S2Buff");
            DeleteEffectTuning("S2Hit");
            DeleteEffectTuning("S3Start");
            DeleteEffectTuning("S3Start02");
            DeleteEffectTuning("Dragon");
            DeleteEffectTuning("S3Hit");
            DeleteEffectTuning("BasicAttack");
            DeleteEffectTuning("BasicHit");
            actorLocalOffset.x = 0f;
            actorLocalOffset.y = 0.45f;
            targetLocalOffset.x = 0f;
            targetLocalOffset.y = 0.75f;
            skill02Scale = 3f;
            skill02SlashScale = 16f;
            skill03Scale = 2.5f;
            dragonBackDepth = 0.45f;
            skill02StartOffset = Vector2.zero;
            skill02BuffOffset = Vector2.zero;
            skill02HitOffset = Vector2.zero;
            skill03StartOffset = Vector2.zero;
            skill03Start02Offset = Vector2.zero;
            dragonOffset = Vector2.zero;
            skill03HitOffset = Vector2.zero;
            basicAttackOffset = Vector2.zero;
            basicHitOffset = Vector2.zero;
            skill02StartScale = 8f;
            skill02BuffScale = 8f;
            skill02HitScale = 8f;
            skill03StartScale = 5.25f;
            skill03Start02Scale = 5.25f;
            dragonScale = 5.25f;
            skill03HitScale = 2.5f;
            basicAttackScale = 3f;
            basicHitScale = 3f;
            PlayerPrefs.Save();
        }

        private void OnEnable()
        {
            var attacks = GetComponent<PlayerAttackController>();
            if (attacks != null)
                attacks.AttackStarted += OnBasicAttackStarted;
            if (attacks != null)
                attacks.AttackHit += OnBasicAttackHit;
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCastSucceeded;
            if (_skill1 != null)
                _skill1.CastStarted += OnSkill1CastStarted;
            if (_skill1 != null)
                _skill1.HitResolved += OnSkill1HitResolved;
            if (_skill2 != null)
                _skill2.StrikeResolved += OnSkill2StrikeResolved;
        }

        private void OnDisable()
        {
            var attacks = GetComponent<PlayerAttackController>();
            if (attacks != null)
                attacks.AttackStarted -= OnBasicAttackStarted;
            if (attacks != null)
                attacks.AttackHit -= OnBasicAttackHit;
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCastSucceeded;
            if (_skill1 != null)
                _skill1.CastStarted -= OnSkill1CastStarted;
            if (_skill1 != null)
                _skill1.HitResolved -= OnSkill1HitResolved;
            if (_skill2 != null)
                _skill2.StrikeResolved -= OnSkill2StrikeResolved;

            for (var i = 0; i < _sequences.Count; i++)
                if (_sequences[i] != null)
                    StopCoroutine(_sequences[i]);
            _sequences.Clear();
            ClearInstances();
            _skill3PresentationUntil = 0f;
        }

        private void OnSkillCastSucceeded(int slot)
        {
            Debug.Log($"[ArknightsACT/ChenFX] 收到技能释放事件：slot={slot}。", this);
            if (slot == 1)
            {
                // ChenSkill1 has a direct CastStarted event; keep this branch as a diagnostic
                // only so a future generic event cannot double-spawn the same FX.
                Debug.Log("[ArknightsACT/ChenFX] slot 1 已由 ChenSkill1.CastStarted 播放拔刀起手帧。", this);
                return;
            }

            if (slot == 2)
            {
                ScheduleSkill3Presentation(skill03Start, 0f);
                ScheduleSkill3Presentation(skill03Start02, skill03StartGap);
                ScheduleSkill3Presentation(skill03Start03, skill03StartGap * 2f);
                var sequence = StartCoroutine(PlaySkill03StartSequence());
                _sequences.Add(sequence);
            }
        }

        private void OnSkill1CastStarted()
        {
            Debug.Log("[ArknightsACT/ChenFX] 收到拔刀直接释放事件。", this);
            SpawnOnActor(skill02Start, skill02StartOffset, true);
            SpawnOnActor(skill02Buff, skill02BuffOffset, true);
        }

        private void OnBasicAttackStarted(int comboIndex)
        {
            _currentBasicComboIndex = comboIndex;
            var startPrefab = comboIndex == 2 && basicCombo3Start != null
                ? basicCombo3Start
                : basicAttackStart;
            SpawnOnActor(startPrefab, basicAttackOffset, true);
        }

        private void OnBasicAttackHit(CombatEntity target)
        {
            var hitPrefab = _currentBasicComboIndex == 2 && basicCombo3Hit != null
                ? basicCombo3Hit
                : basicAttackHit;
            SpawnAt(hitPrefab, target != null ? target.transform : actorMount,
                target != null ? targetLocalOffset + (Vector3)basicHitOffset : actorLocalOffset + (Vector3)basicHitOffset,
                mirrorToFacing: true);
        }

        private IEnumerator PlaySkill03StartSequence()
        {
            SpawnOnActor(skill03Start, skill03StartOffset);
            if (skill03StartGap > 0f)
                yield return new WaitForSecondsRealtime(skill03StartGap);
            SpawnOnActor(skill03Start02, skill03Start02Offset);
            if (skill03StartGap > 0f)
                yield return new WaitForSecondsRealtime(skill03StartGap);
            // skill_03_start_03 is the dragon frame sequence. Keep it on a separate depth
            // plane so it rises behind Ch'en instead of covering her silhouette.
            SpawnOnActorBehind(skill03Start03, dragonOffset);
        }

        private void OnSkill1HitResolved(Transform target)
        {
            SpawnAt(skill02Hit, target != null ? target : actorMount,
                target != null ? targetLocalOffset + (Vector3)skill02HitOffset : actorLocalOffset + (Vector3)skill02HitOffset);
        }

        private void OnSkill2StrikeResolved(int strikeIndex, Transform target, bool isFinal)
        {
            if (skill03Hits == null || skill03Hits.Length == 0)
                return;

            var index = Mathf.Clamp(strikeIndex, 0, skill03Hits.Length - 1);
            ScheduleSkill3Presentation(skill03Hits[index], 0f);
            SpawnAt(skill03Hits[index], target != null ? target : actorMount,
                target != null ? targetLocalOffset + (Vector3)skill03HitOffset : actorLocalOffset + (Vector3)skill03HitOffset);
        }

        private void ScheduleSkill3Presentation(GameObject prefab, float delay)
        {
            if (prefab == null)
                return;

            var playback = prefab.GetComponentInChildren<ExtractedFrameFxPlayback>(true);
            var duration = playback != null ? playback.EffectiveDuration : fallbackLifetime;
            _skill3PresentationUntil = Mathf.Max(
                _skill3PresentationUntil,
                Time.unscaledTime + Mathf.Max(0f, delay) + Mathf.Max(0.01f, duration));
        }

        private void SpawnOnActor(GameObject prefab, Vector2 effectOffset = default, bool mirrorToFacing = false)
        {
            SpawnAt(prefab, actorMount, actorLocalOffset + (Vector3)effectOffset, mirrorToFacing: mirrorToFacing);
        }

        private void SpawnOnActorBehind(GameObject prefab, Vector2 effectOffset = default)
        {
            SpawnAt(
                prefab,
                actorMount,
                actorLocalOffset + (Vector3)effectOffset,
                GetCameraDepthOffset(),
                renderBehindActor: true,
                mirrorToFacing: true);
        }

        private void SpawnAt(
            GameObject prefab,
            Transform anchor,
            Vector3 localOffset,
            Vector3 worldOffset = default,
            bool renderBehindActor = false,
            bool mirrorToFacing = false)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[ArknightsACT/ChenFX] 尝试播放空 Prefab，当前特效映射缺失。", this);
                return;
            }

            if (anchor == null)
            {
                Debug.LogWarning($"[ArknightsACT/ChenFX] {prefab.name} 没有有效挂点，特效未生成。", this);
                return;
            }

            var instance = Instantiate(prefab, anchor, false);
            var horizontalSign = mirrorToFacing ? GetScreenFacingSign() : 1f;
            if (mirrorToFacing)
                localOffset.x *= horizontalSign;
            instance.transform.localPosition = localOffset;
            if (worldOffset.sqrMagnitude > 0.000001f)
                instance.transform.position += worldOffset;
            instance.transform.localRotation = Quaternion.identity;
            var scale = GetScale(prefab);
            instance.transform.localScale = new Vector3(scale * horizontalSign, scale, 1f);
            if (renderBehindActor)
                SetSortingOrder(instance, -10);
            _active.Add(instance);

            var playback = instance.GetComponent<ExtractedFrameFxPlayback>();
            if (playback != null)
            {
                // Existing prefabs may have been imported by the temporary all-2x version.
                // Re-assert the final policy here so skills return to their original speed
                // immediately, while only attack FX stays accelerated.
                playback.SetPlaybackSpeed(IsBasicAttackPrefab(prefab)
                    ? ExtractedFrameFxPlayback.BasicAttackPlaybackSpeed
                    : ExtractedFrameFxPlayback.DefaultPlaybackSpeed);
                playback.PlayFromStart();
                if (!playback.Loop)
                    StartCoroutine(RecycleAfter(instance, playback.EffectiveDuration));
            }
            else
            {
                StartCoroutine(RecycleAfter(instance, fallbackLifetime));
            }
        }

        private void LogMissingPrefab(string slotName, GameObject prefab)
        {
            if (prefab == null)
                Debug.LogError($"[ArknightsACT/ChenFX] {slotName} 未绑定，相关特效不会播放。请重新执行“Import Extracted Frame FX”。", this);
        }

        private bool IsBasicAttackPrefab(GameObject prefab)
        {
            return prefab != null &&
                   (prefab == basicAttackStart || prefab == basicAttackHit ||
                    prefab == basicCombo3Start || prefab == basicCombo3Hit);
        }

        private void LoadSavedTuning()
        {
            if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.ActorLocalX"))
                actorLocalOffset.x = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.ActorLocalX");
            if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.ActorLocalY"))
                actorLocalOffset.y = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.ActorLocalY");
            if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.TargetLocalX"))
                targetLocalOffset.x = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.TargetLocalX");
            if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.TargetLocalY"))
                targetLocalOffset.y = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.TargetLocalY");
            if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.Skill02Scale"))
                skill02Scale = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.Skill02Scale");
            if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.Skill02SlashScale"))
                skill02SlashScale = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.Skill02SlashScale");
            else if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.Skill02HitScale"))
                skill02SlashScale = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.Skill02HitScale");
            if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.Skill03Scale"))
                skill03Scale = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.Skill03Scale");
            if (PlayerPrefs.HasKey("ArknightsACT.ChenFx.DragonBackDepth"))
                dragonBackDepth = PlayerPrefs.GetFloat("ArknightsACT.ChenFx.DragonBackDepth");

            // Migrate the previously saved uniform values into the per-effect controls. This
            // deliberately prefers PlayerPrefs over scene defaults, so the user's last tuning
            // remains the starting point of the new panel.
            var legacySkill02 = PlayerPrefs.HasKey("ArknightsACT.ChenFx.Skill02Scale")
                ? PlayerPrefs.GetFloat("ArknightsACT.ChenFx.Skill02Scale")
                : skill02Scale;
            var legacySkill03 = PlayerPrefs.HasKey("ArknightsACT.ChenFx.Skill03Scale")
                ? PlayerPrefs.GetFloat("ArknightsACT.ChenFx.Skill03Scale")
                : skill03Scale;
            var legacySkill02Start = PlayerPrefs.HasKey("ArknightsACT.ChenFx.Skill02SlashScale")
                ? PlayerPrefs.GetFloat("ArknightsACT.ChenFx.Skill02SlashScale")
                : legacySkill02;
            LoadEffectTuning("S2Start", ref skill02StartOffset, ref skill02StartScale, legacySkill02Start);
            LoadEffectTuning("S2Buff", ref skill02BuffOffset, ref skill02BuffScale, legacySkill02);
            LoadEffectTuning("S2Hit", ref skill02HitOffset, ref skill02HitScale, legacySkill02);
            LoadEffectTuning("S3Start", ref skill03StartOffset, ref skill03StartScale, legacySkill03);
            LoadEffectTuning("S3Start02", ref skill03Start02Offset, ref skill03Start02Scale, legacySkill03);
            LoadEffectTuning("Dragon", ref dragonOffset, ref dragonScale, legacySkill03);
            LoadEffectTuning("S3Hit", ref skill03HitOffset, ref skill03HitScale, skill03Scale);
            LoadEffectTuning("BasicAttack", ref basicAttackOffset, ref basicAttackScale, basicAttackScale);
            LoadEffectTuning("BasicHit", ref basicHitOffset, ref basicHitScale, basicHitScale);

            actorLocalOffset.x = Mathf.Clamp(actorLocalOffset.x, -3f, 3f);
            actorLocalOffset.y = Mathf.Clamp(actorLocalOffset.y, -2f, 3f);
            targetLocalOffset.x = Mathf.Clamp(targetLocalOffset.x, -3f, 3f);
            targetLocalOffset.y = Mathf.Clamp(targetLocalOffset.y, -2f, 3f);
            skill02Scale = Mathf.Clamp(skill02Scale, 0.1f, 32f);
            skill02SlashScale = Mathf.Clamp(skill02SlashScale, 0.1f, 64f);
            skill03Scale = Mathf.Clamp(skill03Scale, 0.1f, 32f);
            dragonBackDepth = Mathf.Clamp(dragonBackDepth, -2f, 2f);
            skill02StartOffset = ClampOffset(skill02StartOffset);
            skill02BuffOffset = ClampOffset(skill02BuffOffset);
            skill02HitOffset = ClampOffset(skill02HitOffset);
            skill03StartOffset = ClampOffset(skill03StartOffset);
            skill03Start02Offset = ClampOffset(skill03Start02Offset);
            dragonOffset = ClampOffset(dragonOffset);
            skill03HitOffset = ClampOffset(skill03HitOffset);
            basicAttackOffset = ClampOffset(basicAttackOffset);
            basicHitOffset = ClampOffset(basicHitOffset);
            skill02StartScale = ClampScale(skill02StartScale);
            skill02BuffScale = ClampScale(skill02BuffScale);
            skill02HitScale = ClampScale(skill02HitScale);
            skill03StartScale = ClampScale(skill03StartScale);
            skill03Start02Scale = ClampScale(skill03Start02Scale);
            dragonScale = ClampScale(dragonScale);
            skill03HitScale = ClampScale(skill03HitScale);
            basicAttackScale = ClampScale(basicAttackScale);
            basicHitScale = ClampScale(basicHitScale);
        }

        private float GetScale(GameObject prefab)
        {
            if (prefab == null)
                return 1f;

            var name = prefab.name;
            if (name.IndexOf("skill_02_start", StringComparison.OrdinalIgnoreCase) >= 0)
                return skill02StartScale;
            if (name.IndexOf("skill_02_buff", StringComparison.OrdinalIgnoreCase) >= 0)
                return skill02BuffScale;
            if (name.IndexOf("skill_02_hit", StringComparison.OrdinalIgnoreCase) >= 0)
                return skill02HitScale;
            if (name.IndexOf("skill_03_start_02", StringComparison.OrdinalIgnoreCase) >= 0)
                return skill03Start02Scale;
            if (name.IndexOf("skill_03_start_03", StringComparison.OrdinalIgnoreCase) >= 0)
                return dragonScale;
            if (name.IndexOf("skill_03_start", StringComparison.OrdinalIgnoreCase) >= 0)
                return skill03StartScale;
            if (name.IndexOf("skill_03_hit", StringComparison.OrdinalIgnoreCase) >= 0)
                return skill03HitScale;
            if (name.IndexOf("attack_01_start", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("skill_01_start", StringComparison.OrdinalIgnoreCase) >= 0)
                return basicAttackScale;
            if (name.IndexOf("attack_01_hit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("skill_01_hit", StringComparison.OrdinalIgnoreCase) >= 0)
                return basicHitScale;
            return name.IndexOf("skill_03", StringComparison.OrdinalIgnoreCase) >= 0
                ? Mathf.Max(0.1f, skill03Scale)
                : Mathf.Max(0.1f, skill02Scale);
        }

        private static float ClampScale(float value) => Mathf.Clamp(value, 0.1f, 64f);
        private static float ClampOffset(float value) => Mathf.Clamp(value, -3f, 3f);
        private static Vector2 ClampOffset(Vector2 value) => new(ClampOffset(value.x), ClampOffset(value.y));

        private static string EffectKey(string name, string suffix) =>
            "ArknightsACT.ChenFx." + name + suffix;

        private static void LoadEffectTuning(string name, ref Vector2 offset, ref float scale, float legacyScale)
        {
            var newOffset = PlayerPrefs.HasKey(EffectKey(name, ".OffsetX")) ||
                            PlayerPrefs.HasKey(EffectKey(name, ".OffsetY"));
            if (newOffset)
            {
                offset = new Vector2(
                    PlayerPrefs.HasKey(EffectKey(name, ".OffsetX")) ? PlayerPrefs.GetFloat(EffectKey(name, ".OffsetX")) : offset.x,
                    PlayerPrefs.HasKey(EffectKey(name, ".OffsetY")) ? PlayerPrefs.GetFloat(EffectKey(name, ".OffsetY")) : offset.y);
            }

            if (PlayerPrefs.HasKey(EffectKey(name, ".Scale")))
                scale = PlayerPrefs.GetFloat(EffectKey(name, ".Scale"));
            else
            {
                // Compatibility with the short-lived non-uniform panel: use X as the uniform
                // scale if it was saved, and otherwise fall back to the previous scalar value.
                var legacyX = LegacyScaleKey(name, "X");
                scale = PlayerPrefs.HasKey(legacyX) ? PlayerPrefs.GetFloat(legacyX) : legacyScale;
            }
        }

        private static void SaveEffectTuning(string name, Vector2 offset, float scale)
        {
            PlayerPrefs.SetFloat(EffectKey(name, ".OffsetX"), offset.x);
            PlayerPrefs.SetFloat(EffectKey(name, ".OffsetY"), offset.y);
            PlayerPrefs.SetFloat(EffectKey(name, ".Scale"), scale);
        }

        private static void DeleteEffectTuning(string name)
        {
            PlayerPrefs.DeleteKey(EffectKey(name, ".OffsetX"));
            PlayerPrefs.DeleteKey(EffectKey(name, ".OffsetY"));
            PlayerPrefs.DeleteKey(EffectKey(name, ".Scale"));
            PlayerPrefs.DeleteKey(LegacyScaleKey(name, "X"));
            PlayerPrefs.DeleteKey(LegacyScaleKey(name, "Y"));
        }

        private static string LegacyScaleKey(string name, string axis)
        {
            var legacyName = name switch
            {
                "S2Start" => "Skill02Start",
                "S2Buff" => "Skill02Buff",
                "S2Hit" => "Skill02Hit",
                "S3Start" => "Skill03Start",
                "S3Start02" => "Skill03Start02",
                "S3Hit" => "Skill03Hit",
                "BasicAttack" => "BasicAttack",
                "BasicHit" => "BasicHit",
                _ => name
            };
            return "ArknightsACT.ChenFx." + legacyName + "Scale" + axis;
        }

        private Vector3 GetCameraDepthOffset()
        {
            if (Mathf.Abs(dragonBackDepth) < 0.0001f)
                return Vector3.zero;

            var camera = Camera.main;
            var direction = camera != null
                ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up)
                : Vector3.forward;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            return direction.normalized * dragonBackDepth;
        }

        private static void SetSortingOrder(GameObject instance, int sortingOrder)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SpriteRenderer spriteRenderer)
                    spriteRenderer.sortingOrder = sortingOrder;
            }
        }

        private float GetScreenFacingSign()
        {
            if (_motor25D != null)
                return _motor25D.FacingSign < 0 ? -1f : 1f;
            if (_motor2D != null)
                return _motor2D.FacingSign < 0 ? -1f : 1f;
            return 1f;
        }

        private IEnumerator RecycleAfter(GameObject instance, float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, seconds));
            Recycle(instance);
        }

        private void ClearInstances()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
                Recycle(_active[i]);
            _active.Clear();
        }

        private void Recycle(GameObject instance)
        {
            if (instance == null)
                return;
            _active.Remove(instance);
            Destroy(instance);
        }
    }
}
