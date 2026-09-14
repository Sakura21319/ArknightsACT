using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerMotor2D))]
    public sealed class PlayerDashController : MonoBehaviour
    {
        [SerializeField] private float dashSpeed = 14f;
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float dashCooldown = 0.35f;

        private Rigidbody2D _body;
        private CombatEntity _entity;
        private PlayerMotor2D _motor;
        private IPlayerInputSource _input;
        private PlayerAttackController _attack;
        private float _cooldownUntil;

        public bool IsDashing { get; private set; }
        public bool IsInvulnerable => IsDashing;

        private bool IsDead => _entity != null && _entity.Health != null && _entity.Health.IsDead;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _entity = GetComponent<CombatEntity>();
            _motor = GetComponent<PlayerMotor2D>();
            _input = GetComponent<IPlayerInputSource>();
            _attack = GetComponent<PlayerAttackController>();
        }

        private void Update()
        {
            if (!IsDead && _input != null && _input.DashPressedThisFrame)
                TryDash();
        }

        public bool TryDash()
        {
            if (IsDead || IsDashing || Time.time < _cooldownUntil)
                return false;

            if (_attack != null && _attack.IsAttacking && !_attack.CanDashCancel)
                return false;

            if (_attack != null && _attack.IsAttacking)
                _attack.CancelCurrentAttack();

            StartCoroutine(DashRoutine());
            return true;
        }

        private IEnumerator DashRoutine()
        {
            IsDashing = true;
            _cooldownUntil = Time.time + dashCooldown;

            var direction = _motor.FacingSign;
            var previousGravity = _body.gravityScale;
            _body.gravityScale = 0f;
            _body.linearVelocity = new Vector2(direction * dashSpeed, 0f);

            var elapsed = 0f;
            while (elapsed < dashDuration && !IsDead)
            {
                elapsed += Time.deltaTime;
                _body.linearVelocity = new Vector2(direction * dashSpeed, 0f);
                yield return null;
            }

            _body.gravityScale = previousGravity;
            if (IsDead)
                _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            IsDashing = false;
        }
    }
}
