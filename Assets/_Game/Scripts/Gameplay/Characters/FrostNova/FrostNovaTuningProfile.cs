using System;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    public enum FrostNovaFxSlot
    {
        BasicStart,
        BasicTrail,
        BasicHit,

        // Legacy/default presentation slots intentionally alias the Winter tuning values.
        DefaultSkill1Range,
        DefaultSkill1Buff,
        DefaultSkill2Start,
        DefaultSkill2Trail,
        DefaultSkill2Range,
        DefaultSkill2Range2,

        WinterSkill2Range,
        WinterSkill3Start,
        WinterSkill3Range,
        WinterSkill3Range2,
        WinterActorBuff03,
        WinterActorBuff04,
        WinterActorBuff05
    }

    [Serializable]
    public sealed class FrostNovaFxTuningSetting
    {
        public Vector2 offset = Vector2.zero;
        public Vector2 leftOffset = Vector2.zero;
        [Min(0.05f)] public float scale = 1f;
        public float angleDegrees;
        public float leftAngleDegrees;
        [Min(0f)] public float delaySeconds;
        [Min(0.05f)] public float playbackSpeed = 1f;

        // Used by the optional Winter actor-attached FX candidates.
        // Regular attack/skill FX ignore these two fields.
        public bool enabled;
        [Range(0, 3)] public int triggerSkill;

        public FrostNovaFxTuningSetting Clone() =>
            new()
            {
                offset = offset,
                leftOffset = leftOffset,
                scale = scale,
                angleDegrees = angleDegrees,
                leftAngleDegrees = leftAngleDegrees,
                delaySeconds = delaySeconds,
                playbackSpeed = playbackSpeed,
                enabled = enabled,
                triggerSkill = triggerSkill
            };
    }

    /// <summary>
    /// Single-source tuning profile for FrostNova.
    /// Winter is the authored baseline. Default/original presentation slots reuse the same
    /// numeric tuning instead of maintaining a second parameter set.
    /// </summary>
    public sealed class FrostNovaTuningProfile : ScriptableObject
    {
        public const string ResourcePath = "Config/FrostNovaTuningProfile";

        [Header("Shared animation speed")]
        [SerializeField, Min(0.05f)] private float moveAnimationSpeed = 2f;
        [SerializeField, Min(0.05f)] private float basicAttackAnimationSpeed = 2f;

        [Header("Winter skill animation speed")]
        [SerializeField, Min(0.05f)] private float skill2AnimationSpeed = 1f;
        [SerializeField, Min(0.05f)] private float skill3AnimationSpeed = 0.70f;

        [Header("Basic attack")]
        [SerializeField, Min(0.02f)] private float basicProjectileFlightSeconds = 0.22f;
        [SerializeField] private FrostNovaFxTuningSetting basicStart = New();
        [SerializeField] private FrostNovaFxTuningSetting basicTrail = New();
        [SerializeField] private FrostNovaFxTuningSetting basicHit = New();

        [Header("Winter skill FX")]
        [SerializeField] private FrostNovaFxTuningSetting winterSkill2Range = New();
        [SerializeField] private FrostNovaFxTuningSetting winterSkill3Start = New();
        [SerializeField] private FrostNovaFxTuningSetting winterSkill3Range = New();
        [SerializeField] private FrostNovaFxTuningSetting winterSkill3Range2 = New();

        [Header("Winter actor-attached FX")]
        [SerializeField] private FrostNovaFxTuningSetting winterActorBuff03 =
            NewActorFx(enabledByDefault: true, triggerSkill: 3);
        [SerializeField] private FrostNovaFxTuningSetting winterActorBuff04 =
            NewActorFx(enabledByDefault: false, triggerSkill: 0);
        [SerializeField] private FrostNovaFxTuningSetting winterActorBuff05 =
            NewActorFx(enabledByDefault: true, triggerSkill: 0);

        public float MoveAnimationSpeed
        {
            get => Mathf.Max(0.05f, moveAnimationSpeed);
            set => moveAnimationSpeed = Mathf.Max(0.05f, value);
        }

        public float BasicAttackAnimationSpeed
        {
            get => Mathf.Max(0.05f, basicAttackAnimationSpeed);
            set => basicAttackAnimationSpeed = Mathf.Max(0.05f, value);
        }

        public float Skill2AnimationSpeed
        {
            get => Mathf.Max(0.05f, skill2AnimationSpeed);
            set => skill2AnimationSpeed = Mathf.Max(0.05f, value);
        }

        public float Skill3AnimationSpeed
        {
            get => Mathf.Max(0.05f, skill3AnimationSpeed);
            set => skill3AnimationSpeed = Mathf.Max(0.05f, value);
        }

        public float BasicProjectileFlightSeconds
        {
            get => Mathf.Max(0.02f, basicProjectileFlightSeconds);
            set => basicProjectileFlightSeconds = Mathf.Max(0.02f, value);
        }

        /// <summary>
        /// Damage resolves when the projectile has actually had time to appear and reach its target.
        /// This is derived so visual timing and gameplay damage cannot drift apart.
        /// </summary>
        public float BasicAttackImpactSeconds =>
            Mathf.Max(0f, basicTrail != null ? basicTrail.delaySeconds : 0f) +
            BasicProjectileFlightSeconds;

        public float GetSkillAnimationSpeedForSlot(int slot) =>
            slot == 2 ? Skill3AnimationSpeed : Skill2AnimationSpeed;

        public FrostNovaFxTuningSetting Get(FrostNovaFxSlot slot) =>
            slot switch
            {
                FrostNovaFxSlot.BasicStart => basicStart,
                FrostNovaFxSlot.BasicTrail => basicTrail,
                FrostNovaFxSlot.BasicHit => basicHit,

                // Original/default presentation uses the same Winter numeric tuning.
                FrostNovaFxSlot.DefaultSkill1Range => winterSkill2Range,
                FrostNovaFxSlot.DefaultSkill1Buff => winterSkill2Range,
                FrostNovaFxSlot.DefaultSkill2Start => winterSkill3Start,
                FrostNovaFxSlot.DefaultSkill2Trail => winterSkill3Start,
                FrostNovaFxSlot.DefaultSkill2Range => winterSkill3Range,
                FrostNovaFxSlot.DefaultSkill2Range2 => winterSkill3Range2,

                FrostNovaFxSlot.WinterSkill2Range => winterSkill2Range,
                FrostNovaFxSlot.WinterSkill3Start => winterSkill3Start,
                FrostNovaFxSlot.WinterSkill3Range => winterSkill3Range,
                FrostNovaFxSlot.WinterSkill3Range2 => winterSkill3Range2,
                FrostNovaFxSlot.WinterActorBuff03 => winterActorBuff03,
                FrostNovaFxSlot.WinterActorBuff04 => winterActorBuff04,
                FrostNovaFxSlot.WinterActorBuff05 => winterActorBuff05,
                _ => basicStart
            };

        public void ResetDefaults()
        {
            moveAnimationSpeed = 2f;
            basicAttackAnimationSpeed = 2f;
            skill2AnimationSpeed = 1f;
            skill3AnimationSpeed = 0.70f;
            basicProjectileFlightSeconds = 0.22f;

            basicStart = New();
            basicTrail = New();
            basicHit = New();
            winterSkill2Range = New();
            winterSkill3Start = New();
            winterSkill3Range = New();
            winterSkill3Range2 = New();
            winterActorBuff03 = NewActorFx(enabledByDefault: true, triggerSkill: 3);
            winterActorBuff04 = NewActorFx(enabledByDefault: false, triggerSkill: 0);
            winterActorBuff05 = NewActorFx(enabledByDefault: true, triggerSkill: 0);
        }

        private static FrostNovaFxTuningSetting NewActorFx(
            bool enabledByDefault,
            int triggerSkill) =>
            new()
            {
                offset = Vector2.zero,
                leftOffset = Vector2.zero,
                scale = 1f,
                angleDegrees = 0f,
                leftAngleDegrees = 0f,
                delaySeconds = 0f,
                playbackSpeed = 1f,
                enabled = enabledByDefault,
                triggerSkill = triggerSkill
            };

        private static FrostNovaFxTuningSetting New() =>
            new()
            {
                offset = Vector2.zero,
                leftOffset = Vector2.zero,
                scale = 1f,
                angleDegrees = 0f,
                leftAngleDegrees = 0f,
                delaySeconds = 0f,
                playbackSpeed = 1f,
                enabled = false,
                triggerSkill = 0
            };
    }
}
