using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CombatEntity))]
    public sealed class TopDownPlayerMotor2D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 5.6f;
        [SerializeField, Min(1f)] private float acceleration = 55f;
        [SerializeField, Min(1f)] private float deceleration = 70f;

        private Rigidbody2D _body;
        private CombatEntity _entity;
        private ITopDownInputSource _input;
        private TopDownPlayerDash2D _dash;

        public Vector2 FacingDirection { get; private set; } = Vector2.right;
        public int FacingSign { get; private set; } = 1;
        public bool IsMoving => _body != null && _body.linearVelocity.sqrMagnitude > 0.04f;
        public Rigidbody2D Body => _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _entity = GetComponent<CombatEntity>();
            _input = GetComponent<ITopDownInputSource>();
            _dash = GetComponent<TopDownPlayerDash2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
        }

        private void FixedUpdate()
        {
            if (_entity == null || _entity.Health == null || _entity.Health.IsDead)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            if (_dash != null && _dash.IsDashing)
                return;

            var move = _input?.Move ?? Vector2.zero;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            if (move.sqrMagnitude > 0.001f)
                SetFacing(move);

            var target = move * moveSpeed;
            var current = _body.linearVelocity;
            var rate = target.sqrMagnitude > current.sqrMagnitude ? acceleration : deceleration;
            _body.linearVelocity = Vector2.MoveTowards(current, target, rate * Time.fixedDeltaTime);
        }

        public void SetFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
                return;

            FacingDirection = direction.normalized;
            if (Mathf.Abs(FacingDirection.x) > 0.08f)
                FacingSign = FacingDirection.x >= 0f ? 1 : -1;
        }
    }
}
