using System;
using System.Collections;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    public enum PrototypeEnemyArchetype
    {
        Melee,
        FastMelee,
        Ranged
    }

    [RequireComponent(typeof(CombatEntity), typeof(Rigidbody2D))]
    public sealed class PrototypeEnemyCombatBrain2D : MonoBehaviour
    {
        [SerializeField] private PrototypeEnemyArchetype archetype = PrototypeEnemyArchetype.Melee;
        [SerializeField] private float aggroRange = 12f;
        [SerializeField] private float moveSpeed = 2.7f;
        [SerializeField] private float attackRange = 1.25f;
        [SerializeField] private float preferredRange = 4.6f;
        [SerializeField] private float attackWindup = 0.20f;
        [SerializeField] private float attackRecovery = 0.18f;
        [SerializeField] private float attackCooldown = 0.80f;
        [SerializeField] private float attackDamage = 5f;
        [SerializeField] private float hitStaggerSeconds = 0.14f;

        private CombatEntity _entity;
        private Rigidbody2D _body;
        private CombatEntity _target;
        private Coroutine _attackRoutine;
        private float _nextAttackAt;
        private float _nextTargetSearchAt;
        private float _staggerUntil;

        public event Action<int> AttackStarted;
        public bool IsMoving { get; private set; }
        public bool IsAttacking => _attackRoutine != null;
        public int FacingSign { get; private set; } = -1;
        public PrototypeEnemyArchetype Archetype => archetype;

        public void Configure(PrototypeEnemyArchetype value)
        {
            archetype = value;
            ApplyArchetypeDefaults();
        }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _body = GetComponent<Rigidbody2D>();
            ApplyArchetypeDefaults();
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
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }
            if (_body != null)
                StopHorizontal();
        }

        private void FixedUpdate()
        {
            if (_entity == null || _entity.Health == null || _entity.Health.IsDead)
                return;

            AcquireTargetIfNeeded();
            if (_target == null || _target.Health == null || _target.Health.IsDead)
            {
                StopHorizontal();
                return;
            }

            var deltaX = _target.transform.position.x - transform.position.x;
            var distance = Mathf.Abs(deltaX);
            if (Mathf.Abs(deltaX) > 0.02f)
                FacingSign = deltaX >= 0f ? 1 : -1;

            if (Time.time < _staggerUntil)
            {
                IsMoving = Mathf.Abs(_body.linearVelocity.x) > 0.08f;
                return;
            }

            if (IsAttacking)
            {
                StopHorizontal();
                return;
            }

            if (distance > aggroRange)
            {
                StopHorizontal();
                return;
            }

            switch (archetype)
            {
                case PrototypeEnemyArchetype.Ranged:
                    TickRanged(deltaX, distance);
                    break;
                default:
                    TickMelee(deltaX, distance);
                    break;
            }
        }

        private void TickMelee(float deltaX, float distance)
        {
            if (distance <= attackRange)
            {
                StopHorizontal();
                TryAttack();
                return;
            }
            Move(Mathf.Sign(deltaX));
        }

        private void TickRanged(float deltaX, float distance)
        {
            var retreatDistance = preferredRange * 0.62f;
            if (distance < retreatDistance)
            {
                Move(-Mathf.Sign(deltaX));
                return;
            }
            if (distance > preferredRange * 1.18f)
            {
                Move(Mathf.Sign(deltaX));
                return;
            }
            StopHorizontal();
            TryAttack();
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackAt || _target == null || _attackRoutine != null)
                return;

            StopHorizontal();
            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            StopHorizontal();
            AttackStarted?.Invoke(FacingSign);

            yield return WaitWhileLocked(attackWindup);

            if (_target != null && _target.Health != null && !_target.Health.IsDead)
            {
                var distance = Mathf.Abs(_target.transform.position.x - transform.position.x);
                var validRange = archetype == PrototypeEnemyArchetype.Ranged
                    ? preferredRange * 1.45f
                    : attackRange + 0.35f;

                if (distance <= validRange)
                {
                    var knockback = new Vector2(FacingSign * (archetype == PrototypeEnemyArchetype.FastMelee ? 2.8f : 1.8f), 0.45f);
                    var context = new DamageContext(
                        _entity,
                        _entity,
                        _target,
                        attackDamage,
                        DamageType.Physical,
                        knockback,
                        sourceId: "PrototypeEnemy_" + archetype);
                    DamageSystem.Apply(context);
                }
            }

            yield return WaitWhileLocked(attackRecovery);
            StopHorizontal();
            _attackRoutine = null;
            _nextAttackAt = Time.time + attackCooldown;
        }

        private IEnumerator WaitWhileLocked(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                StopHorizontal();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            _staggerUntil = Mathf.Max(_staggerUntil, Time.time + hitStaggerSeconds);
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
                _nextAttackAt = Time.time + Mathf.Max(0.15f, attackCooldown * 0.35f);
            }
            IsMoving = false;
        }

        private void AcquireTargetIfNeeded()
        {
            if (_target != null && _target.Health != null && !_target.Health.IsDead)
                return;
            if (Time.time < _nextTargetSearchAt)
                return;

            _nextTargetSearchAt = Time.time + 0.4f;
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            var bestDistance = float.PositiveInfinity;
            CombatEntity best = null;
            foreach (var candidate in entities)
            {
                if (candidate == null || candidate.Team != Team.Player || candidate.Health == null || candidate.Health.IsDead)
                    continue;
                var sqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqr >= bestDistance)
                    continue;
                bestDistance = sqr;
                best = candidate;
            }
            _target = best;
        }

        private void Move(float direction)
        {
            if (Mathf.Abs(direction) < 0.01f)
            {
                StopHorizontal();
                return;
            }

            FacingSign = direction >= 0f ? 1 : -1;
            _body.linearVelocity = new Vector2(FacingSign * moveSpeed, _body.linearVelocity.y);
            IsMoving = true;
        }

        private void StopHorizontal()
        {
            if (_body == null)
                return;
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            IsMoving = false;
        }

        private void ApplyArchetypeDefaults()
        {
            switch (archetype)
            {
                case PrototypeEnemyArchetype.FastMelee:
                    moveSpeed = 4.2f;
                    attackRange = 1.05f;
                    attackWindup = 0.14f;
                    attackRecovery = 0.12f;
                    attackCooldown = 0.55f;
                    attackDamage = 4f;
                    hitStaggerSeconds = 0.15f;
                    break;
                case PrototypeEnemyArchetype.Ranged:
                    moveSpeed = 2.2f;
                    preferredRange = 4.8f;
                    attackWindup = 0.28f;
                    attackRecovery = 0.20f;
                    attackCooldown = 1.05f;
                    attackDamage = 4f;
                    hitStaggerSeconds = 0.17f;
                    break;
                default:
                    moveSpeed = 2.7f;
                    attackRange = 1.25f;
                    attackWindup = 0.22f;
                    attackRecovery = 0.20f;
                    attackCooldown = 0.80f;
                    attackDamage = 5f;
                    hitStaggerSeconds = 0.18f;
                    break;
            }
        }
    }
}
