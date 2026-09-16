using System.Collections.Generic;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// ACT adaptation of active Originium terrain. Actors on the tile take periodic true damage.
    /// Enemies keep the damage bonus permanently once exposed; the player keeps it for ten seconds
    /// after the most recent contact with the tile.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ActiveOriginiumZone25D : MonoBehaviour
    {
        [SerializeField, Range(0.001f, 0.20f)] private float maxHealthDamagePerSecond = 0.025f;
        [SerializeField, Range(0f, 1f)] private float outgoingDamageBonus = 0.30f;
        [SerializeField, Min(0.1f)] private float tickInterval = 0.50f;
        [SerializeField, Min(0.1f)] private float playerBuffDuration = 10f;

        private readonly Dictionary<CombatEntity, float> _nextTickAt = new();

        public void Configure(float damageFractionPerSecond, float damageBonus)
        {
            maxHealthDamagePerSecond = Mathf.Clamp(damageFractionPerSecond, 0.001f, 0.20f);
            outgoingDamageBonus = Mathf.Clamp01(damageBonus);
            var collider = GetComponent<BoxCollider>();
            collider.isTrigger = true;
        }

        private void Awake()
        {
            var collider = GetComponent<BoxCollider>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var entity = other.GetComponentInParent<CombatEntity>();
            if (!IsValid(entity))
                return;

            ApplyExposure(entity);
            _nextTickAt[entity] = Time.time + tickInterval;
        }

        private void OnTriggerStay(Collider other)
        {
            var entity = other.GetComponentInParent<CombatEntity>();
            if (!IsValid(entity))
                return;

            ApplyExposure(entity);
            if (!_nextTickAt.TryGetValue(entity, out var nextTick))
                nextTick = Time.time + tickInterval;
            if (Time.time < nextTick)
            {
                _nextTickAt[entity] = nextTick;
                return;
            }

            _nextTickAt[entity] = Time.time + tickInterval;
            var damage = entity.Health.MaxHealth * maxHealthDamagePerSecond * tickInterval;
            DamageSystem.Apply(new DamageContext(
                null,
                null,
                entity,
                damage,
                DamageType.True,
                Vector2.zero,
                sourceId: "Environment_ActiveOriginium"));
        }

        private void OnTriggerExit(Collider other)
        {
            var entity = other.GetComponentInParent<CombatEntity>();
            if (entity != null)
                _nextTickAt.Remove(entity);
        }

        private void OnDisable()
        {
            _nextTickAt.Clear();
        }

        private void ApplyExposure(CombatEntity entity)
        {
            var buff = entity.GetComponent<ActiveOriginiumExposure25D>();
            if (buff == null)
                buff = entity.gameObject.AddComponent<ActiveOriginiumExposure25D>();

            if (entity.Team == Team.Player)
                buff.RefreshTimed(outgoingDamageBonus, playerBuffDuration);
            else if (entity.Team == Team.Enemy)
                buff.ApplyPermanent(outgoingDamageBonus);
        }

        private static bool IsValid(CombatEntity entity) =>
            entity != null && entity.Health != null && !entity.Health.IsDead;
    }
}
