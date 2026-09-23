using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    [DisallowMultipleComponent]
    public sealed class FrostNovaRangedBasicAttack :
        MonoBehaviour,
        IPlayerBasicAttackResolver,
        IPlayerBasicAttackImpactTimingProvider
    {
        [SerializeField, Min(1f)] private float baseRange = 10.5f;
        [SerializeField, Range(-1f, 1f)] private float minimumForwardDot = 0.05f;
        [SerializeField, Min(0.1f)] private float maxHeightDifference = 2.2f;
        [SerializeField, Min(0.01f)] private float lineRadius = 0.10f;

        private CombatEntity _entity;
        private IPlayerLocomotion _locomotion;
        private FrostNovaTuningProfile _tuning;

        public float CurrentVisualRange => baseRange;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _locomotion = FindLocomotion();
            RefreshTuning();
        }

        public void RefreshTuning()
        {
            _tuning = Resources.Load<FrostNovaTuningProfile>(
                FrostNovaTuningProfile.ResourcePath);
        }

        public float GetBasicAttackImpactSeconds(AttackDefinition definition)
        {
            if (_tuning == null)
                RefreshTuning();

            return _tuning != null
                ? _tuning.BasicAttackImpactSeconds
                : -1f;
        }

        public bool TryGetAimTarget(out CombatEntity target)
        {
            target = FindBestTarget(_entity, _locomotion);
            return target != null;
        }

        public Vector3 GetMissDestination()
        {
            var forward = ResolveForward(_locomotion);
            return transform.position + Vector3.up * 0.82f + forward * Mathf.Max(1f, baseRange);
        }

        public bool TryResolveBasicAttack(
            CombatEntity attacker,
            AttackDefinition definition,
            IPlayerLocomotion locomotion,
            float finalDamage,
            float rangeMultiplier,
            out CombatEntity hitTarget)
        {
            hitTarget = FindBestTarget(attacker, locomotion, Mathf.Max(0.1f, rangeMultiplier));
            if (hitTarget == null)
                return false;

            var context = new DamageContext(
                attacker,
                attacker,
                hitTarget,
                Mathf.Max(0f, finalDamage),
                DamageType.Arts,
                Vector2.zero,
                sourceId: definition != null ? definition.name : "FrostNova_BasicShot",
                tags: DamageTags.BasicAttack);

            if (!DamageSystem.Apply(context).Applied)
            {
                hitTarget = null;
                return false;
            }

            return true;
        }

        private CombatEntity FindBestTarget(
            CombatEntity attacker,
            IPlayerLocomotion locomotion,
            float rangeMultiplier = 1f)
        {
            if (attacker == null || attacker.Health == null || attacker.Health.IsDead)
                return null;

            var forward = ResolveForward(locomotion);
            var origin = transform.position + Vector3.up * 0.82f;
            var range = Mathf.Max(1f, baseRange * rangeMultiplier);
            var colliders = Physics.OverlapSphere(
                transform.position,
                range,
                ~0,
                QueryTriggerInteraction.Ignore);

            CombatEntity best = null;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                    continue;

                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (!CanTarget(attacker, candidate))
                    continue;

                var delta = candidate.transform.position - transform.position;
                var height = Mathf.Abs(delta.y);
                delta.y = 0f;
                if (height > maxHeightDifference || delta.sqrMagnitude < 0.001f)
                    continue;

                var distance = delta.magnitude;
                if (distance > range)
                    continue;

                var dot = Vector3.Dot(forward, delta / distance);
                if (dot < minimumForwardDot || !HasClearLine(attacker, candidate, origin))
                    continue;

                var score = distance + (1f - dot) * range;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private static Vector3 ResolveForward(IPlayerLocomotion locomotion)
        {
            var forward = locomotion != null ? locomotion.PlanarForward : Vector3.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;
            return forward.normalized;
        }

        private bool HasClearLine(CombatEntity attacker, CombatEntity target, Vector3 origin)
        {
            var destination = target.transform.position + Vector3.up * 0.76f;
            var cast = destination - origin;
            var distance = cast.magnitude;
            if (distance < 0.001f)
                return true;

            var hits = Physics.SphereCastAll(
                origin,
                lineRadius,
                cast / distance,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(attacker.transform))
                    continue;

                var entity = collider.GetComponentInParent<CombatEntity>();
                if (entity != null)
                    return entity == target;

                return false;
            }

            return true;
        }

        private IPlayerLocomotion FindLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            return null;
        }

        private static bool CanTarget(CombatEntity attacker, CombatEntity target)
        {
            return target != null &&
                   target != attacker &&
                   target.Team != attacker.Team &&
                   target.Health != null &&
                   !target.Health.IsDead;
        }
    }
}
