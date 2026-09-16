using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    [DisallowMultipleComponent]
    public sealed class ActiveOriginiumExposure25D : MonoBehaviour, IDamageModifier
    {
        private int _zoneCount;
        private float _largestBonus;

        public void AddZone(float outgoingBonus)
        {
            _zoneCount++;
            _largestBonus = Mathf.Max(_largestBonus, Mathf.Max(0f, outgoingBonus));
        }

        public void RemoveZone()
        {
            _zoneCount = Mathf.Max(0, _zoneCount - 1);
            if (_zoneCount == 0)
                _largestBonus = 0f;
        }

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage)
        {
            return _zoneCount > 0 ? currentDamage * (1f + _largestBonus) : currentDamage;
        }

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) => currentDamage;
    }
}
