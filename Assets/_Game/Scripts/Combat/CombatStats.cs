using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatStats : MonoBehaviour
    {
        private readonly List<MonoBehaviour> _componentBuffer = new(16);
        [Header("Mitigation")]
        [SerializeField, Min(0f)] private float physicalDefense;
        [SerializeField] private float artsResistance;

        public float BasePhysicalDefense => Mathf.Max(0f, physicalDefense);
        public float BaseArtsResistance => artsResistance;

        public float PhysicalDefense => Mathf.Max(0f, Resolve(CombatStatType.PhysicalDefense, BasePhysicalDefense));
        public float ArtsResistance => Mathf.Clamp(Resolve(CombatStatType.ArtsResistance, BaseArtsResistance), -100f, 95f);
        public float MoveSpeedMultiplier => Mathf.Max(0f, Resolve(CombatStatType.MoveSpeedMultiplier, 1f));
        public float AttackSpeedMultiplier => Mathf.Max(0.05f, Resolve(CombatStatType.AttackSpeedMultiplier, 1f));

        public void SetBasePhysicalDefense(float value) => physicalDefense = Mathf.Max(0f, value);
        public void SetBaseArtsResistance(float value) => artsResistance = value;

        public float Resolve(CombatStatType stat, float baseValue)
        {
            _componentBuffer.Clear();
            GetComponents(_componentBuffer);

            var value = baseValue;
            value = ResolveLayer(stat, value, CombatStatModifierLayer.MetaProgression);
            value = ResolveLayer(stat, value, CombatStatModifierLayer.RunPermanent);
            value = ResolveLayer(stat, value, CombatStatModifierLayer.CollectibleEquipment);
            value = ResolveLayer(stat, value, CombatStatModifierLayer.Temporary);
            return value;
        }

        private float ResolveLayer(
            CombatStatType stat,
            float currentValue,
            CombatStatModifierLayer layer)
        {
            var flat = 0f;
            var additivePercent = 0f;

            for (var i = 0; i < _componentBuffer.Count; i++)
            {
                if (_componentBuffer[i] is not ICombatStatModifier modifier)
                    continue;

                if (modifier is ILayeredCombatStatModifier layered)
                {
                    if (layered.ModifierLayer != layer)
                        continue;
                }
                else if (layer != CombatStatModifierLayer.Temporary)
                {
                    // Compatibility: pre-P6 modifiers preserve behavior at the final temporary stage.
                    continue;
                }

                modifier.AccumulateStatModifiers(stat, ref flat, ref additivePercent);
            }

            return (currentValue + flat) * Mathf.Max(0f, 1f + additivePercent);
        }
    }
}
