using UnityEngine;

namespace ArknightsACT.Gameplay.Feedback
{
    public sealed class CameraShake2D : MonoBehaviour
    {
        public static CameraShake2D Instance { get; private set; }

        private Vector3 _baseLocalPosition;
        private float _amplitude;
        private float _remaining;

        private void Awake()
        {
            Instance = this;
            _baseLocalPosition = transform.localPosition;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Shake(float amplitude, float duration)
        {
            _amplitude = Mathf.Max(_amplitude, amplitude);
            _remaining = Mathf.Max(_remaining, duration);
        }

        private void LateUpdate()
        {
            if (_remaining <= 0f)
            {
                transform.localPosition = _baseLocalPosition;
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            var offset = Random.insideUnitCircle * _amplitude;
            transform.localPosition = _baseLocalPosition + new Vector3(offset.x, offset.y, 0f);

            if (_remaining <= 0f)
                transform.localPosition = _baseLocalPosition;
        }
    }
}
