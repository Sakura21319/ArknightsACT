using System;
using System.Collections;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatEntity))]
    public sealed class TreasureMonsterBrain25D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 3.0f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.25f;
        [SerializeField, Min(0f)] private float attackWindup = 0.24f;
        [SerializeField, Min(0f)] private float attackRecovery = 0.16f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.78f;
        [SerializeField, Min(0.1f)] private float attackDamage = 7f;
        [SerializeField] private float gravity = -24f;

        private CharacterController _controller;
        private CombatEntity _entity;
        private CombatEntity _target;
        private Coroutine _attackRoutine;
        private float _nextAttackAt;
        private float _verticalVelocity;
        private Vector3 _forward = Vector3.back;

        public event Action Activated;
        public event Action<int> AttackStarted;
        public bool IsActivated { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsAttacking => _attackRoutine != null;
        public Vector3 LogicForward => _forward.sqrMagnitude > 0.001f ? _forward.normalized : Vector3.back;
        public int FacingSign { get; private set; } = -1;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _entity = GetComponent<CombatEntity>();
        }

        public void Activate(CombatEntity target)
        {
            if (target == null || target.Health == null || target.Health.IsDead)
                return;

            _target = target;
            if (IsActivated)
                return;

            IsActivated = true;
            FaceToward(target.transform.position);
            Activated?.Invoke();
        }

        private void OnDisable()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }
            IsMoving = false;
        }

        private void Update()
        {
            if (!IsActivated || _entity?.Health == null || _entity.Health.IsDead)
                return;
            if (_target == null || _target.Health == null || _target.Health.IsDead)
            {
                IsMoving = false;
                return;
            }

            ApplyGravity();
            if (IsAttacking)
            {
                IsMoving = false;
                return;
            }

            var delta = _target.transform.position - transform.position;
            var height = Mathf.Abs(delta.y);
            delta.y = 0f;
            var distance = delta.magnitude;
            FaceToward(_target.transform.position);

            if (height <= 1.1f && distance <= attackRange && HasClearLine())
            {
                IsMoving = false;
                TryAttack();
                return;
            }

            if (distance > 0.05f)
            {
                _controller.Move(delta.normalized * (moveSpeed * Time.deltaTime));
                IsMoving = true;
            }
            else
            {
                IsMoving = false;
            }
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackAt || _attackRoutine != null)
                return;
            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            AttackStarted?.Invoke(FacingSign);
            if (attackWindup > 0f)
                yield return new WaitForSeconds(attackWindup);

            if (_target != null && _target.Health != null && !_target.Health.IsDead)
            {
                var delta = _target.transform.position - transform.position;
                var height = Mathf.Abs(delta.y);
                delta.y = 0f;
                if (height <= 1.1f && delta.magnitude <= attackRange + 0.30f && HasClearLine())
                {
                    DamageSystem.Apply(new DamageContext(
                        _entity,
                        _entity,
                        _target,
                        attackDamage,
                        DamageType.Physical,
                        Vector2.zero,
                        sourceId: "TreasureMonster"));
                }
            }

            if (attackRecovery > 0f)
                yield return new WaitForSeconds(attackRecovery);
            _attackRoutine = null;
            _nextAttackAt = Time.time + attackCooldown;
        }

        private bool HasClearLine()
        {
            if (_target == null)
                return false;
            var origin = transform.position + Vector3.up * 0.62f;
            var destination = _target.transform.position + Vector3.up * 0.62f;
            var cast = destination - origin;
            var distance = cast.magnitude;
            if (distance < 0.001f)
                return true;

            var hits = Physics.SphereCastAll(origin, 0.14f, cast / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;
                var entity = collider.GetComponentInParent<CombatEntity>();
                if (entity != null)
                {
                    if (entity == _target)
                        return true;
                    continue;
                }
                return false;
            }
            return true;
        }

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));
        }

        private void FaceToward(Vector3 position)
        {
            var direction = position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;
            _forward = direction.normalized;

            var camera = Camera.main;
            if (camera == null)
                return;
            var right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up);
            if (right.sqrMagnitude < 0.001f)
                return;
            right.Normalize();
            var dot = Vector3.Dot(_forward, right);
            if (Mathf.Abs(dot) >= 0.25f)
                FacingSign = dot >= 0f ? 1 : -1;
        }
    }
}
