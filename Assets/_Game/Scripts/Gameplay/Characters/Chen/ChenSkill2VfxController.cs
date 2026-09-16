using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Target-side presentation for Ch'en's 赤霄·绝影.
    /// The authored character Spine still owns Skill_3 / Skill_End_3; this controller reinforces the
    /// world-space rapid slash streaks around each resolved target so the ten-hit sequence remains
    /// readable against the brighter city environment.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ChenSkill2))]
    public sealed class ChenSkill2VfxController : MonoBehaviour
    {
        [Header("Slash streaks")]
        [SerializeField, Min(0.05f)] private float slashDuration = 0.21f;
        [SerializeField, Min(0.2f)] private float slashLength = 2.65f;
        [SerializeField, Min(0.01f)] private float slashWidth = 0.13f;
        [SerializeField, Min(0f)] private float slashHeight = 0.92f;
        [SerializeField, Range(0.5f, 3f)] private float intensityMultiplier = 1.65f;
        [SerializeField] private Color slashCore = new Color(1.32f, 1.08f, 0.78f, 1f);
        [SerializeField] private Color slashGlow = new Color(1.28f, 0.30f, 0.055f, 0.92f);

        [Header("Final strike")]
        [SerializeField, Min(1f)] private float finalLengthMultiplier = 1.62f;
        [SerializeField, Min(0.05f)] private float finalDuration = 0.34f;
        [SerializeField, Min(0.2f)] private float finalRingRadius = 1.48f;
        [SerializeField, Min(0f)] private float finalFlashLightIntensity = 2.2f;

        private static readonly float[] SlashAngles =
        {
            18f, -38f, 62f, -66f, 34f, -18f, 78f, -52f, 8f, -82f, 46f, -28f
        };

        private ChenSkill2 _skill;
        private Camera _camera;
        private Transform _effectRoot;
        private Material _lineMaterial;

        private void Awake()
        {
            _skill = GetComponent<ChenSkill2>();
            _camera = Camera.main;
            EnsureResources();
        }

        private void OnEnable()
        {
            _skill ??= GetComponent<ChenSkill2>();
            if (_skill != null)
                _skill.StrikeResolved += HandleStrikeResolved;
        }

        private void OnDisable()
        {
            if (_skill != null)
                _skill.StrikeResolved -= HandleStrikeResolved;
        }

        private void OnDestroy()
        {
            if (_effectRoot != null)
                Destroy(_effectRoot.gameObject);
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        private void HandleStrikeResolved(int strikeIndex, int totalStrikes, Vector3 targetPosition, bool isFinal)
        {
            EnsureResources();
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            var center = targetPosition + Vector3.up * slashHeight;
            var angle = SlashAngles[Mathf.Abs(strikeIndex) % SlashAngles.Length];
            var length = slashLength * (isFinal ? finalLengthMultiplier : 1f);
            var duration = isFinal ? finalDuration : slashDuration;

            // Broad additive glow + hot core. The authored Spine effect stays visible underneath;
            // these target-space streaks merely reinforce it for the 2.5D gameplay camera.
            SpawnSlash(center, angle, length, duration, 1f);
            SpawnSlash(
                center,
                angle + (isFinal ? 92f : 7f),
                length * (isFinal ? 0.94f : 0.80f),
                duration * 0.94f,
                isFinal ? 0.92f : 0.72f);

            // Tiny crossing spark keeps every hit legible without turning the whole screen orange.
            SpawnSlash(center, angle + 90f, length * 0.28f, duration * 0.58f, 0.48f);

            if (isFinal)
            {
                SpawnSlash(center, angle - 48f, length * 0.82f, duration, 0.88f);
                StartCoroutine(AnimateRing(center, finalRingRadius, finalDuration));
                StartCoroutine(AnimateFinalLight(center, finalDuration * 0.62f));
            }
        }

        private void SpawnSlash(Vector3 center, float angleDegrees, float length, float duration, float intensity)
        {
            var slash = new GameObject("JueyingSlash");
            slash.transform.SetParent(_effectRoot, false);

            var outerGlow = slash.AddComponent<LineRenderer>();
            ConfigureLine(outerGlow, slashWidth * 4.8f, slashGlow, 118);

            var innerObject = new GameObject("InnerGlow");
            innerObject.transform.SetParent(slash.transform, false);
            var innerGlow = innerObject.AddComponent<LineRenderer>();
            ConfigureLine(innerGlow, slashWidth * 2.35f, slashGlow, 119);

            var coreObject = new GameObject("Core");
            coreObject.transform.SetParent(slash.transform, false);
            var core = coreObject.AddComponent<LineRenderer>();
            ConfigureLine(core, slashWidth, slashCore, 120);

            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var radians = angleDegrees * Mathf.Deg2Rad;
            var axis = (right * Mathf.Cos(radians) + up * Mathf.Sin(radians)).normalized;
            var normal = (-right * Mathf.Sin(radians) + up * Mathf.Cos(radians)).normalized;
            var half = axis * (length * 0.5f);
            var bend = normal * (0.095f * length);

            var p0 = center - half;
            var p1 = center + bend;
            var p2 = center + half;
            SetLinePoints(outerGlow, p0, p1, p2);
            SetLinePoints(innerGlow, p0, p1, p2);
            SetLinePoints(core, p0, p1, p2);

            StartCoroutine(FadeSlash(slash, outerGlow, innerGlow, core, duration, intensity));
        }

        private IEnumerator FadeSlash(
            GameObject owner,
            LineRenderer outerGlow,
            LineRenderer innerGlow,
            LineRenderer core,
            float duration,
            float intensity)
        {
            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.02f, duration);
            var visualIntensity = Mathf.Max(0.1f, intensity * intensityMultiplier);
            var widthBoost = 1f + Mathf.Max(0f, intensityMultiplier - 1f) * 0.12f;

            while (elapsed < safeDuration && owner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var alpha = 1f - t;
                alpha *= alpha;

                if (outerGlow != null)
                {
                    var c = slashGlow;
                    c.a = Mathf.Clamp01(c.a * alpha * visualIntensity * 0.72f);
                    outerGlow.startColor = c;
                    outerGlow.endColor = new Color(c.r, c.g, c.b, 0f);
                    outerGlow.widthMultiplier = slashWidth * 4.8f * widthBoost * Mathf.Lerp(1.22f, 0.42f, t);
                }

                if (innerGlow != null)
                {
                    var c = slashGlow;
                    c.a = Mathf.Clamp01(c.a * alpha * visualIntensity);
                    innerGlow.startColor = c;
                    innerGlow.endColor = new Color(c.r, c.g, c.b, 0f);
                    innerGlow.widthMultiplier = slashWidth * 2.35f * widthBoost * Mathf.Lerp(1.12f, 0.30f, t);
                }

                if (core != null)
                {
                    var c = slashCore;
                    c.a = Mathf.Clamp01(c.a * alpha * Mathf.Max(1f, visualIntensity * 0.86f));
                    core.startColor = c;
                    core.endColor = new Color(c.r, c.g, c.b, 0f);
                    core.widthMultiplier = slashWidth * widthBoost * Mathf.Lerp(1.08f, 0.20f, t);
                }

                yield return null;
            }

            if (owner != null)
                Destroy(owner);
        }

        private IEnumerator AnimateRing(Vector3 center, float radius, float duration)
        {
            var ringObject = new GameObject("JueyingFinalRing");
            ringObject.transform.SetParent(_effectRoot, false);
            var line = ringObject.AddComponent<LineRenderer>();
            ConfigureLine(line, slashWidth * 1.05f, slashGlow, 117);
            line.loop = true;

            const int segments = 32;
            line.positionCount = segments;
            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.05f, duration);

            while (elapsed < safeDuration && ringObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var currentRadius = radius * Mathf.Lerp(0.30f, 1.24f, t);
                for (var i = 0; i < segments; i++)
                {
                    var a = (i / (float)segments) * Mathf.PI * 2f;
                    line.SetPosition(i, center + right * Mathf.Cos(a) * currentRadius + up * Mathf.Sin(a) * currentRadius);
                }

                var c = slashGlow;
                c.a = Mathf.Clamp01(c.a * (1f - t) * intensityMultiplier);
                line.startColor = c;
                line.endColor = c;
                line.widthMultiplier = slashWidth * Mathf.Lerp(1.55f, 0.28f, t);
                yield return null;
            }

            if (ringObject != null)
                Destroy(ringObject);
        }

        private IEnumerator AnimateFinalLight(Vector3 center, float duration)
        {
            if (finalFlashLightIntensity <= 0f)
                yield break;

            var lightObject = new GameObject("JueyingFinalFlash");
            lightObject.transform.SetParent(_effectRoot, false);
            lightObject.transform.position = center;

            var flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.34f, 0.08f);
            flash.range = 3.3f;
            flash.intensity = finalFlashLightIntensity;
            flash.shadows = LightShadows.None;

            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.04f, duration);
            while (elapsed < safeDuration && lightObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                flash.intensity = finalFlashLightIntensity * (1f - t) * (1f - t);
                yield return null;
            }

            if (lightObject != null)
                Destroy(lightObject);
        }

        private void EnsureResources()
        {
            if (_effectRoot == null)
            {
                var root = new GameObject("[Chen_Jueying_VFX]");
                _effectRoot = root.transform;
            }

            if (_lineMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                             Shader.Find("Sprites/Default") ??
                             Shader.Find("Universal Render Pipeline/Unlit") ??
                             Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    _lineMaterial = new Material(shader)
                    {
                        name = "Chen_Jueying_RuntimeVFX"
                    };

                    // Prefer additive transparent blending when the selected shader exposes the
                    // standard URP blend controls. Fallback shaders still use the brighter HDR
                    // line colors, so this remains safe across Unity/URP versions.
                    if (_lineMaterial.HasProperty("_Surface"))
                        _lineMaterial.SetFloat("_Surface", 1f);
                    if (_lineMaterial.HasProperty("_Blend"))
                        _lineMaterial.SetFloat("_Blend", 2f);
                    if (_lineMaterial.HasProperty("_SrcBlend"))
                        _lineMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    if (_lineMaterial.HasProperty("_DstBlend"))
                        _lineMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
                    if (_lineMaterial.HasProperty("_ZWrite"))
                        _lineMaterial.SetFloat("_ZWrite", 0f);

                    _lineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _lineMaterial.renderQueue = (int)RenderQueue.Transparent + 80;
                }
            }
        }

        private void ConfigureLine(LineRenderer line, float width, Color color, int sortingOrder)
        {
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.widthMultiplier = width;
            line.sharedMaterial = _lineMaterial;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.sortingOrder = sortingOrder;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.10f, 1f),
                new Keyframe(0.78f, 0.82f),
                new Keyframe(1f, 0f));
        }

        private static void SetLinePoints(LineRenderer line, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            line.SetPosition(0, p0);
            line.SetPosition(1, p1);
            line.SetPosition(2, p2);
        }
    }
}
