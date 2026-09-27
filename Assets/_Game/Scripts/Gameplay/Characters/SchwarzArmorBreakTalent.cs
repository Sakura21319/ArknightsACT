using System;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    /// <summary>
    /// Schwarz E2 first talent adaptation. The proc is evaluated inside the generic damage pipeline
    /// only for tagged physical basic attacks. On proc it applies the shared DefenseDown status
    /// before mitigation, so the triggering shot and following attacks use the same formal DEF layer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SchwarzArmorBreakTalent : MonoBehaviour, IDamageModifier
    {
        [SerializeField, Range(0f, 1f)] private float baseProcChance;
        [SerializeField, Range(0f, 1f)] private float skill2ProcChance;
        [SerializeField, Range(0f, 1f)] private float skill3ProcChance;
        [SerializeField, Min(0f)] private float procAttackMultiplier;
        [SerializeField, Range(0f, 1f)] private float defenseDownPercent;
        [SerializeField, Min(0f)] private float defenseDownDuration;

        private CombatEntity _entity;
        private SchwarzSkill1 _skill2;
        private SchwarzSkill2 _skill3;
        private bool _officialBaseTalentApplied;

        public float CurrentProcChance
        {
            get
            {
                EnsureReferences();
                return _skill3 != null && _skill3.IsBuffActive
                    ? skill3ProcChance
                    : _skill2 != null && _skill2.IsBuffActive
                        ? skill2ProcChance
                        : baseProcChance;
            }
        }

        public event Action<CombatEntity> ProcTriggered;

        public void ConfigureOfficialBaseTalent(
            float procChance,
            float attackMultiplier,
            float defenseDownMagnitude,
            float defenseDownSeconds)
        {
            baseProcChance = Mathf.Clamp01(procChance);
            procAttackMultiplier = Mathf.Max(1f, attackMultiplier);
            defenseDownPercent = Mathf.Clamp01(Mathf.Abs(defenseDownMagnitude));
            defenseDownDuration = Mathf.Max(0f, defenseDownSeconds);
            _officialBaseTalentApplied = true;
        }

        public void SetSkill2ProcChance(float value) =>
            skill2ProcChance = Mathf.Clamp01(value);

        public void SetSkill3ProcChance(float value) =>
            skill3ProcChance = Mathf.Clamp01(value);

        private void Awake() => EnsureReferences();

        private void EnsureReferences()
        {
            if (_entity == null)
                _entity = GetComponent<CombatEntity>();
            if (_skill2 == null)
                _skill2 = GetComponent<SchwarzSkill1>();
            if (_skill3 == null)
                _skill3 = GetComponent<SchwarzSkill2>();
        }

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage)
        {
            EnsureReferences();
            if (!_officialBaseTalentApplied ||
                _entity == null ||
                context.Source != _entity ||
                context.DamageType != DamageType.Physical ||
                (context.Tags & DamageTags.BasicAttack) == 0 ||
                (context.Tags & DamageTags.SecondaryProc) != 0 ||
                context.Target == null ||
                context.Target.Health == null ||
                context.Target.Health.IsDead)
                return currentDamage;

            if (UnityEngine.Random.value > CurrentProcChance)
                return currentDamage;

            context.Target.Status?.Apply(
                CombatStatusIds.DefenseDown,
                duration: defenseDownDuration,
                source: _entity,
                owner: _entity,
                magnitude: defenseDownPercent,
                sourceId: "Schwarz_ArmorBreakTalent");

            ProcTriggered?.Invoke(context.Target);
            return currentDamage * Mathf.Max(1f, procAttackMultiplier);
        }

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) => currentDamage;
    }
}
