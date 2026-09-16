using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Feedback;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class ChenSkill2 : MonoBehaviour, IPlayerSkill, IPlayerInvulnerabilitySource
    {
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 14f;
        [SerializeField, Min(0f)] private float startupSeconds = 0.28f;
        [SerializeField, Min(1)] private int strikeCount = 8;
        [SerializeField, Min(0.02f)] private float strikeInterval = 0.12f;
        [SerializeField, Min(0f)] private float strikeDamage = 11f;
        [SerializeField, Min(0f)] private float finalDamage = 30f;
        [SerializeField, Min(0.5f)] private float targetingRadius = 5.5f;
        [SerializeField, Min(0.1f)] private float castLockSeconds = 2.1f;
        [SerializeField, Min(0.1f)] private float max25DHeightDifference = 1.35f;

        private CombatEntity _entity;
        private PlayerMotor25D _motor25D;
        private float _readyAt;

        public int Slot => 2;
        public string DisplayName => "赤霄·绝影";
        public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);
        public bool IsCasting { get; private set; }
        public bool IsInvulnerable => IsCasting;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _motor25D = GetComponent<PlayerMotor25D>();
        }

        public bool TryCast()
        {
            if (IsCasting || Time.time < _readyAt || _entity?.Health == null || _entity.Health.IsDead)
                return false;

            _readyAt = Time.time + cooldownSeconds;
            StartCoroutine(CastRoutine());
            return true;
        }

        public void ReduceCooldown(float seconds)
        {
            if (seconds > 0f)
                _readyAt = Mathf.Max(Time.time, _readyAt - seconds);
        }

        private IEnumerator CastRoutine()
        {
            IsCasting = true;
            var startedAt = Time.time;

            if (startupSeconds > 0f)
                yield return new WaitForSeconds(startupSeconds);

            for (var i = 0; i < strikeCount && _entity?.Health != null && !_entity.Health.IsDead; i++)
            {
                var target = FindNearestTarget();
                if (target != null)
                {
                    var damage = i == strikeCount - 1 ? finalDamage : strikeDamage;
                    var knockback = Vector2.zero;
                    if (_motor25D == null)
                    {
                        var direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                        knockback = i == strikeCount - 1 ? direction * 3.5f : Vector2.zero;
                    }

                    var context = new DamageContext(
                        _entity,
                        _entity,
                        target,
                        damage,
                        DamageType.Physical,
                        knockback,
                        sourceId: i == strikeCount - 1 ? "Chen_Skill2_Final" : "Chen_Skill2_Strike");
                    if (DamageSystem.Apply(context).Applied)
                    {
                        target.GetComponentInChildren<HitFlash2D>()?.Flash();
                        HitStopService.Instance?.Request(i == strikeCount - 1 ? 0.040f : 0.012f);
                        CameraShake2D.Instance?.Shake(i == strikeCount - 1 ? 0.10f : 0.025f, 0.04f);
                    }
                }

                if (i < strikeCount - 1)
                    yield return new WaitForSeconds(strikeInterval);
            }

            var remaining = castLockSeconds - (Time.time - startedAt);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
            IsCasting = false;
        }

        private CombatEntity FindNearestTarget()
        {
            return _motor25D != null ? FindNearestTarget25D() : FindNearestTarget2D();
        }

        private CombatEntity FindNearestTarget2D()
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, targetingRadius);
            CombatEntity best = null;
            var bestSqr = float.PositiveInfinity;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (!CanTarget(candidate))
                    continue;

                var sqr = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = candidate;
            }

            return best;
        }

        private CombatEntity FindNearestTarget25D()
        {
            var colliders = Physics.OverlapSphere(transform.position, targetingRadius, ~0, QueryTriggerInteraction.Ignore);
            CombatEntity best = null;
            var bestSqr = float.PositiveInfinity;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (!CanTarget(candidate))
                    continue;
                if (Mathf.Abs(candidate.transform.position.y - transform.position.y) > max25DHeightDifference)
                    continue;
                if (!HasClear25DLine(candidate))
                    continue;

                var delta = candidate.transform.position - transform.position;
                delta.y = 0f;
                var sqr = delta.sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = candidate;
            }

            return best;
        }

        private bool HasClear25DLine(CombatEntity target)
        {
            var origin = transform.position + Vector3.up * 0.70f;
            var destination = target.transform.position + Vector3.up * 0.70f;
            var cast = destination - origin;
            var distance = cast.magnitude;
            if (distance < 0.001f)
                return true;

            var hits = Physics.SphereCastAll(origin, 0.12f, cast / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;
                var entity = hitCollider.GetComponentInParent<CombatEntity>();
                if (entity != null)
                {
                    if (entity == target)
                        return true;
                    continue;
                }
                return false;
            }
            return true;
        }

        private bool CanTarget(CombatEntity candidate)
        {
            return candidate != null &&
                   candidate != _entity &&
                   candidate.Team != _entity.Team &&
                   candidate.Health != null &&
                   !candidate.Health.IsDead;
        }
    }
}
