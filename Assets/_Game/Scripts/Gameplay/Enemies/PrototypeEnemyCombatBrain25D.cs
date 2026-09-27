using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Navigation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    /// <summary>
    /// 2.5D/XZ enemy brain for the migrated main prototype. Idle enemies patrol a small local
    /// guard area instead of globally hunting the player. Initial acquisition requires the player
    /// to enter the forward cone with clear LOS, while taking damage immediately alerts the enemy.
    /// Once alerted, the enemy remembers the last seen position briefly and uses the lightweight
    /// navigation graph to reach ramps and upper floors instead of walking straight into walls.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatEntity))]
    public sealed class PrototypeEnemyCombatBrain25D : MonoBehaviour, ICombatActionInterruptHandler
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

        [Header("Patrol")]
        [SerializeField] private bool patrolEnabled = true;
        [SerializeField, Min(0.5f)] private float patrolRadius = 3.4f;
        [SerializeField, Min(0.1f)] private float patrolPauseSeconds = 1.10f;
        [SerializeField, Range(0.2f, 1f)] private float patrolSpeedMultiplier = 0.58f;

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
        private Vector3 _homePosition;
        private Vector3 _patrolDestination;
        private Coroutine _attackRoutine;
        private int _pathIndex;
        private float _lastVisibleAt = float.NegativeInfinity;
        private float _nextAttackAt;
        private float _nextSearchAt;
        private float _nextPathRefreshAt;
        private float _verticalVelocity;
        private float _nextBlockedRecoveryAt;
        private float _nextPatrolAt;
        private int _patrolStep;
        private bool _hasLastKnownTarget;
        private bool _hasPatrolDestination;
        private bool _homeInitialized;

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

        public void ApplyThreatProfile(EnemyRank rank)
        {
            switch (rank)
            {
                case EnemyRank.Elite:
                    viewDistance *= 1.20f;
                    viewAngle = Mathf.Min(170f, viewAngle + 12f);
                    loseSightDelay = Mathf.Max(loseSightDelay, 5.5f);
                    patrolRadius *= 1.10f;
                    break;

                case EnemyRank.Boss:
                    viewDistance *= 1.65f;
                    viewAngle = Mathf.Max(viewAngle, 130f);
                    loseSightDelay = Mathf.Max(loseSightDelay, 9f);
                    patrolEnabled = false;
                    break;
            }
        }

        public void ApplyOfficialCombatStats(
            float officialAttack,
            float officialAttackInterval,
            float officialMoveSpeed,
            float officialAttackRadius)
        {
            attackDamage = Mathf.Max(0.1f, officialAttack);
            attackCooldown = Mathf.Max(0.1f, officialAttackInterval);
            moveSpeed = Mathf.Max(0.1f, officialMoveSpeed);

            if (archetype == PrototypeEnemyArchetype.Ranged && officialAttackRadius > 0f)
            {
                preferredRange = officialAttackRadius;
                attackRange = officialAttackRadius;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _entity = GetComponent<CombatEntity>();
            _navigation = PrototypeNavigationGraph25D.Instance;
            ApplyArchetypeDefaults();
            InitializePrototypeMitigation();
            ResetForward();

            // Legacy room/debug spawners can instantiate enemy templates without going through
            // RogueliteStageRuntimeController. Give those enemies the normal-rank baseline too.
            if (GetComponent<EnemyThreatProfile25D>() == null)
                gameObject.AddComponent<EnemyThreatProfile25D>().Configure(EnemyRank.Normal);
        }

        private void OnEnable()
        {
            _homePosition = transform.position;
            _homeInitialized = true;
            _hasPatrolDestination = false;
            _nextPatrolAt = Time.time + 0.35f;
            DamageSystem.DamageApplied += OnDamageApplied;
        }

        private void OnDisable()
        {
            DamageSystem.DamageApplied -= OnDamageApplied;
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }
            _path.Clear();
            _pathIndex = 0;
            IsMoving = false;
            _nextBlockedRecoveryAt = 0f;
            _hasPatrolDestination = false;
        }

        private void Update()
        {
            if (_entity?.Health == null || _entity.Health.IsDead)
                return;

            ApplyGravity();
            AcquirePlayerCandidate();
            if (_target == null || _target.Health == null || _target.Health.IsDead)
            {
                if (IsAlerted)
                    ClearAlertState(clearTarget: true);
                UpdatePatrol();
                return;
            }

            var visible = CanSeeTarget();
            if (!IsAlerted)
            {
                if (!visible)
                {
                    UpdatePatrol();
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
                UpdatePatrol();
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
                    sourceId: "PrototypeEnemy25D_" + archetype, tags: DamageTags.BasicAttack));
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

        private void OnDamageApplied(DamageContext context, DamageResult result)
        {
            if (_entity == null || !result.Applied || context.Target != _entity)
                return;

            var attacker = context.Owner != null && context.Owner.Team == Team.Player
                ? context.Owner
                : context.Source;
            if (attacker == null || attacker.Team != Team.Player || attacker.Health == null || attacker.Health.IsDead)
                return;

            _target = attacker;
            IsAlerted = true;
            _hasPatrolDestination = false;
            _lastVisibleAt = Time.time;
            _lastKnownTargetPosition = attacker.transform.position;
            _hasLastKnownTarget = true;
            _path.Clear();
            _pathIndex = 0;
        }

        private void UpdatePatrol()
        {
            if (!patrolEnabled || _controller == null)
            {
                IsMoving = false;
                return;
            }

            if (!_homeInitialized)
            {
                _homePosition = transform.position;
                _homeInitialized = true;
            }

            if (PlanarDistance(transform.position, _homePosition) > patrolRadius * 1.45f)
            {
                _patrolDestination = _homePosition;
                _hasPatrolDestination = true;
            }

            if (_hasPatrolDestination)
            {
                if (PlanarDistance(transform.position, _patrolDestination) <= 0.48f)
                {
                    _hasPatrolDestination = false;
                    _nextPatrolAt = Time.time + patrolPauseSeconds;
                    IsMoving = false;
                    return;
                }

                var point = ResolveChasePoint(_patrolDestination);
                FaceToward(point);
                Move(point - transform.position, patrolSpeedMultiplier);
                return;
            }

            if (Time.time < _nextPatrolAt)
            {
                IsMoving = false;
                return;
            }

            if (TryPickPatrolDestination(out var destination))
            {
                _patrolDestination = destination;
                _hasPatrolDestination = true;
                _patrolStep++;
            }
            else
            {
                _nextPatrolAt = Time.time + patrolPauseSeconds;
                IsMoving = false;
            }
        }

        private bool TryPickPatrolDestination(out Vector3 destination)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var angle = (_patrolStep * 137.5f + attempt * 45f) * Mathf.Deg2Rad;
                var radiusScale = 0.55f + 0.15f * (attempt % 4);
                var candidate = _homePosition + new Vector3(
                    Mathf.Cos(angle) * patrolRadius * radiusScale,
                    0f,
                    Mathf.Sin(angle) * patrolRadius * radiusScale);
                candidate.y = transform.position.y;

                if (!HasDirectWalkPath(candidate))
                    continue;

                destination = candidate;
                return true;
            }

            destination = transform.position;
            return false;
        }

        private void RememberVisibleTarget()
        {
            _lastVisibleAt = Time.time;
            _lastKnownTargetPosition = _target.transform.position;
            _hasLastKnownTarget = true;
            _hasPatrolDestination = false;
        }

        private void ClearAlertState(bool clearTarget)
        {
            IsAlerted = false;
            IsMoving = false;
            _hasLastKnownTarget = false;
            _path.Clear();
            _pathIndex = 0;
            _hasPatrolDestination = false;
            _nextPatrolAt = Time.time + 0.45f;
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

        private void Move(Vector3 direction, float speedScale = 1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                IsMoving = false;
                return;
            }
            direction.Normalize();
            var before = transform.position;
            var moveMultiplier = _entity?.Stats != null ? _entity.Stats.MoveSpeedMultiplier : 1f;
            var requestedDistance = moveSpeed * moveMultiplier * Mathf.Max(0f, speedScale) * Time.deltaTime;
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

        public void InterruptCombatActions(CombatActionMask actions)
        {
            if ((actions & CombatActionMask.BasicAttack) != 0 && _attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
                _nextAttackAt = Mathf.Max(_nextAttackAt, Time.time + attackCooldown);
            }

            if ((actions & CombatActionMask.Movement) != 0)
                IsMoving = false;
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
                    attackDamage = 360f;
                    attackCooldown = 0.58f;
                    viewDistance = 7.5f;
                    viewAngle = 95f;
                    break;
                case PrototypeEnemyArchetype.Ranged:
                    moveSpeed = 2.0f;
                    preferredRange = 4.8f;
                    attackWindup = 0.32f;
                    attackRecovery = 0.20f;
                    attackDamage = 380f;
                    attackCooldown = 1.05f;
                    viewDistance = 8.5f;
                    viewAngle = 75f;
                    break;
                default:
                    moveSpeed = 2.5f;
                    attackRange = 1.25f;
                    attackWindup = 0.20f;
                    attackRecovery = 0.18f;
                    attackDamage = 400f;
                    attackCooldown = 0.82f;
                    viewDistance = 7f;
                    viewAngle = 85f;
                    break;
            }
        }

        private void InitializePrototypeMitigation()
        {
            if (_entity == null)
                return;

            var stats = _entity.Stats;
            if (stats == null ||
                stats.BasePhysicalDefense > 0.0001f ||
                Mathf.Abs(stats.BaseArtsResistance) > 0.0001f)
                return;

            if (_entity.Health != null && _entity.Health.MaxHealth >= 150f)
            {
                stats.SetBasePhysicalDefense(4f);
                stats.SetBaseArtsResistance(10f);
                return;
            }

            stats.SetBasePhysicalDefense(archetype switch
            {
                PrototypeEnemyArchetype.FastMelee => 0.5f,
                PrototypeEnemyArchetype.Ranged => 0.75f,
                _ => 1f
            });
            stats.SetBaseArtsResistance(archetype == PrototypeEnemyArchetype.Ranged ? 5f : 0f);
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
