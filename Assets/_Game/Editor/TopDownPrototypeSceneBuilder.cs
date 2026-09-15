#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters.Texas;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.TopDown;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Builds a completely separate Soul-Knight-style top-down prototype. It does not touch
    /// PrototypeRun.unity or SurvivorPrototype.unity.
    /// </summary>
    public static class TopDownPrototypeSceneBuilder
    {
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string ScenePath = SceneDir + "/TopDownPrototype.unity";
        private static readonly Vector2 RoomHalfSize = new(7f, 5f);
        private static readonly Vector2[] RoomCenters =
        {
            new(0f, 0f),
            new(18f, 0f),
            new(36f, 0f),
            new(54f, 0f)
        };

        [MenuItem("ArknightsACT/Build Top-Down Prototype Scene")]
        public static void Build()
        {
            PrototypePlayerSettings.Apply();
            EnsureFolder(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateServices();
            var doors = CreateFourRoomMap();
            var player = CreatePlayer();
            var templates = CreateEnemyTemplates();

            var run = new GameObject("[TopDownRun]").AddComponent<TopDownRoomRunController2D>();
            run.Configure(
                player.transform,
                player.GetComponent<TopDownTexasUpgradePanel>(),
                templates,
                RoomCenters,
                RoomHalfSize,
                doors);

            CreateCamera(player.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log(
                $"ArknightsACT top-down prototype generated: {ScenePath}. " +
                "Controls: WASD move, mouse/right-stick aim, LMB/J melee, Space/Shift/K dash, RMB/L Sword Rain. " +
                "No support operators are included in this experiment.");
        }

        private static void CreateServices()
        {
            var services = new GameObject("[Services]");
            services.AddComponent<HitStopService>();
        }

        private static GameObject CreatePlayer()
        {
            var go = new GameObject("Player_Texas_TopDown");
            go.transform.position = RoomCenters[0];

            GameObject combatPresentation = null;
            if (!PrtsGeneratedPresentation.TryAttach(PrtsPrototypeAssetCatalog.Texas.BaseName, go.transform, out combatPresentation))
            {
                CreatePlaceholder(go.transform, "TexasPlaceholder", new Vector2(0.75f, 1.25f), new Color(0.45f, 0.70f, 0.95f), 1000);
                Debug.LogWarning("[ArknightsACT/TopDown] Texas PRTS presentation prefab missing; placeholder will be used.");
            }
            else if (PrtsGeneratedPresentation.TryAttach(PrtsPrototypeAssetCatalog.TexasBaseMotion.BaseName, go.transform, out var motionSource))
            {
                motionSource.name = "MotionSource_Texas_Base_TopDown";
                var retarget = go.AddComponent<SpineBoneMotionRetarget2D>();
                retarget.Configure(combatPresentation.transform, motionSource.transform, "Move");
            }

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.40f;
            collider.offset = new Vector2(0f, -0.08f);

            go.AddComponent<Health>().SetMaxHealth(100f);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);

            go.AddComponent<TopDownPlayerInputReader>();
            go.AddComponent<TopDownPlayerMotor2D>();
            go.AddComponent<TopDownPlayerDash2D>();
            go.AddComponent<TopDownPlayerDamageGate>();
            go.AddComponent<TopDownCombatFx2D>();
            go.AddComponent<TopDownTexasMeleeController>();

            go.AddComponent<SwordRainPresentation2D>();
            go.AddComponent<TexasSwordRainSkill>();
            go.AddComponent<PlayerSkillController>();

            var swift = go.AddComponent<TopDownTexasSwiftBladeEffect>();
            var residual = go.AddComponent<TexasResidualThunderEffect>();
            var conductive = go.AddComponent<TopDownTexasConductiveEffect>();
            swift.enabled = false;
            residual.enabled = false;
            conductive.enabled = false;
            go.AddComponent<TopDownTexasBuildLab>();
            go.AddComponent<TopDownTexasUpgradePanel>();

            go.AddComponent<TopDownTexasPresentationDriver2D>();
            go.AddComponent<TopDownDepthSort2D>();
            return go;
        }

        private static GameObject[] CreateEnemyTemplates()
        {
            var root = new GameObject("[TopDownEnemyTemplates]");
            var catalog = PrtsPrototypeAssetCatalog.PrototypeEnemies;
            var templates = new GameObject[4];
            templates[0] = CreateEnemyTemplate(root.transform, catalog.Length > 1 ? catalog[1] : null, TopDownEnemyArchetype.Melee, 58f);
            templates[1] = CreateEnemyTemplate(root.transform, catalog.Length > 3 ? catalog[3] : null, TopDownEnemyArchetype.FastMelee, 42f);
            templates[2] = CreateEnemyTemplate(root.transform, catalog.Length > 2 ? catalog[2] : null, TopDownEnemyArchetype.Ranged, 48f);
            templates[3] = CreateEnemyTemplate(root.transform, catalog.Length > 5 ? catalog[5] : null, TopDownEnemyArchetype.Boss, 420f);
            return templates;
        }

        private static GameObject CreateEnemyTemplate(
            Transform parent,
            PrtsAssetDescriptor descriptor,
            TopDownEnemyArchetype archetype,
            float healthValue)
        {
            var name = descriptor != null ? descriptor.DisplayName : archetype.ToString();
            var go = new GameObject("TopDownTemplate_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, -30f, 0f);

            var hasPresentation = descriptor != null && PrtsGeneratedPresentation.TryAttach(descriptor.BaseName, go.transform, out _);
            if (!hasPresentation)
                CreatePlaceholder(go.transform, "EnemyPlaceholder", archetype == TopDownEnemyArchetype.Boss ? new Vector2(1.2f, 1.5f) : new Vector2(0.75f, 1.0f), new Color(0.82f, 0.28f, 0.30f), 900);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = archetype == TopDownEnemyArchetype.Boss ? 0.68f : 0.38f;
            collider.offset = new Vector2(0f, -0.06f);

            go.AddComponent<Health>().SetMaxHealth(healthValue);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);

            var brain = go.AddComponent<TopDownEnemyBrain2D>();
            brain.Configure(archetype);
            go.AddComponent<TopDownEnemyPresentationDriver2D>();
            go.AddComponent<TopDownEnemyLifecycle2D>();
            go.AddComponent<TopDownDepthSort2D>();
            go.SetActive(false);
            return go;
        }

        private static GameObject[] CreateFourRoomMap()
        {
            var root = new GameObject("[TopDownMap]");
            var doors = new GameObject[RoomCenters.Length - 1];

            for (var i = 0; i < RoomCenters.Length; i++)
            {
                var center = RoomCenters[i];
                CreateFloor(root.transform, $"Room_{i + 1}_Floor", center, RoomHalfSize * 2f,
                    i == 3 ? new Color(0.17f, 0.12f, 0.15f) : new Color(0.13f + i * 0.012f, 0.15f, 0.19f + i * 0.01f));
                CreateRoomWalls(root.transform, i, center);
                CreateRoomDecoration(root.transform, i, center);

                if (i < RoomCenters.Length - 1)
                {
                    var rightEdge = center.x + RoomHalfSize.x;
                    var nextLeft = RoomCenters[i + 1].x - RoomHalfSize.x;
                    var corridorCenter = new Vector2((rightEdge + nextLeft) * 0.5f, 0f);
                    CreateFloor(root.transform, $"Corridor_{i + 1}", corridorCenter, new Vector2(nextLeft - rightEdge, 2.5f), new Color(0.12f, 0.14f, 0.18f));
                    doors[i] = CreateDoor(root.transform, $"Door_{i + 1}_{i + 2}", new Vector2(rightEdge, 0f));
                }
            }

            return doors;
        }

        private static void CreateRoomWalls(Transform parent, int index, Vector2 center)
        {
            const float thickness = 0.38f;
            const float doorHalfHeight = 1.25f;
            var width = RoomHalfSize.x * 2f;
            var height = RoomHalfSize.y * 2f;
            var wallColor = new Color(0.28f, 0.31f, 0.38f);

            CreateWall(parent, $"R{index + 1}_Top", center + Vector2.up * RoomHalfSize.y, new Vector2(width + thickness, thickness), wallColor);
            CreateWall(parent, $"R{index + 1}_Bottom", center + Vector2.down * RoomHalfSize.y, new Vector2(width + thickness, thickness), wallColor);

            var sideSegmentHeight = (height - doorHalfHeight * 2f) * 0.5f;
            var upperY = center.y + doorHalfHeight + sideSegmentHeight * 0.5f;
            var lowerY = center.y - doorHalfHeight - sideSegmentHeight * 0.5f;

            if (index == 0)
            {
                CreateWall(parent, "R1_Left", center + Vector2.left * RoomHalfSize.x, new Vector2(thickness, height), wallColor);
            }
            else
            {
                var x = center.x - RoomHalfSize.x;
                CreateWall(parent, $"R{index + 1}_Left_Upper", new Vector2(x, upperY), new Vector2(thickness, sideSegmentHeight), wallColor);
                CreateWall(parent, $"R{index + 1}_Left_Lower", new Vector2(x, lowerY), new Vector2(thickness, sideSegmentHeight), wallColor);
            }

            if (index == RoomCenters.Length - 1)
            {
                CreateWall(parent, $"R{index + 1}_Right", center + Vector2.right * RoomHalfSize.x, new Vector2(thickness, height), wallColor);
            }
            else
            {
                var x = center.x + RoomHalfSize.x;
                CreateWall(parent, $"R{index + 1}_Right_Upper", new Vector2(x, upperY), new Vector2(thickness, sideSegmentHeight), wallColor);
                CreateWall(parent, $"R{index + 1}_Right_Lower", new Vector2(x, lowerY), new Vector2(thickness, sideSegmentHeight), wallColor);
            }
        }

        private static void CreateRoomDecoration(Transform parent, int index, Vector2 center)
        {
            // Visual-only cover/props for now; enemy navigation remains direct so we do not add
            // blocking colliders until the combat direction is validated.
            var propColor = new Color(0.20f, 0.24f, 0.30f);
            switch (index)
            {
                case 0:
                    CreatePanel(parent, "R1_Marking_A", center + new Vector2(-2.8f, 2.2f), new Vector2(1.6f, 0.22f), propColor, 2);
                    CreatePanel(parent, "R1_Marking_B", center + new Vector2(2.5f, -2.0f), new Vector2(1.8f, 0.22f), propColor, 2);
                    break;
                case 1:
                    CreatePanel(parent, "R2_Cover_A", center + new Vector2(-2.4f, 0f), new Vector2(1.2f, 2.4f), propColor, 2);
                    CreatePanel(parent, "R2_Cover_B", center + new Vector2(2.4f, 0f), new Vector2(1.2f, 2.4f), propColor, 2);
                    break;
                case 2:
                    CreatePanel(parent, "R3_Core", center, new Vector2(2.0f, 2.0f), new Color(0.17f, 0.25f, 0.30f), 2);
                    break;
                default:
                    CreatePanel(parent, "Boss_Ring", center, new Vector2(5.5f, 0.12f), new Color(0.38f, 0.17f, 0.20f), 2);
                    CreatePanel(parent, "Boss_Ring_V", center, new Vector2(0.12f, 5.5f), new Color(0.38f, 0.17f, 0.20f), 2);
                    break;
            }
        }

        private static GameObject CreateDoor(Transform parent, string name, Vector2 position)
        {
            var door = new GameObject(name);
            door.transform.SetParent(parent, false);
            door.transform.position = position;
            var visual = CreatePanel(door.transform, "Visual", Vector2.zero, new Vector2(0.40f, 2.5f), new Color(0.82f, 0.34f, 0.22f), 20);
            visual.transform.localPosition = Vector3.zero;
            var collider = door.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.40f, 2.5f);
            return door;
        }

        private static void CreateFloor(Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            CreatePanel(parent, name, center, size, color, -20);
        }

        private static void CreateWall(Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            CreatePanel(go.transform, "Visual", Vector2.zero, size, color, 5);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            go.AddComponent<PlaceholderVisual2D>().SetColor(color);
            return go;
        }

        private static void CreatePlaceholder(Transform parent, string name, Vector2 size, Color color, int order)
        {
            CreatePanel(parent, name, Vector2.zero, size, color, order);
        }

        private static void CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(RoomCenters[0].x, RoomCenters[0].y, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.052f, 0.070f);
            camera.allowDynamicResolution = false;
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraShake2D>();

            var follow = go.AddComponent<TopDownCameraFollow2D>();
            follow.Configure(target, new Vector2(-1f, -1.2f), new Vector2(55f, 1.2f));
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
