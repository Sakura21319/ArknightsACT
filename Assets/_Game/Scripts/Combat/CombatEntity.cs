using System;
using UnityEngine;

namespace ArknightsACT.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class CombatEntity : MonoBehaviour
    {
        [SerializeField] private Team team = Team.Neutral;
        private Health _health;
        public string EntityId { get; private set; }
        public Team Team => team;
        public Health Health => _health != null ? _health : (_health = GetComponent<Health>());
        public event Action<DamageContext, DamageResult> Damaged;
        private void Awake()
        {
            EntityId = Guid.NewGuid().ToString("N");
            _health = GetComponent<Health>();
        }
        public void SetTeam(Team value) => team = value;
        internal void NotifyDamaged(DamageContext context, DamageResult result) => Damaged?.Invoke(context, result);
    }
}
