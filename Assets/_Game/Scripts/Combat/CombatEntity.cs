using System;
using ArknightsACT.Combat.Status;
using UnityEngine;

namespace ArknightsACT.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(StatusController), typeof(CombatStats))]
    public sealed class CombatEntity : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Neutral;
        private Health _health;
        private StatusController _status;
        private CombatStats _stats;

        public string EntityId { get; private set; }
        public Team Team => team;
        public Health Health => _health != null ? _health : (_health = GetComponent<Health>());
        public StatusController Status => _status != null ? _status : (_status = GetComponent<StatusController>());
        public CombatStats Stats => _stats != null ? _stats : (_stats = GetComponent<CombatStats>());

        public event Action<DamageContext, DamageResult> Damaged;

        private void Awake()
        {
            EntityId = Guid.NewGuid().ToString("N");
            _health = GetComponent<Health>();
            _status = GetComponent<StatusController>();
            _stats = GetComponent<CombatStats>();

            // RequireComponent is not retroactive for scenes/prefabs that already serialized
            // CombatEntity before CombatStats existed.
            if (_stats == null)
                _stats = gameObject.AddComponent<CombatStats>();
        }

        public void SetTeam(Team value) => team = value;
        internal void NotifyDamaged(DamageContext context, DamageResult result) => Damaged?.Invoke(context, result);
    }
}
