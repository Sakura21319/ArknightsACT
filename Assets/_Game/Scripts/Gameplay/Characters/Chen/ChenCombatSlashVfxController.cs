using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// High-contrast slash readability layer shared by Ch'en's normal attacks and Jueying impacts.
    /// The authored Spine animation/effects remain the primary presentation; this layer recreates the
    /// bright white crossing blades, hot orange impact core and dark trailing streaks visible in the
    /// reference combat footage, which otherwise get washed out by the brighter 2.5D city scene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController), typeof(ChenSkill2))]
    public sealed class ChenCombatSlashVfxController : MonoBehaviour
    {
        [Header("Reference look")]
        [SerializeField] private Color bladeCore = new Color(1f, 0.985f, 0.92f, 1f);
        [SerializeField] private Color hotOrange = new Color(1f, 0.22f, 0.015f, 0.98f);
        [SerializeField] private Color darkTrail = new Color(0.055f, 0.012f, 0.008f, 0.88f);
        [SerializeField, Min(0.01f)] private float coreWidth = 0.105f;
        [SerializeField, Min(0f)] private float effectHeight = 0.92f;

        [Header("Normal attack")]
        [SerializeField, Min(0.2f)] private float normalArcRadius = 1.72f;
        [SerializeField, Min(0.05f)] private float normalArcDuration = 0.15f;
        [SerializeField, Min(0.2f)] private float normalImpactSize = 1.62f;

        [Header("Jueying booster")]
        [SerializeField, Min(0.2f)] private float jueyingImpactSize = 2.48f;
        [SerializeField, Min(0.05f)] private float jueyingImpactDuration = 0.17f;
        [SerializeField, Min(0f)] private float jueyingFlashIntensity = 4.8f;

        private static readonly float[] JueyingAngles =
        {
            16f, -42f, 68f, -72f, 34f, -20f, 82f, -54f, 7f, -84f, 48f, -30f
        };

        private PlayerAttackController _attack;
        private ChenSkill2 _skill2;
        private IPlayerLocomotion _motor;
        private Camera _camera;
        private Transform _effectRoot;
        private Material _lineMaterial;
        private bool _warnedMissingShader;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _skill2 = GetComponent<ChenSkill2>();
            _motor = ResolveLocomotion();
            _camera = Camera.main;
            EnsureResources();
        }

        private void OnEnable()
        {
            _attack ??= GetComponent<PlayerAttackController>();
            _skill2 ??= GetComponent<ChenSkill2>();
            if (_attack != null)
            {
                _attack.AttackStarted += HandleAttackStarted;
                _attack.AttackHit += HandleAttackHit;
            }
            if (_skill2 != null)
                _skill2.StrikeResolved += HandleJueyingStrike;
        }

        private void OnDisable()
        {
            if (_attack != null)
            {
                _attack.AttackStarted -= HandleAttackStarted;
                _attack.AttackHit -= HandleAttackHit;
            }
            if (_skill2 != null)
                _skill2.StrikeResolved -= HandleJueyingStrike;
        }

        private void OnDestroy()
        {
            if (_effectRoot != null)
                Destroy(_effectRoot.gameObject);
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        private void HandleAttackStarted(int comboIndex)
        {
            EnsureResources();
            EnsureCamera();
            if (_camera == null || _lineMaterial == null)
                return;

            var forward = ResolveForward();
            var center = transform.position + forward * 0.95f + Vector3.up * effectHeight;
            var combo = Mathf.Abs(comboIndex) % 3;
            var radius = normalArcRadius * (combo == 2 ? 1.16f : 1f);
            var start = combo switch
            {
                0 => -78f,
                1 => 72f,
                _ => -102f
            };
            var end = combo switch
            {
                0 => 56f,
                1 => -62f,
                _ => 82f
            };

            SpawnSwingArc(center, forward, radius, start, end, normalArcDuration * (combo == 2 ? 1.12f : 1f));
        }

        private void HandleAttackHit(CombatEntity target)
        {
            if (target == null)
                return;
            EnsureResources();
            EnsureCamera();
            if (_camera == null || _lineMaterial == null)
                return;

            var center = target.transform.position + Vector3.up * effectHeight;
            var angle = 22f + (Time.frameCount % 3) * 17f;
            SpawnImpactCluster(center, angle, normalImpactSize, 0.12f, false);
        }

        private void HandleJueyingStrike(int strikeIndex, int totalStrikes, Vector3 targetPosition, bool isFinal)
        {
            EnsureResources();
            EnsureCamera();
            if (_camera == null || _lineMaterial == null)
                return;

            var center = targetPosition + Vector3.up * effectHeight;
            var angle = JueyingAngles[Mathf.Abs(strikeIndex) % JueyingAngles.Length];
            var size = jueyingImpactSize * (isFinal ? 1.42f : 1f);
            var duration = jueyingImpactDuration * (isFinal ? 1.32f : 1f);
            SpawnImpactCluster(center, angle, size, duration, true);
            StartCoroutine(FlashImpact(center, duration * 0.58f, isFinal ? 1.35f : 0.78f));
        }

        private void SpawnSwingArc(Vector3 center, Vector3 forward, float radius, float startDeg, float endDeg, float life)
        {
            var owner = new GameObject("ChenNormalSwingArc");
            owner.transform.SetParent(_effectRoot, false);

            var shadow = owner.AddComponent<LineRenderer>();
            ConfigureLine(shadow, coreWidth * 4.6f, darkTrail, 124, 13);

            var hotObject = new GameObject("HotTrail");
            hotObject.transform.SetParent(owner.transform, false);
            var hot = hotObject.AddComponent<LineRenderer>();
            ConfigureLine(hot, coreWidth * 2.55f, hotOrange, 125, 13);

            var coreObject = new GameObject("BladeCore");
            coreObject.transform.SetParent(owner.transform, false);
            var core = coreObject.AddComponent<LineRenderer>();
            ConfigureLine(core, coreWidth, bladeCore, 126, 13);

            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var forwardBias = Vector3.ProjectOnPlane(forward, _camera.transform.forward).normalized;
            if (forwardBias.sqrMagnitude < 0.001f)
                forwardBias = right;

            const int points = 13;
            for (var i = 0; i < points; i++)
            {
                var t = i / (float)(points - 1);
                var a = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                var point = center +
                            right * Mathf.Sin(a) * radius +
                            up * Mathf.Cos(a) * radius * 0.52f +
                            forwardBias * Mathf.Lerp(-0.20f, 0.24f, t) * radius;
                shadow.SetPosition(i, point - up * 0.035f);
                hot.SetPosition(i, point);
                core.SetPosition(i, point + up * 0.012f);
            }

            StartCoroutine(FadeTriple(owner, shadow, hot, core, life, 1f));
        }

        private void SpawnImpactCluster(Vector3 center, float angle, float size, float life, bool dense)
        {
            // The reference has a hot central crossing plus several long black/red streaks punching
            // through the target. Use fixed high-opacity layers instead of a broad translucent glow.
            SpawnImpactLine(center, angle, size * 1.30f, coreWidth * 1.15f, bladeCore, life, 132);
            SpawnImpactLine(center, angle + 88f, size * 1.08f, coreWidth * 0.95f, bladeCore, life * 0.92f, 132);
            SpawnImpactLine(center, angle - 16f, size * 1.46f, coreWidth * 2.9f, hotOrange, life, 130);
            SpawnImpactLine(center, angle + 104f, size * 1.26f, coreWidth * 2.5f, hotOrange, life * 0.90f, 130);
            SpawnImpactLine(center, angle + 24f, size * 1.70f, coreWidth * 3.4f, darkTrail, life * 1.08f, 128);
            SpawnImpactLine(center, angle - 58f, size * 1.52f, coreWidth * 2.8f, darkTrail, life, 128);

            if (!dense)
                return;

            SpawnImpactLine(center, angle + 46f, size * 0.92f, coreWidth * 0.66f, bladeCore, life * 0.72f, 133);
            SpawnImpactLine(center, angle - 86f, size * 0.78f, coreWidth * 0.62f, hotOrange, life * 0.68f, 131);
        }

        private void SpawnImpactLine(
            Vector3 center,
            float angleDegrees,
            float length,
            float width,
            Color color,
            float life,
            int sortingOrder)
        {
            var owner = new GameObject("ChenImpactStreak");
            owner.transform.SetParent(_effectRoot, false);
            var line = owner.AddComponent<LineRenderer>();
            ConfigureLine(line, width, color, sortingOrder, 3);

            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var radians = angleDegrees * Mathf.Deg2Rad;
            var axis = (right * Mathf.Cos(radians) + up * Mathf.Sin(radians)).normalized;
            var normal = (-right * Mathf.Sin(radians) + up * Mathf.Cos(radians)).normalized;
            var half = axis * (length * 0.5f);
            line.SetPosition(0, center - half);
            line.SetPosition(1, center + normal * length * 0.055f);
            line.SetPosition(2, center + half);
            StartCoroutine(FadeSingle(owner, line, color, width, life));
        }

        private IEnumerator FadeTriple(
            GameObject owner,
            LineRenderer shadow,
            LineRenderer hot,
            LineRenderer core,
            float duration,
            float intensity)
        {
            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.04f, duration);
            while (elapsed < safeDuration && owner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var alpha = (1f - t) * (1f - t);
                SetLineAlpha(shadow, darkTrail, alpha * 0.92f * intensity);
                SetLineAlpha(hot, hotOrange, alpha * intensity);
                SetLineAlpha(core, bladeCore, Mathf.Min(1f, alpha * 1.25f) * intensity);
                yield return null;
            }
            if (owner != null)
                Destroy(owner);
        }

        private IEnumerator FadeSingle(GameObject owner, LineRenderer line, Color baseColor, float baseWidth, float duration)
        {
            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.035f, duration);
            while (elapsed < safeDuration && owner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var alpha = (1f - t) * (1f - t);
                SetLineAlpha(line, baseColor, alpha);
                if (line != null)
                    line.widthMultiplier = baseWidth * Mathf.Lerp(1.18f, 0.20f, t);
                yield return null;
            }
            if (owner != null)
                Destroy(owner);
        }

        private IEnumerator FlashImpact(Vector3 center, float duration, float scale)
        {
            if (jueyingFlashIntensity <= 0f)
                yield break;

            var owner = new GameObject("ChenSlashImpactLight");
            owner.transform.SetParent(_effectRoot, false);
            owner.transform.position = center;
            var light = owner.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.25f, 0.035f);
            light.range = 3.4f * scale;
            light.intensity = jueyingFlashIntensity * scale;
            light.shadows = LightShadows.None;

            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.035f, duration);
            while (elapsed < safeDuration && owner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                light.intensity = jueyingFlashIntensity * scale * (1f - t) * (1f - t);
                yield return null;
            }
            if (owner != null)
                Destroy(owner);
        }

        private void EnsureResources()
        {
            if (_effectRoot == null)
            {
                var root = new GameObject("[Chen_CombatSlash_VFX]");
                _effectRoot = root.transform;
            }

            if (_lineMaterial != null)
                return;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                if (!_warnedMissingShader)
                {
                    _warnedMissingShader = true;
                    Debug.LogWarning("[ArknightsACT/ChenVFX] No safe line shader was found; combat slash enhancement is disabled.", this);
                }
                return;
            }

            _lineMaterial = new Material(shader)
            {
                name = "Chen_CombatSlash_RuntimeVFX",
                renderQueue = (int)RenderQueue.Transparent + 95
            };
            if (_lineMaterial.HasProperty("_Color"))
                _lineMaterial.SetColor("_Color", Color.white);
        }

        private void ConfigureLine(LineRenderer line, float width, Color color, int sortingOrder, int positionCount)
        {
            line.useWorldSpace = true;
            line.positionCount = positionCount;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.widthMultiplier = width;
            line.sharedMaterial = _lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (positionCount > 2)
            {
                line.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.09f, 1f),
                    new Keyframe(0.88f, 0.78f),
                    new Keyframe(1f, 0f));
            }
        }

        private static void SetLineAlpha(LineRenderer line, Color baseColor, float alphaMultiplier)
        {
            if (line == null)
                return;
            var c = baseColor;
            c.a = Mathf.Clamp01(baseColor.a * alphaMultiplier);
            line.startColor = c;
            line.endColor = c;
        }

        private void EnsureCamera()
        {
            if (_camera == null)
                _camera = Camera.main;
        }

        private Vector3 ResolveForward()
        {
            _motor ??= ResolveLocomotion();
            var forward = _motor != null ? _motor.PlanarForward : transform.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            return forward.normalized;
        }

        private IPlayerLocomotion ResolveLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            return null;
        }
    }
}
