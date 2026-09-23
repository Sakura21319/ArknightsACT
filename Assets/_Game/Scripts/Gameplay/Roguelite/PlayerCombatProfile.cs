using System;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite
{
    public enum OperatorProfession { Unspecified, Vanguard, Guard, Defender, Sniper, Caster, Supporter, Medic, Specialist }
    [Flags]
    public enum CombatFeature
    {
        None = 0,
        BasicAttack = 1 << 0,
        ActiveSkills = 1 << 1,
        Dash = 1 << 2,
        PhysicalDamage = 1 << 3,
        ArtsDamage = 1 << 4,
        TrueDamage = 1 << 5
    }

    /// <summary>
    /// Declares what a playable character can actually use. Reward generation uses this profile
    /// to avoid dead choices without ever checking a concrete operator type or name.
    /// </summary>
    public sealed class PlayerCombatProfile : MonoBehaviour
    {
        [SerializeField] private CombatFeature features = CombatFeature.BasicAttack | CombatFeature.ActiveSkills;
        [SerializeField] private OperatorProfession profession;
        [Header("Prototype mitigation")]
        [SerializeField, Min(0f)] private float basePhysicalDefense = 1f;
        [SerializeField] private float baseArtsResistance = 0f;
        public OperatorProfession Profession => profession;
        public void SetProfession(OperatorProfession value) => profession = value;

        public CombatFeature Features => features;

        private void Awake()
        {
            var stats = GetComponent<CombatStats>();
            if (stats == null)
                stats = gameObject.AddComponent<CombatStats>();
            stats.SetBasePhysicalDefense(basePhysicalDefense);
            stats.SetBaseArtsResistance(baseArtsResistance);
        }

        public void Configure(CombatFeature value) => features = value;

        public void ConfigureMitigation(float physicalDefense, float artsResistance)
        {
            basePhysicalDefense = Mathf.Max(0f, physicalDefense);
            baseArtsResistance = artsResistance;

            var stats = GetComponent<CombatStats>();
            if (stats == null)
                stats = gameObject.AddComponent<CombatStats>();
            stats.SetBasePhysicalDefense(basePhysicalDefense);
            stats.SetBaseArtsResistance(baseArtsResistance);
        }

        public bool Supports(CombatFeature required) =>
            required == CombatFeature.None || (features & required) == required;
    }
}
