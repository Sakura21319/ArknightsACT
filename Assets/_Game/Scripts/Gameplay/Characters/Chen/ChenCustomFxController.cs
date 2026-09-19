using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Small authored FX pass for Ch'en's attacks and the two active skills.
    ///
    /// Gameplay remains the source of truth: this component only listens to resolved events and
    /// never changes damage, targeting, combo state or skill state.  The controller owns every
    /// runtime instance and recycles it when the short-lived effect finishes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController), typeof(PlayerSkillController))]
    [RequireComponent(typeof(ChenSkill1), typeof(ChenSkill2))]
    public sealed class ChenCustomFxController : MonoBehaviour
    {
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SourceBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DestinationBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

        [Header("Generated slash texture")]
        [Tooltip("Optional transparent slash texture. The procedural line fallback still works when this is empty.")]
        [SerializeField] private Texture2D slashTexture;

        [Header("Palette")]
        [SerializeField] private Color emberColor = new(1f, 0.19f, 0.025f, 1f);
        [SerializeField] private Color goldColor = new(1f, 0.52f, 0.08f, 1f);
        [SerializeField] private Color coreColor = new(1f, 0.94f, 0.78f, 1f);

        [Header("Lifetime")]
        [SerializeField, Min(0.05f)] private float basicSlashLifetime = 0.20f;
        [SerializeField, Min(0.05f)] private float hitSparkLifetime = 0.16f;
        [SerializeField, Min(0.05f)] private float skillSlashLifetime = 0.28f;

        private readonly List<GameObject> _activeFx = new();

        private PlayerAttackController _attacks;
        private PlayerSkillController _skills;
        private ChenSkill1 _skill1;
        private ChenSkill2 _skill2;
        private PlayerMotor2D _motor2D;
        private PlayerMotor25D _motor25D;
        private Transform _mount;
        private Material _lineMaterial;
        private Shader _fxShader;

        public void Configure(Texture2D authoredSlashTexture)
        {
            if (authoredSlashTexture != null)
                slashTexture = authoredSlashTexture;
        }

        private void Awake()
        {
            _attacks = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _skill1 = GetComponent<ChenSkill1>();
            _skill2 = GetComponent<ChenSkill2>();
            _motor2D = GetComponent<PlayerMotor2D>();
            _motor25D = GetComponent<PlayerMotor25D>();
            _mount = transform.Find("CustomFxMountPoint");

            if (_mount == null)
            {
                var mountObject = new GameObject("CustomFxMountPoint");
                _mount = mountObject.transform;
                _mount.SetParent(transform, false);
                _mount.localPosition = new Vector3(0f, 0.8f, 0f);
                _mount.localRotation = Quaternion.identity;
                _mount.localScale = Vector3.one;
            }
        }

        private void OnEnable()
        {
            if (_attacks != null)
                _attacks.AttackStarted += OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCastSucceeded;
            if (_skill1 != null)
                _skill1.HitResolved += OnSkill1HitResolved;
            if (_skill2 != null)
                _skill2.StrikeResolved += OnSkill2StrikeResolved;
        }

        private void OnDisable()
        {
            if (_attacks != null)
                _attacks.AttackStarted -= OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCastSucceeded;
            if (_skill1 != null)
                _skill1.HitResolved -= OnSkill1HitResolved;
            if (_skill2 != null)
                _skill2.StrikeResolved -= OnSkill2StrikeResolved;

            StopAllCoroutines();
            for (var i = _activeFx.Count - 1; i >= 0; i--)
                Recycle(_activeFx[i]);
            _activeFx.Clear();
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        private void OnAttackStarted(int comboIndex)
        {
            var forward = GetActionForward();
            var center = GetActorCenter() + forward * 0.18f;
            var angle = comboIndex switch
            {
                0 => -25f,
                1 => 24f,
                _ => 4f
            };
            var length = comboIndex >= 2 ? 1.62f : 1.42f;
            var height = comboIndex >= 2 ? 0.34f : 0.26f;

            CreateSlash(
                center,
                forward,
                angle,
                length,
                height,
                coreColor,
                coreWidth: comboIndex >= 2 ? 0.040f : 0.032f,
                lifetime: basicSlashLifetime * 0.72f,
                textureScale: 0f,
                useTexture: false);
        }

        private void OnSkillCastSucceeded(int slot)
        {
            var center = GetActorCenter();
            if (slot == 1)
            {
                CreateCharge(center, goldColor, 1.12f, 0.25f);
                return;
            }

            if (slot == 2)
                CreateCharge(center, emberColor, 1.42f, 0.30f);
        }

        private void OnSkill1HitResolved(Transform target)
        {
            if (target == null)
                return;

            var center = GetTargetCenter(target);
            var forward = GetActionForward();
            CreateSlash(
                center,
                forward,
                0f,
                3.35f,
                0.62f,
                emberColor,
                coreWidth: 0.095f,
                lifetime: skillSlashLifetime,
                textureScale: 1.80f);
            CreateImpact(center, goldColor, 5, 0.56f, skillSlashLifetime * 0.86f);
        }

        private void OnSkill2StrikeResolved(int strikeIndex, Transform target, bool isFinal)
        {
            if (target == null)
                return;

            var center = GetTargetCenter(target);
            var forward = GetDirectionTo(GetActorCenter(), center);
            var angle = (strikeIndex & 1) == 0 ? 34f : -34f;
            angle += (strikeIndex % 3 - 1) * 7f;

            CreateSlash(
                center,
                forward,
                angle,
                isFinal ? 3.70f : 2.35f,
                isFinal ? 0.82f : 0.42f,
                isFinal ? goldColor : emberColor,
                coreWidth: isFinal ? 0.12f : 0.060f,
                lifetime: isFinal ? skillSlashLifetime * 1.25f : skillSlashLifetime * 0.72f,
                textureScale: isFinal ? 2.20f : 1.08f);

            if (isFinal)
            {
                CreateSlash(center, forward, -angle, 3.35f, 0.78f, coreColor, 0.060f, 0.26f, 1.72f);
                CreateBurst(center, goldColor, 6, 0.22f, 0.72f, 0.26f);
            }
            else if ((strikeIndex & 1) == 0)
            {
                CreateImpact(center, goldColor, 3, 0.32f, hitSparkLifetime * 0.75f);
            }
        }

        private void CreateCharge(Vector3 center, Color color, float radius, float lifetime)
        {
            GetFxBasis(GetActionForward(), out var forward, out var up, out var normal);
            var root = CreateEffectRoot(center, "Charge");
            var line = root.AddComponent<LineRenderer>();
            ConfigureLine(line, color.WithAlpha(0.52f), 0.026f, true);
            line.positionCount = 25;
            for (var i = 0; i <= 24; i++)
            {
                var radians = i / 24f * Mathf.PI * 2f;
                line.SetPosition(i, forward * (Mathf.Cos(radians) * radius) + up * (Mathf.Sin(radians) * radius));
            }
            root.transform.rotation = Quaternion.AngleAxis(10f, normal) * root.transform.rotation;
            StartCoroutine(AnimateLine(root, line, lifetime, 0.65f, 1.12f));

            CreateBurst(center, color, 6, radius * 0.72f, radius * 0.98f, lifetime * 0.9f);
        }

        private void CreateImpact(Vector3 center, Color color, int rayCount, float radius, float lifetime)
        {
            CreateBurst(center, color, rayCount, radius * 0.35f, radius, lifetime);
            CreateBurst(center, coreColor, Mathf.Max(3, rayCount / 2), radius * 0.18f, radius * 0.58f, lifetime * 0.72f);
        }

        private void CreateBurst(Vector3 center, Color color, int rayCount, float innerRadius, float outerRadius, float lifetime)
        {
            GetFxBasis(GetActionForward(), out var forward, out var up, out _);
            var phase = (center.x + center.y + center.z) * 13.17f;
            for (var i = 0; i < Mathf.Max(1, rayCount); i++)
            {
                var angle = phase + i * 360f / Mathf.Max(1, rayCount);
                var radians = angle * Mathf.Deg2Rad;
                var start = forward * (Mathf.Cos(radians) * innerRadius) + up * (Mathf.Sin(radians) * innerRadius);
                var lengthJitter = 0.84f + 0.16f * Mathf.Abs(Mathf.Sin((i + 1f) * 7.31f + phase));
                var endRadius = Mathf.Lerp(innerRadius, outerRadius, lengthJitter);
                var end = forward * (Mathf.Cos(radians) * endRadius) + up * (Mathf.Sin(radians) * endRadius);
                CreateSegment(center, start, end, color.WithAlpha(0.66f), 0.028f, lifetime);
            }
        }

        private void CreateSlash(
            Vector3 center,
            Vector3 forward,
            float angle,
            float length,
            float height,
            Color color,
            float coreWidth,
            float lifetime,
            float textureScale,
            bool useTexture = true)
        {
            GetFxBasis(forward, out var fxForward, out var fxUp, out var fxNormal);
            var points = BuildSlashPoints(fxForward, fxUp, fxNormal, angle, length, height);

            CreatePolyline(center, points, color.WithAlpha(0.16f), coreWidth * 1.60f, lifetime * 1.08f, "Slash_Glow");
            CreatePolyline(center, points, Color.Lerp(color, coreColor, 0.58f).WithAlpha(0.78f), coreWidth * 0.78f, lifetime, "Slash_Core");

            if (useTexture && slashTexture != null)
                CreateTextureSlash(center, fxForward, fxUp, fxNormal, angle, color, length * textureScale, lifetime * 1.05f);
        }

        private Vector3[] BuildSlashPoints(Vector3 forward, Vector3 up, Vector3 normal, float angle, float length, float height)
        {
            var rotation = Quaternion.AngleAxis(angle, normal);
            var points = new Vector3[7];
            var xValues = new[] { -0.78f, -0.56f, -0.30f, 0f, 0.30f, 0.56f, 0.78f };
            var curve = new[] { 0.02f, 0.30f, 0.49f, 0.58f, 0.47f, 0.25f, 0.01f };
            for (var i = 0; i < points.Length; i++)
            {
                var local = forward * (xValues[i] * length) + up * (curve[i] * height);
                points[i] = rotation * local;
            }
            return points;
        }

        private void CreatePolyline(Vector3 center, Vector3[] points, Color color, float width, float lifetime, string name)
        {
            var root = CreateEffectRoot(center, name);
            var line = root.AddComponent<LineRenderer>();
            ConfigureLine(line, color, width, true);
            line.positionCount = points.Length;
            for (var i = 0; i < points.Length; i++)
                line.SetPosition(i, points[i]);
            StartCoroutine(AnimateLine(root, line, lifetime, 0.72f, 1.08f));
        }

        private void CreateSegment(Vector3 center, Vector3 start, Vector3 end, Color color, float width, float lifetime)
        {
            var root = CreateEffectRoot(center, "Spark");
            var line = root.AddComponent<LineRenderer>();
            ConfigureLine(line, color, width, false);
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            StartCoroutine(AnimateLine(root, line, lifetime, 0.35f, 1.16f));
        }

        private void CreateTextureSlash(
            Vector3 center,
            Vector3 forward,
            Vector3 up,
            Vector3 normal,
            float angle,
            Color color,
            float length,
            float lifetime)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = "TextureSlash";
            var collider = root.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            root.transform.SetParent(_mount, true);
            root.transform.position = center;

            var orientation = Quaternion.LookRotation(normal, up);
            root.transform.rotation = Quaternion.AngleAxis(angle, normal) * orientation;
            root.transform.localScale = new Vector3(length, Mathf.Max(0.36f, length * 0.48f), 1f);

            var renderer = root.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 80;
            var material = CreateMaterial(slashTexture);
            SetMaterialColor(material, TintColorId, Color.white.WithAlpha(Mathf.Clamp01(color.a * 0.72f)));
            SetMaterialColor(material, ColorId, Color.white);
            renderer.sharedMaterial = material;
            _activeFx.Add(root);
            StartCoroutine(AnimateTexture(root, material, lifetime));
        }

        private IEnumerator AnimateLine(GameObject root, LineRenderer line, float lifetime, float fromScale, float toScale)
        {
            var width = line.widthMultiplier;
            var original = line.startColor;
            var elapsed = 0f;
            while (elapsed < lifetime && root != null)
            {
                var normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
                var intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized / 0.16f));
                var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((normalized - 0.42f) / 0.58f));
                root.transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, intro);
                line.widthMultiplier = width * Mathf.Lerp(0.20f, 1f, intro) * Mathf.Lerp(0.82f, 1f, fade);
                var color = original.WithAlpha(original.a * fade);
                line.startColor = color;
                line.endColor = color;
                elapsed += Time.deltaTime;
                yield return null;
            }
            Recycle(root);
        }

        private IEnumerator AnimateTexture(GameObject root, Material material, float lifetime)
        {
            var originalScale = root != null ? root.transform.localScale : Vector3.one;
            var elapsed = 0f;
            while (elapsed < lifetime && root != null)
            {
                var normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
                var intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized / 0.13f));
                var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((normalized - 0.38f) / 0.62f));
                var scale = Mathf.Lerp(0.86f, 1.02f, intro);
                root.transform.localScale = originalScale * scale;
                SetMaterialColor(material, TintColorId, Color.white.WithAlpha(fade * 0.72f));
                SetMaterialColor(material, ColorId, Color.white);
                elapsed += Time.deltaTime;
                yield return null;
            }
            Recycle(root);
        }

        private GameObject CreateEffectRoot(Vector3 center, string name)
        {
            var root = new GameObject($"ChenFx_{name}");
            root.transform.SetParent(_mount, true);
            root.transform.position = center;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            _activeFx.Add(root);
            return root;
        }

        private void ConfigureLine(LineRenderer line, Color color, float width, bool curved)
        {
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = Mathf.Max(0.005f, width);
            line.widthCurve = curved
                ? new AnimationCurve(new Keyframe(0f, 0.34f), new Keyframe(0.42f, 1f), new Keyframe(1f, 0.08f))
                : new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.18f));
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.startColor = color;
            line.endColor = color;
            line.sharedMaterial = GetLineMaterial();
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 82;
        }

        private Material GetLineMaterial()
        {
            if (_lineMaterial != null)
                return _lineMaterial;

            _lineMaterial = CreateMaterial(Texture2D.whiteTexture);
            _lineMaterial.name = "ChenCustomFx_LineMaterial";
            _lineMaterial.SetColor(TintColorId, Color.white);
            _lineMaterial.SetColor(ColorId, Color.white);
            return _lineMaterial;
        }

        private Material CreateMaterial(Texture2D texture)
        {
            var material = new Material(GetFxShader())
            {
                name = "ChenCustomFx_RuntimeMaterial"
            };
            if (material.HasProperty(MainTextureId))
                material.SetTexture(MainTextureId, texture != null ? texture : Texture2D.whiteTexture);
            if (material.HasProperty(SourceBlendId))
                material.SetFloat(SourceBlendId, (float)BlendMode.SrcAlpha);
            if (material.HasProperty(DestinationBlendId))
                material.SetFloat(DestinationBlendId, (float)BlendMode.One);
            if (material.HasProperty(ZWriteId))
                material.SetFloat(ZWriteId, 0f);
            if (material.HasProperty(ZTestId))
                material.SetFloat(ZTestId, (float)CompareFunction.LessEqual);
            return material;
        }

        private static void SetMaterialColor(Material material, int propertyId, Color color)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetColor(propertyId, color);
        }

        private Shader GetFxShader()
        {
            if (_fxShader != null)
                return _fxShader;
            _fxShader = Shader.Find("ArknightsACT/ImportedClientFX") ??
                        Shader.Find("Sprites/Default") ??
                        Shader.Find("Unlit/Color");
            return _fxShader;
        }

        private void Recycle(GameObject root)
        {
            if (root == null)
                return;
            _activeFx.Remove(root);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (material != null && material != _lineMaterial)
                    Destroy(material);
            }
            Destroy(root);
        }

        private Vector3 GetActorCenter()
        {
            if (_mount != null)
                return _mount.position;
            return transform.position + Vector3.up * 0.8f;
        }

        private Vector3 GetTargetCenter(Transform target)
        {
            if (target == null)
                return GetActorCenter();
            return target.position + Vector3.up * (_motor25D != null ? 0.76f : 0.68f);
        }

        private Vector3 GetActionForward()
        {
            if (_motor25D != null)
            {
                var forward = _motor25D.PlanarForward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            }

            var facing = _motor2D != null ? _motor2D.FacingSign : 1;
            return Vector3.right * (facing >= 0 ? 1f : -1f);
        }

        private void GetFxBasis(Vector3 requestedForward, out Vector3 horizontal, out Vector3 vertical, out Vector3 normal)
        {
            var camera = Camera.main;
            if (camera != null)
            {
                // Keep every FX on a stable camera-facing plane. The attack direction
                // only chooses which way the slash points; it must not turn the quad
                // edge-on when the character changes direction in 2.5D.
                horizontal = camera.transform.right.normalized;
                vertical = camera.transform.up.normalized;

                if (requestedForward.sqrMagnitude > 0.001f && Vector3.Dot(horizontal, requestedForward) < 0f)
                    horizontal = -horizontal;
                // Rebuild the normal after mirroring so the texture's local X axis
                // still matches the selected left/right direction. Cull is disabled
                // on the FX material, so either side of the billboard remains visible.
                normal = Vector3.Cross(horizontal, vertical).normalized;
                return;
            }

            vertical = Vector3.up;
            if (_motor25D != null)
            {
                horizontal = requestedForward;
                horizontal.y = 0f;
                if (horizontal.sqrMagnitude < 0.001f)
                    horizontal = Vector3.forward;
                horizontal.Normalize();
                normal = Vector3.Cross(horizontal, vertical).normalized;
                if (normal.sqrMagnitude < 0.001f)
                    normal = Vector3.right;
                return;
            }

            horizontal = requestedForward.sqrMagnitude > 0.001f ? requestedForward.normalized : Vector3.right;
            horizontal.z = 0f;
            if (horizontal.sqrMagnitude < 0.001f)
                horizontal = Vector3.right;
            horizontal.Normalize();
            normal = Vector3.forward;
        }

        private Vector3 GetDirectionTo(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            if (_motor25D != null)
                direction.y = 0f;
            else
                direction.z = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return GetActionForward();
            return direction.normalized;
        }
    }

    internal static class ChenCustomFxColorExtensions
    {
        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
