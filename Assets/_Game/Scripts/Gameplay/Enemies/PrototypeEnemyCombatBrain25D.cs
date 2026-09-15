using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    /// <summary>
    /// 2.5D/XZ enemy brain for the migrated main prototype. Enemies only acquire the player
    /// inside a forward vision cone with line of sight, then chase/attack using 3D collisions.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatEntity))]
    public sealed class PrototypeEnemyCombatBrain25D : MonoBehaviour
    {
        [Header("Archetype")]
        [SerializeField] private PrototypeEnemyArchetype archetype = PrototypeEnemyArchetype.Melee;

        [Header("Vision")]
        [SerializeField, Min(0.5f)] private float viewDistance = 7f;
        [SerializeField, Range(10f, 170f)] private float viewAngle = 85f;
        [SerializeField, Min(0f)] private float loseSightDelay = 1.0f;
        [SerializeField] private Vector3 initialForward = Vector3.back;

        [Header("Combat")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.4f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.25f;
        [SerializeField, Min(0.1f)] private float preferredRange = 4.6f;
        [SerializeField, Min(0.1f)] private float attackDamage = 5f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.8f;

        private CharacterController _controller;
        private CombatEntity _entity;
        private CombatEntity _target;
        private Vector3 _forward;
        private float _lastVisibleAt = float.NegativeInfinity;
        private float _nextAttackAt;
        private float _nextSearchAt;

        public PrototypeEnemyArchetype Archetype => archetype;
        public Vector3 LogicForward => _forward.sqrMagnitude > 0.001f ? _forward.normalized : Vector3.back;
        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public bool IsAlerted { get; private set; }
        public bool IsMoving { get; private set; }
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
            ApplyArchetypeDefaults();
            ResetForward();
        }

        private void Update()
        {
            if (_entity?.Health == null || _entity.Health.IsDead)
                return;

            AcquirePlayerCandidate();
            if (_target == null || _target.Health == null || _target.Health.IsDead)
            {
                IsMoving = false;
                IsAlerted = false;
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
                _lastVisibleAt = Time.time;
            }
            else if (visible)
            {
                _lastVisibleAt = Time.time;
            }
            else if (Time.time - _lastVisibleAt > loseSightDelay)
            {
                IsAlerted = false;
                IsMoving = false;
                _target = null;
                return;
            }

            var delta = _target.transform.position - transform.position;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance > 0.04f)
            {
                _forward = delta / distance;
                UpdateFacingSign();
            }

            if (archetype == PrototypeEnemyArchetype.Ranged)
                TickRanged(distance);
            else
                TickMelee(distance);
        }

        private void TickMelee(float distance)
        {
            if (distance <= attackRange)
            {
                IsMoving = false;
                TryAttack();
                return;
            }

            Move(_forward);
        }

        private void TickRanged(float distance)
        {
            if (distance < preferredRange * 0.62f)
            {
                Move(-_forward);
                return;
            }
            if (distance > preferredRange * 1.15f)
            {
                Move(_forward);
                return;
            }

            IsMoving = false;
            TryAttack();
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
            _controller.Move(direction * (moveSpeed * Time.deltaTime));
            IsMoving = true;
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackAt || _target == null)
                return;

            _nextAttackAt = Time.time + attackCooldown;
            var delta = _target.transform.position - transform.position;
            delta.y = 0f;
            var maxRange = archetype == PrototypeEnemyArchetype.Ranged ? preferredRange * 1.45f : attackRange + 0.3f;
            if (delta.magnitude > maxRange)
                return;

            DamageSystem.Apply(new DamageContext(
                _entity,
                _entity,
                _target,
                attackDamage,
                DamageType.Physical,
                Vector2.zero,
                sourceId: "PrototypeEnemy25D_" + archetype));
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
                delta.y = 0f;
                var sqr = delta.sqrMagnitude;
                if (sqr > viewDistance * viewDistance || sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = candidate;
            }
            _target = best;
        }

        private bool CanSeeTarget()
        {
            if (_target == null)
                return false;

            var delta = _target.transform.position - transform.position;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance < 0.001f || distance > viewDistance)
                return false;

            var direction = delta / distance;
            if (Vector3.Angle(LogicForward, direction) > viewAngle * 0.5f)
                return false;

            var origin = transform.position + Vector3.up * 0.8f;
            var destination = _target.transform.position + Vector3.up * 0.8f;
            var ray = destination - origin;
            var hits = Physics.RaycastAll(origin, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;
                var entity = collider.GetComponentInParent<CombatEntity>();
                return entity == _target;
            }
            return true;
        }

        private void ApplyArchetypeDefaults()
        {
            switch (archetype)
            {
                case PrototypeEnemyArchetype.FastMelee:
                    moveSpeed = 3.5f;
                    attackRange = 1.05f;
                    attackDamage = 4f;
                    attackCooldown = 0.58f;
                    viewDistance = 7.5f;
                    viewAngle = 95f;
                    break;
                case PrototypeEnemyArchetype.Ranged:
                    moveSpeed = 2.0f;
                    preferredRange = 4.8f;
                    attackDamage = 4f;
                    attackCooldown = 1.05f;
                    viewDistance = 8.5f;
                    viewAngle = 75f;
                    break;
                default:
                    moveSpeed = 2.5f;
                    attackRange = 1.25f;
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
            var right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            var dot = Vector3.Dot(LogicForward, right);
            if (Mathf.Abs(dot) > 0.08f)
                FacingSign = dot >= 0f ? 1 : -1;
        }
    }
}
