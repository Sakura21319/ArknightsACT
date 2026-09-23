using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    [DisallowMultipleComponent]
    public sealed class TemporaryCombatBuffs : MonoBehaviour, IDamageModifier, IPlayerSwitchStateTransfer
    {
        private readonly List<BuffEntry> _damageBuffs = new();

        public void AddAllDamagePercent(float percent, float durationSeconds)
        {
            if (percent <= 0f || durationSeconds <= 0f)
                return;
            _damageBuffs.Add(new BuffEntry(Mathf.Max(0f, percent), Time.time + durationSeconds));
        }

        public void CopySwitchStateTo(Transform destination)
        {
            if (destination == null)
                return;
            var target = destination.GetComponent<TemporaryCombatBuffs>();
            if (target == null || target == this)
                return;

            CleanupExpired();
            target._damageBuffs.Clear();
            for (var i = 0; i < _damageBuffs.Count; i++)
            {
                var remaining = _damageBuffs[i].ExpiresAt - Time.time;
                if (remaining > 0f)
                    target._damageBuffs.Add(new BuffEntry(_damageBuffs[i].Percent, Time.time + remaining));
            }
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
