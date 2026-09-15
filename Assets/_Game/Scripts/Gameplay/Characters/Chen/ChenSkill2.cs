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

        private CombatEntity _entity;
        private float _readyAt;

        public int Slot => 2;
        public string DisplayName => "赤霄·绝影";
        public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);
        public bool IsCasting { get; private set; }
        public bool IsInvulnerable => IsCasting;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
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
                    var direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                    var knockback = i == strikeCount - 1 ? direction * 3.5f : Vector2.zero;
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
            var colliders = Physics2D.OverlapCircleAll(transform.position, targetingRadius);
            CombatEntity best = null;
            var bestSqr = float.PositiveInfinity;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (candidate == null || candidate == _entity || candidate.Team == _entity.Team ||
                    candidate.Health == null || candidate.Health.IsDead)
                    continue;

                var sqr = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr >= bestSqr)
                    continue;
                bestSqr = sqr;
                best = candidate;
            }

            return best;
        }
    }
}
