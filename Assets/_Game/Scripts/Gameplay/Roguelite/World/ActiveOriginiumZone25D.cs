using System.Collections.Generic;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// ACT adaptation of Arknights active Originium terrain: actors standing on the tile
    /// take periodic true damage but deal increased outgoing damage. Attack-speed handling is
    /// intentionally deferred until locomotion/action playback modifiers are centralized.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ActiveOriginiumZone25D : MonoBehaviour
    {
        [SerializeField, Range(0.001f, 0.20f)] private float maxHealthDamagePerSecond = 0.025f;
        [SerializeField, Range(0f, 1f)] private float outgoingDamageBonus = 0.30f;
        [SerializeField, Min(0.1f)] private float tickInterval = 0.50f;

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
            if (!IsValid(entity) || _nextTickAt.ContainsKey(entity))
                return;
            AddExposure(entity);
            _nextTickAt[entity] = Time.time + tickInterval;
        }

        private void OnTriggerStay(Collider other)
        {
            var entity = other.GetComponentInParent<CombatEntity>();
            if (!IsValid(entity))
                return;
            if (!_nextTickAt.ContainsKey(entity))
            {
                AddExposure(entity);
                _nextTickAt[entity] = Time.time + tickInterval;
            }

            if (Time.time < _nextTickAt[entity])
                return;
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
            if (entity == null || !_nextTickAt.Remove(entity))
                return;
            RemoveExposure(entity);
        }

        private void OnDisable()
        {
            foreach (var entity in _nextTickAt.Keys)
                RemoveExposure(entity);
            _nextTickAt.Clear();
        }

        private void AddExposure(CombatEntity entity)
        {
            var buff = entity.GetComponent<ActiveOriginiumExposure25D>();
            if (buff == null)
                buff = entity.gameObject.AddComponent<ActiveOriginiumExposure25D>();
            buff.AddZone(outgoingDamageBonus);
        }

        private static void RemoveExposure(CombatEntity entity)
        {
            if (entity == null)
                return;
            entity.GetComponent<ActiveOriginiumExposure25D>()?.RemoveZone();
        }

        private static bool IsValid(CombatEntity entity) =>
            entity != null && entity.Health != null && !entity.Health.IsDead;
    }
}
