using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Combat;
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

        [Header("Jump")]
        // Prototype ACT jump: deliberately short and decisive. The previous ~0.65s arc still
        // felt floaty, so this version targets roughly 0.47s total airtime with a slightly
        // lower peak. Keep jump tuning here independent from animation/presentation code.
        [SerializeField] private float jumpVelocity = 15.5f;
        [SerializeField] private float riseGravityMultiplier = 6.5f;
        [SerializeField] private float fallGravityMultiplier = 7.5f;

        [Header("Ground")]
        [SerializeField] private float groundCastDistance = 0.08f;

        private Rigidbody2D _body;
        private Collider2D _collider;
        private CombatEntity _entity;
        private IPlayerInputSource _input;
        private PlayerDashController _dash;
        private PlayerAttackController _attack;
        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];

        public bool IsGrounded { get; private set; }
        public int FacingSign { get; private set; } = 1;
        public Rigidbody2D Body => _body;

        private bool IsDead => _entity != null && _entity.Health != null && _entity.Health.IsDead;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _entity = GetComponent<CombatEntity>();
            _input = GetComponent<IPlayerInputSource>();
            _dash = GetComponent<PlayerDashController>();
            _attack = GetComponent<PlayerAttackController>();
        }

        private void Update()
        {
            UpdateGrounded();

            if (IsDead || (_dash != null && _dash.IsDashing))
                return;

            // Basic attacks are committed grounded actions in the prototype. Starting an attack
            // stops horizontal movement and also prevents jump from being injected mid-swing.
            if (_attack != null && _attack.IsAttacking)
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
            if (IsDead)
            {
                var deadVelocity = _body.linearVelocity;
                deadVelocity.x = 0f;
                _body.linearVelocity = deadVelocity;
                _body.gravityScale = fallGravityMultiplier;
                return;
            }

            if (_dash != null && _dash.IsDashing)
                return;

            var current = _body.linearVelocity;

            // A normal attack owns horizontal locomotion for its whole visible swing. Pressing J
            // while running therefore means "plant feet and attack", never run-and-slash.
            if (_attack != null && _attack.IsAttacking)
            {
                current.x = 0f;
                _body.gravityScale = current.y > 0.05f ? riseGravityMultiplier : fallGravityMultiplier;
                _body.linearVelocity = current;
                return;
            }

            var inputX = _input?.Move.x ?? 0f;
            if (Mathf.Abs(inputX) > 0.01f)
                FacingSign = inputX > 0f ? 1 : -1;

            var targetX = inputX * moveSpeed;
            var rate = Mathf.Abs(targetX) > Mathf.Abs(current.x) ? acceleration : deceleration;
            current.x = Mathf.MoveTowards(current.x, targetX, rate * Time.fixedDeltaTime);

            _body.gravityScale = current.y > 0.05f ? riseGravityMultiplier : fallGravityMultiplier;
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
