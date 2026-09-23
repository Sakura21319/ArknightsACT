using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Prototype25D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatEntity))]
    public sealed class Prototype25DEnemyBrain : MonoBehaviour
    {
        [Header("Vision")]
        [SerializeField] private CombatEntity target;
        [SerializeField] private Vector3 logicForward = Vector3.forward;
        [SerializeField, Min(0.5f)] private float viewDistance = 6f;
        [SerializeField, Range(10f, 170f)] private float viewAngle = 80f;
        [SerializeField, Min(0f)] private float loseSightDelay = 0.8f;

        [Header("Combat")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.4f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.15f;
        [SerializeField, Min(0.1f)] private float attackDamage = 7f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.95f;

        private CharacterController _controller;
        private CombatEntity _entity;
        private float _lastVisibleTime = float.NegativeInfinity;
        private float _nextAttackTime;
        private bool _alerted;

        public Vector3 LogicForward => logicForward;
        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public bool IsAlerted => _alerted;

        public void Configure(CombatEntity targetEntity, Vector3 initialForward, float distance = 6f, float angle = 80f)
        {
            target = targetEntity;
            initialForward.y = 0f;
            if (initialForward.sqrMagnitude > 0.001f)
                logicForward = initialForward.normalized;
            viewDistance = Mathf.Max(0.5f, distance);
            viewAngle = Mathf.Clamp(angle, 10f, 170f);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _entity = GetComponent<CombatEntity>();
        }

        private void OnEnable()
        {
            if (_entity != null && _entity.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_entity != null && _entity.Health != null)
                _entity.Health.Died -= OnDied;
        }

        private void Update()
        {
            if (_entity == null || _entity.Health == null || _entity.Health.IsDead)
                return;
            if (target == null || target.Health == null || target.Health.IsDead)
                return;

            var visible = CanSeeTarget();
            if (!_alerted)
            {
                if (!visible)
                    return;
                _alerted = true;
                _lastVisibleTime = Time.time;
            }
            else if (visible)
            {
                _lastVisibleTime = Time.time;
            }
            else if (Time.time - _lastVisibleTime > loseSightDelay)
            {
                _alerted = false;
                return;
            }

            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance > 0.05f)
                logicForward = delta / distance;

            if (distance > attackRange)
            {
                if (!CombatActionUtility.IsBlocked(_entity, CombatActionMask.Movement))
                {
                    var moveMultiplier = _entity?.Stats != null ? _entity.Stats.MoveSpeedMultiplier : 1f;
                    _controller.Move(logicForward * moveSpeed * moveMultiplier * Time.deltaTime);
                }
                return;
            }

            if (Time.time >= _nextAttackTime &&
                !CombatActionUtility.IsBlocked(_entity, CombatActionMask.BasicAttack))
                AttackTarget();
        }

        private bool CanSeeTarget()
        {
            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance > viewDistance || distance < 0.001f)
                return false;
            if (Vector3.Angle(logicForward, delta / distance) > viewAngle * 0.5f)
                return false;

            var origin = transform.position + Vector3.up * 0.75f;
            var targetPoint = target.transform.position + Vector3.up * 0.75f;
            var ray = targetPoint - origin;
            var hits = Physics.RaycastAll(origin, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;
                var entity = hit.GetComponentInParent<CombatEntity>();
                return entity == target;
            }
            return true;
        }

        private void AttackTarget()
        {
            var attackSpeed = _entity?.Stats != null ? _entity.Stats.AttackSpeedMultiplier : 1f;
            _nextAttackTime = Time.time + attackCooldown / Mathf.Max(0.05f, attackSpeed);
            DamageSystem.Apply(new DamageContext(
                _entity,
                _entity,
                target,
                attackDamage,
                DamageType.Physical,
                Vector2.zero,
                sourceId: "Prototype25D_EnemyBasic", tags: DamageTags.BasicAttack));
        }

        private void OnDied()
        {
            _alerted = false;
            if (_controller != null)
                _controller.enabled = false;
            Destroy(gameObject, 0.12f);
        }
    }
}
