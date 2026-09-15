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
            var attacks = BuildChenAttackDefinitions();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PrototypeFactory.CreateServices();
            PrototypeBackdropFactory.Create();
            var player = ChenPrototypePlayerFactory.Create(attacks, PlayerSpawnY);

            PrototypeFactory.CreateFloor();
            PrototypeFactory.CreateWorldBounds();
            PrototypeFactory.CreatePlatform(new Vector2(5f, 1.5f), new Vector2(4f, 0.35f));
            PrototypeFactory.CreatePlatform(new Vector2(11f, 2.6f), new Vector2(3f, 0.35f));

            var enemyTemplates = PrototypeFactory.CreateEnemyTemplates(PrtsPrototypeAssetCatalog.PrototypeEnemies);
            PrototypeFactory.CreateRoomLoop(player.transform, enemyTemplates);
            PrototypeFactory.CreateCamera(player.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log(
                $"ArknightsACT Chen 2D ACT prototype generated: {ScenePath}. " +
                "Controls: A/D move, Space jump, J/LMB three-hit combo, K/Shift dash, L skill1, I/RMB skill2.");
        }

        private static AttackDefinition[] BuildChenAttackDefinitions()
        {
            // Presentation mapping:
            // combo 1 -> Attack first half
            // combo 2 -> Attack second half
            // combo 3 -> Skill
            return new[]
            {
                GetOrCreateAttack(
                    "Chen_Basic_1",
                    startup: 0.12f,
                    active: 0.03f,
                    recovery: 0.21f,
                    damageMultiplier: 1.00f,
                    hitboxOffset: new Vector2(0.92f, 0.03f),
                    hitboxSize: new Vector2(1.50f, 1.12f),
                    knockback: new Vector2(2.2f, 0.45f),
                    dashCancel: 0.42f,
                    hitStop: 0.018f,
                    shake: 0.045f),
                GetOrCreateAttack(
                    "Chen_Basic_2",
                    startup: 0.11f,
                    active: 0.03f,
                    recovery: 0.22f,
                    damageMultiplier: 1.15f,
                    hitboxOffset: new Vector2(1.00f, 0.05f),
                    hitboxSize: new Vector2(1.65f, 1.20f),
                    knockback: new Vector2(2.8f, 0.60f),
                    dashCancel: 0.40f,
                    hitStop: 0.022f,
                    shake: 0.055f),
                GetOrCreateAttack(
                    "Chen_Basic_3",
                    startup: 0.16f,
                    active: 0.04f,
                    recovery: 0.32f,
                    damageMultiplier: 1.55f,
                    hitboxOffset: new Vector2(1.08f, 0.08f),
                    hitboxSize: new Vector2(2.05f, 1.42f),
                    knockback: new Vector2(4.8f, 1.05f),
                    dashCancel: 0.52f,
                    hitStop: 0.040f,
                    shake: 0.10f)
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
            float hitStop,
            float shake)
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
