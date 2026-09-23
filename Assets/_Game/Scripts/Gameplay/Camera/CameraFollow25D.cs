using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(-10.5f, 6.8f, -10.5f);
        private Vector3 _viewOffset;

        public void Configure(Transform targetValue, Vector3 offsetValue)
        {
            target = targetValue;
            offset = offsetValue;
            Snap();
        }

        public void SetViewOffset(Vector3 value)
        {
            value.y = 0f;
            _viewOffset = value;
        }

        public void ClearViewOffset() => _viewOffset = Vector3.zero;

        private void LateUpdate() => Snap();

        private void Snap()
        {
            target = PlayerRuntimeContext.Resolve(target);
            if (target == null)
                return;
            transform.position = target.position + offset + _viewOffset;
        }
    }
}
