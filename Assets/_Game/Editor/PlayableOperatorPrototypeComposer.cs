#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Progression;
using ArknightsACT.Gameplay.Roguelite.SkillUpgrades;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Common operator composition used by every prototype character factory.
    /// Character factories should only add their own attacks, skills, presentation and data profiles.
    /// </summary>
    internal static class PlayableOperatorPrototypeComposer
    {
        public static void AddFoundation(
            GameObject go,
            AttackDefinition[] attacks,
            float baseAttackDamage,
            OperatorProfession profession,
            CombatFeature features,
            string operatorId,
            string displayName,
            string skinId,
            string avatarResourceKey,
            string skill1IconResourceKey,
            string skill2IconResourceKey,
            float maxHealth = 100f,
            float physicalDefense = 1f,
            float artsResistance = 0f,
            float attackInterval = 1f,
            float basicAttackRange = 1f,
            float skillRange = 1f)
        {
            if (go.GetComponent<Health>() == null)
                go.AddComponent<Health>();

            if (go.GetComponent<StatusController>() == null)
                go.AddComponent<StatusController>();

            var entity = go.GetComponent<CombatEntity>() ?? go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);

            // One authoritative base-stat component owns player HP/ATK/DEF/RES. CombatStats remains
            // the shared modifier resolver used by statuses, relics and future in-run progression.
            var runtimeStats = go.GetComponent<OperatorRuntimeStats>() ?? go.AddComponent<OperatorRuntimeStats>();
            runtimeStats.ConfigureBase(
                maxHealth,
                baseAttackDamage,
                physicalDefense,
                artsResistance,
                attackInterval,
                basicAttackRange,
                skillRange);

            if (go.GetComponent<PlayerInputReader>() == null)
                go.AddComponent<PlayerInputReader>();
            if (go.GetComponent<PlayerDashController>() == null)
                go.AddComponent<PlayerDashController>();

            var attack = go.GetComponent<PlayerAttackController>() ?? go.AddComponent<PlayerAttackController>();
            attack.Configure(attacks, baseAttackDamage);

            if (go.GetComponent<PlayerDamageGate>() == null)
                go.AddComponent<PlayerDamageGate>();

            var profile = go.GetComponent<PlayerCombatProfile>() ?? go.AddComponent<PlayerCombatProfile>();
            profile.SetProfession(profession);
            profile.Configure(features);
            profile.ConfigureMitigation(physicalDefense, artsResistance);

            var identity = go.GetComponent<PlayableOperatorIdentity>() ?? go.AddComponent<PlayableOperatorIdentity>();
            identity.Configure(
                operatorId,
                displayName,
                skinId,
                avatarResourceKey,
                skill1IconResourceKey,
                skill2IconResourceKey);
        }

        public static void ApplyFormalCombatProfile(
            GameObject go,
            float maxHealth,
            float physicalDefense,
            float artsResistance)
        {
            if (go == null)
                return;

            var attack = go.GetComponent<PlayerAttackController>();
            var configuredAttack = attack != null ? attack.ConfiguredBaseAttack : 10f;
            ApplyFormalCombatProfile(
                go,
                new OperatorBaseStats(
                    maxHealth,
                    configuredAttack,
                    physicalDefense,
                    artsResistance));
        }

        public static void ApplyFormalCombatProfile(GameObject go, OperatorBaseStats baseStats)
        {
            if (go == null)
                return;

            var runtimeStats = go.GetComponent<OperatorRuntimeStats>() ?? go.AddComponent<OperatorRuntimeStats>();
            runtimeStats.ConfigureBasePreserveHealthRatio(baseStats);

            var profile = go.GetComponent<PlayerCombatProfile>() ?? go.AddComponent<PlayerCombatProfile>();
            profile.ConfigureMitigation(baseStats.PhysicalDefense, baseStats.ArtsResistance);
        }

        public static void CompleteGameplay(GameObject go)
        {
            if (go.GetComponent<PlayerSkillController>() == null)
                go.AddComponent<PlayerSkillController>();
            if (go.GetComponent<CollectibleInventory>() == null)
                go.AddComponent<CollectibleInventory>();
            if (go.GetComponent<LevelUpgradeInventory>() == null)
                go.AddComponent<LevelUpgradeInventory>();
            if (go.GetComponent<CharacterSkillUpgradeInventory>() == null)
                go.AddComponent<CharacterSkillUpgradeInventory>();
            if (go.GetComponent<TemporaryCombatBuffs>() == null)
                go.AddComponent<TemporaryCombatBuffs>();
        }
    }
}
#endif
