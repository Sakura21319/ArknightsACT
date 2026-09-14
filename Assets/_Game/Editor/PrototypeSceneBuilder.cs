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
            var player = PrototypeFactory.CreatePlayer(attacks);
            PrototypeFactory.CreateFloor();
            PrototypeFactory.CreatePlatform(new Vector2(5f, 1.5f), new Vector2(4f, 0.35f));
            PrototypeFactory.CreatePlatform(new Vector2(11f, 2.6f), new Vector2(3f, 0.35f));
            PrototypeFactory.CreateDummy(new Vector2(4f, 0f));
            PrototypeFactory.CreateDummy(new Vector2(8f, 0f));
            PrototypeFactory.CreateCamera(player.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);
            Debug.Log($"ArknightsACT prototype scene generated: {ScenePath}. Standalone default: Windowed 1280x720.");
        }

        private static AttackDefinition[] BuildTexasAttackDefinitions()
        {
            return new[]
            {
                GetOrCreateAttack("Texas_A1", 0.07f, 0.05f, 0.12f, 0.85f, new Vector2(2.2f, 0.6f), 0.25f),
                GetOrCreateAttack("Texas_A2", 0.06f, 0.05f, 0.13f, 0.90f, new Vector2(2.5f, 0.8f), 0.25f),
                GetOrCreateAttack("Texas_A3", 0.08f, 0.06f, 0.15f, 1.00f, new Vector2(3.2f, 1.0f), 0.30f),
                GetOrCreateAttack("Texas_A4", 0.11f, 0.07f, 0.22f, 1.35f, new Vector2(6.5f, 2.4f), 0.40f, 0.055f, 0.11f)
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
