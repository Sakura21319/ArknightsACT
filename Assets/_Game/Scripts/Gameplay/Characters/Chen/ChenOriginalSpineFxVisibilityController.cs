using System;
using System.Collections.Generic;
using System.Linq;
using ArknightsACT.Gameplay.Presentation;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Keeps Ch'en's authored combat-effect slots readable without drawing replacement VFX.
    /// The default PRTS battle-front atlas already contains BG/BG1..BG6 effect regions; this
    /// component only operates on those original Spine slots after the animation has applied.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChenOriginalSpineFxVisibilityController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minimumActiveEffectAlpha = 0.92f;

        private readonly List<int> _effectSlotIndices = new();
        private SkeletonAnimation _skeletonAnimation;
        private bool _loggedActiveFx;
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
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private bool TryBind()
        {
            if (_bound && _skeletonAnimation != null)
                return true;

            var presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
            _skeletonAnimation = presentation != null
                ? presentation.GetComponentInChildren<SkeletonAnimation>(true)
                : GetComponentsInChildren<SkeletonAnimation>(true)
                    .FirstOrDefault(item =>
                        item != null && item.name.IndexOf("MotionSource", StringComparison.OrdinalIgnoreCase) < 0);

            if (_skeletonAnimation == null)
                return false;

            _skeletonAnimation.Initialize(false);
            if (_skeletonAnimation.Skeleton == null)
                return false;

            // Spine's native PMA path supports additive slots in the same batch when this is true.
            // No LineRenderer, generated slash sprite or replacement animation is used here.
            _skeletonAnimation.pmaVertexColors = true;

            DiscoverEffectSlots(_skeletonAnimation.Skeleton.Data);
            _skeletonAnimation.UpdateComplete += OnSpineUpdateComplete;
            _bound = true;

            var names = _effectSlotIndices.Count == 0
                ? "<none>"
                : string.Join(", ", _effectSlotIndices.Select(index => _skeletonAnimation.Skeleton.Data.Slots.Items[index].Name));
            Debug.Log($"[ArknightsACT/ChenFX] Original Spine effect slots: {names}", this);
            return true;
        }

        private void Unbind()
        {
            if (_bound && _skeletonAnimation != null)
                _skeletonAnimation.UpdateComplete -= OnSpineUpdateComplete;
            _bound = false;
            _skeletonAnimation = null;
            _effectSlotIndices.Clear();
        }

        private void DiscoverEffectSlots(SkeletonData data)
        {
            _effectSlotIndices.Clear();
            if (data == null)
                return;

            var entries = new List<Skin.SkinEntry>();
            for (var slotIndex = 0; slotIndex < data.Slots.Count; slotIndex++)
            {
                var slotData = data.Slots.Items[slotIndex];
                var isEffectSlot = slotData != null && IsOriginalEffectName(slotData.Name);

                if (!isEffectSlot)
                {
                    foreach (var skin in data.Skins)
                    {
                        if (skin == null)
                            continue;
                        entries.Clear();
                        skin.GetAttachments(slotIndex, entries);
                        if (entries.Any(entry =>
                                IsOriginalEffectName(entry.Name) ||
                                (entry.Attachment != null && IsOriginalEffectName(entry.Attachment.Name))))
                        {
                            isEffectSlot = true;
                            break;
                        }
                    }
                }

                if (isEffectSlot)
                    _effectSlotIndices.Add(slotIndex);
            }
        }

        private void OnSpineUpdateComplete(ISkeletonAnimation animated)
        {
            var skeleton = _skeletonAnimation != null ? _skeletonAnimation.Skeleton : null;
            if (skeleton == null)
                return;

            var active = new List<string>();
            for (var i = 0; i < _effectSlotIndices.Count; i++)
            {
                var slotIndex = _effectSlotIndices[i];
                if (slotIndex < 0 || slotIndex >= skeleton.Slots.Count)
                    continue;

                var slot = skeleton.Slots.Items[slotIndex];
                if (slot?.Attachment == null)
                    continue;

                // Keep the authored attachment and its timing, only prevent an active effect slot
                // from being imported at near-zero opacity in this project's PMA render path.
                slot.A = Mathf.Max(slot.A, minimumActiveEffectAlpha);
                active.Add(slot.Attachment.Name ?? slot.Data.Name);
            }

            if (!_loggedActiveFx && active.Count > 0)
            {
                _loggedActiveFx = true;
                Debug.Log(
                    "[ArknightsACT/ChenFX] Original Spine FX attachments became active: " +
                    string.Join(", ", active.Distinct(StringComparer.OrdinalIgnoreCase)),
                    this);
            }
        }

        private static bool IsOriginalEffectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Default Ch'en battle-front uses BG/BG1..BG6. Extracted skin atlases use names such
            // as Bg_Skill_3_A..E for the same authored effect family.
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
