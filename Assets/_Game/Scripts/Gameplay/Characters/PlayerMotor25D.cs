using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Production 2.5D player motor. Gameplay movement is free on XZ while Y is reserved for jump height.
    /// Input is interpreted relative to the fixed gameplay camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatEntity))]
    public sealed class PlayerMotor25D : MonoBehaviour, IPlayerLocomotion
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.8f;
        [SerializeField, Min(0.1f)] private float acceleration = 24f;
        [SerializeField, Min(0.1f)] private float deceleration = 32f;

        [Header("Jump")]
        [SerializeField, Min(0.1f)] private float jumpVelocity = 8.2f;
        [SerializeField] private float gravity = -24f;

        [Header("References")]
        [SerializeField] private Camera gameplayCamera;

        private CharacterController _controller;
        private CombatEntity _entity;
        private IPlayerInputSource _input;
        private PlayerDashController _dash;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private Vector3 _planarVelocity;
        private Vector3 _planarForward = Vector3.forward;
        private float _verticalVelocity;

        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsMoving => _planarVelocity.sqrMagnitude > 0.01f;
        public int FacingSign { get; private set; } = 1;
        public Vector3 PlanarForward => _planarForward.sqrMagnitude > 0.001f ? _planarForward.normalized : Vector3.forward;
        public float PlanarSpeed => _planarVelocity.magnitude;
        public CharacterController Controller => _controller;

        public void SetCamera(Camera value) => gameplayCamera = value;

        private bool IsDead => _entity != null && _entity.Health != null && _entity.Health.IsDead;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _entity = GetComponent<CombatEntity>();
            _input = GetComponent<IPlayerInputSource>();
            _dash = GetComponent<PlayerDashController>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
        }

        private void Update()
        {
            if (_controller == null)
                return;

            if (IsDead)
            {
                _planarVelocity = Vector3.zero;
                ApplyGravityOnly();
                return;
            }

            if (_dash != null && _dash.IsDashing)
            {
                _planarVelocity = Vector3.zero;
                return;
            }

            var movementLocked = (_attack != null && _attack.IsMovementLocked) ||
                                 (_skills != null && _skills.IsCasting);
            var desired = movementLocked ? Vector3.zero : ResolveDesiredPlanarVelocity();
            var rate = desired.sqrMagnitude > _planarVelocity.sqrMagnitude ? acceleration : deceleration;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, desired, rate * Time.deltaTime);

            if (!movementLocked && _input != null && _input.JumpPressedThisFrame && IsGrounded)
                _verticalVelocity = jumpVelocity;

            if (IsGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            _controller.Move((_planarVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        public void ResetMotion()
        {
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        private Vector3 ResolveDesiredPlanarVelocity()
        {
            var input = _input?.Move ?? Vector2.zero;
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            var cameraTransform = gameplayCamera != null ? gameplayCamera.transform : null;
            var forward = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : Vector3.forward;
            var right = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
                : Vector3.right;

            var direction = right * input.x + forward * input.y;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            if (direction.sqrMagnitude > 0.001f)
            {
                _planarForward = direction.normalized;
                var screenHorizontal = Vector3.Dot(_planarForward, right);
                if (Mathf.Abs(screenHorizontal) > 0.08f)
                    FacingSign = screenHorizontal >= 0f ? 1 : -1;
            }

            return direction * moveSpeed;
        }

        private void ApplyGravityOnly()
        {
            if (IsGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));
        }
    }
}
