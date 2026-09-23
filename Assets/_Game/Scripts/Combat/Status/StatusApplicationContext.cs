using UnityEngine;

namespace ArknightsACT.Combat.Status
{
    public readonly struct StatusApplicationContext
    {
        public string StatusId { get; }
        public CombatEntity Source { get; }
        public CombatEntity Owner { get; }
        public float Duration { get; }
        public int Stacks { get; }
        public float Magnitude { get; }
        public DamageType PeriodicDamageType { get; }
        public int ProcGeneration { get; }
        public string SourceId { get; }

        public StatusApplicationContext(
            string statusId,
            CombatEntity source = null,
            CombatEntity owner = null,
            float duration = 0f,
            int stacks = 1,
            float magnitude = 0f,
            DamageType periodicDamageType = DamageType.Arts,
            int procGeneration = 0,
            string sourceId = "")
        {
            StatusId = statusId ?? string.Empty;
            Source = source;
            Owner = owner ?? source;
            Duration = Mathf.Max(0f, duration);
            Stacks = Mathf.Max(1, stacks);
            Magnitude = Mathf.Max(0f, magnitude);
            PeriodicDamageType = periodicDamageType;
            ProcGeneration = Mathf.Max(0, procGeneration);
            SourceId = sourceId ?? string.Empty;
        }
    }
}
