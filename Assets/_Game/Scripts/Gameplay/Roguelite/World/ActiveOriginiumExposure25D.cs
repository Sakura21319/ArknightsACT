using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    [DisallowMultipleComponent]
    public sealed class ActiveOriginiumExposure25D : MonoBehaviour, IDamageModifier
    {
        private bool _permanent;
        private float _permanentBonus;
        private float _timedBonus;
        private float _timedExpiresAt;

        public bool IsActive => _permanent || Time.time < _timedExpiresAt;

        public void ApplyPermanent(float outgoingBonus)
        {
            _permanent = true;
            _permanentBonus = Mathf.Max(_permanentBonus, Mathf.Max(0f, outgoingBonus));
        }

        public void RefreshTimed(float outgoingBonus, float durationSeconds)
        {
            _timedBonus = Mathf.Max(_timedBonus, Mathf.Max(0f, outgoingBonus));
            _timedExpiresAt = Mathf.Max(_timedExpiresAt, Time.time + Mathf.Max(0.1f, durationSeconds));
        }

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage)
        {
            var bonus = 0f;
            if (_permanent)
                bonus = Mathf.Max(bonus, _permanentBonus);
            if (Time.time < _timedExpiresAt)
                bonus = Mathf.Max(bonus, _timedBonus);
            return bonus > 0f ? currentDamage * (1f + bonus) : currentDamage;
        }

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) => currentDamage;

        private void Update()
        {
            if (_permanent || _timedExpiresAt <= 0f || Time.time < _timedExpiresAt)
                return;
            _timedExpiresAt = 0f;
            _timedBonus = 0f;
        }
    }
}
