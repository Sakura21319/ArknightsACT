using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Player-side locomotion while seated on a <see cref="Wheelchair25D"/>.
    /// Replaces <see cref="PlayerMotor25D"/> (disabled for the duration) and drives the same
    /// CharacterController with manual-wheelchair physics:
    /// modest top speed reached through slow spin-up, gentle coasting brake after input
    /// release, limited turn rate (no instant reversal), no jump and no dash.
    /// Holding Shift while rolling fast starts a drift: the wheels turn sharply but
    /// momentum keeps the chair sliding along its old line, scrubbing speed and
    /// leaning the body into the slide. Destroyed on dismount.
    /// </summary>
    public sealed class WheelchairLocomotion25D : MonoBehaviour, IPlayerControlLockSource
    {
        private const float MaxSpeed = 3.0f;              // slightly brisker manual-chair pace
        private const float Acceleration = 2.0f;          // slow spin-up: ~1.5s of pushing to reach top speed
        private const float Deceleration = 1.0f;          // gentle brake: ~3s to roll to a stop from full speed
        private const float InputLagPerSecond = 5f;       // push rhythm smoothing
        private const float TurnRateSlow = 220f;          // deg/s when nearly stopped
        private const float TurnRateFast = 105f;          // deg/s at full speed
        private const float DriftMinSpeed = 1.1f;         // below this the wheels just grip
        private const float DriftTurnMultiplier = 2.1f;   // wheels whip around while drifting
        private const float DriftGripDegPerSecond = 150f; // momentum chases the new heading slowly = slide
        private const float NormalGripDegPerSecond = 900f;// effectively locked when not drifting
        private const float DriftDrag = 1.3f;             // extra speed scrubbed while sliding
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
        private bool _drifting;

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
            var driftHeld = !locked && Keyboard.current != null &&
                            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            _drifting = driftHeld && speed > DriftMinSpeed && desired.sqrMagnitude > .001f;

            if (desired.sqrMagnitude > .001f)
            {
                if (speed < .05f)
                    _heading = desired.normalized;
                var turnRate = Mathf.Lerp(TurnRateSlow, TurnRateFast, Mathf.Clamp01(speed / MaxSpeed)) * Mathf.Deg2Rad;
                if (_drifting)
                    turnRate *= DriftTurnMultiplier;
                _heading = Vector3.RotateTowards(_heading, desired.normalized, turnRate * dt, 0f);
                // Large heading error (tight turn / reversal) bleeds speed: the chair must arc around.
                var error = Vector3.Angle(_heading, desired);
                var targetSpeed = MaxSpeed * (1f - Mathf.Clamp01(error / 130f));
                speed = Mathf.MoveTowards(speed, targetSpeed, Acceleration * dt);
                // Sliding sideways scrubs speed on top of the push.
                if (_drifting)
                    speed = Mathf.MoveTowards(speed, 0f, DriftDrag * dt);
            }
            else
            {
                speed = Mathf.MoveTowards(speed, 0f, Deceleration * dt);
            }

            // Grip: the momentum direction chases the wheel heading. Normally the chase is
            // near-instant; while drifting it lags, so the chair slides along its old line.
            var velocityDir = speed > .01f && _velocity.sqrMagnitude > .0001f ? _velocity.normalized : _heading;
            var grip = (_drifting ? DriftGripDegPerSecond : NormalGripDegPerSecond) * Mathf.Deg2Rad;
            velocityDir = Vector3.RotateTowards(velocityDir, _heading, grip * dt, 0f);
            if (velocityDir.sqrMagnitude < .001f)
                velocityDir = _heading;
            _velocity = speed > .01f ? velocityDir.normalized * speed : Vector3.zero;

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += Gravity * dt;
            _controller.Move((_velocity + Vector3.up * _verticalVelocity) * dt);

            if (speed > .05f && _motor != null)
                _motor.SetPlanarFacing(_heading);
            if (_chair != null)
            {
                // Lean into the slide: while drifting the heading leads the momentum.
                var slide = speed > .2f ? Vector3.SignedAngle(velocityDir, _heading, Vector3.up) : 0f;
                _chair.SyncPose(transform.position, _heading, speed * dt, Mathf.Clamp(slide * .6f, -12f, 12f));
                _chair.SetTrailEmitting(speed > .4f);
            }
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
