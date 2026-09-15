#if UNITY_EDITOR
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Debugging;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Survivor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Builds an isolated survivor-style experiment without touching PrototypeRun.unity.
    /// The goal is to validate: manual positioning + automatic melee + continuous enemy pressure.
    /// </summary>
    public static class SurvivorPrototypeSceneBuilder
    {
        private const string DataDir = "Assets/_Game/Data/Attacks/Texas";
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string ScenePath = SceneDir + "/SurvivorPrototype.unity";

        private const float FloorTopY = -1.10f;
        private const float EnemyGroundY = FloorTopY + 0.73f;
        private const float WorldMinX = -8f;
        private const float WorldMaxX = 112f;

        [MenuItem("ArknightsACT/Build Survivor Prototype Scene")]
        public static void Build()
        {
            PrototypePlayerSettings.Apply();
            EnsureFolder(DataDir);
            EnsureFolder(SceneDir);

            var attacks = BuildTexasAttackDefinitions();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PrototypeFactory.CreateServices();
            CreateLongBackdrop();
            CreateWorldGeometry();

            var player = PrototypeFactory.CreatePlayer(attacks);
            player.transform.position = new Vector3(0f, FloorTopY + 0.76f, 0f);

            // Survivor mode gets its own input adapter: no J/LMB basic attack.
            var normalInput = player.GetComponent<PlayerInputReader>();
            if (normalInput != null)
                Object.DestroyImmediate(normalInput);
            player.AddComponent<SurvivorPlayerInputReader>();

            // Keep authored PRTS animation only; remove all experimental action-pose layers.
            var legacyAccent = player.GetComponent<PlayerComboMotionAccent2D>();
            if (legacyAccent != null)
                Object.DestroyImmediate(legacyAccent);
            var proceduralAnimator = player.GetComponent<TexasProceduralActionAnimator2D>();
            if (proceduralAnimator != null)
                Object.DestroyImmediate(proceduralAnimator);

            var oldHud = player.GetComponent<TexasPrototypeHud>();
            if (oldHud != null)
                Object.DestroyImmediate(oldHud);

            player.AddComponent<TexasSurvivorAutoMelee2D>();

            var enemyTemplates = PrototypeFactory.CreateEnemyTemplates(PrtsPrototypeAssetCatalog.PrototypeEnemies);
            var loopObject = new GameObject("[SurvivorLoop]");
            var survivorLoop = loopObject.AddComponent<SurvivorPrototypeController2D>();
            survivorLoop.Configure(
                player.transform,
                player.GetComponent<ArknightsACT.Gameplay.Characters.Texas.TexasUpgradeChoicePanel>(),
                enemyTemplates,
                WorldMinX,
                WorldMaxX,
                EnemyGroundY);

            PrototypeFactory.CreateCamera(player.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log(
                $"ArknightsACT survivor experiment generated: {ScenePath}. " +
                "Basic attack is automatic melee with NO magnet and NO auto-step. " +
                "Controls: A/D move, Space jump, K/Shift dash, L/RMB Sword Rain.");
        }

        private static void CreateWorldGeometry()
        {
            CreateStaticBlock(
                "Survivor_Floor",
                new Vector2((WorldMinX + WorldMaxX) * 0.5f, FloorTopY - 0.25f),
                new Vector2(WorldMaxX - WorldMinX, 0.5f),
                new Color(0.20f, 0.21f, 0.24f),
                0);

            // Boundaries keep the continuous-spawn experiment inside one long traversal space.
            CreateStaticBlock(
                "Survivor_Boundary_Left",
                new Vector2(WorldMinX - 0.28f, 2.4f),
                new Vector2(0.55f, 8.0f),
                new Color(0.10f, 0.11f, 0.14f),
                1);
            CreateStaticBlock(
                "Survivor_Boundary_Right",
                new Vector2(WorldMaxX + 0.28f, 2.4f),
                new Vector2(0.55f, 8.0f),
                new Color(0.10f, 0.11f, 0.14f),
                1);

            // Deliberately varied elevations. They are traversal/positioning choices, not arenas.
            CreateStaticBlock("Highway_A", new Vector2(8f, 0.15f), new Vector2(7.5f, 0.32f), new Color(0.34f, 0.37f, 0.43f), 2);
            CreateStaticBlock("Highway_B", new Vector2(20f, 1.05f), new Vector2(5.0f, 0.32f), new Color(0.34f, 0.37f, 0.43f), 2);
            CreateStaticBlock("Highway_C", new Vector2(32f, 0.30f), new Vector2(8.0f, 0.32f), new Color(0.34f, 0.37f, 0.43f), 2);
            CreateStaticBlock("Highway_D", new Vector2(47f, 1.55f), new Vector2(6.5f, 0.32f), new Color(0.34f, 0.37f, 0.43f), 2);
            CreateStaticBlock("Highway_E", new Vector2(61f, 0.65f), new Vector2(9.0f, 0.32f), new Color(0.34f, 0.37f, 0.43f), 2);
            CreateStaticBlock("Highway_F", new Vector2(77f, 1.30f), new Vector2(5.5f, 0.32f), new Color(0.34f, 0.37f, 0.43f), 2);
            CreateStaticBlock("Highway_G", new Vector2(90f, 0.20f), new Vector2(8.0f, 0.32f), new Color(0.34f, 0.37f, 0.43f), 2);
            CreateStaticBlock("Highway_H", new Vector2(103f, 1.15f), new Vector2(5.5f, 0.32f), new Color(0.34f, 0.37f, 0.43f), 2);
        }

        private static void CreateLongBackdrop()
        {
            var root = new GameObject("[SurvivorBackdrop]");
            var center = (WorldMinX + WorldMaxX) * 0.5f;
            CreateBackdropPanel(root.transform, "BackWall", new Vector2(center, 2.4f), new Vector2(WorldMaxX - WorldMinX + 4f, 8.8f), new Color(0.075f, 0.09f, 0.125f), -100);
            CreateBackdropPanel(root.transform, "FarFloorBand", new Vector2(center, -0.78f), new Vector2(WorldMaxX - WorldMinX + 4f, 0.10f), new Color(0.18f, 0.20f, 0.25f), -80);

            for (var i = 0; i < 11; i++)
            {
                var x = WorldMinX + 5f + i * 11f;
                var height = 3.5f + (i % 3) * 0.8f;
                CreateBackdropPanel(root.transform, "Column_" + i, new Vector2(x, 1.2f), new Vector2(0.55f, height), new Color(0.11f, 0.13f, 0.17f), -90);

                if (i % 2 == 0)
                    CreateBackdropPanel(root.transform, "FarPlatform_" + i, new Vector2(x + 3.0f, 2.6f + (i % 3) * 0.35f), new Vector2(4.5f, 0.14f), new Color(0.22f, 0.24f, 0.30f), -70);
            }
        }

        private static void CreateStaticBlock(string name, Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = Vector3.one;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(color);

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static void CreateBackdropPanel(Transform parent, string name, Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            go.AddComponent<PlaceholderVisual2D>().SetColor(color);
        }

        private static AttackDefinition[] BuildTexasAttackDefinitions()
        {
            return new[]
            {
                GetOrCreateAttack("Texas_Basic_1", 0.095f, 0.030f, 0.055f, 1.00f, new Vector2(0.90f, 0.02f), new Vector2(1.40f, 1.10f), new Vector2(2.2f, 0.45f), 0.34f, 0.018f, 0.045f),
                GetOrCreateAttack("Texas_Basic_2", 0.090f, 0.030f, 0.050f, 1.10f, new Vector2(0.96f, 0.05f), new Vector2(1.55f, 1.18f), new Vector2(2.8f, 0.60f), 0.32f, 0.022f, 0.055f),
                GetOrCreateAttack("Texas_Basic_3_Heavy", 0.105f, 0.035f, 0.060f, 1.60f, new Vector2(1.05f, 0.08f), new Vector2(2.05f, 1.42f), new Vector2(5.0f, 1.15f), 0.42f, 0.045f, 0.11f)
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
