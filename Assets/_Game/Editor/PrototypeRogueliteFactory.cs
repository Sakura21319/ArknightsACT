#if UNITY_EDITOR
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Rooms;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypeRogueliteFactory
    {
        private const string DataRoot = "Assets/_Game/Data/Roguelite";
        private const string CollectibleDir = DataRoot + "/Collectibles";

        public static void Create(PrototypeRoomLoopController roomLoop, Transform player)
        {
            if (roomLoop == null || player == null)
                return;

            EnsureFolder(CollectibleDir);
            var pool = BuildCollectiblePool();
            var inventory = player.GetComponent<CollectibleInventory>();
            var profile = player.GetComponent<PlayerCombatProfile>();
            if (inventory == null || profile == null)
            {
                Debug.LogError(
                    "[ArknightsACT/Roguelite] Player is missing CollectibleInventory or PlayerCombatProfile.",
                    player);
                return;
            }

            roomLoop.SetExternalContinueGate(true);
            var root = new GameObject("[Roguelite]");
            var runState = root.AddComponent<RogueliteRunState>();
            var rewards = root.AddComponent<RogueliteRewardController>();
            rewards.Configure(inventory, profile, pool);
            var routes = root.AddComponent<RogueliteRouteController>();
            routes.Configure(roomLoop, rewards, runState, player);
        }

        private static CollectibleDefinition[] BuildCollectiblePool()
        {
            return new[]
            {
                GetOrCreate(
                    "tactical_calibration",
                    "战术校准芯片",
                    "所有造成的伤害 +8%。",
                    CollectibleRarity.Common,
                    CombatFeature.None,
                    CollectibleEffectType.AllDamagePercent,
                    0.08f,
                    3),
                GetOrCreate(
                    "edge_alignment",
                    "锋刃校准器",
                    "物理伤害 +15%。",
                    CollectibleRarity.Common,
                    CombatFeature.PhysicalDamage,
                    CollectibleEffectType.PhysicalDamagePercent,
                    0.15f,
                    3),
                GetOrCreate(
                    "arts_resonator",
                    "术式共振器",
                    "法术伤害 +18%。",
                    CollectibleRarity.Common,
                    CombatFeature.ArtsDamage,
                    CollectibleEffectType.ArtsDamagePercent,
                    0.18f,
                    3),
                GetOrCreate(
                    "purified_originium_core",
                    "纯化源石核心",
                    "真实伤害 +12%。",
                    CollectibleRarity.Rare,
                    CombatFeature.TrueDamage,
                    CollectibleEffectType.TrueDamagePercent,
                    0.12f,
                    3),
                GetOrCreate(
                    "reinforced_field_suit",
                    "强化防护服",
                    "最大生命值 +15%，并补足新增的生命上限。",
                    CollectibleRarity.Common,
                    CombatFeature.None,
                    CollectibleEffectType.MaxHealthPercent,
                    0.15f,
                    3),
                GetOrCreate(
                    "sp_battery",
                    "技力电池",
                    "普攻每命中一个敌人，使所有主动技能冷却减少 0.12 秒。",
                    CollectibleRarity.Rare,
                    CombatFeature.BasicAttack | CombatFeature.ActiveSkills,
                    CollectibleEffectType.CooldownOnBasicHitSeconds,
                    0.12f,
                    3),
                GetOrCreate(
                    "medical_drone_module",
                    "医疗无人机模块",
                    "成功释放主动技能后，恢复 2.5% 最大生命值。",
                    CollectibleRarity.Rare,
                    CombatFeature.ActiveSkills,
                    CollectibleEffectType.HealOnSkillCastFraction,
                    0.025f,
                    3),
                GetOrCreate(
                    "overclocked_sp_module",
                    "过载技力模块",
                    "成功释放主动技能后，使所有主动技能冷却额外减少 0.60 秒。",
                    CollectibleRarity.Epic,
                    CombatFeature.ActiveSkills,
                    CollectibleEffectType.CooldownOnSkillCastSeconds,
                    0.60f,
                    2)
            };
        }

        private static CollectibleDefinition GetOrCreate(
            string id,
            string displayName,
            string description,
            CollectibleRarity rarity,
            CombatFeature requirements,
            CollectibleEffectType effectType,
            float value,
            int maxStacks)
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
