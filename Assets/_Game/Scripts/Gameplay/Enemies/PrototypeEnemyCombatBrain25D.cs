using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Navigation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    /// <summary>
    /// 2.5D/XZ enemy brain for the migrated main prototype. Initial acquisition requires the
    /// player to be inside the forward cone with clear LOS. Once alerted, the enemy can remember
    /// the last seen position briefly and use the lightweight navigation graph to reach ramps and
    /// upper floors instead of walking straight into walls.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatEntity))]
    public sealed class PrototypeEnemyCombatBrain25D : MonoBehaviour
    {
        private const float FacingHorizontalDeadzone = 0.30f;

        [Header("Archetype")]
        [SerializeField] private PrototypeEnemyArchetype archetype = PrototypeEnemyArchetype.Melee;

        [Header("Vision")]
        [SerializeField, Min(0.5f)] private float viewDistance = 7f;
        [SerializeField, Range(10f, 170f)] private float viewAngle = 85f;
        [SerializeField, Min(0f)] private float loseSightDelay = 3.25f;
        [SerializeField, Range(0.35f, 1.2f)] private float visionProbeHeight = 0.62f;
        [SerializeField, Range(0.05f, 0.35f)] private float visionProbeRadius = 0.16f;
        [SerializeField] private Vector3 initialForward = Vector3.back;

        [Header("Navigation")]
        [SerializeField, Min(0.05f)] private float pathRefreshSeconds = 0.35f;
        [SerializeField, Min(0.1f)] private float waypointReachDistance = 0.42f;
        [SerializeField, Min(0.05f)] private float walkProbeRadius = 0.24f;
        [SerializeField, Min(0.1f)] private float walkProbeHeight = 0.55f;
        [SerializeField, Min(0.1f)] private float directWalkHeightTolerance = 0.70f;
        [SerializeField, Min(0.1f)] private float blockedEscapeDistance = 1.10f;
        [SerializeField, Min(0.05f)] private float blockedRecoveryCooldown = 0.18f;

        [Header("Combat")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.4f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.25f;
        [SerializeField, Min(0.1f)] private float preferredRange = 4.6f;
        [SerializeField, Min(0f)] private float attackWindup = 0.20f;
        [SerializeField, Min(0f)] private float attackRecovery = 0.18f;
        [SerializeField, Min(0.1f)] private float attackDamage = 5f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.8f;
        [SerializeField, Min(0.1f)] private float maxMeleeHeightDifference = 1.05f;
        [SerializeField, Min(0.1f)] private float maxRangedHeightDifference = 2.45f;
        [SerializeField] private float gravity = -24f;

        private readonly List<Vector3> _path = new(16);

        private CharacterController _controller;
        private CombatEntity _entity;
        private CombatEntity _target;
        private PrototypeNavigationGraph25D _navigation;
        private Vector3 _forward;
        private Vector3 _lastKnownTargetPosition;
        private Coroutine _attackRoutine;
        private int _pathIndex;
        private float _lastVisibleAt = float.NegativeInfinity;
        private float _nextAttackAt;
        private float _nextSearchAt;
        private float _nextPathRefreshAt;
        private float _verticalVelocity;
        private float _nextBlockedRecoveryAt;
        private bool _hasLastKnownTarget;

        public event Action<int> AttackStarted;

        public PrototypeEnemyArchetype Archetype => archetype;
        public Vector3 LogicForward => _forward.sqrMagnitude > 0.001f ? _forward.normalized : Vector3.back;
        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public bool IsAlerted { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsAttacking => _attackRoutine != null;
        public int FacingSign { get; private set; } = -1;

        public void Configure(PrototypeEnemyArchetype value, Vector3 forward)
        {
            archetype = value;
            initialForward = forward;
            ApplyArchetypeDefaults();
            ResetForward();
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _entity = GetComponent<CombatEntity>();
            _navigation = PrototypeNavigationGraph25D.Instance;
            ApplyArchetypeDefaults();
            ResetForward();
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
            if (_entity?.Health == null || _entity.Health.IsDead)
                return;

            ApplyGravity();
            AcquirePlayerCandidate();
            if (_target == null || _target.Health == null || _target.Health.IsDead)
            {
                ClearAlertState(clearTarget: true);
                return;
            }

            var visible = CanSeeTarget();
            if (!IsAlerted)
            {
                if (!visible)
                {
                    IsMoving = false;
                    return;
                }

                IsAlerted = true;
                RememberVisibleTarget();
            }
            else if (visible)
            {
                RememberVisibleTarget();
            }
            else if (Time.time - _lastVisibleAt > loseSightDelay)
            {
                ClearAlertState(clearTarget: true);
                return;
            }

            if (IsAttacking)
            {
                IsMoving = false;
                return;
            }

            var targetPosition = _target.transform.position;
            var planarDistance = PlanarDistance(transform.position, targetPosition);
            var heightDifference = Mathf.Abs(targetPosition.y - transform.position.y);

            if (visible)
            {
                FaceToward(targetPosition);

                if (archetype == PrototypeEnemyArchetype.Ranged)
                {
                    var retreatDistance = preferredRange * 0.62f;
                    if (heightDifference <= maxRangedHeightDifference && HasClearCombatLine())
                    {
                        if (planarDistance < retreatDistance)
                        {
                            var away = transform.position - targetPosition;
                            away.y = 0f;
                            Move(away);
                            return;
                        }

                        if (planarDistance <= preferredRange * 1.18f)
                        {
                            IsMoving = false;
                            TryAttack();
                            return;
                        }
                    }
                }
                else if (heightDifference <= maxMeleeHeightDifference &&
                         planarDistance <= attackRange &&
                         HasClearCombatLine())
                {
                    IsMoving = false;
                    TryAttack();
                    return;
                }
            }

            var chaseDestination = visible
                ? targetPosition
                : (_hasLastKnownTarget ? _lastKnownTargetPosition : targetPosition);
            var chasePoint = ResolveChasePoint(chaseDestination);
            FaceToward(chasePoint);
            Move(chasePoint - transform.position);
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackAt || _target == null || _attackRoutine != null)
                return;
            if (!CanAttackTargetNow())
                return;

            IsMoving = false;
            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            AttackStarted?.Invoke(FacingSign);

            if (attackWindup > 0f)
                yield return new WaitForSeconds(attackWindup);

            if (CanAttackTargetNow())
            {
                DamageSystem.Apply(new DamageContext(
                    _entity,
                    _entity,
                    _target,
                    attackDamage,
                    DamageType.Physical,
                    Vector2.zero,
                    sourceId: "PrototypeEnemy25D_" + archetype));
            }

            if (attackRecovery > 0f)
                yield return new WaitForSeconds(attackRecovery);

            _attackRoutine = null;
            _nextAttackAt = Time.time + attackCooldown;
        }

        private bool CanAttackTargetNow()
        {
            if (_target == null || _target.Health == null || _target.Health.IsDead)
                return false;

            var targetPosition = _target.transform.position;
            var planarDistance = PlanarDistance(transform.position, targetPosition);
            var heightDifference = Mathf.Abs(targetPosition.y - transform.position.y);
            var maxRange = archetype == PrototypeEnemyArchetype.Ranged
                ? preferredRange * 1.45f
                : attackRange + 0.30f;
            var maxHeight = archetype == PrototypeEnemyArchetype.Ranged
                ? maxRangedHeightDifference
                : maxMeleeHeightDifference;

            return planarDistance <= maxRange &&
                   heightDifference <= maxHeight &&
                   HasClearCombatLine();
        }

        private void AcquirePlayerCandidate()
        {
            if (_target != null && _target.Health != null && !_target.Health.IsDead)
                return;
            if (Time.time < _nextSearchAt)
                return;

            _nextSearchAt = Time.time + 0.25f;
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            var bestSqr = float.PositiveInfinity;
            CombatEntity best = null;
            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (candidate == null || candidate.Team != Team.Player || candidate.Health == null || candidate.Health.IsDead)
                    continue;
                var delta = candidate.transform.position - transform.position;
                var planarSqr = delta.x * delta.x + delta.z * delta.z;
                if (planarSqr > viewDistance * viewDistance || planarSqr >= bestSqr)
                    continue;
                bestSqr = planarSqr;
                best = candidate;
            }
            _target = best;
        }

        private bool CanSeeTarget()
        {
            if (_target == null)
                return false;

            var planarDelta = _target.transform.position - transform.position;
            planarDelta.y = 0f;
            var planarDistance = planarDelta.magnitude;
            if (planarDistance < 0.001f || planarDistance > viewDistance)
                return false;

            var planarDirection = planarDelta / planarDistance;
            if (Vector3.Angle(LogicForward, planarDirection) > viewAngle * 0.5f)
                return false;

            return HasClearLineTo(_target.transform.position, _target);
        }

        private bool HasClearCombatLine()
        {
            return _target != null && HasClearLineTo(_target.transform.position, _target);
        }

        private bool HasClearLineTo(Vector3 targetPosition, CombatEntity expectedTarget)
        {
            var origin = transform.position + Vector3.up * visionProbeHeight;
            var destination = targetPosition + Vector3.up * visionProbeHeight;
            var cast = destination - origin;
            var castDistance = cast.magnitude;
            if (castDistance < 0.001f)
                return true;

            var hits = Physics.SphereCastAll(
                origin,
                visionProbeRadius,
                cast / castDistance,
                castDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;

                var hitEntity = collider.GetComponentInParent<CombatEntity>();
                if (hitEntity != null)
                {
                    if (expectedTarget != null && hitEntity == expectedTarget)
                        return true;
                    continue;
                }

                return false;
            }

            return true;
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
                _navigation = PrototypeNavigationGraph25D.Instance ??
                              FindFirstObjectByType<PrototypeNavigationGraph25D>();
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
                var planar = PlanarDistance(transform.position, waypoint);
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

        private void RememberVisibleTarget()
        {
            _lastVisibleAt = Time.time;
            _lastKnownTargetPosition = _target.transform.position;
            _hasLastKnownTarget = true;
        }

        private void ClearAlertState(bool clearTarget)
        {
            IsAlerted = false;
            IsMoving = false;
            _hasLastKnownTarget = false;
            _path.Clear();
            _pathIndex = 0;
            if (clearTarget)
                _target = null;
        }

        private void FaceToward(Vector3 position)
        {
            var direction = position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;
            _forward = direction.normalized;
            UpdateFacingSign();
        }

        private void Move(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                IsMoving = false;
                return;
            }
            direction.Normalize();
            var before = transform.position;
            var requestedDistance = moveSpeed * Time.deltaTime;
            var flags = _controller.Move(direction * requestedDistance);
            var movedDistance = PlanarDistance(before, transform.position);

            if ((flags & CollisionFlags.Sides) != 0 && movedDistance < requestedDistance * 0.35f)
            {
                _path.Clear();
                _pathIndex = 0;
                _nextPathRefreshAt = 0f;
                if (Time.time >= _nextBlockedRecoveryAt)
                {
                    _nextBlockedRecoveryAt = Time.time + blockedRecoveryCooldown;
                    var escape = FindOpenEscapeDirection(direction);
                    if (escape.sqrMagnitude > 0.001f)
                        _controller.Move(escape * (requestedDistance * 1.35f));
                }
            }
            IsMoving = true;
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

        private void ApplyGravity()
        {
            if (_controller == null)
                return;

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));
        }

        private void ApplyArchetypeDefaults()
        {
            switch (archetype)
            {
                case PrototypeEnemyArchetype.FastMelee:
                    moveSpeed = 3.5f;
                    attackRange = 1.05f;
                    attackWindup = 0.12f;
                    attackRecovery = 0.12f;
                    attackDamage = 4f;
                    attackCooldown = 0.58f;
                    viewDistance = 7.5f;
                    viewAngle = 95f;
                    break;
                case PrototypeEnemyArchetype.Ranged:
                    moveSpeed = 2.0f;
                    preferredRange = 4.8f;
                    attackWindup = 0.32f;
                    attackRecovery = 0.20f;
                    attackDamage = 4f;
                    attackCooldown = 1.05f;
                    viewDistance = 8.5f;
                    viewAngle = 75f;
                    break;
                default:
                    moveSpeed = 2.5f;
                    attackRange = 1.25f;
                    attackWindup = 0.20f;
                    attackRecovery = 0.18f;
                    attackDamage = 5f;
                    attackCooldown = 0.82f;
                    viewDistance = 7f;
                    viewAngle = 85f;
                    break;
            }
        }

        private void ResetForward()
        {
            _forward = initialForward;
            _forward.y = 0f;
            if (_forward.sqrMagnitude < 0.001f)
                _forward = Vector3.back;
            _forward.Normalize();
            UpdateFacingSign();
        }

        private void UpdateFacingSign()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            var right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up);
            if (right.sqrMagnitude < 0.001f)
                return;
            right.Normalize();

            var dot = Vector3.Dot(LogicForward, right);
            if (Mathf.Abs(dot) >= FacingHorizontalDeadzone)
                FacingSign = dot >= 0f ? 1 : -1;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
