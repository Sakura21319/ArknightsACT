using System;
using System.Collections;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    public enum TopDownEnemyArchetype
    {
        Melee,
        FastMelee,
        Ranged,
        Boss
    }

    [RequireComponent(typeof(CombatEntity), typeof(Rigidbody2D))]
    public sealed class TopDownEnemyBrain2D : MonoBehaviour
    {
        [SerializeField] private TopDownEnemyArchetype archetype = TopDownEnemyArchetype.Melee;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField, Range(0.4f, 1f)] private float verticalMoveScale = 0.72f;
        [SerializeField] private float attackRange = 1.15f;
        [SerializeField] private float preferredRange = 4.5f;
        [SerializeField] private float attackWindup = 0.20f;
        [SerializeField] private float attackRecovery = 0.18f;
        [SerializeField] private float attackCooldown = 0.82f;
        [SerializeField] private float attackDamage = 5f;

        private CombatEntity _entity;
        private Rigidbody2D _body;
        private CombatEntity _target;
        private Coroutine _attackRoutine;
        private float _nextAttackAt;
        private float _retargetAt;
        private float _staggerUntil;

        public bool IsMoving { get; private set; }
        public bool IsAttacking => _attackRoutine != null;
        public int FacingSign { get; private set; } = -1;
        public TopDownEnemyArchetype Archetype => archetype;

        public event Action<int> AttackStarted;

        public void Configure(TopDownEnemyArchetype value)
        {
            archetype = value;
            ApplyDefaults();
        }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            ApplyDefaults();
        }

        private void OnEnable()
        {
            if (_entity != null)
                _entity.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_entity != null)
                _entity.Damaged -= OnDamaged;
            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);
            _attackRoutine = null;
            if (_body != null)
                _body.linearVelocity = Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (_entity == null || _entity.Health == null || _entity.Health.IsDead)
            {
                Stop();
                return;
            }

            AcquireTarget();
            if (_target == null || _target.Health == null || _target.Health.IsDead)
            {
                Stop();
                return;
            }

            var delta = (Vector2)_target.transform.position - (Vector2)transform.position;
            var distance = delta.magnitude;
            if (Mathf.Abs(delta.x) > 0.02f)
                FacingSign = delta.x >= 0f ? 1 : -1;

            if (Time.time < _staggerUntil || IsAttacking)
            {
                Stop();
                return;
            }

            switch (archetype)
            {
                case TopDownEnemyArchetype.Ranged:
                    TickRanged(delta, distance);
                    break;
                case TopDownEnemyArchetype.Boss:
                    TickBoss(delta, distance);
                    break;
                default:
                    TickMelee(delta, distance);
                    break;
            }
        }

        private void TickMelee(Vector2 delta, float distance)
        {
            if (distance <= attackRange)
            {
                Stop();
                TryAttack();
                return;
            }
            Move(delta.normalized);
        }

        private void TickRanged(Vector2 delta, float distance)
        {
            if (distance > preferredRange * 1.12f)
            {
                Move(delta.normalized);
                return;
            }
            if (distance < preferredRange * 0.62f)
            {
                Move(-delta.normalized);
                return;
            }
            Stop();
            TryAttack();
        }

        private void TickBoss(Vector2 delta, float distance)
        {
            if (distance > attackRange)
            {
                Move(delta.normalized);
                return;
            }
            Stop();
            TryAttack();
        }

        private void TryAttack()
        {
            if (_attackRoutine != null || Time.time < _nextAttackAt)
                return;
            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            Stop();
            AttackStarted?.Invoke(FacingSign);
            yield return new WaitForSeconds(attackWindup);

            if (_target != null && _target.Health != null && !_target.Health.IsDead)
            {
                var delta = (Vector2)_target.transform.position - (Vector2)transform.position;
                var distance = delta.magnitude;
                if (archetype == TopDownEnemyArchetype.Ranged)
                {
                    if (distance <= preferredRange * 1.6f)
                        TopDownEnemyProjectile2D.Spawn(_entity, transform.position, delta.normalized, 6.8f, attackDamage);
                }
                else if (distance <= attackRange + 0.35f)
                {
                    var direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector2.right;
                    var context = new DamageContext(
                        _entity,
                        _entity,
                        _target,
                        attackDamage,
                        DamageType.Physical,
                        direction * (archetype == TopDownEnemyArchetype.Boss ? 3.2f : 1.6f),
                        sourceId: "TopDownEnemy_" + archetype);
                    DamageSystem.Apply(context);
                }
            }

            yield return new WaitForSeconds(attackRecovery);
            _attackRoutine = null;
            _nextAttackAt = Time.time + attackCooldown;
        }

        private void AcquireTarget()
        {
            if (_target != null && _target.Health != null && !_target.Health.IsDead)
                return;
            if (Time.time < _retargetAt)
                return;

            _retargetAt = Time.time + 0.35f;
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            var bestSqr = float.PositiveInfinity;
            CombatEntity best = null;
            foreach (var candidate in entities)
            {
                if (candidate == null || candidate.Team != Team.Player || candidate.Health == null || candidate.Health.IsDead)
                    continue;
                var sqr = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = candidate;
            }
            _target = best;
        }

        private void Move(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                Stop();
                return;
            }
            if (Mathf.Abs(direction.x) > 0.02f)
                FacingSign = direction.x >= 0f ? 1 : -1;

            var projected = new Vector2(direction.x, direction.y * verticalMoveScale);
            if (projected.sqrMagnitude > 0.001f)
                projected.Normalize();
            _body.linearVelocity = projected * moveSpeed;
            IsMoving = true;
        }

        private void Stop()
        {
            if (_body == null)
                return;
            _body.linearVelocity = Vector2.zero;
            IsMoving = false;
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            _staggerUntil = Mathf.Max(_staggerUntil, Time.time + (archetype == TopDownEnemyArchetype.Boss ? 0.06f : 0.12f));
            if (_attackRoutine != null && archetype != TopDownEnemyArchetype.Boss)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
                _nextAttackAt = Time.time + 0.18f;
            }
        }

        private void ApplyDefaults()
        {
            switch (archetype)
            {
                case TopDownEnemyArchetype.FastMelee:
                    moveSpeed = 3.5f;
                    attackRange = 0.95f;
                    attackWindup = 0.12f;
                    attackRecovery = 0.10f;
                    attackCooldown = 0.48f;
                    attackDamage = 4f;
                    break;
                case TopDownEnemyArchetype.Ranged:
                    moveSpeed = 1.8f;
                    preferredRange = 4.8f;
                    attackWindup = 0.28f;
                    attackRecovery = 0.18f;
                    attackCooldown = 1.05f;
                    attackDamage = 5f;
                    break;
                case TopDownEnemyArchetype.Boss:
                    moveSpeed = 1.45f;
                    attackRange = 1.55f;
                    attackWindup = 0.34f;
                    attackRecovery = 0.28f;
                    attackCooldown = 0.72f;
                    attackDamage = 12f;
                    break;
                default:
                    moveSpeed = 2.25f;
                    attackRange = 1.10f;
                    attackWindup = 0.20f;
                    attackRecovery = 0.18f;
                    attackCooldown = 0.78f;
                    attackDamage = 6f;
                    break;
            }
        }
    }
}
