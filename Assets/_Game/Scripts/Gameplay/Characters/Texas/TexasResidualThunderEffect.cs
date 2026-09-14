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
        [SerializeField, Min(0f)] private float duration = 3f;
        [SerializeField, Min(0.05f)] private float tickInterval = 0.5f;
        [SerializeField, Min(0f)] private float tickDamage = 3f;
        [SerializeField, Min(0f)] private float shockDuration = 1.2f;

        private TexasSwordRainSkill _skill;
        private CombatEntity _entity;
        private SwordRainPresentation2D _presentation;

        private void Awake()
        {
            _skill = GetComponent<TexasSwordRainSkill>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<SwordRainPresentation2D>();
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
            StartCoroutine(ThunderField(center, radius));
        }

        private IEnumerator ThunderField(Vector2 center, float radius)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                _presentation?.PlayFieldPulse(center, radius);
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
                    CameraShake2D.Instance?.Shake(0.035f, 0.05f);

                elapsed += tickInterval;
                yield return new WaitForSeconds(tickInterval);
            }
        }
    }
}
