using System;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Wisadel
{
    public enum WisadelFxSlot
    {
        BasicStart,
        BasicTrail,
        BasicHit,
        Skill2Start,
        Skill2Buff,
        Skill2Hit,
        Skill3Start,
        Skill3Trail,
        Skill3Hit,

        // Keep new composite layers appended so existing serialized offsets retain their indices.
        BasicDownStart,
        BasicHit02,
        Skill2Buff02,
        Skill2Hit02,
        Skill2OverloadStart,
        Skill3UpStart,
        Skill3DownStart,
        Skill3BuffBack,
        Skill3BuffFront,
        Skill3Buff02Back,
        Skill3Buff02Front,
        Skill3Hit02,
        Skill3Hit03
    }

    [Serializable]
    public sealed class WisadelFxOffsetSetting
    {
        // Store the negative form so existing serialized profiles migrate safely:
        // newly-added bool fields deserialize as false, which means existing FX stay enabled.
        public bool disabled;
        public Vector2 rightOffset;
        public Vector2 leftOffset;
        public float scaleMultiplier;
        public float startDelaySeconds;

        public bool Enabled => !disabled;

        // Existing serialized profiles predate these fields, so zero must migrate to neutral 1x.
        public float ScaleMultiplier => scaleMultiplier > 0f ? scaleMultiplier : 1f;
        public float StartDelaySeconds => Mathf.Max(0f, startDelaySeconds);

        public Vector2 GetOffset(int facingSign) =>
            facingSign < 0 ? leftOffset : rightOffset;
    }

    /// <summary>
    /// Per-effect, per-facing placement values. These offsets are presentation data and never
    /// affect gameplay targets, range, damage, or projectile destinations.
    /// </summary>
    public sealed class WisadelFxTuningProfile : ScriptableObject
    {
        public const string ResourcePath = "Config/WisadelFxTuningProfile";

        [SerializeField] private WisadelFxOffsetSetting[] settings = Array.Empty<WisadelFxOffsetSetting>();

        public WisadelFxOffsetSetting Get(WisadelFxSlot slot)
        {
            EnsureSettings();
            var index = (int)slot;
            return index >= 0 && index < settings.Length ? settings[index] : settings[0];
        }

        public void ResetDefaults()
        {
            var count = Enum.GetValues(typeof(WisadelFxSlot)).Length;
            settings = new WisadelFxOffsetSetting[count];
            for (var i = 0; i < settings.Length; i++)
                settings[i] = new WisadelFxOffsetSetting
                {
                    disabled = false,
                    scaleMultiplier = 1f,
                    startDelaySeconds = 0f
                };
        }

        private void OnEnable() => EnsureSettings();

        private void EnsureSettings()
        {
            var count = Enum.GetValues(typeof(WisadelFxSlot)).Length;
            if (settings != null && settings.Length == count)
            {
                for (var i = 0; i < settings.Length; i++)
                    settings[i] ??= new WisadelFxOffsetSetting();
                return;
            }

            var previous = settings;
            settings = new WisadelFxOffsetSetting[count];
            for (var i = 0; i < count; i++)
                settings[i] = previous != null && i < previous.Length && previous[i] != null
                    ? previous[i]
                    : new WisadelFxOffsetSetting();
        }
    }
}
