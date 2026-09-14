using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Arknights-style damage readability: briefly tint the whole combat entity red.
    /// Base colors are captured once and never re-sampled while the character is already red,
    /// preventing rapid hits from making red become the new permanent base color.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class DamageTintFlash2D : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float duration = 0.14f;
        [SerializeField] private Color hitColor = new(1f, 0.05f, 0.05f, 1f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<RendererState> _rendererStates = new();
        private readonly List<SpineTintState> _spineStates = new();
        private CombatEntity _entity;
        private Coroutine _routine;
        private bool _rendererBaseCaptured;
        private bool _spineBaseCaptured;
        private bool _warnedNoTargets;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            CaptureRendererBaseOnce();
        }

        private void Start()
        {
            // Spine skeleton initialization can happen after Awake.
            CaptureSpineBaseOnce();
        }

        private void OnEnable()
        {
            if (_entity != null)
                _entity.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_entity != null)
                _entity.Damaged -= OnDamaged;
            Restore();
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            CaptureRendererBaseOnce();
            CaptureSpineBaseOnce();

            if (_rendererStates.Count == 0 && _spineStates.Count == 0)
            {
                if (!_warnedNoTargets)
                {
                    _warnedNoTargets = true;
                    Debug.LogWarning($"[ArknightsACT/HitTint] No tintable Renderer or Spine Skeleton found under {name}.", this);
                }
                return;
            }

            // Do not restore before restarting. A second hit simply extends the red window,
            // while the original non-red base colors remain stored separately.
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            ApplyHitTint();
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Restore();
            _routine = null;
        }

        private void CaptureRendererBaseOnce()
        {
            if (_rendererBaseCaptured)
                return;

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                var baseColor = Color.white;
                var material = renderer.sharedMaterial;
                if (material != null && material.HasProperty(ColorId))
                    baseColor = material.GetColor(ColorId);
                _rendererStates.Add(new RendererState(renderer, baseColor));
            }

            _rendererBaseCaptured = _rendererStates.Count > 0;
        }

        private void CaptureSpineBaseOnce()
        {
            if (_spineBaseCaptured)
                return;

            var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().FullName != "Spine.Unity.SkeletonAnimation")
                    continue;

                var skeleton = behaviour.GetType()
                    .GetProperty("Skeleton", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(behaviour);
                if (skeleton == null)
                    continue;

                if (TryReadColor(skeleton, out var directColor))
                {
                    _spineStates.Add(new SpineTintState(skeleton, directColor));
                    continue;
                }

                var colorObject = skeleton.GetType()
                    .GetProperty("Color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(skeleton);
                if (colorObject != null && TryReadColor(colorObject, out var nestedColor))
                    _spineStates.Add(new SpineTintState(colorObject, nestedColor));
            }

            _spineBaseCaptured = _spineStates.Count > 0;
        }

        private void ApplyHitTint()
        {
            for (var i = 0; i < _rendererStates.Count; i++)
            {
                var state = _rendererStates[i];
                if (state.Renderer == null)
                    continue;

                var block = new MaterialPropertyBlock();
                state.Renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, hitColor);
                state.Renderer.SetPropertyBlock(block);
            }

            for (var i = 0; i < _spineStates.Count; i++)
                WriteColor(_spineStates[i].Target, hitColor);
        }

        private void Restore()
        {
            for (var i = 0; i < _rendererStates.Count; i++)
            {
                var state = _rendererStates[i];
                if (state.Renderer == null)
                    continue;

                var block = new MaterialPropertyBlock();
                state.Renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, state.BaseColor);
                state.Renderer.SetPropertyBlock(block);
            }

            for (var i = 0; i < _spineStates.Count; i++)
                WriteColor(_spineStates[i].Target, _spineStates[i].BaseColor);
        }

        private static bool TryReadColor(object target, out Color color)
        {
            color = Color.white;
            if (target == null ||
                !TryReadFloat(target, "R", "r", out var r) ||
                !TryReadFloat(target, "G", "g", out var g) ||
                !TryReadFloat(target, "B", "b", out var b))
                return false;

            if (!TryReadFloat(target, "A", "a", out var a) || a <= 0f)
                a = 1f;

            color = new Color(r, g, b, a);
            return true;
        }

        private static void WriteColor(object target, Color color)
        {
            if (target == null)
                return;

            TryWriteFloat(target, "R", "r", color.r);
            TryWriteFloat(target, "G", "g", color.g);
            TryWriteFloat(target, "B", "b", color.b);
            TryWriteFloat(target, "A", "a", color.a);
        }

        private static bool TryReadFloat(object target, string propertyName, string fieldName, out float value)
        {
            value = 0f;
            if (target == null)
                return false;

            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead && property.GetValue(target) is float propertyValue)
            {
                value = propertyValue;
                return true;
            }

            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.GetValue(target) is float fieldValue)
            {
                value = fieldValue;
                return true;
            }

            return false;
        }

        private static void TryWriteFloat(object target, string propertyName, string fieldName, float value)
        {
            if (target == null)
                return;

            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType == typeof(float))
            {
                property.SetValue(target, value);
                return;
            }

            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(float))
                field.SetValue(target, value);
        }

        private readonly struct RendererState
        {
            public readonly Renderer Renderer;
            public readonly Color BaseColor;

            public RendererState(Renderer renderer, Color baseColor)
            {
                Renderer = renderer;
                BaseColor = baseColor;
            }
        }

        private readonly struct SpineTintState
        {
            public readonly object Target;
            public readonly Color BaseColor;

            public SpineTintState(object target, Color baseColor)
            {
                Target = target;
                BaseColor = baseColor;
            }
        }
    }
}
