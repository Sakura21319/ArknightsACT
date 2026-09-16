using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Readability layer for Ch'en's 赤霄·拔刀. The authored Spine Skill_2 animation stays the
    /// primary source of character FX; this controller adds a short, warm white/orange draw arc in
    /// world space on the exact impact frame so the slash remains visible against the 2.5D city.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ChenSkill1))]
    public sealed class ChenSkill1VfxController : MonoBehaviour
    {
        [Header("Draw slash")]
        [SerializeField, Min(0.05f)] private float duration = 0.24f;
        [SerializeField, Min(0.4f)] private float baseRadius = 2.15f;
        [SerializeField, Min(0.01f)] private float coreWidth = 0.10f;
        [SerializeField, Min(0f)] private float height = 0.92f;
        [SerializeField] private Color coreColor = new Color(1.00f, 0.91f, 0.68f, 1f);
        [SerializeField] private Color glowColor = new Color(1.00f, 0.22f, 0.035f, 0.82f);

        [Header("Impact accent")]
        [SerializeField, Min(0.05f)] private float sparkDuration = 0.13f;
        [SerializeField, Min(0.2f)] private float sparkLength = 1.10f;

        private ChenSkill1 _skill;
        private Camera _camera;
        private Transform _effectRoot;
        private Material _lineMaterial;
        private bool _warnedMissingShader;

        private void Awake()
        {
            _skill = GetComponent<ChenSkill1>();
            _camera = Camera.main;
            EnsureResources();
        }

        private void OnEnable()
        {
            _skill ??= GetComponent<ChenSkill1>();
            if (_skill != null)
                _skill.ImpactResolved += HandleImpactResolved;
        }

        private void OnDisable()
        {
            if (_skill != null)
                _skill.ImpactResolved -= HandleImpactResolved;
        }

        private void OnDestroy()
        {
            if (_effectRoot != null)
                Destroy(_effectRoot.gameObject);
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        private void HandleImpactResolved(Vector3 origin, Vector3 forward, float rangeMultiplier, bool hitAny)
        {
            EnsureResources();
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null || _lineMaterial == null)
                return;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            var range = Mathf.Clamp(rangeMultiplier, 0.8f, 2.5f);
            var center = origin + forward * (1.45f * range) + Vector3.up * height;
            var radius = baseRadius * Mathf.Lerp(0.95f, 1.32f, Mathf.InverseLerp(1f, 2.5f, range));

            SpawnArc(center, radius, duration, 1f);
            SpawnArc(center + _camera.transform.up * 0.06f, radius * 0.84f, duration * 0.82f, 0.62f);

            if (hitAny)
                SpawnImpactSpark(center + forward * (0.28f * range));
        }

        private void SpawnArc(Vector3 center, float radius, float life, float intensity)
        {
            var owner = new GameObject("ChixiaoDrawArc");
            owner.transform.SetParent(_effectRoot, false);

            var glow = owner.AddComponent<LineRenderer>();
            ConfigureLine(glow, coreWidth * 4.0f, glowColor, 116, 11);

            var coreObject = new GameObject("Core");
            coreObject.transform.SetParent(owner.transform, false);
            var core = coreObject.AddComponent<LineRenderer>();
            ConfigureLine(core, coreWidth, coreColor, 117, 11);

            var right = _camera.transform.right;
            var up = _camera.transform.up;
            const int points = 11;
            for (var i = 0; i < points; i++)
            {
                var t = i / (float)(points - 1);
                var angle = Mathf.Lerp(-64f, 64f, t) * Mathf.Deg2Rad;
                var horizontal = Mathf.Sin(angle) * radius;
                var vertical = Mathf.Cos(angle) * radius * 0.50f;
                var sweep = Mathf.Lerp(-0.22f, 0.18f, t) * radius;
                var point = center + right * horizontal + up * (vertical + sweep);
                glow.SetPosition(i, point);
                core.SetPosition(i, point);
            }

            StartCoroutine(FadeArc(owner, glow, core, life, intensity));
        }

        private IEnumerator FadeArc(GameObject owner, LineRenderer glow, LineRenderer core, float life, float intensity)
        {
            var elapsed = 0f;
            var safeLife = Mathf.Max(0.05f, life);
            while (elapsed < safeLife && owner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeLife);
                var alpha = 1f - t;
                alpha *= alpha;

                if (glow != null)
                {
                    var color = glowColor;
                    color.a = Mathf.Clamp01(color.a * alpha * intensity);
                    glow.startColor = color;
                    glow.endColor = new Color(color.r, color.g, color.b, 0f);
                    glow.widthMultiplier = coreWidth * 4f * Mathf.Lerp(1.15f, 0.28f, t);
                }

                if (core != null)
                {
                    var color = coreColor;
                    color.a = Mathf.Clamp01(color.a * alpha);
                    core.startColor = color;
                    core.endColor = new Color(color.r, color.g, color.b, 0f);
                    core.widthMultiplier = coreWidth * Mathf.Lerp(1.15f, 0.18f, t);
                }

                yield return null;
            }

            if (owner != null)
                Destroy(owner);
        }

        private void SpawnImpactSpark(Vector3 center)
        {
            var owner = new GameObject("ChixiaoImpactSpark");
            owner.transform.SetParent(_effectRoot, false);

            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var lineA = owner.AddComponent<LineRenderer>();
            ConfigureLine(lineA, coreWidth * 0.75f, coreColor, 118, 2);
            lineA.SetPosition(0, center - (right + up).normalized * (sparkLength * 0.5f));
            lineA.SetPosition(1, center + (right + up).normalized * (sparkLength * 0.5f));

            var child = new GameObject("Cross");
            child.transform.SetParent(owner.transform, false);
            var lineB = child.AddComponent<LineRenderer>();
            ConfigureLine(lineB, coreWidth * 0.62f, glowColor, 118, 2);
            lineB.SetPosition(0, center - (right - up).normalized * (sparkLength * 0.42f));
            lineB.SetPosition(1, center + (right - up).normalized * (sparkLength * 0.42f));

            StartCoroutine(FadeSpark(owner, lineA, lineB));
        }

        private IEnumerator FadeSpark(GameObject owner, LineRenderer a, LineRenderer b)
        {
            var elapsed = 0f;
            var safeLife = Mathf.Max(0.04f, sparkDuration);
            while (elapsed < safeLife && owner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeLife);
                var alpha = (1f - t) * (1f - t);

                if (a != null)
                {
                    var c = coreColor;
                    c.a *= alpha;
                    a.startColor = c;
                    a.endColor = c;
                }
                if (b != null)
                {
                    var c = glowColor;
                    c.a *= alpha;
                    b.startColor = c;
                    b.endColor = c;
                }
                yield return null;
            }

            if (owner != null)
                Destroy(owner);
        }

        private void EnsureResources()
        {
            if (_effectRoot == null)
            {
                var root = new GameObject("[Chen_ChixiaoDraw_VFX]");
                _effectRoot = root.transform;
            }

            if (_lineMaterial != null)
                return;

            // Use the simple sprite shader first. The previous URP Particles/Unlit runtime material
            // could render magenta/purple on some renderer configurations when its blend keywords did
            // not match the active pipeline. This path intentionally avoids runtime keyword surgery.
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                if (!_warnedMissingShader)
                {
                    _warnedMissingShader = true;
                    Debug.LogWarning("[ArknightsACT/ChenVFX] No safe line shader was found; skill VFX enhancement is disabled.", this);
                }
                return;
            }

            _lineMaterial = new Material(shader)
            {
                name = "Chen_Chixiao_RuntimeVFX",
                renderQueue = (int)RenderQueue.Transparent + 60
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
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.sortingOrder = sortingOrder;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
