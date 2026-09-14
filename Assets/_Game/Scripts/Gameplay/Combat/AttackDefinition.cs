using UnityEngine;

namespace ArknightsACT.Gameplay.Combat
{
    [CreateAssetMenu(menuName = "ArknightsACT/Combat/Attack Definition")]
    public sealed class AttackDefinition : ScriptableObject
    {
        [Header("Timing")]
        [Min(0f)] public float startup = 0.08f;
        [Min(0.01f)] public float active = 0.06f;
        [Min(0f)] public float recovery = 0.16f;

        [Header("Hit")]
        [Min(0f)] public float damageMultiplier = 1f;
        public Vector2 hitboxOffset = new(0.9f, 0f);
        public Vector2 hitboxSize = new(1.4f, 1.0f);
        public Vector2 knockback = new(3f, 1f);

        [Header("Cancel")]
        [Range(0f, 1f)] public float dashCancelNormalizedTime = 0.35f;

        [Header("Feedback")]
        [Min(0f)] public float hitStopSeconds = 0.035f;
        [Min(0f)] public float cameraShakeAmplitude = 0.07f;

        public float TotalDuration => startup + active + recovery;
    }
}
