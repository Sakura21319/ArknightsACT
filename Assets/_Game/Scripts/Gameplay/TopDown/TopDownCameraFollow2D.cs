using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Soul-Knight-like room framing: horizontal travel is followed normally while vertical
    /// movement only nudges the camera a little. Upright Spine characters therefore stay
    /// visually planted in a pseudo-3/4 room instead of the whole scene drifting like a flat map.
    /// </summary>
    public sealed class TopDownCameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float followSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float verticalFollowFactor = 0.16f;
        [SerializeField] private float verticalOffset = 0.35f;
        [SerializeField] private Vector2 minBounds = new(-8f, -1.5f);
        [SerializeField] private Vector2 maxBounds = new(62f, 1.5f);

        public void Configure(Transform newTarget, Vector2 min, Vector2 max, float yFollow = 0.16f, float yOffset = 0.35f)
        {
            target = newTarget;
            minBounds = min;
            maxBounds = max;
            verticalFollowFactor = Mathf.Clamp01(yFollow);
            verticalOffset = yOffset;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var wanted = new Vector2(
                target.position.x,
                target.position.y * verticalFollowFactor + verticalOffset);
            wanted.x = Mathf.Clamp(wanted.x, minBounds.x, maxBounds.x);
            wanted.y = Mathf.Clamp(wanted.y, minBounds.y, maxBounds.y);

            var current = transform.position;
            var t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            var next = Vector2.Lerp(current, wanted, t);
            transform.position = new Vector3(next.x, next.y, current.z);
        }
    }
}
