using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Player-side locomotion while seated on a <see cref="Wheelchair25D"/>.
    /// Replaces <see cref="PlayerMotor25D"/> (disabled for the duration) and drives the same
    /// CharacterController with manual-wheelchair physics:
    /// slow top speed, sluggish acceleration, long coasting after input release,
    /// limited turn rate (no instant reversal), no jump and no dash.
    /// Destroyed on dismount.
    /// </summary>
    public sealed class WheelchairLocomotion25D : MonoBehaviour, IPlayerControlLockSource
    {
        private const float MaxSpeed = 2.0f;          // manual wheelchair pace
        private const float Acceleration = 2.4f;      // sluggish push-off
        private const float Deceleration = 0.8f;      // coasting: keeps rolling after release
        private const float InputLagPerSecond = 5f;   // push rhythm smoothing
        private const float TurnRateSlow = 220f;      // deg/s when nearly stopped
        private const float TurnRateFast = 105f;      // deg/s at full speed
        private const float Gravity = -24f;

        private Wheelchair25D _chair;
        private PlayerMotor25D _motor;
        private CharacterController _controller;
        private CombatEntity _entity;
        private IPlayerInputSource _input;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private Camera _camera;
        private Vector3 _velocity;
        private Vector2 _smoothedInput;
        private Vector3 _heading = Vector3.forward;
        private float _verticalVelocity;

        public bool BlocksMovement => false;
        public bool BlocksDash => true;
        public bool BlocksBasicAttack => false;

        public void Configure(Wheelchair25D chair, PlayerMotor25D motor)
        {
            _chair = chair;
            _motor = motor;
            _controller = GetComponent<CharacterController>();
            _entity = GetComponent<CombatEntity>();
            _input = GetComponent<IPlayerInputSource>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _camera = Camera.main;
            var forward = motor != null ? motor.PlanarForward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > .001f)
                _heading = forward.normalized;
        }

        private void Update()
        {
            if (_controller == null)
                return;
            var dt = Time.deltaTime;
            var dead = _entity != null && _entity.Health != null && _entity.Health.IsDead;
            var locked = dead || GameplayInputBlocker.IsBlocked ||
                         (_attack != null && _attack.IsMovementLocked) ||
                         (_skills != null && _skills.IsCasting) ||
                         CombatActionUtility.IsBlocked(_entity, CombatActionMask.Movement);

            var rawInput = locked || _input == null ? Vector2.zero : _input.Move;
            if (rawInput.sqrMagnitude > 1f)
                rawInput.Normalize();
            // Wheel pushes come in strokes: input eases in instead of applying instantly.
            _smoothedInput = Vector2.MoveTowards(_smoothedInput, rawInput, InputLagPerSecond * dt);

            var desired = CameraRelative(_smoothedInput);
            var speed = _velocity.magnitude;
            if (desired.sqrMagnitude > .001f)
            {
                if (speed < .05f)
                    _heading = desired.normalized;
                var turnRate = Mathf.Lerp(TurnRateSlow, TurnRateFast, Mathf.Clamp01(speed / MaxSpeed)) * Mathf.Deg2Rad;
                _heading = Vector3.RotateTowards(_heading, desired.normalized, turnRate * dt, 0f);
                // Large heading error (tight turn / reversal) bleeds speed: the chair must arc around.
                var error = Vector3.Angle(_heading, desired);
                var targetSpeed = MaxSpeed * (1f - Mathf.Clamp01(error / 130f));
                speed = Mathf.MoveTowards(speed, targetSpeed, Acceleration * dt);
            }
            else
            {
                speed = Mathf.MoveTowards(speed, 0f, Deceleration * dt);
            }
            _velocity = speed > .01f ? _heading * speed : Vector3.zero;

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += Gravity * dt;
            _controller.Move((_velocity + Vector3.up * _verticalVelocity) * dt);

            if (speed > .05f && _motor != null)
                _motor.SetPlanarFacing(_heading);
            if (_chair != null)
                _chair.SyncPose(transform.position, _heading);
        }

        private Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < .0001f)
                return Vector3.zero;
            if (_camera == null)
                _camera = Camera.main;
            var t = _camera != null ? _camera.transform : null;
            var forward = t != null ? Vector3.ProjectOnPlane(t.forward, Vector3.up) : Vector3.forward;
            var right = t != null ? Vector3.ProjectOnPlane(t.right, Vector3.up) : Vector3.right;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            if (right.sqrMagnitude < .001f) right = Vector3.right;
            forward.Normalize();
            right.Normalize();
            var direction = right * input.x + forward * input.y;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }
    }
}
