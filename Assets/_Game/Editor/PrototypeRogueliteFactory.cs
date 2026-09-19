#if UNITY_EDITOR
using ArknightsACT.Gameplay.Characters.Chen;
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Progression;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.Shop;
using ArknightsACT.Gameplay.Roguelite.SkillUpgrades;
using ArknightsACT.Gameplay.Roguelite.World;
using ArknightsACT.Gameplay.Rooms;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypeRogueliteFactory
    {
        private const string DataRoot = "Assets/_Game/Data/Roguelite";
        private const string CollectibleDir = DataRoot + "/Collectibles";
        private const string UpgradeDir = DataRoot + "/LevelUpgrades";
        private const string SkillUpgradeDir = DataRoot + "/SkillUpgrades/Chen";

        public static GameObject CreateExploration(Transform player)
        {
            return CreateInternal(null, player, false);
        }

        public static GameObject Create(PrototypeRoomLoopController roomLoop, Transform player)
        {
            return CreateInternal(roomLoop, player, true);
        }

        private static GameObject CreateInternal(PrototypeRoomLoopController roomLoop, Transform player, bool createLegacyRoute)
        {
            if (player == null || (createLegacyRoute && roomLoop == null))
                return null;

            EnsureFolder(CollectibleDir);
            EnsureFolder(UpgradeDir);
            EnsureFolder(SkillUpgradeDir);
            var collectiblePool = BuildCollectiblePool();
            var upgradePool = BuildLevelUpgradePool();
            var skillUpgradePool = BuildChenSkillUpgradePool();

            var inventory = player.GetComponent<CollectibleInventory>();
            var upgradeInventory = player.GetComponent<LevelUpgradeInventory>();
            var skillUpgradeInventory = player.GetComponent<CharacterSkillUpgradeInventory>();
            var profile = player.GetComponent<PlayerCombatProfile>();
            if (inventory == null || upgradeInventory == null || skillUpgradeInventory == null || profile == null)
            {
                Debug.LogError(
                    "[ArknightsACT/Roguelite] Player is missing progression inventory/profile components.",
                    player);
                return null;
            }

            if (roomLoop != null)
                roomLoop.SetExternalContinueGate(true);

            var root = new GameObject("[Roguelite]");
            root.SetActive(false);

            root.AddComponent<RewardSelectionCoordinator>();
            var runState = root.AddComponent<RogueliteRunState>();
            var stageMap = root.AddComponent<RogueliteStageMapController>();
            stageMap.Configure(runState);

            var rewards = root.AddComponent<RogueliteRewardController>();
            rewards.Configure(inventory, profile, collectiblePool);

            var levelUps = root.AddComponent<LevelUpRewardController>();
            levelUps.Configure(runState, upgradeInventory, profile, upgradePool, player);

            var skillRewards = root.AddComponent<CharacterSkillUpgradeRewardController>();
            skillRewards.Configure(skillUpgradeInventory, skillUpgradePool);

            var shop = root.AddComponent<RogueliteShopController>();
            shop.Configure(
                player,
                runState,
                stageMap,
                inventory,
                upgradeInventory,
                skillUpgradeInventory,
                profile,
                collectiblePool,
                upgradePool,
                skillUpgradePool);

            var environment = root.AddComponent<RogueliteStageEnvironmentController>();
            environment.Configure(player, runState, stageMap);

            var hud = root.AddComponent<RogueliteProgressHUD>();
            hud.Configure(runState);

            if (createLegacyRoute && roomLoop != null)
            {
                var routes = root.AddComponent<RogueliteRouteController>();
                routes.Configure(roomLoop, rewards, runState, player);
            }

            root.SetActive(true);
            return root;
        }

        private static CollectibleDefinition[] BuildCollectiblePool()
        {
            return new[]
            {
                GetOrCreateCollectible("tactical_calibration", "战术校准芯片", "所有造成的伤害 +8%。", CollectibleRarity.Common, CombatFeature.None, CollectibleEffectType.AllDamagePercent, 0.08f, 3),
                GetOrCreateCollectible("edge_alignment", "锋刃校准器", "物理伤害 +15%。", CollectibleRarity.Common, CombatFeature.PhysicalDamage, CollectibleEffectType.PhysicalDamagePercent, 0.15f, 3),
                GetOrCreateCollectible("arts_resonator", "术式共振器", "法术伤害 +18%。", CollectibleRarity.Common, CombatFeature.ArtsDamage, CollectibleEffectType.ArtsDamagePercent, 0.18f, 3),
                GetOrCreateCollectible("purified_originium_core", "纯化源石核心", "真实伤害 +12%。", CollectibleRarity.Rare, CombatFeature.TrueDamage, CollectibleEffectType.TrueDamagePercent, 0.12f, 3),
                GetOrCreateCollectible("reinforced_field_suit", "强化防护服", "最大生命值 +15%，并补足新增的生命上限。", CollectibleRarity.Common, CombatFeature.None, CollectibleEffectType.MaxHealthPercent, 0.15f, 3),
                GetOrCreateCollectible("sp_battery", "技力电池", "普攻每命中一个敌人，使所有主动技能冷却减少 0.12 秒。", CollectibleRarity.Rare, CombatFeature.BasicAttack | CombatFeature.ActiveSkills, CollectibleEffectType.CooldownOnBasicHitSeconds, 0.12f, 3),
                GetOrCreateCollectible("medical_drone_module", "医疗无人机模块", "成功释放主动技能后，恢复 2.5% 最大生命值。", CollectibleRarity.Rare, CombatFeature.ActiveSkills, CollectibleEffectType.HealOnSkillCastFraction, 0.025f, 3),
                GetOrCreateCollectible("overclocked_sp_module", "过载技力模块", "成功释放主动技能后，使所有主动技能冷却额外减少 0.60 秒。", CollectibleRarity.Epic, CombatFeature.ActiveSkills, CollectibleEffectType.CooldownOnSkillCastSeconds, 0.60f, 2)
            };
        }

        private static LevelUpgradeDefinition[] BuildLevelUpgradePool()
        {
            return new[]
            {
                GetOrCreateUpgrade("level_damage_training", "战斗校准", "所有伤害 +7%。最多强化 3 次。", CombatFeature.None, LevelUpgradeEffectType.AllDamagePercent, 0.07f, 3, 1.0f),
                GetOrCreateUpgrade("level_physical_training", "物理攻击强化", "物理伤害 +12%。最多强化 3 次。", CombatFeature.PhysicalDamage, LevelUpgradeEffectType.PhysicalDamagePercent, 0.12f, 3, 1.15f),
                GetOrCreateUpgrade("level_arts_training", "法术攻击强化", "Arts 伤害 +12%。最多强化 3 次。", CombatFeature.ArtsDamage, LevelUpgradeEffectType.ArtsDamagePercent, 0.12f, 3, 1.15f),
                GetOrCreateUpgrade("level_burn_module", "灼烧模块", "直接伤害有概率点燃目标，造成 3 次 Arts 灼烧伤害；升级会提高触发率和每跳伤害。", CombatFeature.None, LevelUpgradeEffectType.BurnOnHit, 0.08f, 3, 0.78f),
                GetOrCreateUpgrade("level_chain_module", "连锁导体", "连续命中会向附近另一名敌人释放 Arts 电弧；升级后触发更频繁、伤害更高。", CombatFeature.None, LevelUpgradeEffectType.ChainLightning, 0.35f, 3, 0.78f)
            };
        }

        private static CharacterSkillUpgradeDefinition[] BuildChenSkillUpgradePool()
        {
            return new[]
            {
                GetOrCreateSkillUpgrade("chen_s1_reach", ChenSkillUpgradeApplier.Skill1Range, "拔刀·延展", "赤霄·拔刀攻击距离 +25%。", 0.25f, 2),
                GetOrCreateSkillUpgrade("chen_s1_power", ChenSkillUpgradeApplier.Skill1Damage, "拔刀·断势", "赤霄·拔刀的物理与 Arts 伤害 +20%。", 0.20f, 3),
                GetOrCreateSkillUpgrade("chen_s1_cycle", ChenSkillUpgradeApplier.Skill1Cooldown, "拔刀·迅捷", "赤霄·拔刀基础冷却 -15%。", 0.15f, 2),
                GetOrCreateSkillUpgrade("chen_s2_strikes", ChenSkillUpgradeApplier.Skill2ExtraStrikes, "绝影·九闪", "赤霄·绝影额外增加 2 次斩击。", 2f, 3),
                GetOrCreateSkillUpgrade("chen_s2_finisher", ChenSkillUpgradeApplier.Skill2FinalDamage, "绝影·收刀", "赤霄·绝影终结斩伤害 +35%。", 0.35f, 3),
                GetOrCreateSkillUpgrade("chen_s2_hunt", ChenSkillUpgradeApplier.Skill2Radius, "绝影·逐猎", "赤霄·绝影索敌半径 +20%。", 0.20f, 2)
            };
        }

        private static CollectibleDefinition GetOrCreateCollectible(string id, string displayName, string description, CollectibleRarity rarity, CombatFeature requirements, CollectibleEffectType effectType, float value, int maxStacks)
        {
            var path = $"{CollectibleDir}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CollectibleDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CollectibleDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(id, displayName, description, rarity, requirements, effectType, value, maxStacks);
            asset.name = id;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static LevelUpgradeDefinition GetOrCreateUpgrade(string id, string displayName, string description, CombatFeature requirements, LevelUpgradeEffectType effectType, float value, int maxStacks, float rewardWeight)
        {
            var path = $"{UpgradeDir}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<LevelUpgradeDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LevelUpgradeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(id, displayName, description, requirements, effectType, value, maxStacks, rewardWeight);
            asset.name = id;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static CharacterSkillUpgradeDefinition GetOrCreateSkillUpgrade(string id, string effectId, string displayName, string description, float value, int maxStacks)
        {
            var path = $"{SkillUpgradeDir}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CharacterSkillUpgradeDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CharacterSkillUpgradeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(id, ChenSkillUpgradeApplier.CharacterKey, effectId, displayName, description, value, maxStacks, 1f);
            asset.name = id;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
