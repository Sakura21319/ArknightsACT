#if UNITY_EDITOR
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsACT.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string DataDir = "Assets/_Game/Data/Attacks/Chen";
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string ScenePath = SceneDir + "/PrototypeRun.unity";
        private const float PlayerSpawnY = -0.34f;

        [MenuItem("ArknightsACT/Build Prototype Scene")]
        public static void Build()
        {
            PrototypePlayerSettings.Apply();
            EnsureFolder(DataDir);
            EnsureFolder(SceneDir);
            var attacks = BuildChenPlaceholderAttackDefinitions();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PrototypeFactory.CreateServices();
            PrototypeBackdropFactory.Create();
            var player = ChenPrototypePlayerFactory.Create(attacks, PlayerSpawnY);

            PrototypeFactory.CreateFloor();
            PrototypeFactory.CreateWorldBounds();
            PrototypeFactory.CreatePlatform(new Vector2(5f, 1.5f), new Vector2(4f, 0.35f));
            PrototypeFactory.CreatePlatform(new Vector2(11f, 2.6f), new Vector2(3f, 0.35f));

            var enemies = PrtsPrototypeAssetCatalog.PrototypeEnemies;
            var enemyTemplates = PrototypeFactory.CreateEnemyTemplates(enemies);
            PrototypeFactory.CreateRoomLoop(player.transform, enemyTemplates);
            PrototypeFactory.CreateCamera(player.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);
            Debug.Log(
                $"ArknightsACT 2D ACT prototype generated: {ScenePath}. " +
                "Player is now Ch'en. Texas-specific skill/build components are intentionally not attached. " +
                "Current attack data is a temporary gameplay shell until Ch'en's authored PRTS clips are catalogued.");
        }

        private static AttackDefinition[] BuildChenPlaceholderAttackDefinitions()
        {
            // Temporary collision/timing shell only. Do not treat these as Ch'en's final combo.
            // The next step is to inspect her complete authored PRTS animation catalog and map
            // gameplay actions to real clips before tuning startup/recovery/hit timing.
            return new[]
            {
                GetOrCreateAttack(
                    "Chen_Basic_1_Placeholder",
                    startup: 0.095f,
                    active: 0.030f,
                    recovery: 0.055f,
                    damageMultiplier: 1.00f,
                    hitboxOffset: new Vector2(0.90f, 0.02f),
                    hitboxSize: new Vector2(1.40f, 1.10f),
                    knockback: new Vector2(2.2f, 0.45f),
                    dashCancel: 0.34f,
                    hitStop: 0.018f,
                    shake: 0.045f),
                GetOrCreateAttack(
                    "Chen_Basic_2_Placeholder",
                    startup: 0.095f,
                    active: 0.030f,
                    recovery: 0.055f,
                    damageMultiplier: 1.00f,
                    hitboxOffset: new Vector2(0.90f, 0.02f),
                    hitboxSize: new Vector2(1.40f, 1.10f),
                    knockback: new Vector2(2.2f, 0.45f),
                    dashCancel: 0.34f,
                    hitStop: 0.018f,
                    shake: 0.045f),
                GetOrCreateAttack(
                    "Chen_Basic_3_Placeholder",
                    startup: 0.095f,
                    active: 0.030f,
                    recovery: 0.055f,
                    damageMultiplier: 1.00f,
                    hitboxOffset: new Vector2(0.90f, 0.02f),
                    hitboxSize: new Vector2(1.40f, 1.10f),
                    knockback: new Vector2(2.2f, 0.45f),
                    dashCancel: 0.34f,
                    hitStop: 0.018f,
                    shake: 0.045f)
            };
        }

        private static AttackDefinition GetOrCreateAttack(
            string name,
            float startup,
            float active,
            float recovery,
            float damageMultiplier,
            Vector2 hitboxOffset,
            Vector2 hitboxSize,
            Vector2 knockback,
            float dashCancel,
            float hitStop = 0.035f,
            float shake = 0.07f)
        {
            var path = $"{DataDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AttackDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.name = name;
            asset.startup = startup;
            asset.active = active;
            asset.recovery = recovery;
            asset.damageMultiplier = damageMultiplier;
            asset.hitboxOffset = hitboxOffset;
            asset.hitboxSize = hitboxSize;
            asset.knockback = knockback;
            asset.dashCancelNormalizedTime = dashCancel;
            asset.hitStopSeconds = hitStop;
            asset.cameraShakeAmplitude = shake;
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
