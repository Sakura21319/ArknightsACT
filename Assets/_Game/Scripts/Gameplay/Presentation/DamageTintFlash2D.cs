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
    ///
    /// Different Spine runtime generations expose skeleton tint differently. Spine 3.8-style
    /// runtimes commonly expose R/G/B/A directly on Skeleton, while newer/forked runtimes may
    /// expose a Color object. Support both layouts through reflection so Gameplay stays
    /// independent from the concrete Spine package.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class DamageTintFlash2D : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float duration = 0.16f;
        [SerializeField] private Color hitColor = new(1f, 0.06f, 0.06f, 1f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<RendererState> _rendererStates = new();
        private readonly List<SpineTintState> _spineStates = new();
        private CombatEntity _entity;
        private Coroutine _routine;
        private bool _warnedNoTargets;

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
            // Skeletons can initialize after this component's Awake, so refresh on every hit.
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

            _spineStates.Clear();
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().FullName != "Spine.Unity.SkeletonAnimation")
                    continue;

                var skeleton = behaviour.GetType()
                    .GetProperty("Skeleton", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(behaviour);
                if (skeleton == null)
                    continue;

                // Spine 3.8 and many forks keep tint directly on Skeleton.R/G/B/A.
                if (TryReadColor(skeleton, out var directColor))
                {
                    _spineStates.Add(new SpineTintState(skeleton, directColor));
                    continue;
                }

                // Some newer/forked runtimes expose Skeleton.Color instead.
                var colorObject = skeleton.GetType()
                    .GetProperty("Color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(skeleton);
                if (colorObject != null && TryReadColor(colorObject, out var nestedColor))
                    _spineStates.Add(new SpineTintState(colorObject, nestedColor));
            }

            if (_rendererStates.Count == 0 && _spineStates.Count == 0 && !_warnedNoTargets)
            {
                _warnedNoTargets = true;
                Debug.LogWarning($"[ArknightsACT/HitTint] No tintable Renderer or Spine Skeleton found under {name}.", this);
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

            foreach (var state in _spineStates)
                WriteColor(state.Target, hitColor);
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

            foreach (var state in _spineStates)
                WriteColor(state.Target, state.BaseColor);
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
