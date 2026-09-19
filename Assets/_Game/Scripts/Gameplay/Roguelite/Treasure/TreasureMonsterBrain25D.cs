using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Navigation;
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

        [Header("Navigation")]
        [SerializeField, Min(0.05f)] private float pathRefreshSeconds = 0.35f;
        [SerializeField, Min(0.1f)] private float waypointReachDistance = 0.42f;
        [SerializeField, Min(0.05f)] private float walkProbeRadius = 0.22f;
        [SerializeField, Min(0.1f)] private float walkProbeHeight = 0.50f;
        [SerializeField, Min(0.1f)] private float directWalkHeightTolerance = 0.70f;
        [SerializeField, Min(0.1f)] private float blockedEscapeDistance = 1.10f;
        [SerializeField, Min(0.05f)] private float blockedRecoveryCooldown = 0.18f;

        private readonly List<Vector3> _path = new(16);

        private CharacterController _controller;
        private CombatEntity _entity;
        private CombatEntity _target;
        private PrototypeNavigationGraph25D _navigation;
        private Coroutine _attackRoutine;
        private float _nextAttackAt;
        private float _nextPathRefreshAt;
        private float _verticalVelocity;
        private Vector3 _forward = Vector3.back;
        private int _pathIndex;
        private float _nextBlockedRecoveryAt;

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
            _navigation = PrototypeNavigationGraph25D.Instance;
        }

        public void Activate(CombatEntity target)
        {
            if (target == null || target.Health == null || target.Health.IsDead)
                return;

            _target = target;
            if (IsActivated)
                return;

            IsActivated = true;
            _path.Clear();
            _pathIndex = 0;
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
            _path.Clear();
            _pathIndex = 0;
            IsMoving = false;
            _nextBlockedRecoveryAt = 0f;
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

            var targetPosition = _target.transform.position;
            var delta = targetPosition - transform.position;
            var height = Mathf.Abs(delta.y);
            delta.y = 0f;
            var distance = delta.magnitude;

            if (height <= 1.1f && distance <= attackRange && HasClearLine())
            {
                FaceToward(targetPosition);
                IsMoving = false;
                TryAttack();
                return;
            }

            var chasePoint = ResolveChasePoint(targetPosition);
            var move = chasePoint - transform.position;
            move.y = 0f;
            if (move.sqrMagnitude > 0.0025f)
            {
                FaceToward(chasePoint);
                MoveSafely(move.normalized);
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

        private Vector3 ResolveChasePoint(Vector3 destination)
        {
            if (HasDirectWalkPath(destination))
            {
                _path.Clear();
                _pathIndex = 0;
                return destination;
            }

            if (_navigation == null)
                _navigation = PrototypeNavigationGraph25D.Instance ?? FindFirstObjectByType<PrototypeNavigationGraph25D>();
            if (_navigation == null)
                return ResolveBlockedChasePoint(destination);

            if (_path.Count == 0 || _pathIndex >= _path.Count || Time.time >= _nextPathRefreshAt)
            {
                _nextPathRefreshAt = Time.time + pathRefreshSeconds;
                if (_navigation.TryBuildPath(transform.position, destination, _path))
                    _pathIndex = 0;
                else
                {
                    _path.Clear();
                    _pathIndex = 0;
                }
            }

            AdvanceReachedWaypoints();
            if (_pathIndex >= _path.Count)
                return HasDirectWalkPath(destination) ? destination : ResolveBlockedChasePoint(destination);

            var waypoint = _path[_pathIndex];
            return HasDirectWalkPath(waypoint)
                ? waypoint
                : ResolveBlockedChasePoint(destination);
        }

        private void AdvanceReachedWaypoints()
        {
            while (_pathIndex < _path.Count)
            {
                var waypoint = _path[_pathIndex];
                var dx = waypoint.x - transform.position.x;
                var dz = waypoint.z - transform.position.z;
                var planar = Mathf.Sqrt(dx * dx + dz * dz);
                var vertical = Mathf.Abs(transform.position.y - waypoint.y);
                if (planar > waypointReachDistance || vertical > 0.72f)
                    break;
                _pathIndex++;
            }
        }

        private bool HasDirectWalkPath(Vector3 destination)
        {
            if (Mathf.Abs(destination.y - transform.position.y) > directWalkHeightTolerance)
                return false;

            var planar = destination - transform.position;
            planar.y = 0f;
            var distance = planar.magnitude;
            if (distance <= 0.10f)
                return true;

            var origin = transform.position + Vector3.up * walkProbeHeight;
            var hits = Physics.SphereCastAll(
                origin,
                walkProbeRadius,
                planar / distance,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;
                if (collider.GetComponentInParent<CombatEntity>() != null)
                    continue;
                return false;
            }
            return true;
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

        private void MoveSafely(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;
            direction.Normalize();

            var before = transform.position;
            var requestedDistance = moveSpeed * Time.deltaTime;
            var flags = _controller.Move(direction * requestedDistance);
            var movedDistance = PlanarDistance(before, transform.position);
            if ((flags & CollisionFlags.Sides) == 0 || movedDistance >= requestedDistance * 0.35f)
                return;

            _path.Clear();
            _pathIndex = 0;
            _nextPathRefreshAt = 0f;
            if (Time.time < _nextBlockedRecoveryAt)
                return;

            _nextBlockedRecoveryAt = Time.time + blockedRecoveryCooldown;
            var escape = FindOpenEscapeDirection(direction);
            if (escape.sqrMagnitude > 0.001f)
                _controller.Move(escape * (requestedDistance * 1.35f));
        }

        private Vector3 ResolveBlockedChasePoint(Vector3 destination)
        {
            var direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return transform.position;

            var escape = FindOpenEscapeDirection(direction.normalized);
            return escape.sqrMagnitude > 0.001f
                ? transform.position + escape * blockedEscapeDistance
                : transform.position;
        }

        private Vector3 FindOpenEscapeDirection(Vector3 blockedDirection)
        {
            blockedDirection.y = 0f;
            if (blockedDirection.sqrMagnitude < 0.001f)
                return Vector3.zero;
            blockedDirection.Normalize();

            var side = Vector3.Cross(Vector3.up, blockedDirection).normalized;
            var candidates = new[] { side, -side, -blockedDirection };
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (HasDirectWalkPath(transform.position + candidate * blockedEscapeDistance))
                    return candidate;
            }
            return Vector3.zero;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
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
