using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Keeps Ch'en's authored combat-effect slots readable without drawing replacement VFX.
    ///
    /// Gameplay intentionally has no compile-time dependency on a particular Spine runtime assembly.
    /// This bridge therefore follows the same reflection pattern as SpineCharacterPresentation2D:
    /// it finds the visible SkeletonAnimation, discovers the original BG/effect slots from the
    /// imported skeleton/skins, and only raises the alpha of an authored attachment while it is
    /// already active on the Spine timeline.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class ChenOriginalSpineFxVisibilityController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minimumActiveEffectAlpha = 0.92f;

        private readonly List<int> _effectSlotIndices = new();
        private Component _skeletonAnimation;
        private object _skeleton;
        private PropertyInfo _skeletonProperty;
        private bool _loggedActiveFx;
        private bool _loggedBinding;
        private bool _bound;

        private void Start()
        {
            TryBind();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            ResetBinding();
        }

        private void OnDestroy()
        {
            ResetBinding();
        }

        private void LateUpdate()
        {
            if (!_bound && !TryBind())
                return;

            if (_skeletonAnimation == null)
            {
                ResetBinding();
                return;
            }

            _skeleton = _skeletonProperty?.GetValue(_skeletonAnimation) ?? _skeleton;
            if (_skeleton == null)
                return;

            var slots = GetProperty(_skeleton, "Slots");
            if (slots == null)
                return;

            var active = new List<string>();
            for (var i = 0; i < _effectSlotIndices.Count; i++)
            {
                var slot = GetIndexedItem(slots, _effectSlotIndices[i]);
                if (slot == null)
                    continue;

                var attachment = GetProperty(slot, "Attachment");
                if (attachment == null)
                    continue;

                var alphaProperty = slot.GetType().GetProperty(
                    "A",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (alphaProperty != null && alphaProperty.CanRead && alphaProperty.CanWrite)
                {
                    var raw = alphaProperty.GetValue(slot);
                    if (raw is float alpha && alpha < minimumActiveEffectAlpha)
                        alphaProperty.SetValue(slot, minimumActiveEffectAlpha);
                }

                var attachmentName = GetString(attachment, "Name");
                var slotData = GetProperty(slot, "Data");
                var slotName = GetString(slotData, "Name");
                active.Add(!string.IsNullOrWhiteSpace(attachmentName) ? attachmentName : slotName);
            }

            if (!_loggedActiveFx && active.Count > 0)
            {
                _loggedActiveFx = true;
                Debug.Log(
                    "[ArknightsACT/ChenFX] Original Spine FX attachments became active: " +
                    string.Join(", ", active.Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                    this);
            }
        }

        private bool TryBind()
        {
            if (_bound && _skeletonAnimation != null && _skeleton != null)
                return true;

            var presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
            var searchRoot = presentation != null ? presentation.transform : transform;

            _skeletonAnimation = searchRoot.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component != null &&
                    component.GetType().FullName == "Spine.Unity.SkeletonAnimation" &&
                    component.name.IndexOf("MotionSource", StringComparison.OrdinalIgnoreCase) < 0);
            if (_skeletonAnimation == null)
                return false;

            var animationType = _skeletonAnimation.GetType();
            var initialize = animationType.GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);
            initialize?.Invoke(_skeletonAnimation, new object[] { false });

            _skeletonProperty = animationType.GetProperty(
                "Skeleton",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _skeleton = _skeletonProperty?.GetValue(_skeletonAnimation);
            if (_skeleton == null)
                return false;

            // Keep Spine's native PMA single-batch additive path enabled without referencing the
            // Spine assembly from gameplay code.
            var pmaField = animationType.GetField(
                "pmaVertexColors",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pmaField != null && pmaField.FieldType == typeof(bool))
                pmaField.SetValue(_skeletonAnimation, true);
            else
            {
                var pmaProperty = animationType.GetProperty(
                    "PmaVertexColors",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pmaProperty != null && pmaProperty.CanWrite && pmaProperty.PropertyType == typeof(bool))
                    pmaProperty.SetValue(_skeletonAnimation, true);
            }

            DiscoverEffectSlots(_skeleton);
            _bound = true;

            if (!_loggedBinding)
            {
                _loggedBinding = true;
                var names = DescribeEffectSlots(_skeleton);
                Debug.Log($"[ArknightsACT/ChenFX] Original Spine effect slots: {names}", this);
            }
            return true;
        }

        private void ResetBinding()
        {
            _bound = false;
            _skeletonAnimation = null;
            _skeleton = null;
            _skeletonProperty = null;
            _effectSlotIndices.Clear();
        }

        private void DiscoverEffectSlots(object skeleton)
        {
            _effectSlotIndices.Clear();
            var data = GetProperty(skeleton, "Data");
            var slots = GetProperty(data, "Slots");
            if (data == null || slots == null)
                return;

            var slotDataItems = Enumerate(slots).ToList();
            for (var slotIndex = 0; slotIndex < slotDataItems.Count; slotIndex++)
            {
                var slotData = slotDataItems[slotIndex];
                var isEffectSlot = IsOriginalEffectName(GetString(slotData, "Name"));
                if (!isEffectSlot)
                    isEffectSlot = SkinContainsEffectAttachment(data, slotIndex);

                if (isEffectSlot)
                    _effectSlotIndices.Add(slotIndex);
            }
        }

        private static bool SkinContainsEffectAttachment(object skeletonData, int slotIndex)
        {
            var skins = GetProperty(skeletonData, "Skins");
            if (skins == null)
                return false;

            foreach (var skin in Enumerate(skins))
            {
                if (skin == null)
                    continue;

                var skinType = skin.GetType();
                var entryType = skinType.GetNestedType(
                    "SkinEntry",
                    BindingFlags.Public | BindingFlags.NonPublic);
                if (entryType == null)
                    continue;

                var listType = typeof(List<>).MakeGenericType(entryType);
                var entries = Activator.CreateInstance(listType);
                var getAttachments = skinType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method =>
                    {
                        if (method.Name != "GetAttachments")
                            return false;
                        var parameters = method.GetParameters();
                        return parameters.Length == 2 && parameters[0].ParameterType == typeof(int);
                    });
                if (getAttachments == null)
                    continue;

                try
                {
                    getAttachments.Invoke(skin, new[] { (object)slotIndex, entries });
                }
                catch
                {
                    continue;
                }

                foreach (var entry in Enumerate(entries))
                {
                    if (entry == null)
                        continue;
                    var name = GetString(entry, "Name");
                    var attachment = GetProperty(entry, "Attachment");
                    if (IsOriginalEffectName(name) ||
                        IsOriginalEffectName(GetString(attachment, "Name")))
                        return true;
                }
            }

            return false;
        }

        private string DescribeEffectSlots(object skeleton)
        {
            if (_effectSlotIndices.Count == 0)
                return "<none>";

            var data = GetProperty(skeleton, "Data");
            var slots = GetProperty(data, "Slots");
            var names = new List<string>();
            foreach (var index in _effectSlotIndices)
            {
                var slotData = GetIndexedItem(slots, index);
                names.Add(GetString(slotData, "Name") ?? $"#{index}");
            }
            return string.Join(", ", names);
        }

        private static IEnumerable<object> Enumerate(object collection)
        {
            if (collection is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    yield return item;
                yield break;
            }

            var items = GetProperty(collection, "Items") as Array;
            var count = GetInt(collection, "Count", items?.Length ?? 0);
            if (items == null)
                yield break;
            for (var i = 0; i < Mathf.Min(count, items.Length); i++)
                yield return items.GetValue(i);
        }

        private static object GetIndexedItem(object collection, int index)
        {
            if (collection == null || index < 0)
                return null;

            if (collection is IList list)
                return index < list.Count ? list[index] : null;

            var items = GetProperty(collection, "Items") as Array;
            if (items != null)
            {
                var count = GetInt(collection, "Count", items.Length);
                return index < count && index < items.Length ? items.GetValue(index) : null;
            }

            var current = 0;
            foreach (var item in Enumerate(collection))
            {
                if (current++ == index)
                    return item;
            }
            return null;
        }

        private static object GetProperty(object target, string name)
        {
            if (target == null)
                return null;
            return target.GetType()
                .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static string GetString(object target, string name)
        {
            if (target == null)
                return null;
            var value = GetProperty(target, name);
            if (value is string text)
                return text;

            var field = target.GetType().GetField(
                char.ToLowerInvariant(name[0]) + name.Substring(1),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(target) as string;
        }

        private static int GetInt(object target, string name, int fallback)
        {
            if (target == null)
                return fallback;
            var value = GetProperty(target, name);
            return value is int result ? result : fallback;
        }

        private static bool IsOriginalEffectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (value.Equals("BG", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value.StartsWith("BG", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value.StartsWith("Bg_Skill_", StringComparison.OrdinalIgnoreCase))
                return true;
            return value.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.StartsWith("FX", StringComparison.OrdinalIgnoreCase);
        }
    }
}
