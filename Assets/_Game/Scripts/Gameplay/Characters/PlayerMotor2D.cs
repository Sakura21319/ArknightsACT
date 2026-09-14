using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float acceleration = 55f;
        [SerializeField] private float deceleration = 70f;
        [SerializeField] private float jumpVelocity = 7.0f;
        [SerializeField] private float fallGravityMultiplier = 2.0f;

        [Header("Ground")]
        [SerializeField] private float groundCastDistance = 0.08f;

        private Rigidbody2D _body;
        private Collider2D _collider;
        private IPlayerInputSource _input;
        private PlayerDashController _dash;
        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];

        public bool IsGrounded { get; private set; }
        public int FacingSign { get; private set; } = 1;
        public Rigidbody2D Body => _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _input = GetComponent<IPlayerInputSource>();
            _dash = GetComponent<PlayerDashController>();
        }

        private void Update()
        {
            UpdateGrounded();

            if (_dash != null && _dash.IsDashing)
                return;

            if (_input != null && _input.JumpPressedThisFrame && IsGrounded)
            {
                var velocity = _body.linearVelocity;
                velocity.y = jumpVelocity;
                _body.linearVelocity = velocity;
            }
        }

        private void FixedUpdate()
        {
            if (_dash != null && _dash.IsDashing)
                return;

            var inputX = _input?.Move.x ?? 0f;
            if (Mathf.Abs(inputX) > 0.01f)
                FacingSign = inputX > 0f ? 1 : -1;

            var targetX = inputX * moveSpeed;
            var current = _body.linearVelocity;
            var rate = Mathf.Abs(targetX) > Mathf.Abs(current.x) ? acceleration : deceleration;
            current.x = Mathf.MoveTowards(current.x, targetX, rate * Time.fixedDeltaTime);

            var gravityScale = current.y < 0f ? fallGravityMultiplier : 1f;
            _body.gravityScale = gravityScale;
            _body.linearVelocity = current;
        }

        private void UpdateGrounded()
        {
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };

            IsGrounded = _collider.Cast(Vector2.down, filter, _groundHits, groundCastDistance) > 0;
        }
    }
}
