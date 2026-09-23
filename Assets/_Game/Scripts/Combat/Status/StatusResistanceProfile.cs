using System;
using UnityEngine;

namespace ArknightsACT.Combat.Status
{
    [DisallowMultipleComponent]
    public sealed class StatusResistanceProfile : MonoBehaviour
    {
        [Serializable]
        private sealed class TagRule
        {
            public CombatStatusTags tags = CombatStatusTags.None;
            public bool immune;
            [Min(0f)] public float durationMultiplier = 1f;
        }

        [Serializable]
        private sealed class IdRule
        {
            public string statusId = string.Empty;
            public bool immune;
            [Min(0f)] public float durationMultiplier = 1f;
        }

        [SerializeField] private TagRule[] tagRules = Array.Empty<TagRule>();
        [SerializeField] private IdRule[] idRules = Array.Empty<IdRule>();

        public void ConfigureIdRule(
            string statusId,
            bool immune = false,
            float durationMultiplier = 1f)
        {
            if (string.IsNullOrWhiteSpace(statusId))
                return;

            var next = new IdRule[idRules.Length + 1];
            Array.Copy(idRules, next, idRules.Length);
            next[next.Length - 1] = new IdRule
            {
                statusId = statusId,
                immune = immune,
                durationMultiplier = Mathf.Max(0f, durationMultiplier)
            };
            idRules = next;
        }

        public void ConfigureTagRule(
            CombatStatusTags tags,
            bool immune = false,
            float durationMultiplier = 1f)
        {
            if (tags == CombatStatusTags.None)
                return;

            var next = new TagRule[tagRules.Length + 1];
            Array.Copy(tagRules, next, tagRules.Length);
            next[next.Length - 1] = new TagRule
            {
                tags = tags,
                immune = immune,
                durationMultiplier = Mathf.Max(0f, durationMultiplier)
            };
            tagRules = next;
        }

        public void ClearRules()
        {
            tagRules = Array.Empty<TagRule>();
            idRules = Array.Empty<IdRule>();
        }

        public bool TryResolveDuration(CombatStatusDefinition definition, float requestedDuration, out float resolvedDuration)
        {
            resolvedDuration = Mathf.Max(0f, requestedDuration);
            if (definition == null)
                return false;

            var multiplier = 1f;
            for (var i = 0; i < idRules.Length; i++)
            {
                var rule = idRules[i];
                if (rule == null || !string.Equals(rule.statusId, definition.Id, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (rule.immune)
                    return false;
                multiplier *= Mathf.Max(0f, rule.durationMultiplier);
            }

            for (var i = 0; i < tagRules.Length; i++)
            {
                var rule = tagRules[i];
                if (rule == null || rule.tags == CombatStatusTags.None ||
                    (definition.Tags & rule.tags) == 0)
                    continue;
                if (rule.immune)
                    return false;
                multiplier *= Mathf.Max(0f, rule.durationMultiplier);
            }

            resolvedDuration *= multiplier;
            return resolvedDuration > 0.0001f;
        }
    }
}
