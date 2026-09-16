using UnityEngine;

namespace ArknightsACT.Gameplay.Prototype25D
{
    /// <summary>
    /// Keeps a flat 2D/Spine presentation facing the fixed 2.5D camera while the actor moves on XZ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Prototype25DBillboard : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Prototype25DPlayerMotor motor;

        private Vector3 _baseScale;

        public void Configure(Camera cameraValue, Prototype25DPlayerMotor motorValue)
        {
            targetCamera = cameraValue;
            motor = motorValue;
        }

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (motor == null)
                motor = GetComponentInParent<Prototype25DPlayerMotor>();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera != null)
                transform.rotation = targetCamera.transform.rotation;

            var sign = motor != null ? motor.ScreenFacingSign : 1;
            transform.localScale = new Vector3(
                Mathf.Abs(_baseScale.x) * sign,
                _baseScale.y,
                _baseScale.z);
        }
    }
}
