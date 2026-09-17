using System;
using System.Linq;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Playback bridge for Ch'en's real client battle-effect prefabs.
    ///
    /// This component deliberately does not draw replacement slash lines or generate textures.
    /// It only instantiates original extracted GameObjects such as chen_attack_01_*, chen_skill_02_*
    /// and chen_skill_03_hit_01..10 when the matching gameplay event occurs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController), typeof(PlayerSkillController), typeof(ChenSkill1))]
    [RequireComponent(typeof(ChenSkill2))]
    public sealed class ChenOriginalSkillFxController : MonoBehaviour
    {
        [Header("普通攻击 / original client FX")]
        [SerializeField] private GameObject attackStartFx;
        [SerializeField] private GameObject attackHitFx;

        [Header("赤霄·拔刀 / original client FX")]
        [SerializeField] private GameObject drawStartFx;
        [SerializeField] private GameObject drawHitFx;
        [SerializeField] private GameObject drawBuffFx;

        [Header("赤霄·绝影 / original client FX")]
        [SerializeField] private GameObject jueyingStartFx;
        [SerializeField] private GameObject jueyingStart02Fx;
        [SerializeField] private GameObject[] jueyingHitFx = new GameObject[10];

        [Header("Placement")]
        [SerializeField] private Vector3 actorLocalOffset = new(0f, 0.78f, 0f);
        [SerializeField] private Vector3 hitWorldOffset = new(0f, 0.72f, 0f);
        [SerializeField, Min(0.25f)] private float fallbackLifetimeSeconds = 4f;

        private PlayerAttackController _attacks;
        private PlayerSkillController _skills;
        private ChenSkill1 _draw;
        private ChenSkill2 _jueying;
        private bool _jueyingStart02Played;
        private bool _warnedMissing;

        public void Configure(
            GameObject attackStart,
            GameObject attackHit,
            GameObject drawStart,
            GameObject drawHit,
            GameObject drawBuff,
            GameObject jueyingStart,
            GameObject jueyingStart02,
            GameObject[] jueyingHits)
        {
            attackStartFx = attackStart;
            attackHitFx = attackHit;
            drawStartFx = drawStart;
            drawHitFx = drawHit;
            drawBuffFx = drawBuff;
            jueyingStartFx = jueyingStart;
            jueyingStart02Fx = jueyingStart02;
            jueyingHitFx = jueyingHits != null ? jueyingHits.ToArray() : new GameObject[10];
        }

        private void Awake()
        {
            _attacks = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _draw = GetComponent<ChenSkill1>();
            _jueying = GetComponent<ChenSkill2>();
        }

        private void OnEnable()
        {
            if (_attacks != null)
            {
                _attacks.AttackStarted += OnAttackStarted;
                _attacks.AttackHit += OnAttackHit;
            }
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCastSucceeded;
            if (_draw != null)
                _draw.HitResolved += OnDrawHitResolved;
            if (_jueying != null)
                _jueying.StrikeResolved += OnJueyingStrikeResolved;
        }

        private void Start()
        {
            WarnIfOriginalAssetsMissing();
        }

        private void OnDisable()
        {
            if (_attacks != null)
            {
                _attacks.AttackStarted -= OnAttackStarted;
                _attacks.AttackHit -= OnAttackHit;
            }
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCastSucceeded;
            if (_draw != null)
                _draw.HitResolved -= OnDrawHitResolved;
            if (_jueying != null)
                _jueying.StrikeResolved -= OnJueyingStrikeResolved;
        }

        private void OnAttackStarted(int comboIndex)
        {
            SpawnOnActor(attackStartFx, "chen_attack_01_start");
        }

        private void OnAttackHit(CombatEntity target)
        {
            SpawnAtTarget(attackHitFx, target != null ? target.transform : null, "chen_attack_01_hit");
        }

        private void OnSkillCastSucceeded(int slot)
        {
            if (slot == 1)
            {
                SpawnOnActor(drawStartFx, "chen_skill_02_start");
                SpawnOnActor(drawBuffFx, "chen_skill_02_buff");
                return;
            }

            if (slot != 2)
                return;

            _jueyingStart02Played = false;
            SpawnOnActor(jueyingStartFx, "chen_skill_03_start");
        }

        private void OnDrawHitResolved(Transform target)
        {
            SpawnAtTarget(drawHitFx, target, "chen_skill_02_hit");
        }

        private void OnJueyingStrikeResolved(int strikeIndex, Transform target, bool isFinal)
        {
            if (!_jueyingStart02Played)
            {
                _jueyingStart02Played = true;
                SpawnOnActor(jueyingStart02Fx, "chen_skill_03_start_02");
            }

            if (jueyingHitFx == null || jueyingHitFx.Length == 0)
                return;

            var sourceIndex = Mathf.Abs(strikeIndex) % jueyingHitFx.Length;
            var keyIndex = sourceIndex + 1;
            SpawnAtTarget(
                jueyingHitFx[sourceIndex],
                target,
                $"chen_skill_03_hit_{keyIndex:00}");
        }

        private void SpawnOnActor(GameObject prefab, string key)
        {
            if (prefab == null)
                return;

            var instance = Instantiate(prefab, transform);
            instance.name = key + "_Runtime";
            instance.transform.localPosition = actorLocalOffset;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ArmOriginalFx(instance);
        }

        private void SpawnAtTarget(GameObject prefab, Transform target, string key)
        {
            if (prefab == null || target == null)
                return;

            var instance = Instantiate(
                prefab,
                target.position + hitWorldOffset,
                Quaternion.identity);
            instance.name = key + "_Runtime";
            ArmOriginalFx(instance);
        }

        private void ArmOriginalFx(GameObject instance)
        {
            if (instance == null)
                return;

            var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var system = particleSystems[i];
                if (system == null)
                    continue;
                system.gameObject.SetActive(true);
                system.Play(true);
            }

            var animators = instance.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].enabled = true;
            }

            Destroy(instance, fallbackLifetimeSeconds);
        }

        private void WarnIfOriginalAssetsMissing()
        {
            if (_warnedMissing)
                return;

            var availableJueying = jueyingHitFx?.Count(item => item != null) ?? 0;
            var available =
                (attackStartFx != null ? 1 : 0) +
                (attackHitFx != null ? 1 : 0) +
                (drawStartFx != null ? 1 : 0) +
                (drawHitFx != null ? 1 : 0) +
                (drawBuffFx != null ? 1 : 0) +
                (jueyingStartFx != null ? 1 : 0) +
                (jueyingStart02Fx != null ? 1 : 0) +
                availableJueying;
            if (available >= 17)
                return;

            _warnedMissing = true;
            Debug.LogWarning(
                $"[ArknightsACT/ChenSkillFX] Original client combat FX are incomplete ({available}/17). " +
                "No synthetic fallback will be drawn. Import Ch'en battle FX from an OHMS structured export into " +
                "Assets/_Game/Art/FX/OriginalClient/Chen, then rebuild Prototype Scene.",
                this);
        }
    }
}
