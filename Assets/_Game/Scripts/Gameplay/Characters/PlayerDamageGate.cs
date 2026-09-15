using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Player-specific damage acceptance rule. Any component implementing
    /// IPlayerInvulnerabilitySource can temporarily reject incoming damage.
    /// </summary>
    public sealed class PlayerDamageGate : MonoBehaviour, IDamageGate
    {
        private MonoBehaviour[] _behaviours;

        private void Awake()
        {
            _behaviours = GetComponents<MonoBehaviour>();
        }

        public bool CanReceiveDamage(in DamageContext context)
        {
            if (_behaviours == null || _behaviours.Length == 0)
                _behaviours = GetComponents<MonoBehaviour>();

            for (var i = 0; i < _behaviours.Length; i++)
            {
                if (_behaviours[i] is IPlayerInvulnerabilitySource source && source.IsInvulnerable)
                    return false;
            }
            return true;
        }
    }
}
