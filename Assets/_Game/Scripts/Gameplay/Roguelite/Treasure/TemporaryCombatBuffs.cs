using System.Collections.Generic;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    [DisallowMultipleComponent]
    public sealed class TemporaryCombatBuffs : MonoBehaviour, IDamageModifier
    {
        private readonly List<BuffEntry> _damageBuffs = new();

        public void AddAllDamagePercent(float percent, float durationSeconds)
        {
            if (percent <= 0f || durationSeconds <= 0f)
                return;
            _damageBuffs.Add(new BuffEntry(Mathf.Max(0f, percent), Time.time + durationSeconds));
        }

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage)
        {
            CleanupExpired();
            var percent = 0f;
            for (var i = 0; i < _damageBuffs.Count; i++)
                percent += _damageBuffs[i].Percent;
            return currentDamage * Mathf.Max(0f, 1f + percent);
        }

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) => currentDamage;

        private void Update() => CleanupExpired();

        private void CleanupExpired()
        {
            for (var i = _damageBuffs.Count - 1; i >= 0; i--)
            {
                if (Time.time >= _damageBuffs[i].ExpiresAt)
                    _damageBuffs.RemoveAt(i);
            }
        }

        private readonly struct BuffEntry
        {
            public float Percent { get; }
            public float ExpiresAt { get; }

            public BuffEntry(float percent, float expiresAt)
            {
                Percent = percent;
                ExpiresAt = expiresAt;
            }
        }
    }
}
