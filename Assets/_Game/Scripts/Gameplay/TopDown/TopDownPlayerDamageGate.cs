using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    public sealed class TopDownPlayerDamageGate : MonoBehaviour, IDamageGate
    {
        private TopDownPlayerDash2D _dash;

        private void Awake() => _dash = GetComponent<TopDownPlayerDash2D>();

        public bool CanReceiveDamage(in DamageContext context)
        {
            return _dash == null || !_dash.IsInvulnerable;
        }
    }
}
