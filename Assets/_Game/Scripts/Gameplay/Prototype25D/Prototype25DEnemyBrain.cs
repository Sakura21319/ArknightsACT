using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Prototype25D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(CombatEntity))]
    public sealed class Prototype25DEnemyBrain : MonoBehaviour
    {
        [Header("Vision")]
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
        private CombatEntity _target;
        private Vector3 _forward = Vector3.forward;
        private float _lastVisibleTime = float.NegativeInfinity;
        private float _nextAttackTime;
        private bool _alerted;

        public Vector3 LogicForward => _forward;
        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public bool IsAlerted => _alerted;

        public void Configure(CombatEntity target, Vector3 initialForward, float distance = 6f, float angle = 80f)
        {
            _target = target;
            initialForward.y = 0f;
            if (initialForward.sqrMagnitude > 0.001f)
                _forward = initialForward.normalized;
            viewDistance = Mathf.Max(0.5f, distance);
            viewAngle = Mathf.Clamp(angle, 10f, 170f);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _entity = GetComponent<CombatEntity>();
        }

        private void Update()
        {
            if (_entity == null || _entity.Health == null || _entity.Health.IsDead)
                return;
            if (_target == null || _target.Health == null || _target.Health.IsDead)
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

            var delta = _target.transform.position - transform.position;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance > 0.05f)
                _forward = delta / distance;

            if (distance > attackRange)
            {
                _controller.Move(_forward * moveSpeed * Time.deltaTime);
                return;
            }

            if (Time.time >= _nextAttackTime)
                AttackTarget();
        }

        private bool CanSeeTarget()
        {
            var delta = _target.transform.position - transform.position;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance > viewDistance || distance < 0.001f)
                return false;
            if (Vector3.Angle(_forward, delta / distance) > viewAngle * 0.5f)
                return false;

            var origin = transform.position + Vector3.up * 0.75f;
            var targetPoint = _target.transform.position + Vector3.up * 0.75f;
            var ray = targetPoint - origin;
            var hits = Physics.RaycastAll(origin, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;
                var entity = hit.GetComponentInParent<CombatEntity>();
                return entity == _target;
            }
            return true;
        }

        private void AttackTarget()
        {
            _nextAttackTime = Time.time + attackCooldown;
            DamageSystem.Apply(new DamageContext(
                _entity,
                _entity,
                _target,
                attackDamage,
                DamageType.Physical,
                Vector2.zero,
                sourceId: "Prototype25D_EnemyBasic"));
        }
    }
}
