using UnityEngine;

namespace ArknightsACT.Gameplay.Prototype25D
{
    /// <summary>
    /// Keeps the camera at a fixed world-space offset and rotation, following the player without orbiting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Prototype25DCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(-10.5f, 6.8f, -10.5f);

        public void Configure(Transform targetValue, Vector3 offsetValue)
        {
            target = targetValue;
            offset = offsetValue;
            Snap();
        }

        private void LateUpdate() => Snap();

        private void Snap()
        {
            if (target == null)
                return;
            transform.position = target.position + offset;
        }
    }
}
