using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Keeps a flat Spine/sprite presentation facing the fixed 2.5D camera while allowing a
    /// small camera-local yaw/roll cue. The actor never turns edge-on; the cue only sells an
    /// eight-direction / three-quarter pose with side-view Spine assets.
    /// Character left/right mirroring remains owned by SpineCharacterPresentation2D.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BillboardPresentation25D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        [Header("Directional pose")]
        [SerializeField, Range(0f, 24f)] private float maxDirectionalYaw = 16f;
        [SerializeField, Range(0f, 5f)] private float maxDirectionalRoll = 2.25f;
        [SerializeField, Min(0.1f)] private float poseSmoothing = 14f;

        private float _targetYaw;
        private float _targetRoll;
        private float _currentYaw;
        private float _currentRoll;

        public void Configure(Camera cameraValue) => targetCamera = cameraValue;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        /// <summary>
        /// Applies a stable eight-direction visual cue without changing gameplay facing.
        /// strength: idle ~= .25, locomotion ~= .65, attacks/skills ~= 1.0.
        /// </summary>
        public void SetPlanarDirection(Vector3 worldForward, int facingSign, float strength)
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null)
                return;

            worldForward.y = 0f;
            if (worldForward.sqrMagnitude < 0.001f)
            {
                ResetDirectionalCue();
                return;
            }
            worldForward.Normalize();

            var cameraRight = Vector3.ProjectOnPlane(targetCamera.transform.right, Vector3.up);
            var cameraForward = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up);
            if (cameraRight.sqrMagnitude < 0.001f || cameraForward.sqrMagnitude < 0.001f)
                return;
            cameraRight.Normalize();
            cameraForward.Normalize();

            var screenDirection = new Vector2(
                Vector3.Dot(worldForward, cameraRight),
                Vector3.Dot(worldForward, cameraForward));
            if (screenDirection.sqrMagnitude < 0.001f)
                return;
            screenDirection.Normalize();

            var quantized = QuantizeEightDirections(screenDirection);
            var resolvedStrength = Mathf.Clamp01(strength);
            var sign = facingSign < 0 ? -1f : 1f;

            // Pure side movement remains almost exactly camera-facing. Moving toward/away from
            // the camera adds a controlled three-quarter yaw; diagonals add a tiny body lean.
            _targetYaw = quantized.y * sign * maxDirectionalYaw * resolvedStrength;
            _targetRoll = -quantized.x * quantized.y * maxDirectionalRoll * resolvedStrength;
        }

        public void ResetDirectionalCue()
        {
            _targetYaw = 0f;
            _targetRoll = 0f;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null)
                return;

            var dt = Time.unscaledDeltaTime;
            var blend = dt > 0f ? 1f - Mathf.Exp(-poseSmoothing * dt) : 1f;
            _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, blend);
            _currentRoll = Mathf.Lerp(_currentRoll, _targetRoll, blend);

            transform.rotation = targetCamera.transform.rotation *
                                 Quaternion.Euler(0f, _currentYaw, _currentRoll);
        }

        private static Vector2 QuantizeEightDirections(Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var sector = Mathf.RoundToInt(angle / 45f);
            sector = (sector % 8 + 8) % 8;

            const float diagonal = 0.70710678f;
            return sector switch
            {
                0 => Vector2.right,
                1 => new Vector2(diagonal, diagonal),
                2 => Vector2.up,
                3 => new Vector2(-diagonal, diagonal),
                4 => Vector2.left,
                5 => new Vector2(-diagonal, -diagonal),
                6 => Vector2.down,
                _ => new Vector2(diagonal, -diagonal)
            };
        }
    }
}
