using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Abilities;
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
            if (_casting || Time.time < _readyAt)
                return false;

            _readyAt = Time.time + cooldown;
            StartCoroutine(CastRoutine());
            return true;
        }

        public void ReduceCooldown(float seconds)
        {
            if (seconds <= 0f) return;
            _readyAt = Mathf.Max(Time.time, _readyAt - seconds);
        }

        private IEnumerator CastRoutine()
        {
            _casting = true;
            _presentation?.PlayCast(radius);
            yield return new WaitForSeconds(0.08f);
            ResolveWave(0.95f);
            yield return new WaitForSeconds(0.13f);
            ResolveWave(1.10f);
            _casting = false;
            CastResolved?.Invoke(transform.position, radius);
        }

        private void ResolveWave(float multiplier)
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, radius);
            var hit = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var collider in colliders)
            {
                var target = collider.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team || !hit.Add(target))
                    continue;

                var context = new DamageContext(_entity, _entity, target, waveDamage * multiplier, DamageType.Arts, Vector2.up * 0.5f, sourceId: "texas_sword_rain");
                var result = DamageSystem.Apply(context);
                if (!result.Applied) continue;

                target.Status.Apply(CombatStatusType.Shock, shockDuration);
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
                hitAny = true;
            }

            if (hitAny)
            {
                HitStopService.Instance?.Request(0.04f);
                CameraShake2D.Instance?.Shake(0.10f, 0.10f);
            }
        }
    }
}
