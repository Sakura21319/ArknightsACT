using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Combat.Status
{
    [DisallowMultipleComponent]
    public sealed class StatusController : MonoBehaviour, ICombatStatModifier, ICombatActionBlockSource, IDamageModifier
    {
        private readonly Dictionary<string, ActiveStatusInstance> _active =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _expired = new(8);

        private CombatEntity _entity;
        private StatusResistanceProfile _resistance;

        private CombatEntity Entity =>
            _entity != null ? _entity : (_entity = GetComponent<CombatEntity>());

        // Legacy event retained for existing presentation/debug code.
        public event Action<CombatStatusType, float> StatusApplied;
        public event Action<ActiveStatusInstance> StatusAppliedDetailed;
        public event Action<ActiveStatusInstance> StatusRemovedDetailed;

        public CombatActionMask BlockedActions
        {
            get
            {
                var result = CombatActionMask.None;
                foreach (var pair in _active)
                {
                    var instance = pair.Value;
                    if (instance?.Definition != null && instance.ExpiresAt > Time.time)
                        result |= instance.Definition.BlockedActions;
                }
                return result;
            }
        }

        public IReadOnlyCollection<ActiveStatusInstance> ActiveStatuses => _active.Values;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _resistance = GetComponent<StatusResistanceProfile>();
        }

        public void Apply(CombatStatusType type, float duration)
        {
            var id = CombatStatusCatalog.LegacyId(type);
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (Apply(new StatusApplicationContext(id, duration: duration)))
                StatusApplied?.Invoke(type, duration);
        }

        public bool Apply(in StatusApplicationContext context)
        {
            return ApplyInternal(context, allowReaction: true);
        }

        public bool Apply(
            string statusId,
            float duration = 0f,
            CombatEntity source = null,
            CombatEntity owner = null,
            float magnitude = 0f,
            DamageType periodicDamageType = DamageType.Arts,
            int procGeneration = 0,
            string sourceId = "")
        {
            return Apply(new StatusApplicationContext(
                statusId,
                source,
                owner,
                duration,
                1,
                magnitude,
                periodicDamageType,
                procGeneration,
                sourceId));
        }

        public bool Has(CombatStatusType type)
        {
            var id = CombatStatusCatalog.LegacyId(type);
            return !string.IsNullOrWhiteSpace(id) && Has(id);
        }

        public bool Has(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId))
                return false;

            foreach (var pair in _active)
            {
                var instance = pair.Value;
                if (instance == null || instance.ExpiresAt <= Time.time)
                    continue;
                if (string.Equals(instance.Id, statusId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public float Remaining(CombatStatusType type)
        {
            var id = CombatStatusCatalog.LegacyId(type);
            return string.IsNullOrWhiteSpace(id) ? 0f : Remaining(id);
        }

        public float Remaining(string statusId)
        {
            var remaining = 0f;
            foreach (var pair in _active)
            {
                var instance = pair.Value;
                if (instance == null ||
                    !string.Equals(instance.Id, statusId, StringComparison.OrdinalIgnoreCase))
                    continue;
                remaining = Mathf.Max(remaining, instance.ExpiresAt - Time.time);
            }
            return Mathf.Max(0f, remaining);
        }

        public void Remove(CombatStatusType type)
        {
            var id = CombatStatusCatalog.LegacyId(type);
            if (!string.IsNullOrWhiteSpace(id))
                Remove(id);
        }

        public void Remove(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId))
                return;

            _expired.Clear();
            foreach (var pair in _active)
            {
                if (pair.Value != null &&
                    string.Equals(pair.Value.Id, statusId, StringComparison.OrdinalIgnoreCase))
                    _expired.Add(pair.Key);
            }

            for (var i = 0; i < _expired.Count; i++)
                RemoveByKey(_expired[i]);
        }

        public int RemoveByTags(CombatStatusTags tags)
        {
            if (tags == CombatStatusTags.None)
                return 0;

            _expired.Clear();
            foreach (var pair in _active)
            {
                var definition = pair.Value?.Definition;
                if (definition != null && (definition.Tags & tags) != 0)
                    _expired.Add(pair.Key);
            }

            for (var i = 0; i < _expired.Count; i++)
                RemoveByKey(_expired[i]);
            return _expired.Count;
        }

        public void ClearAll()
        {
            _expired.Clear();
            _expired.AddRange(_active.Keys);
            for (var i = 0; i < _expired.Count; i++)
                RemoveByKey(_expired[i]);
        }

        public void AccumulateStatModifiers(CombatStatType stat, ref float flat, ref float additivePercent)
        {
            foreach (var pair in _active)
            {
                var instance = pair.Value;
                var definition = instance?.Definition;
                if (definition == null || instance.ExpiresAt <= Time.time)
                    continue;

                var modifiers = definition.StatModifiers;
                for (var i = 0; i < modifiers.Count; i++)
                {
                    var modifier = modifiers[i];
                    if (modifier.Stat != stat)
                        continue;
                    var magnitudeScale = modifier.ScaleByApplicationMagnitude
                        ? instance.Magnitude
                        : 1f;
                    var stackScale = Mathf.Max(1, instance.Stacks);
                    flat += modifier.Flat * magnitudeScale * stackScale;
                    additivePercent += modifier.AdditivePercent * magnitudeScale * stackScale;
                }
            }
        }

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage) =>
            ApplyDamageModifiers(StatusDamageModifierDirection.Outgoing, context, currentDamage);

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) =>
            ApplyDamageModifiers(StatusDamageModifierDirection.Incoming, context, currentDamage);

        private float ApplyDamageModifiers(
            StatusDamageModifierDirection direction,
            in DamageContext context,
            float currentDamage)
        {
            var additivePercent = 0f;
            foreach (var pair in _active)
            {
                var instance = pair.Value;
                var definition = instance?.Definition;
                if (definition == null || instance.ExpiresAt <= Time.time)
                    continue;

                var modifiers = definition.DamageModifiers;
                for (var i = 0; i < modifiers.Count; i++)
                {
                    var modifier = modifiers[i];
                    if (modifier.Direction != direction || !modifier.Matches(context.DamageType))
                        continue;

                    var magnitudeScale = modifier.ScaleByApplicationMagnitude
                        ? instance.Magnitude
                        : 1f;
                    additivePercent += modifier.AdditivePercent *
                                       magnitudeScale *
                                       Mathf.Max(1, instance.Stacks);
                }
            }

            return currentDamage * Mathf.Max(0f, 1f + additivePercent);
        }

        private bool ApplyInternal(in StatusApplicationContext context, bool allowReaction)
        {
            var entity = Entity;
            if (entity?.Health != null && entity.Health.IsDead)
                return false;
            if (!CombatStatusCatalog.TryGet(context.StatusId, out var definition))
                return false;

            if (allowReaction && TryApplyReaction(context))
                return true;

            var duration = context.Duration > 0f ? context.Duration : definition.DefaultDuration;
            if (_resistance == null)
                _resistance = GetComponent<StatusResistanceProfile>();
            if (_resistance != null && !_resistance.TryResolveDuration(definition, duration, out duration))
                return false;
            if (duration <= 0f)
                return false;

            var key = BuildKey(definition, context.Source);
            var now = Time.time;
            var expiresAt = now + duration;

            if (!_active.TryGetValue(key, out var instance) || instance == null || instance.ExpiresAt <= now)
            {
                instance = new ActiveStatusInstance
                {
                    Definition = definition,
                    Source = context.Source,
                    Owner = context.Owner,
                    Stacks = Mathf.Clamp(context.Stacks, 1, definition.MaxStacks),
                    Magnitude = context.Magnitude,
                    ExpiresAt = expiresAt,
                    NextTickAt = definition.TickInterval > 0f ? now + definition.TickInterval : float.PositiveInfinity,
                    PeriodicDamageType = context.PeriodicDamageType,
                    ProcGeneration = context.ProcGeneration,
                    SourceId = context.SourceId
                };
                _active[key] = instance;
            }
            else
            {
                RefreshInstance(instance, definition, context, duration, expiresAt);
            }

            if (definition.InterruptActions != CombatActionMask.None)
                CombatActionUtility.Interrupt(entity, definition.InterruptActions);

            StatusAppliedDetailed?.Invoke(instance);
            return true;
        }

        public void CopyActiveTo(StatusController target)
        {
            if (target == null || target == this)
                return;

            target.ClearAll();
            var now = Time.time;
            var sourceEntity = Entity;
            var targetEntity = target.Entity;

            foreach (var pair in _active)
            {
                var instance = pair.Value;
                if (instance?.Definition == null || instance.ExpiresAt <= now)
                    continue;

                var remappedSource = instance.Source == sourceEntity ? targetEntity : instance.Source;
                var remappedOwner = instance.Owner == sourceEntity ? targetEntity : instance.Owner;
                var clone = new ActiveStatusInstance
                {
                    Definition = instance.Definition,
                    Source = remappedSource,
                    Owner = remappedOwner,
                    Stacks = instance.Stacks,
                    Magnitude = instance.Magnitude,
                    ExpiresAt = instance.ExpiresAt,
                    NextTickAt = instance.NextTickAt,
                    PeriodicDamageType = instance.PeriodicDamageType,
                    ProcGeneration = instance.ProcGeneration,
                    SourceId = instance.SourceId
                };

                var key = BuildKey(clone.Definition, clone.Source);
                target._active[key] = clone;
                target.StatusAppliedDetailed?.Invoke(clone);
            }
        }

        private bool TryApplyReaction(in StatusApplicationContext incoming)
        {
            var matched = false;
            var existingId = string.Empty;
            StatusReactionRule reaction = default;

            foreach (var pair in _active)
            {
                var existing = pair.Value;
                if (existing == null || existing.ExpiresAt <= Time.time)
                    continue;
                if (!CombatStatusCatalog.TryResolveReaction(existing.Id, incoming.StatusId, out reaction))
                    continue;

                matched = true;
                existingId = existing.Id;
                break;
            }

            if (!matched)
                return false;

            var reacted = new StatusApplicationContext(
                reaction.ResultId,
                incoming.Source,
                incoming.Owner,
                duration: 0f,
                stacks: 1,
                magnitude: incoming.Magnitude,
                periodicDamageType: incoming.PeriodicDamageType,
                procGeneration: incoming.ProcGeneration,
                sourceId: incoming.SourceId);

            if (!ApplyInternal(reacted, allowReaction: false))
                return false;

            if (reaction.ConsumeExisting)
                Remove(existingId);
            return true;
        }

        private static string BuildKey(CombatStatusDefinition definition, CombatEntity source)
        {
            if (definition.StackPolicy != CombatStatusStackPolicy.IndependentBySource)
                return definition.Id;
            return definition.Id + "@" + (source != null ? source.EntityId : "world");
        }

        private static void RefreshInstance(
            ActiveStatusInstance instance,
            CombatStatusDefinition definition,
            in StatusApplicationContext context,
            float duration,
            float expiresAt)
        {
            switch (definition.StackPolicy)
            {
                case CombatStatusStackPolicy.ExtendDuration:
                    instance.ExpiresAt = Mathf.Max(instance.ExpiresAt, Time.time) + duration;
                    break;
                case CombatStatusStackPolicy.StackAndRefresh:
                    instance.Stacks = Mathf.Clamp(
                        instance.Stacks + context.Stacks,
                        1,
                        definition.MaxStacks);
                    instance.ExpiresAt = expiresAt;
                    break;
                case CombatStatusStackPolicy.ReplaceIfStronger:
                    if (context.Magnitude + 0.0001f >= instance.Magnitude)
                    {
                        instance.Magnitude = context.Magnitude;
                        instance.Source = context.Source;
                        instance.Owner = context.Owner;
                        instance.PeriodicDamageType = context.PeriodicDamageType;
                        instance.ProcGeneration = context.ProcGeneration;
                        instance.SourceId = context.SourceId;
                    }
                    instance.ExpiresAt = Mathf.Max(instance.ExpiresAt, expiresAt);
                    break;
                default:
                    instance.ExpiresAt = Mathf.Max(instance.ExpiresAt, expiresAt);
                    break;
            }

            if (definition.TickInterval > 0f && float.IsPositiveInfinity(instance.NextTickAt))
                instance.NextTickAt = Time.time + definition.TickInterval;
        }

        private void Update()
        {
            if (_active.Count == 0)
                return;

            var now = Time.time;
            _expired.Clear();

            foreach (var pair in _active)
            {
                var instance = pair.Value;
                if (instance == null || instance.ExpiresAt <= now)
                {
                    _expired.Add(pair.Key);
                    continue;
                }

                TickPeriodic(instance, now);
            }

            for (var i = 0; i < _expired.Count; i++)
                RemoveByKey(_expired[i]);
        }

        private void TickPeriodic(ActiveStatusInstance instance, float now)
        {
            var definition = instance.Definition;
            if (definition == null || definition.TickInterval <= 0f || now < instance.NextTickAt)
                return;

            // Catch up at most one tick per frame to avoid a hitch creating a burst of proc damage.
            instance.NextTickAt = now + definition.TickInterval;
            var damage = definition.PeriodicFlatDamage;
            if (definition.PeriodicUsesApplicationMagnitude)
                damage += instance.Magnitude;
            damage *= Mathf.Max(1, instance.Stacks);
            if (damage <= 0f)
                return;

            var entity = Entity;
            if (entity == null)
                return;

            DamageSystem.Apply(new DamageContext(
                instance.Source,
                instance.Owner,
                entity,
                damage,
                instance.PeriodicDamageType,
                Vector2.zero,
                instance.ProcGeneration + 1,
                string.IsNullOrWhiteSpace(instance.SourceId) ? "Status_" + definition.Id : instance.SourceId,
                tags: DamageTags.DamageOverTime | DamageTags.SecondaryProc));
        }

        private void RemoveByKey(string key)
        {
            if (!_active.TryGetValue(key, out var instance))
                return;
            _active.Remove(key);
            if (instance != null)
                StatusRemovedDetailed?.Invoke(instance);
        }
    }
}
