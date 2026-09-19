using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArknightsACT.Combat;
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
        [SerializeField, Min(0.25f)] private float fallbackLifetimeSeconds = 4f;

        private PlayerAttackController _attacks;
        private PlayerSkillController _skills;
        private ChenSkill1 _draw;
        private ChenSkill2 _jueying;
        private bool _jueyingStart02Played;
        private bool _warnedMissing;
        private readonly HashSet<string> _warnedMissingKeys = new(StringComparer.OrdinalIgnoreCase);

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
            Debug.Log(
                $"[ArknightsACT/ChenSkillFX] Runtime bridge ready on '{name}': " +
                $"attack={_attacks != null}, skills={_skills != null}, draw={_draw != null}, jueying={_jueying != null}, " +
                $"fx={CountConfiguredFx()}/17.",
                this);
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
            {
                WarnMissingKey(key);
                return;
            }

            var instance = Instantiate(prefab, transform);
            instance.name = key + "_Runtime";

            // Original client FX prefabs already contain static_offset / rotation_y / emitter
            // hierarchy. Do not overwrite their root transform with a generic actor offset.
            // The previous placement code collapsed the whole FX hierarchy onto Chen's body,
            // producing the large white vertical slash in runtime.
            var root = instance.transform;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            Debug.Log($"[ArknightsACT/ChenSkillFX] SpawnOnActor '{key}' from '{prefab.name}'.", this);
            ArmOriginalFx(instance);
        }

        private void SpawnAtTarget(GameObject prefab, Transform target, string key)
        {
            if (prefab == null)
            {
                WarnMissingKey(key);
                return;
            }
            if (target == null)
                return;

            var instance = Instantiate(
                prefab,
                target.position,
                Quaternion.identity);
            instance.name = key + "_Runtime";

            // Keep source prefab offsets. Hit FX already owns its static_offset placement.
            // Adding gameplay-side offsets here duplicates the original client offset.
            Debug.Log($"[ArknightsACT/ChenSkillFX] SpawnAtTarget '{key}' from '{prefab.name}'.", this);
            ArmOriginalFx(instance);
        }

        private void ArmOriginalFx(GameObject instance)
        {
            if (instance == null)
                return;

            // The extracted hierarchy already contains the source m_IsActive state.  Do not
            // recursively enable every child here: the original client deliberately keeps
            // helper emitters inactive until their animation/script asks for them.  Enabling the
            // whole tree at once was the main reason a single slash became a cloud of unrelated
            // particles in the prototype.
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var hiddenMissingMaterialRenderers = 0;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                    continue;

                // OHMS keeps unresolved cross-bundle references as a diagnostic material.  That
                // red marker is useful in the importer report, but it must never become gameplay
                // VFX.  Hide renderers whose complete material set is unresolved while allowing
                // partially reconstructed renderers to keep their valid slots.
                if (UsesOnlyMissingExternalMaterials(renderer))
                {
                    renderer.enabled = false;
                    hiddenMissingMaterialRenderers++;
                }
            }

            var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var system = particleSystems[i];
                if (system == null)
                    continue;

                var particleRenderer = system.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null && UsesOnlyMissingExternalMaterials(particleRenderer))
                {
                    particleRenderer.enabled = false;
                    continue;
                }

                var main = system.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Reconstruct the client FX as a timeline. The prefab is a container, not a finished
            // animation clip: every child renderer/particle group has its own attack-frame timing.
            PrepareChenFxTimeline(instance);
            StartCoroutine(PlayChenFxTimeline(instance));

            if (hiddenMissingMaterialRenderers > 0)
            {
                Debug.Log(
                    $"[ArknightsACT/ChenSkillFX] '{instance.name}' hid {hiddenMissingMaterialRenderers} renderer(s) " +
                    "whose source dependency material is unavailable; diagnostic red placeholders are not played as combat FX.",
                    instance);
            }

            if (renderers.Length == 0 && particleSystems.Length == 0)
            {
                Debug.LogWarning(
                    $"[ArknightsACT/ChenSkillFX] '{instance.name}' contains no Renderer or ParticleSystem after instantiation.",
                    instance);
            }

            var animators = instance.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animators[i].gameObject.activeInHierarchy)
                    animators[i].enabled = true;
            }

            // Original hit FX are short animation fragments. The old 4s fallback lifetime left
            // completed slash textures lingering on screen and made the blade look like a static
            // white wall. Let the imported particle lifetime drive the effect; this is only a safety
            // cleanup.
            Destroy(instance, Mathf.Min(fallbackLifetimeSeconds, 1.6f));
        }

        private static bool UsesOnlyMissingExternalMaterials(Renderer renderer)
        {
            if (renderer == null)
                return false;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return false;

            var sawMaterial = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                    continue;

                sawMaterial = true;
                if (!material.name.StartsWith("OHMS_MissingExternalMaterial", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return sawMaterial;
        }

        private static void PrepareChenFxTimeline(GameObject instance)
        {
            if (instance == null)
                return;

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var name = renderer.transform.name;
                if (IsOriginalDisabledNode(name))
                    renderer.enabled = false;
                else if (IsTimelineControllerNode(renderer.transform))
                    renderer.enabled = false;
                else if (IsImpactFxNode(name) || IsSecondaryFxNode(name))
                    renderer.enabled = false;
            }
        }

        private static IEnumerator PlayChenFxTimeline(GameObject instance)
        {
            if (instance == null)
                yield break;

            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);

            // Frame 0-3: blade release.
            foreach (var renderer in renderers)
            {
                if (renderer != null && IsBladeFxNode(renderer.transform.name) &&
                    !IsOriginalDisabledNode(renderer.transform.name))
                    renderer.enabled = true;
            }
            foreach (var system in systems)
            {
                if (system != null && IsBladeFxNode(system.transform.name) &&
                    !IsOriginalDisabledNode(system.transform.name) && !IsTimelineControllerNode(system.transform))
                    system.Play(false);
            }

            yield return new WaitForSeconds(0.035f);

            // Frame 4-8: hit flash. This is intentionally delayed; original client does not show
            // the impact layer together with the outgoing slash.
            foreach (var renderer in renderers)
            {
                if (renderer != null && IsImpactFxNode(renderer.transform.name))
                    renderer.enabled = true;
            }
            foreach (var system in systems)
            {
                if (system != null && IsImpactFxNode(system.transform.name))
                    system.Play(false);
            }

            yield return new WaitForSeconds(0.06f);

            foreach (var renderer in renderers)
            {
                if (renderer != null && IsSecondaryFxNode(renderer.transform.name))
                    renderer.enabled = true;
            }
            foreach (var system in systems)
            {
                if (system != null && IsSecondaryFxNode(system.transform.name))
                    system.Play(false);
            }
        }

        private static bool IsTimelineControllerNode(Transform node)
        {
            if (node == null)
                return false;
            var parent = node.parent;
            while (parent != null)
            {
                if (parent.name.Equals("static_offset", StringComparison.OrdinalIgnoreCase) ||
                    parent.name.Equals("rotation_y", StringComparison.OrdinalIgnoreCase))
                    return true;
                parent = parent.parent;
            }
            return false;
        }

        private static bool IsBladeFxNode(string name)
        {
            return name.Contains("daoguang", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("jian", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOriginalDisabledNode(string name)
        {
            // chen_skill_03_hit_01's original FX controller explicitly lists daoguang_liang
            // in _disableObjects.  It is a helper highlight layer, not the outgoing blade; the
            // extracted prefab keeps it active because the controller MonoBehaviour is not run.
            return name.Contains("daoguang_liang", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImpactFxNode(string name)
        {
            return name.Contains("baoci", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("baoshan", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("manyue", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSecondaryFxNode(string name)
        {
            return name.Contains("huoxing", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("smoke", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("lizi", StringComparison.OrdinalIgnoreCase);
        }

        private int CountConfiguredFx()
        {
            var count = 0;
            count += attackStartFx != null ? 1 : 0;
            count += attackHitFx != null ? 1 : 0;
            count += drawStartFx != null ? 1 : 0;
            count += drawHitFx != null ? 1 : 0;
            count += drawBuffFx != null ? 1 : 0;
            count += jueyingStartFx != null ? 1 : 0;
            count += jueyingStart02Fx != null ? 1 : 0;
            if (jueyingHitFx != null)
                count += jueyingHitFx.Count(item => item != null);
            return count;
        }

        private void WarnMissingKey(string key)
        {
            if (!_warnedMissingKeys.Add(key))
                return;
            Debug.LogWarning(
                $"[ArknightsACT/ChenSkillFX] FX prefab '{key}' is not configured on '{name}'. " +
                "Rebuild the Prototype scene after importing the Chen package.",
                this);
        }

        private static void ActivateAncestorChain(Transform current, Transform root)
        {
            while (current != null)
            {
                current.gameObject.SetActive(true);
                if (current == root)
                    break;
                current = current.parent;
            }
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
