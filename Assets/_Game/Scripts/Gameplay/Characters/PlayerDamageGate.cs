using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Player-specific damage acceptance rule. Dash invulnerability lives here,
    /// keeping Health and DamageSystem free of player knowledge.
    /// </summary>
    public sealed class PlayerDamageGate : MonoBehaviour, IDamageGate
    {
        private PlayerDashController _dash;

        private void Awake()
        {
            _dash = GetComponent<PlayerDashController>();
        }

        public bool CanReceiveDamage(in DamageContext context)
        {
            return _dash == null || !_dash.IsInvulnerable;
        }
    }
}
