using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Combat.Status
{
    [DisallowMultipleComponent]
    public sealed class StatusController : MonoBehaviour
    {
        private readonly Dictionary<CombatStatusType, float> _expiresAt = new();
        private readonly List<CombatStatusType> _expired = new(4);

        public event Action<CombatStatusType, float> StatusApplied;

        public void Apply(CombatStatusType type, float duration)
        {
            if (duration <= 0f)
                return;

            var expires = Time.time + duration;
            if (_expiresAt.TryGetValue(type, out var current))
                expires = Mathf.Max(expires, current);

            _expiresAt[type] = expires;
            StatusApplied?.Invoke(type, duration);
        }

        public bool Has(CombatStatusType type)
        {
            return _expiresAt.TryGetValue(type, out var expires) && expires > Time.time;
        }

        public float Remaining(CombatStatusType type)
        {
            return _expiresAt.TryGetValue(type, out var expires)
                ? Mathf.Max(0f, expires - Time.time)
                : 0f;
        }

        public void Remove(CombatStatusType type) => _expiresAt.Remove(type);

        private void Update()
        {
            if (_expiresAt.Count == 0)
                return;

            _expired.Clear();
            foreach (var pair in _expiresAt)
            {
                if (pair.Value <= Time.time)
                    _expired.Add(pair.Key);
            }

            for (var i = 0; i < _expired.Count; i++)
                _expiresAt.Remove(_expired[i]);
        }
    }
}
