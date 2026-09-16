#if UNITY_EDITOR
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

        [MenuItem("ArknightsACT/Build Prototype Scene")]
        public static void Build()
        {
            PrototypePlayerSettings.Apply();
            EnsureFolder(DataDir);
            EnsureFolder(SceneDir);
            var attacks = BuildChenAttackDefinitions();

            Prototype25DSceneBuilder.Build();
            Prototype25DVerticalityEnhancer.Apply();
            RemoveDemoActor("Player_Chen_25D");
            RemoveDemoActor("Enemy_Soldier");
            RemoveDemoActor("Enemy_Hound");
            RemoveDemoActor("Enemy_Crossbowman");

            var camera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError("[ArknightsACT/25D] Main Camera was not created by the 2.5D world builder.");
                return;
            }

            PrototypeFactory.CreateServices();
            var player = ChenPrototypePlayerFactory.Create25D(attacks, camera);
            Prototype25DProductionFactory.ConfigureCamera(camera, player.transform);

            var enemyTemplates = Prototype25DProductionFactory.CreateEnemyTemplates(camera);
            var roomLoop = Prototype25DProductionFactory.CreateRoomLoop(player.transform, enemyTemplates);
            PrototypeRogueliteFactory.Create(roomLoop, player.transform);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log(
                $"ArknightsACT Chen 2.5D ACT roguelite prototype generated: {ScenePath}. " +
                "Controls: WASD/Stick move on XZ, Space jump, J/LMB combo, K/Shift dash, L skill1, I/RMB skill2. " +
                "Enemies acquire only inside their forward vision cone with obstacle-aware line of sight. " +
                "The NE structure is walkable across multiple floors. R3 rewards and route nodes remain active.");
        }

        private static void RemoveDemoActor(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
                Object.DestroyImmediate(go);
        }

        private static AttackDefinition[] BuildChenAttackDefinitions()
        {
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