using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    public enum SchwarzFxSlot
    {
        BasicStart,
        BasicTrail,
        BasicHit,
        Skill2IgniteRed,
        Skill2Combustion,
        Skill2Trail,
        Skill3Start,
        Skill3Trail,
        Skill3Hit,
        Skill3Buff02,
        Skill3Buff03
    }

    [Serializable]
    public sealed class SchwarzFxTuningSetting
    {
        // Existing tuning assets used offset/scale/angleDegrees. Preserve those values as the
        // right-facing setup so already tuned right-side Schwarz FX never regress.
        [FormerlySerializedAs("offset")]
        public Vector2 rightOffset = Vector2.zero;
        [FormerlySerializedAs("scale"), Min(0.05f)]
        public float rightScale = 1f;
        [FormerlySerializedAs("angleDegrees")]
        public float rightAngleDegrees;

        // Left-facing values are intentionally independent. Schwarz's extracted FX are not
        // perfectly symmetric, so mathematical mirroring is not sufficient for authored placement.
        public Vector2 leftOffset = Vector2.zero;
        [Min(0.05f)] public float leftScale = 1f;
        public float leftAngleDegrees;

        public Vector2 GetOffset(int facingSign) =>
            facingSign < 0 ? leftOffset : rightOffset;

        public float GetScale(int facingSign) =>
            Mathf.Max(0.05f, facingSign < 0 ? leftScale : rightScale);

        public float GetAngle(int facingSign) =>
            facingSign < 0 ? leftAngleDegrees : rightAngleDegrees;

        public SchwarzFxTuningSetting Clone() =>
            new()
            {
                rightOffset = rightOffset,
                rightScale = rightScale,
                rightAngleDegrees = rightAngleDegrees,
                leftOffset = leftOffset,
                leftScale = leftScale,
                leftAngleDegrees = leftAngleDegrees
            };
    }

    /// <summary>
    /// Persistent Schwarz FX placement configuration.
    /// Stored under Resources so both editor preview and runtime use exactly the same values.
    /// </summary>
    public sealed class SchwarzFxTuningProfile : ScriptableObject
    {
        public const string ResourcePath = "Config/SchwarzFxTuningProfile";

        [SerializeField] private SchwarzFxTuningSetting basicStart = new();
        [SerializeField] private SchwarzFxTuningSetting basicTrail = new();
        [SerializeField] private SchwarzFxTuningSetting basicHit = new();
        [SerializeField] private SchwarzFxTuningSetting skill2IgniteRed = new();
        [SerializeField] private SchwarzFxTuningSetting skill2Combustion = new();
        [SerializeField] private SchwarzFxTuningSetting skill2Trail = new();
        [SerializeField] private SchwarzFxTuningSetting skill3Start = new();
        [SerializeField] private SchwarzFxTuningSetting skill3Trail = new();
        [SerializeField] private SchwarzFxTuningSetting skill3Hit = new();
        [SerializeField] private SchwarzFxTuningSetting skill3Buff02 = new();
        [SerializeField] private SchwarzFxTuningSetting skill3Buff03 = new();

        public SchwarzFxTuningSetting BasicStart => basicStart;
        public SchwarzFxTuningSetting BasicTrail => basicTrail;
        public SchwarzFxTuningSetting BasicHit => basicHit;
        public SchwarzFxTuningSetting Skill2IgniteRed => skill2IgniteRed;
        public SchwarzFxTuningSetting Skill2Combustion => skill2Combustion;
        public SchwarzFxTuningSetting Skill2Trail => skill2Trail;
        public SchwarzFxTuningSetting Skill3Start => skill3Start;
        public SchwarzFxTuningSetting Skill3Trail => skill3Trail;
        public SchwarzFxTuningSetting Skill3Hit => skill3Hit;
        public SchwarzFxTuningSetting Skill3Buff02 => skill3Buff02;
        public SchwarzFxTuningSetting Skill3Buff03 => skill3Buff03;

        public SchwarzFxTuningSetting Get(SchwarzFxSlot slot) =>
            slot switch
            {
                SchwarzFxSlot.BasicStart => basicStart,
                SchwarzFxSlot.BasicTrail => basicTrail,
                SchwarzFxSlot.BasicHit => basicHit,
                SchwarzFxSlot.Skill2IgniteRed => skill2IgniteRed,
                SchwarzFxSlot.Skill2Combustion => skill2Combustion,
                SchwarzFxSlot.Skill2Trail => skill2Trail,
                SchwarzFxSlot.Skill3Start => skill3Start,
                SchwarzFxSlot.Skill3Trail => skill3Trail,
                SchwarzFxSlot.Skill3Hit => skill3Hit,
                SchwarzFxSlot.Skill3Buff02 => skill3Buff02,
                SchwarzFxSlot.Skill3Buff03 => skill3Buff03,
                _ => basicStart
            };

        public void ResetDefaults()
        {
            basicStart = New();
            basicTrail = New();
            basicHit = New();
            skill2IgniteRed = New();
            skill2Combustion = New();
            skill2Trail = New();
            skill3Start = New();
            skill3Trail = New();
            skill3Hit = New();
            skill3Buff02 = New();
            skill3Buff03 = New();
        }

        private static SchwarzFxTuningSetting New() =>
            new()
            {
                rightOffset = Vector2.zero,
                rightScale = 1f,
                rightAngleDegrees = 0f,
                leftOffset = Vector2.zero,
                leftScale = 1f,
                leftAngleDegrees = 0f
            };
    }
}
