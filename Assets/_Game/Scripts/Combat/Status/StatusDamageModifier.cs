using System;

namespace ArknightsACT.Combat.Status
{
    public enum StatusDamageModifierDirection
    {
        Outgoing,
        Incoming
    }

    public enum StatusDamageTypeFilter
    {
        Any,
        Physical,
        Arts,
        True
    }

    [Serializable]
    public struct StatusDamageModifier
    {
        public StatusDamageModifierDirection Direction;
        public StatusDamageTypeFilter TypeFilter;
        public float AdditivePercent;
        public bool ScaleByApplicationMagnitude;

        public StatusDamageModifier(
            StatusDamageModifierDirection direction,
            float additivePercent,
            StatusDamageTypeFilter damageType = StatusDamageTypeFilter.Any,
            bool scaleByApplicationMagnitude = false)
        {
            Direction = direction;
            TypeFilter = damageType;
            AdditivePercent = additivePercent;
            ScaleByApplicationMagnitude = scaleByApplicationMagnitude;
        }

        public bool Matches(ArknightsACT.Combat.DamageType type) => TypeFilter switch
        {
            StatusDamageTypeFilter.Physical => type == ArknightsACT.Combat.DamageType.Physical,
            StatusDamageTypeFilter.Arts => type == ArknightsACT.Combat.DamageType.Arts,
            StatusDamageTypeFilter.True => type == ArknightsACT.Combat.DamageType.True,
            _ => true
        };
    }
}
