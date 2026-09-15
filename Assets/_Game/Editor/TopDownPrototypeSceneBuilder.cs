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
    /// Builds a completely separate Soul-Knight-style pseudo-3/4 prototype. Gameplay remains
    /// 2D XY, while room proportions, wall faces, shadows and camera framing make upright PRTS
    /// Spine characters read as standing on a floor instead of being pasted onto a top-down map.
    /// </summary>
    public static class TopDownPrototypeSceneBuilder
    {
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string ScenePath = SceneDir + "/TopDownPrototype.unity";
        private static readonly Vector2 RoomHalfSize = new(7.5f, 4.2f);
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

            var runObject = new GameObject("[TopDownRun]");
            var run = runObject.AddComponent<TopDownRoomRunController2D>();
            run.Configure(
                player.transform,
                player.GetComponent<TopDownTexasUpgradePanel>(),
                templates,
                RoomCenters,
                RoomHalfSize,
                doors);
            EditorUtility.SetDirty(run);

            CreateCamera(player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log(
                $"ArknightsACT oblique-room prototype generated: {ScenePath}. " +
                "Controls: WASD move, mouse/right-stick aim, LMB/J melee, Space/Shift/K dash, RMB/L Sword Rain. " +
                "Room/enemy/template references are serialized for Play Mode domain reload.");
        }

        private static void CreateServices()
        {
            var services = new GameObject("[Services]");
            services.AddComponent<HitStopService>();
        }

        private static GameObject CreatePlayer()
        {
            var go = new GameObject("Player_Texas_TopDown");
            go.transform.position = RoomCenters[0] + new Vector2(-2.8f, -0.3f);

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
            var shadow = go.AddComponent<TopDownGroundShadow2D>();
            shadow.Configure(new Vector2(0.95f, 0.34f), new Vector2(0f, -0.10f), 0.34f);
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
            go.transform.localPosition = new Vector3(0f, -30f, 0f);

            var hasPresentation = descriptor != null && PrtsGeneratedPresentation.TryAttach(descriptor.BaseName, go.transform, out _);
            if (!hasPresentation)
                CreatePlaceholder(
                    go.transform,
                    "EnemyPlaceholder",
                    archetype == TopDownEnemyArchetype.Boss ? new Vector2(1.2f, 1.5f) : new Vector2(0.75f, 1.0f),
                    new Color(0.82f, 0.28f, 0.30f),
                    900);

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
            var shadow = go.AddComponent<TopDownGroundShadow2D>();
            shadow.Configure(
                archetype == TopDownEnemyArchetype.Boss ? new Vector2(1.35f, 0.46f) : new Vector2(0.82f, 0.28f),
                new Vector2(0f, -0.08f),
                archetype == TopDownEnemyArchetype.Boss ? 0.40f : 0.28f);

            go.SetActive(false);
            EditorUtility.SetDirty(brain);
            return go;
        }

        private static GameObject[] CreateFourRoomMap()
        {
            var root = new GameObject("[TopDownMap]");
            var doors = new GameObject[RoomCenters.Length - 1];
            var corridorWallColor = new Color(0.23f, 0.27f, 0.34f);

            for (var i = 0; i < RoomCenters.Length; i++)
            {
                var center = RoomCenters[i];
                var floorColor = i == 3
                    ? new Color(0.17f, 0.12f, 0.15f)
                    : new Color(0.13f + i * 0.012f, 0.15f, 0.19f + i * 0.01f);

                CreateFloor(root.transform, $"Room_{i + 1}_Floor", center, RoomHalfSize * 2f, floorColor);
                CreateFloorPerspectiveLines(root.transform, i, center);
                CreateRoomWalls(root.transform, i, center);
                CreateRoomDecoration(root.transform, i, center);

                if (i < RoomCenters.Length - 1)
                {
                    var rightEdge = center.x + RoomHalfSize.x;
                    var nextLeft = RoomCenters[i + 1].x - RoomHalfSize.x;
                    var corridorWidth = nextLeft - rightEdge;
                    var corridorCenter = new Vector2((rightEdge + nextLeft) * 0.5f, 0f);
                    CreateFloor(root.transform, $"Corridor_{i + 1}", corridorCenter, new Vector2(corridorWidth, 2.15f), new Color(0.12f, 0.14f, 0.18f));
                    CreateWall(root.transform, $"Corridor_{i + 1}_Top", corridorCenter + Vector2.up * 1.08f, new Vector2(corridorWidth, 0.32f), corridorWallColor);
                    CreateWall(root.transform, $"Corridor_{i + 1}_Bottom", corridorCenter + Vector2.down * 1.08f, new Vector2(corridorWidth, 0.26f), corridorWallColor);
                    doors[i] = CreateDoor(root.transform, $"Door_{i + 1}_{i + 2}", new Vector2(rightEdge, 0f));
                }
            }

            return doors;
        }

        private static void CreateRoomWalls(Transform parent, int index, Vector2 center)
        {
            const float colliderThickness = 0.34f;
            const float doorHalfHeight = 1.10f;
            var width = RoomHalfSize.x * 2f;
            var height = RoomHalfSize.y * 2f;
            var wallColor = new Color(0.29f, 0.32f, 0.39f);
            var wallFaceColor = new Color(0.18f, 0.205f, 0.26f);

            // The upper edge gets a visible vertical face. This is the main cue that changes the
            // room from a flat map into a pseudo-3/4 stage while collision remains ordinary XY.
            CreatePanel(parent, $"R{index + 1}_BackWallFace", center + new Vector2(0f, RoomHalfSize.y - 0.46f), new Vector2(width, 0.92f), wallFaceColor, 35);
            CreatePanel(parent, $"R{index + 1}_BackWallCap", center + new Vector2(0f, RoomHalfSize.y + 0.08f), new Vector2(width + 0.35f, 0.28f), wallColor, 38);
            CreateWall(parent, $"R{index + 1}_TopCollider", center + Vector2.up * RoomHalfSize.y, new Vector2(width + colliderThickness, colliderThickness), wallColor, false);

            // Front edge is intentionally thin; a thick lower wall would make the room look like
            // a strict top-down rectangle again.
            CreateWall(parent, $"R{index + 1}_Bottom", center + Vector2.down * RoomHalfSize.y, new Vector2(width + colliderThickness, 0.24f), new Color(0.24f, 0.27f, 0.33f));

            var sideSegmentHeight = (height - doorHalfHeight * 2f) * 0.5f;
            var upperY = center.y + doorHalfHeight + sideSegmentHeight * 0.5f;
            var lowerY = center.y - doorHalfHeight - sideSegmentHeight * 0.5f;

            if (index == 0)
            {
                CreateWall(parent, "R1_Left", center + Vector2.left * RoomHalfSize.x, new Vector2(colliderThickness, height), wallColor);
            }
            else
            {
                var x = center.x - RoomHalfSize.x;
                CreateWall(parent, $"R{index + 1}_Left_Upper", new Vector2(x, upperY), new Vector2(colliderThickness, sideSegmentHeight), wallColor);
                CreateWall(parent, $"R{index + 1}_Left_Lower", new Vector2(x, lowerY), new Vector2(colliderThickness, sideSegmentHeight), wallColor);
            }

            if (index == RoomCenters.Length - 1)
            {
                CreateWall(parent, $"R{index + 1}_Right", center + Vector2.right * RoomHalfSize.x, new Vector2(colliderThickness, height), wallColor);
            }
            else
            {
                var x = center.x + RoomHalfSize.x;
                CreateWall(parent, $"R{index + 1}_Right_Upper", new Vector2(x, upperY), new Vector2(colliderThickness, sideSegmentHeight), wallColor);
                CreateWall(parent, $"R{index + 1}_Right_Lower", new Vector2(x, lowerY), new Vector2(colliderThickness, sideSegmentHeight), wallColor);
            }
        }

        private static void CreateFloorPerspectiveLines(Transform parent, int index, Vector2 center)
        {
            var lineColor = index == 3 ? new Color(0.25f, 0.16f, 0.19f) : new Color(0.19f, 0.22f, 0.27f);
            var ys = new[] { -2.7f, -1.55f, -0.45f, 0.62f, 1.64f, 2.58f };
            for (var i = 0; i < ys.Length; i++)
            {
                var inset = Mathf.Abs(ys[i]) * 0.18f;
                CreatePanel(
                    parent,
                    $"R{index + 1}_FloorLine_{i}",
                    center + new Vector2(0f, ys[i]),
                    new Vector2(RoomHalfSize.x * 2f - 0.8f - inset, 0.045f),
                    lineColor,
                    -15);
            }
        }

        private static void CreateRoomDecoration(Transform parent, int index, Vector2 center)
        {
            var propTop = new Color(0.24f, 0.29f, 0.36f);
            var propFace = new Color(0.16f, 0.19f, 0.24f);
            switch (index)
            {
                case 0:
                    CreateFloorMark(parent, "R1_Marking_A", center + new Vector2(-2.8f, 1.6f), new Vector2(1.6f, 0.16f));
                    CreateFloorMark(parent, "R1_Marking_B", center + new Vector2(2.5f, -1.7f), new Vector2(1.8f, 0.16f));
                    break;
                case 1:
                    CreatePseudoProp(parent, "R2_Cover_A", center + new Vector2(-2.4f, 0.4f), new Vector2(1.25f, 0.52f), propTop, propFace);
                    CreatePseudoProp(parent, "R2_Cover_B", center + new Vector2(2.5f, -0.8f), new Vector2(1.25f, 0.52f), propTop, propFace);
                    break;
                case 2:
                    CreatePseudoProp(parent, "R3_Core", center + new Vector2(0f, 0.25f), new Vector2(1.8f, 0.72f), new Color(0.18f, 0.30f, 0.34f), new Color(0.10f, 0.18f, 0.21f));
                    break;
                default:
                    CreateFloorMark(parent, "Boss_Ring_H", center, new Vector2(5.5f, 0.10f), new Color(0.38f, 0.17f, 0.20f));
                    CreateFloorMark(parent, "Boss_Ring_V", center, new Vector2(0.10f, 4.4f), new Color(0.38f, 0.17f, 0.20f));
                    break;
            }
        }

        private static void CreatePseudoProp(Transform parent, string name, Vector2 position, Vector2 footprint, Color top, Color face)
        {
            var shadow = new Color(0f, 0f, 0f, 0.22f);
            CreatePanel(parent, name + "_Shadow", position + new Vector2(0.10f, -0.12f), footprint * new Vector2(1.12f, 0.82f), shadow, 3);
            CreatePanel(parent, name + "_Face", position + new Vector2(0f, -0.18f), new Vector2(footprint.x, footprint.y * 0.72f), face, 410);
            CreatePanel(parent, name + "_Top", position + new Vector2(0f, 0.15f), footprint, top, 420);
        }

        private static void CreateFloorMark(Transform parent, string name, Vector2 position, Vector2 size)
        {
            CreateFloorMark(parent, name, position, size, new Color(0.23f, 0.28f, 0.34f));
        }

        private static void CreateFloorMark(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            CreatePanel(parent, name, position, size, color, -10);
        }

        private static GameObject CreateDoor(Transform parent, string name, Vector2 position)
        {
            var door = new GameObject(name);
            door.transform.SetParent(parent, false);
            door.transform.localPosition = position;
            CreatePanel(door.transform, "Shadow", new Vector2(0.10f, -0.12f), new Vector2(0.62f, 2.25f), new Color(0f, 0f, 0f, 0.28f), 12);
            CreatePanel(door.transform, "Face", Vector2.zero, new Vector2(0.46f, 2.25f), new Color(0.72f, 0.25f, 0.18f), 25);
            var collider = door.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.46f, 2.25f);
            return door;
        }

        private static void CreateFloor(Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            CreatePanel(parent, name, center, size, color, -20);
        }

        private static void CreateWall(Transform parent, string name, Vector2 center, Vector2 size, Color color, bool createVisual = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            if (createVisual)
                CreatePanel(go.transform, "Visual", Vector2.zero, size, color, 30);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
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
            go.transform.position = new Vector3(RoomCenters[0].x, 0.35f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.052f, 0.070f);
            camera.allowDynamicResolution = false;
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraShake2D>();

            var follow = go.AddComponent<TopDownCameraFollow2D>();
            follow.Configure(target, new Vector2(-1.2f, -0.4f), new Vector2(55.2f, 0.9f), 0.16f, 0.35f);
            EditorUtility.SetDirty(follow);
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
