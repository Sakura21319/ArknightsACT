using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    [RequireComponent(typeof(TopDownTexasMeleeController), typeof(CombatEntity))]
    public sealed class TopDownTexasSwiftBladeEffect : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float waveLength = 5.0f;
        [SerializeField, Min(0.1f)] private float halfWidth = 0.75f;

        private TopDownTexasMeleeController _melee;
        private CombatEntity _entity;
        private TopDownCombatFx2D _fx;
        private int _level;
        private int _attackCount;
        private Vector2 _lastDirection = Vector2.right;

        private void Awake()
        {
            _melee = GetComponent<TopDownTexasMeleeController>();
            _entity = GetComponent<CombatEntity>();
            _fx = GetComponent<TopDownCombatFx2D>();
        }

        public void SetLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, TopDownTexasBuildLab.MaxUpgradeLevel);
            _attackCount = 0;
        }

        private void OnEnable()
        {
            if (_melee != null)
                _melee.AttackStarted += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_melee != null)
                _melee.AttackStarted -= OnAttackStarted;
            StopAllCoroutines();
            _attackCount = 0;
        }

        private void OnAttackStarted(Vector2 direction)
        {
            if (_level <= 0)
                return;

            _lastDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            _attackCount++;
            var threshold = _level <= 1 ? 4 : 3;
            if (_attackCount < threshold)
                return;

            _attackCount = 0;
            FireWave(_lastDirection, 0f);
            if (_level >= 3)
                StartCoroutine(SecondWave());
        }

        private IEnumerator SecondWave()
        {
            yield return new WaitForSeconds(0.07f);
            FireWave(_lastDirection, 0.35f);
        }

        private void FireWave(Vector2 direction, float extraLength)
        {
            if (_entity == null)
                return;

            var length = waveLength + extraLength;
            var origin = (Vector2)transform.position;
            var colliders = Physics2D.OverlapCircleAll(origin + direction * (length * 0.5f), length * 0.65f + halfWidth);
            var seen = new HashSet<CombatEntity>();
            var hitAny = false;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var damage = _level switch { 1 => 7f, 2 => 10f, _ => 12f };

            for (var i = 0; i < colliders.Length; i++)
            {
                var target = colliders[i]?.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team ||
                    target.Health == null || target.Health.IsDead || !seen.Add(target))
                    continue;

                var delta = (Vector2)target.transform.position - origin;
                var forward = Vector2.Dot(delta, direction);
                var side = Mathf.Abs(Vector2.Dot(delta, perpendicular));
                if (forward < 0f || forward > length || side > halfWidth)
                    continue;

                var context = new DamageContext(
                    _entity,
                    _entity,
                    target,
                    damage,
                    DamageType.Arts,
                    direction * 1.0f,
                    sourceId: "TopDown_Texas_SwiftBlade");
                if (!DamageSystem.Apply(context).Applied)
                    continue;

                hitAny = true;
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
            }

            _fx?.PlayWave(origin, direction, length);
            if (hitAny)
            {
                HitStopService.Instance?.Request(_level >= 3 ? 0.022f : 0.016f);
                CameraShake2D.Instance?.Shake(_level >= 3 ? 0.055f : 0.038f, 0.055f);
            }
        }
    }
}
