using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    [RequireComponent(typeof(CombatEntity), typeof(TopDownPlayerMotor2D))]
    public sealed class TopDownTexasMeleeController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackRange = 1.65f;
        [SerializeField, Range(20f, 180f)] private float attackArcDegrees = 105f;
        [SerializeField, Min(0f)] private float baseDamage = 14f;
        [SerializeField, Min(0.05f)] private float attackInterval = 0.38f;
        [SerializeField, Min(0f)] private float impactDelay = 0.10f;
        [SerializeField, Min(0f)] private float aimAssistRadius = 4.8f;
        [SerializeField, Range(0f, 90f)] private float mouseAssistAngle = 24f;

        private CombatEntity _entity;
        private TopDownPlayerMotor2D _motor;
        private ITopDownInputSource _input;
        private TopDownCombatFx2D _fx;
        private Coroutine _attackRoutine;
        private float _readyAt;
        private Vector2 _lastAim = Vector2.right;
        private Vector2 _currentAttackDirection = Vector2.right;

        public bool IsAttacking => _attackRoutine != null;
        public Vector2 CurrentAttackDirection => _currentAttackDirection;

        public event Action<Vector2> AttackStarted;
        public event Action<CombatEntity> AttackHit;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _motor = GetComponent<TopDownPlayerMotor2D>();
            _input = GetComponent<ITopDownInputSource>();
            _fx = GetComponent<TopDownCombatFx2D>();
        }

        private void Update()
        {
            if (_entity == null || _entity.Health == null || _entity.Health.IsDead || _input == null)
                return;

            if ((_input.AttackPressedThisFrame || _input.AttackHeld) && Time.time >= _readyAt && _attackRoutine == null)
            {
                var direction = ResolveAimDirection();
                _attackRoutine = StartCoroutine(AttackRoutine(direction));
            }
        }

        private IEnumerator AttackRoutine(Vector2 direction)
        {
            _currentAttackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : _motor.FacingDirection;
            if (_currentAttackDirection.sqrMagnitude <= 0.001f)
                _currentAttackDirection = Vector2.right;

            _lastAim = _currentAttackDirection;
            _motor.SetFacing(_currentAttackDirection);
            _readyAt = Time.time + attackInterval;
            AttackStarted?.Invoke(_currentAttackDirection);

            if (impactDelay > 0f)
                yield return new WaitForSeconds(impactDelay);

            if (_entity != null && _entity.Health != null && !_entity.Health.IsDead)
                ResolveHit(_currentAttackDirection);

            var recovery = Mathf.Max(0.01f, attackInterval - impactDelay);
            yield return new WaitForSeconds(recovery);
            _attackRoutine = null;
        }

        private void ResolveHit(Vector2 direction)
        {
            var origin = (Vector2)transform.position;
            var colliders = Physics2D.OverlapCircleAll(origin, attackRange);
            var processed = new HashSet<CombatEntity>();
            var hitAny = false;

            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                    continue;

                var target = collider.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team ||
                    target.Health == null || target.Health.IsDead || !processed.Add(target))
                    continue;

                var delta = (Vector2)target.transform.position - origin;
                if (delta.sqrMagnitude <= 0.0001f)
                    continue;

                var angle = Vector2.Angle(direction, delta.normalized);
                if (angle > attackArcDegrees * 0.5f)
                    continue;

                var knockback = direction.normalized * 1.7f;
                var context = new DamageContext(
                    _entity,
                    _entity,
                    target,
                    baseDamage,
                    DamageType.Physical,
                    knockback,
                    sourceId: "TopDown_Texas_Melee");
                var result = DamageSystem.Apply(context);
                if (!result.Applied)
                    continue;

                hitAny = true;
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
                AttackHit?.Invoke(target);
            }

            _fx?.PlaySlash(origin, direction, attackRange, attackArcDegrees);
            if (!hitAny)
                return;

            HitStopService.Instance?.Request(0.025f);
            CameraShake2D.Instance?.Shake(0.055f, 0.07f);
        }

        private Vector2 ResolveAimDirection()
        {
            var stick = _input.AimStick;
            if (stick.sqrMagnitude > 0.16f)
                return AssistAim(stick.normalized, mouseAssistAngle);

            if (_input.HasPointer && Camera.main != null)
            {
                var screen = _input.PointerScreenPosition;
                var world = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -Camera.main.transform.position.z));
                var pointerDirection = (Vector2)world - (Vector2)transform.position;
                if (pointerDirection.sqrMagnitude > 0.01f)
                    return AssistAim(pointerDirection.normalized, mouseAssistAngle);
            }

            var nearest = FindNearestEnemyDirection(aimAssistRadius);
            if (nearest.HasValue)
                return nearest.Value;

            var move = _input.Move;
            if (move.sqrMagnitude > 0.01f)
                return move.normalized;
            return _lastAim;
        }

        private Vector2 AssistAim(Vector2 requested, float maxSnapAngle)
        {
            var nearest = FindNearestEnemyDirection(aimAssistRadius);
            if (!nearest.HasValue)
                return requested;
            return Vector2.Angle(requested, nearest.Value) <= maxSnapAngle ? nearest.Value : requested;
        }

        private Vector2? FindNearestEnemyDirection(float radius)
        {
            var origin = (Vector2)transform.position;
            var colliders = Physics2D.OverlapCircleAll(origin, radius);
            var bestSqr = float.PositiveInfinity;
            Vector2? best = null;
            var seen = new HashSet<CombatEntity>();

            for (var i = 0; i < colliders.Length; i++)
            {
                var target = colliders[i]?.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team ||
                    target.Health == null || target.Health.IsDead || !seen.Add(target))
                    continue;

                var delta = (Vector2)target.transform.position - origin;
                var sqr = delta.sqrMagnitude;
                if (sqr >= bestSqr || sqr <= 0.0001f)
                    continue;
                bestSqr = sqr;
                best = delta.normalized;
            }

            return best;
        }

        private void OnDisable()
        {
            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }
}
