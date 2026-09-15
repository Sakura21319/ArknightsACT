using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Feedback;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class ChenSkill1 : MonoBehaviour, IPlayerSkill
    {
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 6f;
        [SerializeField, Min(0f)] private float impactDelay = 0.34f;
        [SerializeField, Min(0.1f)] private float castLockSeconds = 1.15f;
        [SerializeField, Min(0f)] private float physicalDamage = 28f;
        [SerializeField, Min(0f)] private float artsDamage = 28f;
        [SerializeField] private Vector2 hitboxOffset = new(1.25f, 0.08f);
        [SerializeField] private Vector2 hitboxSize = new(3.4f, 1.8f);

        private CombatEntity _entity;
        private IPlayerLocomotion _motor;
        private PlayerMotor25D _motor25D;
        private float _readyAt;

        public int Slot => 1;
        public string DisplayName => "赤霄·拔刀";
        public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);
        public bool IsCasting { get; private set; }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _motor = FindLocomotion();
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
            if (impactDelay > 0f)
                yield return new WaitForSeconds(impactDelay);

            ResolveHit();

            var remaining = Mathf.Max(0f, castLockSeconds - impactDelay);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
            IsCasting = false;
        }

        private void ResolveHit()
        {
            if (_entity?.Health == null || _entity.Health.IsDead)
                return;

            if (_motor25D != null)
                ResolveHit25D();
            else
                ResolveHit2D();
        }

        private void ResolveHit2D()
        {
            var facing = _motor != null ? _motor.FacingSign : 1;
            var offset = hitboxOffset;
            offset.x *= facing;
            var center = (Vector2)transform.position + offset;
            var colliders = Physics2D.OverlapBoxAll(center, hitboxSize, 0f);
            var seen = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var target = collider.GetComponentInParent<CombatEntity>();
                if (!CanHit(target, seen))
                    continue;

                if (ApplyDamagePair(target, new Vector2(4.2f * facing, 0.9f)))
                    hitAny = true;
            }

            ApplyFeedback(hitAny);
        }

        private void ResolveHit25D()
        {
            var forward = _motor != null ? _motor.PlanarForward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            var center = transform.position + forward * Mathf.Max(0.8f, Mathf.Abs(hitboxOffset.x)) + Vector3.up * 0.85f;
            var halfExtents = new Vector3(
                Mathf.Max(0.9f, hitboxSize.x * 0.5f),
                1.0f,
                Mathf.Max(0.75f, hitboxSize.y * 0.45f));
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var colliders = Physics.OverlapBox(center, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var collider in colliders)
            {
                if (collider == null)
                    continue;
                var target = collider.GetComponentInParent<CombatEntity>();
                if (!CanHit(target, seen))
                    continue;

                if (ApplyDamagePair(target, Vector2.zero))
                    hitAny = true;
            }

            ApplyFeedback(hitAny);
        }

        private bool CanHit(CombatEntity target, HashSet<CombatEntity> seen)
        {
            return target != null &&
                   target != _entity &&
                   target.Team != _entity.Team &&
                   target.Health != null &&
                   !target.Health.IsDead &&
                   seen.Add(target);
        }

        private bool ApplyDamagePair(CombatEntity target, Vector2 knockback)
        {
            var hitAny = false;
            var physical = new DamageContext(
                _entity, _entity, target, physicalDamage, DamageType.Physical, knockback,
                sourceId: "Chen_Skill1_Physical");
            var physicalResult = DamageSystem.Apply(physical);
            if (physicalResult.Applied)
                hitAny = true;

            if (!target.Health.IsDead && artsDamage > 0f)
            {
                var arts = new DamageContext(
                    _entity, _entity, target, artsDamage, DamageType.Arts, Vector2.zero,
                    sourceId: "Chen_Skill1_Arts");
                if (DamageSystem.Apply(arts).Applied)
                    hitAny = true;
            }

            if (physicalResult.Applied)
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
            return hitAny;
        }

        private static void ApplyFeedback(bool hitAny)
        {
            if (!hitAny)
                return;
            HitStopService.Instance?.Request(0.045f);
            CameraShake2D.Instance?.Shake(0.11f, 0.08f);
        }

        private IPlayerLocomotion FindLocomotion()
        {
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerLocomotion locomotion)
                    return locomotion;
            }
            return null;
        }
    }
}
