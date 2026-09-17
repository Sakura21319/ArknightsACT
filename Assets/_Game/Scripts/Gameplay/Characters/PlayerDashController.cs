using System;
using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class PlayerDashController : MonoBehaviour, IPlayerInvulnerabilitySource
    {
        [SerializeField] private float dashSpeed = 14f;
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float dashCooldown = 0.35f;

        private Rigidbody2D _body2D;
        private CharacterController _controller3D;
        private CombatEntity _entity;
        private IPlayerLocomotion _motor;
        private IPlayerInputSource _input;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private float _cooldownUntil;

        public bool IsDashing { get; private set; }
        public bool IsInvulnerable => IsDashing;

        public event Action DashStarted;
        public event Action DashEnded;

        private bool IsDead => _entity != null && _entity.Health != null && _entity.Health.IsDead;
        private bool Uses25D => _controller3D != null && _body2D == null;

        private void Awake()
        {
            _body2D = GetComponent<Rigidbody2D>();
            _controller3D = GetComponent<CharacterController>();
            _entity = GetComponent<CombatEntity>();
            _motor = FindLocomotion();
            _input = GetComponent<IPlayerInputSource>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
        }

        private void Update()
        {
            if (!IsDead && _input != null && _input.DashPressedThisFrame)
                TryDash();
        }

        public bool TryDash()
        {
            if (IsDead || IsDashing || Time.time < _cooldownUntil || (_skills != null && _skills.IsCasting))
                return false;
            if (_body2D == null && _controller3D == null)
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
            DashStarted?.Invoke();

            if (Uses25D)
                yield return Dash25D();
            else
                yield return Dash2D();

            IsDashing = false;
            DashEnded?.Invoke();
        }

        private IEnumerator Dash2D()
        {
            if (_body2D == null)
                yield break;

            var direction = _motor != null ? _motor.FacingSign : 1;
            var previousGravity = _body2D.gravityScale;
            _body2D.gravityScale = 0f;
            _body2D.linearVelocity = new Vector2(direction * dashSpeed, 0f);

            var elapsed = 0f;
            while (elapsed < dashDuration && !IsDead)
            {
                elapsed += Time.deltaTime;
                _body2D.linearVelocity = new Vector2(direction * dashSpeed, 0f);
                yield return null;
            }

            _body2D.gravityScale = previousGravity;
            if (IsDead)
                _body2D.linearVelocity = new Vector2(0f, _body2D.linearVelocity.y);
        }

        private IEnumerator Dash25D()
        {
            if (_controller3D == null)
                yield break;

            var direction = _motor != null ? _motor.PlanarForward : Vector3.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.forward;
            direction.Normalize();

            var elapsed = 0f;
            while (elapsed < dashDuration && !IsDead)
            {
                var dt = Time.deltaTime;
                elapsed += dt;
                _controller3D.Move(direction * (dashSpeed * dt));
                yield return null;
            }
        }

        private IPlayerLocomotion FindLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            }
            return null;
        }
    }
}
