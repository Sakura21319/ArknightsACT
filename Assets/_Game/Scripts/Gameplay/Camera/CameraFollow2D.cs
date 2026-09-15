using UnityEngine;

namespace ArknightsACT.Gameplay.CameraSystem
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset = new(1.5f, 1.0f);
        [SerializeField] private float followSpeed = 8f;
        [SerializeField] private Vector2 minBounds = new(-10f, -2f);
        [SerializeField] private Vector2 maxBounds = new(20f, 8f);

        public void SetTarget(Transform value) => target = value;

        public void SetBounds(Vector2 min, Vector2 max)
        {
            minBounds = new Vector2(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y));
            maxBounds = new Vector2(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var wanted = (Vector2)target.position + offset;
            wanted.x = Mathf.Clamp(wanted.x, minBounds.x, maxBounds.x);
            wanted.y = Mathf.Clamp(wanted.y, minBounds.y, maxBounds.y);

            var current = transform.position;
            var next = Vector2.Lerp(current, wanted, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
            transform.position = new Vector3(next.x, next.y, current.z);
        }
    }
}
