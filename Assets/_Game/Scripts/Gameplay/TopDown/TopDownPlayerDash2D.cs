using System.Collections;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    [RequireComponent(typeof(Rigidbody2D), typeof(TopDownPlayerMotor2D), typeof(CombatEntity))]
    public sealed class TopDownPlayerDash2D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float dashSpeed = 13.5f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float dashCooldown = 0.42f;

        private Rigidbody2D _body;
        private TopDownPlayerMotor2D _motor;
        private CombatEntity _entity;
        private ITopDownInputSource _input;
        private float _cooldownUntil;

        public bool IsDashing { get; private set; }
        public bool IsInvulnerable => IsDashing;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _motor = GetComponent<TopDownPlayerMotor2D>();
            _entity = GetComponent<CombatEntity>();
            _input = GetComponent<ITopDownInputSource>();
        }

        private void Update()
        {
            if (Time.timeScale > 0.001f && _input != null && _input.DashPressedThisFrame)
                TryDash();
        }

        public bool TryDash()
        {
            if (IsDashing || Time.time < _cooldownUntil ||
                _entity == null || _entity.Health == null || _entity.Health.IsDead)
                return false;

            var move = _input?.Move ?? Vector2.zero;
            var facing = move.sqrMagnitude > 0.04f ? move.normalized : _motor.FacingDirection;
            if (facing.sqrMagnitude <= 0.001f)
                facing = Vector2.right;

            _motor.SetFacing(facing);
            var projected = _motor.ProjectDirection(facing);
            if (projected.sqrMagnitude <= 0.001f)
                projected = Vector2.right;

            StartCoroutine(DashRoutine(projected));
            return true;
        }

        private IEnumerator DashRoutine(Vector2 direction)
        {
            IsDashing = true;
            _cooldownUntil = Time.time + dashCooldown;
            var elapsed = 0f;

            while (elapsed < dashDuration && _entity != null && _entity.Health != null && !_entity.Health.IsDead)
            {
                _body.linearVelocity = direction * dashSpeed;
                elapsed += Time.deltaTime;
                yield return null;
            }

            _body.linearVelocity *= 0.28f;
            IsDashing = false;
        }
    }
}
