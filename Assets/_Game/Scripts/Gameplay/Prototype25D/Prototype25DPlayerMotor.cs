using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Prototype25D
{
    /// <summary>
    /// Small isolated movement controller for the 2.5D visual prototype.
    /// Movement is free on the world XZ ground plane and interpreted relative to the camera.
    /// It intentionally does not reuse the production 2D ACT motor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class Prototype25DPlayerMotor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private Camera gameplayCamera;

        private CharacterController _controller;
        private float _verticalVelocity;

        public bool IsMoving { get; private set; }
        public int ScreenFacingSign { get; private set; } = 1;

        public void SetCamera(Camera value) => gameplayCamera = value;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                    input.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                    input.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    input.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    input.y += 1f;
            }

            if (input.sqrMagnitude > 1f)
                input.Normalize();

            var cameraTransform = gameplayCamera != null ? gameplayCamera.transform : null;
            var forward = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : Vector3.forward;
            var right = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
                : Vector3.right;

            var horizontal = right * input.x + forward * input.y;
            if (horizontal.sqrMagnitude > 1f)
                horizontal.Normalize();

            IsMoving = horizontal.sqrMagnitude > 0.001f;
            if (Mathf.Abs(input.x) > 0.05f)
                ScreenFacingSign = input.x >= 0f ? 1 : -1;

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            var velocity = horizontal * moveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
