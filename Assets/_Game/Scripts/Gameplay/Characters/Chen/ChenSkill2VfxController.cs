using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Target-side presentation for Ch'en's 赤霄·绝影.
    /// The authored character Spine still owns Skill_3 / Skill_End_3; this controller adds the
    /// world-space rapid slash streaks around each resolved target so the ten-hit sequence reads
    /// clearly in the 2.5D ACT camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ChenSkill2))]
    public sealed class ChenSkill2VfxController : MonoBehaviour
    {
        [Header("Slash streaks")]
        [SerializeField, Min(0.05f)] private float slashDuration = 0.16f;
        [SerializeField, Min(0.2f)] private float slashLength = 2.35f;
        [SerializeField, Min(0.01f)] private float slashWidth = 0.095f;
        [SerializeField, Min(0f)] private float slashHeight = 0.92f;
        [SerializeField] private Color slashCore = new Color(1.00f, 0.92f, 0.72f, 1f);
        [SerializeField] private Color slashGlow = new Color(1.00f, 0.30f, 0.08f, 0.58f);

        [Header("Final strike")]
        [SerializeField, Min(1f)] private float finalLengthMultiplier = 1.45f;
        [SerializeField, Min(0.05f)] private float finalDuration = 0.28f;
        [SerializeField, Min(0.2f)] private float finalRingRadius = 1.28f;

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

            SpawnSlash(center, angle, length, duration, 1f);
            SpawnSlash(center, angle + (isFinal ? 92f : 7f), length * (isFinal ? 0.92f : 0.78f), duration * 0.92f, 0.58f);

            if (isFinal)
            {
                SpawnSlash(center, angle - 48f, length * 0.78f, duration, 0.72f);
                StartCoroutine(AnimateRing(center, finalRingRadius, finalDuration));
            }
        }

        private void SpawnSlash(Vector3 center, float angleDegrees, float length, float duration, float intensity)
        {
            var slash = new GameObject("JueyingSlash");
            slash.transform.SetParent(_effectRoot, false);

            var glow = slash.AddComponent<LineRenderer>();
            ConfigureLine(glow, slashWidth * 2.8f, slashGlow, 10);

            var coreObject = new GameObject("Core");
            coreObject.transform.SetParent(slash.transform, false);
            var core = coreObject.AddComponent<LineRenderer>();
            ConfigureLine(core, slashWidth, slashCore, 11);

            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var radians = angleDegrees * Mathf.Deg2Rad;
            var axis = (right * Mathf.Cos(radians) + up * Mathf.Sin(radians)).normalized;
            var normal = (-right * Mathf.Sin(radians) + up * Mathf.Cos(radians)).normalized;
            var half = axis * (length * 0.5f);
            var bend = normal * (0.10f * length);

            var p0 = center - half;
            var p1 = center + bend;
            var p2 = center + half;
            SetLinePoints(glow, p0, p1, p2);
            SetLinePoints(core, p0, p1, p2);

            StartCoroutine(FadeSlash(slash, glow, core, duration, intensity));
        }

        private IEnumerator FadeSlash(
            GameObject owner,
            LineRenderer glow,
            LineRenderer core,
            float duration,
            float intensity)
        {
            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.02f, duration);
            while (elapsed < safeDuration && owner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var alpha = 1f - t;
                alpha *= alpha;

                if (glow != null)
                {
                    var c = slashGlow;
                    c.a *= alpha * intensity;
                    glow.startColor = c;
                    glow.endColor = new Color(c.r, c.g, c.b, 0f);
                    glow.widthMultiplier = slashWidth * 2.8f * Mathf.Lerp(1.15f, 0.35f, t);
                }

                if (core != null)
                {
                    var c = slashCore;
                    c.a *= alpha * Mathf.Clamp01(intensity + 0.25f);
                    core.startColor = c;
                    core.endColor = new Color(c.r, c.g, c.b, 0f);
                    core.widthMultiplier = slashWidth * Mathf.Lerp(1f, 0.22f, t);
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
            ConfigureLine(line, slashWidth * 0.72f, slashGlow, 9);
            line.loop = true;

            const int segments = 28;
            line.positionCount = segments;
            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.05f, duration);

            while (elapsed < safeDuration && ringObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var currentRadius = radius * Mathf.Lerp(0.38f, 1.18f, t);
                for (var i = 0; i < segments; i++)
                {
                    var a = (i / (float)segments) * Mathf.PI * 2f;
                    line.SetPosition(i, center + right * Mathf.Cos(a) * currentRadius + up * Mathf.Sin(a) * currentRadius);
                }

                var c = slashGlow;
                c.a *= (1f - t) * 0.78f;
                line.startColor = c;
                line.endColor = c;
                line.widthMultiplier = slashWidth * Mathf.Lerp(1.15f, 0.25f, t);
                yield return null;
            }

            if (ringObject != null)
                Destroy(ringObject);
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
                var shader = Shader.Find("Sprites/Default") ??
                             Shader.Find("Universal Render Pipeline/Unlit") ??
                             Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    _lineMaterial = new Material(shader)
                    {
                        name = "Chen_Jueying_RuntimeVFX"
                    };
                    _lineMaterial.renderQueue = 3100;
                }
            }
        }

        private void ConfigureLine(LineRenderer line, float width, Color color, int sortingOrder)
        {
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.widthMultiplier = width;
            line.sharedMaterial = _lineMaterial;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.sortingOrder = sortingOrder;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.14f, 1f),
                new Keyframe(0.82f, 0.72f),
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
