using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    [RequireComponent(typeof(TexasSwordRainSkill), typeof(CombatEntity))]
    public sealed class TexasResidualThunderEffect : MonoBehaviour
    {
        private TexasSwordRainSkill _skill;
        private CombatEntity _entity;
        private SwordRainPresentation2D _presentation;
        private int _level;

        private void Awake()
        {
            _skill = GetComponent<TexasSwordRainSkill>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<SwordRainPresentation2D>();
        }

        public void SetLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, TexasBuildLab.MaxUpgradeLevel);
        }

        private void OnEnable()
        {
            if (_skill != null)
                _skill.CastResolved += OnSwordRainResolved;
        }

        private void OnDisable()
        {
            if (_skill != null)
                _skill.CastResolved -= OnSwordRainResolved;
            StopAllCoroutines();
        }

        private void OnSwordRainResolved(Vector2 center, float radius)
        {
            if (_level <= 0)
                return;

            StartCoroutine(ThunderField(center, radius));
        }

        private IEnumerator ThunderField(Vector2 center, float baseRadius)
        {
            var duration = _level switch
            {
                1 => 3.0f,
                2 => 4.0f,
                _ => 5.0f
            };
            var tickInterval = _level switch
            {
                1 => 0.50f,
                2 => 0.40f,
                _ => 0.30f
            };
            var tickDamage = _level switch
            {
                1 => 3f,
                2 => 4f,
                _ => 5f
            };
            var radius = baseRadius * (_level >= 3 ? 1.25f : 1f);
            var shockDuration = _level >= 3 ? 1.6f : 1.2f;

            var elapsed = 0f;
            var tickIndex = 0;
            while (elapsed < duration)
            {
                _presentation?.PlayFieldPulse(center, radius);
                if (_level >= 3 && tickIndex % 3 == 0)
                    _presentation?.PlayImpact(center, radius, 0.9f);

                var colliders = Physics2D.OverlapCircleAll(center, radius);
                var count = AreaDamageResolver.ApplyUnique(
                    colliders,
                    _entity,
                    _entity,
                    tickDamage,
                    DamageType.Arts,
                    Vector2.zero,
                    "texas_residual_thunder",
                    (target, _) =>
                    {
                        target.Status.Apply(CombatStatusType.Shock, shockDuration);
                        target.GetComponentInChildren<HitFlash2D>()?.Flash();
                    });

                if (count > 0)
                    CameraShake2D.Instance?.Shake(_level >= 3 ? 0.055f : 0.035f, 0.05f);

                tickIndex++;
                elapsed += tickInterval;
                yield return new WaitForSeconds(tickInterval);
            }
        }
    }
}
