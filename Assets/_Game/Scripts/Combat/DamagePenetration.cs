using System;
using UnityEngine;

namespace ArknightsACT.Combat
{
    [Serializable]
    public readonly struct DamagePenetration
    {
        public float PhysicalDefenseIgnoreFlat { get; }
        public float PhysicalDefenseIgnorePercent { get; }
        public float ArtsResistanceIgnoreFlat { get; }
        public float ArtsResistanceIgnorePercent { get; }

        public DamagePenetration(
            float physicalDefenseIgnoreFlat = 0f,
            float physicalDefenseIgnorePercent = 0f,
            float artsResistanceIgnoreFlat = 0f,
            float artsResistanceIgnorePercent = 0f)
        {
            PhysicalDefenseIgnoreFlat = Mathf.Max(0f, physicalDefenseIgnoreFlat);
            PhysicalDefenseIgnorePercent = Mathf.Clamp01(physicalDefenseIgnorePercent);
            ArtsResistanceIgnoreFlat = Mathf.Max(0f, artsResistanceIgnoreFlat);
            ArtsResistanceIgnorePercent = Mathf.Clamp01(artsResistanceIgnorePercent);
        }

        public DamagePenetration Add(in DamagePenetration other) => new(
            PhysicalDefenseIgnoreFlat + other.PhysicalDefenseIgnoreFlat,
            Mathf.Clamp01(PhysicalDefenseIgnorePercent + other.PhysicalDefenseIgnorePercent),
            ArtsResistanceIgnoreFlat + other.ArtsResistanceIgnoreFlat,
            Mathf.Clamp01(ArtsResistanceIgnorePercent + other.ArtsResistanceIgnorePercent));

        public static DamagePenetration None => new();
    }
}
