using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Combat
{
    /// <summary>
    /// Runtime bucket for additive combat-stat modifiers belonging to one pipeline layer.
    /// Source ids make repeated upgrades deterministic and allow one subsystem to clear only its own values.
    /// </summary>
    public sealed class CombatStatModifierBucket : MonoBehaviour, ILayeredCombatStatModifier
    {
        private readonly Dictionary<ModifierKey, ModifierValue> _values = new();
        [SerializeField] private CombatStatModifierLayer modifierLayer = CombatStatModifierLayer.RunPermanent;

        public CombatStatModifierLayer ModifierLayer => modifierLayer;

        public static CombatStatModifierBucket GetOrCreate(GameObject owner, CombatStatModifierLayer layer)
        {
            if (owner == null)
                return null;

            var buckets = owner.GetComponents<CombatStatModifierBucket>();
            for (var i = 0; i < buckets.Length; i++)
                if (buckets[i] != null && buckets[i].ModifierLayer == layer)
                    return buckets[i];

            var created = owner.AddComponent<CombatStatModifierBucket>();
            created.modifierLayer = layer;
            return created;
        }

        public void Set(
            CombatStatType stat,
            string sourceId,
            float flat = 0f,
            float additivePercent = 0f)
        {
            var key = new ModifierKey(stat, sourceId);
            if (Mathf.Abs(flat) <= 0.000001f &&
                Mathf.Abs(additivePercent) <= 0.000001f)
            {
                _values.Remove(key);
                return;
            }

            _values[key] = new ModifierValue(flat, additivePercent);
        }

        public void Add(
            CombatStatType stat,
            string sourceId,
            float flat = 0f,
            float additivePercent = 0f)
        {
            var key = new ModifierKey(stat, sourceId);
            _values.TryGetValue(key, out var current);
            Set(
                stat,
                sourceId,
                current.Flat + flat,
                current.AdditivePercent + additivePercent);
        }

        public void ClearSource(string sourceId)
        {
            var normalized = sourceId ?? string.Empty;
            if (_values.Count == 0)
                return;

            var remove = new List<ModifierKey>();
            foreach (var pair in _values)
                if (string.Equals(pair.Key.SourceId, normalized, StringComparison.Ordinal))
                    remove.Add(pair.Key);

            for (var i = 0; i < remove.Count; i++)
                _values.Remove(remove[i]);
        }

        public void ClearAll() => _values.Clear();

        public void AccumulateStatModifiers(
            CombatStatType stat,
            ref float flat,
            ref float additivePercent)
        {
            foreach (var pair in _values)
            {
                if (pair.Key.Stat != stat)
                    continue;
                flat += pair.Value.Flat;
                additivePercent += pair.Value.AdditivePercent;
            }
        }

        private readonly struct ModifierKey : IEquatable<ModifierKey>
        {
            public readonly CombatStatType Stat;
            public readonly string SourceId;

            public ModifierKey(CombatStatType stat, string sourceId)
            {
                Stat = stat;
                SourceId = sourceId ?? string.Empty;
            }

            public bool Equals(ModifierKey other) =>
                Stat == other.Stat &&
                string.Equals(SourceId, other.SourceId, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is ModifierKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Stat * 397) ^ StringComparer.Ordinal.GetHashCode(SourceId);
                }
            }
        }

        private readonly struct ModifierValue
        {
            public readonly float Flat;
            public readonly float AdditivePercent;

            public ModifierValue(float flat, float additivePercent)
            {
                Flat = flat;
                AdditivePercent = additivePercent;
            }
        }
    }
}
