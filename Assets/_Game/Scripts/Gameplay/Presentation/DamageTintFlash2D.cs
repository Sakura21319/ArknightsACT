using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Arknights-style hit readability: briefly tints the whole character red on damage.
    /// Supports ordinary Renderers through MaterialPropertyBlock and Spine Skeleton.Color
    /// through reflection, so gameplay remains independent from a specific Spine runtime.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class DamageTintFlash2D : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float duration = 0.10f;
        [SerializeField] private Color hitColor = new(1f, 0.16f, 0.16f, 1f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<RendererState> _rendererStates = new();
        private readonly List<SkeletonState> _skeletonStates = new();
        private CombatEntity _entity;
        private Coroutine _routine;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            CacheTargets();
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
            CacheTargets();
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

        private void CacheTargets()
        {
            _rendererStates.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled)
                    continue;
                var baseColor = Color.white;
                var material = renderer.sharedMaterial;
                if (material != null && material.HasProperty(ColorId))
                    baseColor = material.GetColor(ColorId);
                _rendererStates.Add(new RendererState(renderer, baseColor));
            }

            _skeletonStates.Clear();
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().FullName != "Spine.Unity.SkeletonAnimation")
                    continue;

                var skeleton = behaviour.GetType().GetProperty("Skeleton", BindingFlags.Instance | BindingFlags.Public)?.GetValue(behaviour);
                if (skeleton == null)
                    continue;

                var colorObject = skeleton.GetType().GetProperty("Color", BindingFlags.Instance | BindingFlags.Public)?.GetValue(skeleton);
                if (colorObject == null)
                    continue;

                if (TryReadColor(colorObject, out var baseColor))
                    _skeletonStates.Add(new SkeletonState(colorObject, baseColor));
            }
        }

        private void ApplyHitTint()
        {
            foreach (var state in _rendererStates)
            {
                if (state.Renderer == null)
                    continue;
                var block = new MaterialPropertyBlock();
                state.Renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, hitColor);
                state.Renderer.SetPropertyBlock(block);
            }

            foreach (var state in _skeletonStates)
                WriteColor(state.ColorObject, hitColor);
        }

        private void Restore()
        {
            foreach (var state in _rendererStates)
            {
                if (state.Renderer == null)
                    continue;
                var block = new MaterialPropertyBlock();
                state.Renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, state.BaseColor);
                state.Renderer.SetPropertyBlock(block);
            }

            foreach (var state in _skeletonStates)
                WriteColor(state.ColorObject, state.BaseColor);
        }

        private static bool TryReadColor(object colorObject, out Color color)
        {
            color = Color.white;
            if (!TryReadFloat(colorObject, "R", "r", out var r) ||
                !TryReadFloat(colorObject, "G", "g", out var g) ||
                !TryReadFloat(colorObject, "B", "b", out var b))
                return false;
            TryReadFloat(colorObject, "A", "a", out var a);
            if (a <= 0f) a = 1f;
            color = new Color(r, g, b, a);
            return true;
        }

        private static void WriteColor(object colorObject, Color color)
        {
            if (colorObject == null)
                return;
            TryWriteFloat(colorObject, "R", "r", color.r);
            TryWriteFloat(colorObject, "G", "g", color.g);
            TryWriteFloat(colorObject, "B", "b", color.b);
            TryWriteFloat(colorObject, "A", "a", color.a);
        }

        private static bool TryReadFloat(object target, string propertyName, string fieldName, out float value)
        {
            value = 0f;
            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead)
            {
                var raw = property.GetValue(target);
                if (raw is float f)
                {
                    value = f;
                    return true;
                }
            }
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.GetValue(target) is float ff)
            {
                value = ff;
                return true;
            }
            return false;
        }

        private static void TryWriteFloat(object target, string propertyName, string fieldName, float value)
        {
            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType == typeof(float))
            {
                property.SetValue(target, value);
                return;
            }
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

        private readonly struct SkeletonState
        {
            public readonly object ColorObject;
            public readonly Color BaseColor;
            public SkeletonState(object colorObject, Color baseColor)
            {
                ColorObject = colorObject;
                BaseColor = baseColor;
            }
        }
    }
}
