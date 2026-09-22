#if UNITY_EDITOR
using ArknightsACT.Gameplay.Characters.Schwarz;
using ArknightsACT.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsACT.Editor
{
    public static class SchwarzPrototypeSceneBuilder
    {
        private const string DataDir = "Assets/_Game/Data/Attacks/Schwarz";
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string ScenePath = SceneDir + "/PrototypeRun.unity";

        public static void BuildDefault() => Build(SchwarzSkinVariant.Default);

        public static void BuildSnow() => Build(SchwarzSkinVariant.Snow);

        public static void BuildStriker() => Build(SchwarzSkinVariant.Striker);

        private static void Build(SchwarzSkinVariant skin)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "不能在 Play Mode 中构建场景。请先停止运行。",
                    "确定");
                return;
            }

            SchwarzLocalAssetBootstrap.PrepareForBuild(skin);
            PrototypePlayerSettings.Apply();
            EnsureFolder(DataDir);
            EnsureFolder(SceneDir);
            var attacks = BuildAttackDefinitions();

            Prototype25DSceneBuilder.Build();
            RemoveDemoActor("Player_Chen_25D");
            RemoveDemoActor("Player_Chen");
            RemoveDemoActor("Player_Schwarz");
            RemoveDemoActor("Enemy_Soldier");
            RemoveDemoActor("Enemy_Hound");
            RemoveDemoActor("Enemy_Crossbowman");
            RemoveDemoActor("[3D Map]");

            var camera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError("[ArknightsACT/Schwarz] Main Camera was not created by the 2.5D world builder.");
                return;
            }

            PrototypeFactory.CreateServices();
            var player = SchwarzPrototypePlayerFactory.Create25D(attacks, camera, skin);
            Prototype25DProductionFactory.ConfigureCamera(camera, player.transform);

            var enemyTemplates = Prototype25DProductionFactory.CreateEnemyTemplates(camera);
            var treasureTemplates = PrototypeTreasureFactory.CreateTemplates(camera);
            var rogueliteRoot = PrototypeRogueliteFactory.CreateExploration(player.transform);
            PrototypeStageRuntimeFactory.Create(
                player.transform,
                enemyTemplates,
                treasureTemplates,
                rogueliteRoot);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log(
                $"[ArknightsACT/Schwarz] 2.5D exploration scene generated with skin={skin}: {ScenePath}. " +
                "Skill slots: L = 暮眼锐瞳 (S2), I/RMB = 战术的终结 (S3). Schwarz basic attacks use sniper-range hitboxes.");
        }

        private static AttackDefinition[] BuildAttackDefinitions()
        {
            return new[]
            {
                GetOrCreateAttack("Schwarz_Basic_1", 0.24f, 0.02f, 0.30f, 1.00f, 5.75f, 11.00f, 0.62f, 0.46f, 0.018f, 0.035f),
                GetOrCreateAttack("Schwarz_Basic_2", 0.22f, 0.02f, 0.31f, 1.10f, 6.00f, 11.50f, 0.64f, 0.44f, 0.020f, 0.040f),
                GetOrCreateAttack("Schwarz_Basic_3", 0.28f, 0.02f, 0.36f, 1.30f, 6.25f, 12.00f, 0.68f, 0.50f, 0.026f, 0.055f)
            };
        }

        private static AttackDefinition GetOrCreateAttack(
            string name,
            float startup,
            float active,
            float recovery,
            float damageMultiplier,
            float forwardOffset,
            float forwardSize,
            float lateralSize,
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
            asset.hitboxOffset = new Vector2(forwardOffset, 0.04f);
            asset.hitboxSize = new Vector2(forwardSize, lateralSize);
            asset.knockback = Vector2.zero;
            asset.dashCancelNormalizedTime = dashCancel;
            asset.hitStopSeconds = hitStop;
            asset.cameraShakeAmplitude = shake;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void RemoveDemoActor(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
                Object.DestroyImmediate(go);
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
