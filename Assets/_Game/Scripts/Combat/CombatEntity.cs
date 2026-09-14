using System;
using ArknightsACT.Combat.Status;
using UnityEngine;

namespace ArknightsACT.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(StatusController))]
    public sealed class CombatEntity : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Neutral;
        private Health _health;
        private StatusController _status;

        public string EntityId { get; private set; }
        public Team Team => team;
        public Health Health => _health != null ? _health : (_health = GetComponent<Health>());
        public StatusController Status => _status != null ? _status : (_status = GetComponent<StatusController>());

        public event Action<DamageContext, DamageResult> Damaged;

        private void Awake()
        {
            EntityId = Guid.NewGuid().ToString("N");
            _health = GetComponent<Health>();
            _status = GetComponent<StatusController>();
        }

        public void SetTeam(Team value) => team = value;
        internal void NotifyDamaged(DamageContext context, DamageResult result) => Damaged?.Invoke(context, result);
    }
}
