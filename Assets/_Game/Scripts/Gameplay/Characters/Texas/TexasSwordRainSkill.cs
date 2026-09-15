using System;
using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class TexasSwordRainSkill : MonoBehaviour, IPlayerSkill
    {
        [SerializeField] private float cooldown = 10f;
        [SerializeField] private float radius = 2.9f;
        [SerializeField] private float waveDamage = 13f;
        [SerializeField] private float shockDuration = 4f;

        private CombatEntity _entity;
        private SwordRainPresentation2D _presentation;
        private float _readyAt;
        private bool _casting;

        public string DisplayName => "Sword Rain";
        public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);
        public event Action<Vector2, float> CastResolved;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<SwordRainPresentation2D>();
        }

        public bool TryCast()
        {
            if (_casting || Time.time < _readyAt ||
                _entity == null || _entity.Health == null || _entity.Health.IsDead)
                return false;

            _readyAt = Time.time + cooldown;
            StartCoroutine(CastRoutine());
            return true;
        }

        public void ReduceCooldown(float seconds)
        {
            if (seconds <= 0f)
                return;
            _readyAt = Mathf.Max(Time.time, _readyAt - seconds);
        }

        private IEnumerator CastRoutine()
        {
            _casting = true;
            _presentation?.PlayCast(radius);

            // The first wave lands after the falling blades have visibly reached the floor.
            yield return new WaitForSeconds(0.16f);
            ResolveWave(0.95f);

            // A short second thunder burst gives the skill a readable two-hit signature.
            yield return new WaitForSeconds(0.12f);
            ResolveWave(1.10f);

            _casting = false;
            CastResolved?.Invoke(transform.position, radius);
        }

        private void ResolveWave(float multiplier)
        {
            _presentation?.PlayImpact(transform.position, radius, multiplier);

            var colliders = Physics2D.OverlapCircleAll(transform.position, radius);
            var hitCount = AreaDamageResolver.ApplyUnique(
                colliders,
                _entity,
                _entity,
                waveDamage * multiplier,
                DamageType.Arts,
                Vector2.up * 0.5f,
                "texas_sword_rain",
                (target, _) =>
                {
                    target.Status.Apply(CombatStatusType.Shock, shockDuration);
                    target.GetComponentInChildren<HitFlash2D>()?.Flash();
                });

            if (hitCount <= 0)
                return;

            HitStopService.Instance?.Request(0.04f);
            CameraShake2D.Instance?.Shake(0.10f * multiplier, 0.10f);
        }
    }
}
