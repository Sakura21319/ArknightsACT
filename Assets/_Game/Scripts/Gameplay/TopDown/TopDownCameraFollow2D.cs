using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    public sealed class TopDownCameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float followSpeed = 9f;
        [SerializeField] private Vector2 minBounds = new(-8f, -5f);
        [SerializeField] private Vector2 maxBounds = new(62f, 5f);

        public void Configure(Transform newTarget, Vector2 min, Vector2 max)
        {
            target = newTarget;
            minBounds = min;
            maxBounds = max;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var wanted = (Vector2)target.position;
            wanted.x = Mathf.Clamp(wanted.x, minBounds.x, maxBounds.x);
            wanted.y = Mathf.Clamp(wanted.y, minBounds.y, maxBounds.y);

            var current = transform.position;
            var t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            var next = Vector2.Lerp(current, wanted, t);
            transform.position = new Vector3(next.x, next.y, current.z);
        }
    }
}
