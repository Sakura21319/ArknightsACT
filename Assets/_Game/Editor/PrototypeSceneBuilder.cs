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
        private const string DataDir = "Assets/_Game/Data/Attacks/Texas";
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string ScenePath = SceneDir + "/PrototypeRun.unity";

        [MenuItem("ArknightsACT/Build Prototype Scene")]
        public static void Build()
        {
            PrototypePlayerSettings.Apply();
            EnsureFolder(DataDir);
            EnsureFolder(SceneDir);
            var attacks = BuildTexasAttackDefinitions();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PrototypeFactory.CreateServices();
            PrototypeBackdropFactory.Create();
            var player = PrototypeFactory.CreatePlayer(attacks);
            PrototypeFactory.CreateFloor();
            PrototypeFactory.CreateWorldBounds();
            PrototypeFactory.CreatePlatform(new Vector2(5f, 1.5f), new Vector2(4f, 0.35f));
            PrototypeFactory.CreatePlatform(new Vector2(11f, 2.6f), new Vector2(3f, 0.35f));

            // Keep local PRTS enemy objects as inactive scene templates. Runtime room progression
            // clones these templates instead of hardcoding one disposable wave into the scene.
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
                $"ArknightsACT prototype scene generated: {ScenePath}. Runtime loop is now " +
                "combat room -> localized three-choice reward -> next room, with enemy count/HP scaling per room.");
        }

        private static AttackDefinition[] BuildTexasAttackDefinitions()
        {
            // Fallback timings only. When the PRTS Spine presentation is available,
            // IAttackTimingProvider replaces these timing values with Attack_Loop duration / speed.
            return new[]
            {
                GetOrCreateAttack(
                    "Texas_Basic",
                    startup: 0.095f,
                    active: 0.030f,
                    recovery: 0.060f,
                    damageMultiplier: 1.0f,
                    knockback: new Vector2(3.0f, 0.85f),
                    dashCancel: 0.38f,
                    hitStop: 0.025f,
                    shake: 0.065f)
            };
        }

        private static AttackDefinition GetOrCreateAttack(string name, float startup, float active, float recovery, float damageMultiplier, Vector2 knockback, float dashCancel, float hitStop = 0.035f, float shake = 0.07f)
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
            asset.hitboxOffset = new Vector2(0.95f, 0f);
            asset.hitboxSize = new Vector2(1.45f, 1.15f);
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
