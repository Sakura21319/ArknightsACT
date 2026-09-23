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
            float artsResistance = 0f)
        {
            var health = go.GetComponent<Health>() ?? go.AddComponent<Health>();
            health.SetMaxHealth(maxHealth);

            if (go.GetComponent<StatusController>() == null)
                go.AddComponent<StatusController>();

            var entity = go.GetComponent<CombatEntity>() ?? go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);

            var stats = go.GetComponent<CombatStats>() ?? go.AddComponent<CombatStats>();
            stats.SetBasePhysicalDefense(physicalDefense);
            stats.SetBaseArtsResistance(artsResistance);

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
