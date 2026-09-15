#if UNITY_EDITOR
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
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

            // Replace the old whole-presentation Transform accent with real Texas Spine bone
            // posing now that the local PRTS rig has been mapped. This keeps only one action
            // authoring layer active at a time and makes move differences readable without VFX.
            var legacyAccent = player.GetComponent<PlayerComboMotionAccent2D>();
            if (legacyAccent != null)
                UnityEngine.Object.DestroyImmediate(legacyAccent);
            player.AddComponent<TexasProceduralActionAnimator2D>();

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
                $"ArknightsACT prototype scene generated: {ScenePath}. Texas now uses mapped Spine bone poses " +
                "for ground combo, dash slash, air slash and plunge actions.");
        }

        private static AttackDefinition[] BuildTexasAttackDefinitions()
        {
            // Spine supplies the authoritative visible cycle/impact timing at runtime. These three
            // definitions intentionally share that cadence but vary damage, hit volume, knockback
            // and feedback so repeated J presses read as a real 1-2-finisher combo instead of one
            // identical swing forever.
            return new[]
            {
                GetOrCreateAttack(
                    "Texas_Basic_1",
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
                    "Texas_Basic_2",
                    startup: 0.090f,
                    active: 0.030f,
                    recovery: 0.050f,
                    damageMultiplier: 1.10f,
                    hitboxOffset: new Vector2(0.96f, 0.05f),
                    hitboxSize: new Vector2(1.55f, 1.18f),
                    knockback: new Vector2(2.8f, 0.60f),
                    dashCancel: 0.32f,
                    hitStop: 0.022f,
                    shake: 0.055f),
                GetOrCreateAttack(
                    "Texas_Basic_3_Heavy",
                    startup: 0.105f,
                    active: 0.035f,
                    recovery: 0.060f,
                    damageMultiplier: 1.60f,
                    hitboxOffset: new Vector2(1.05f, 0.08f),
                    hitboxSize: new Vector2(2.05f, 1.42f),
                    knockback: new Vector2(5.0f, 1.15f),
                    dashCancel: 0.42f,
                    hitStop: 0.045f,
                    shake: 0.11f)
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
