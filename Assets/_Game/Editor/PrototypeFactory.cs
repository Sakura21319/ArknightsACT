#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.CameraSystem;
using ArknightsACT.Gameplay.Enemies;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Rooms;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypeFactory
    {
        private static readonly Vector2 FloorSize = new(30f, 0.5f);
        private const float FloorCenterX = 6f;
        private const float FloorCenterY = -1.35f;
        private const float FloorTopY = -1.10f;
        private const float FloorLeftX = -9f;
        private const float FloorRightX = 21f;
        private const float EnemyGroundY = FloorTopY + 0.73f;

        public static void CreateServices()
        {
            var services = new GameObject("[Services]");
            services.AddComponent<HitStopService>();
        }

        public static void CreateFloor() =>
            CreateStaticBlock("Floor", new Vector2(FloorCenterX, FloorCenterY), FloorSize, new Color(0.20f, 0.21f, 0.24f));

        public static void CreateWorldBounds()
        {
            const float wallThickness = 0.55f;
            const float wallHeight = 9.0f;
            var wallCenterY = FloorTopY + wallHeight * 0.5f;
            var wallColor = new Color(0.12f, 0.13f, 0.17f);

            CreateStaticBlock(
                "Boundary_Left",
                new Vector2(FloorLeftX + wallThickness * 0.5f, wallCenterY),
                new Vector2(wallThickness, wallHeight),
                wallColor);
            CreateStaticBlock(
                "Boundary_Right",
                new Vector2(FloorRightX - wallThickness * 0.5f, wallCenterY),
                new Vector2(wallThickness, wallHeight),
                wallColor);
            CreateStaticCollider(
                "Boundary_BottomSafety",
                new Vector2(FloorCenterX, -4.25f),
                new Vector2(FloorSize.x + 1.5f, 0.65f));
        }

        public static void CreatePlatform(Vector2 position, Vector2 size) =>
            CreateStaticBlock("Platform", position, size, new Color(0.34f, 0.37f, 0.43f));

        public static GameObject CreateDummy(
            Vector2 position,
            PrtsAssetDescriptor descriptor = null,
            bool active = true,
            string namePrefix = "DummyEnemy_")
        {
            var displayName = descriptor != null ? descriptor.DisplayName : "DummyEnemy";
            var go = new GameObject(namePrefix + displayName);
            go.transform.position = new Vector3(position.x, active ? EnemyGroundY : position.y, 0f);
            go.transform.localScale = Vector3.one;

            var hasPrtsPresentation = descriptor != null &&
                                      PrtsGeneratedPresentation.TryAttach(descriptor.BaseName, go.transform, out _);
            if (!hasPrtsPresentation)
                CreateVisualChild(go.transform, "Visual", new Vector2(0.8f, 1.45f), new Color(1.00f, 0.18f, 0.22f), 15, true);

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.gravityScale = 1f;

            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.72f, 1.40f);

            var archetype = ResolvePrototypeArchetype(descriptor);
            go.AddComponent<Health>().SetMaxHealth(ResolvePrototypeHealth(archetype));
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);
            go.AddComponent<StatusIndicator2D>();
            go.AddComponent<DummyEnemy>();

            var brain = go.AddComponent<PrototypeEnemyCombatBrain2D>();
            brain.Configure(archetype);
            go.AddComponent<EnemyHitReaction2D>();
            go.AddComponent<DamageTintFlash2D>();
            go.AddComponent<EnemyPresentationDriver2D>();

            go.SetActive(active);
            return go;
        }

        public static GameObject[] CreateEnemyTemplates(PrtsAssetDescriptor[] enemies)
        {
            var root = new GameObject("[EnemyTemplates]");
            var templates = new GameObject[3];
            templates[0] = CreateDummy(new Vector2(0f, -20f), enemies.Length > 1 ? enemies[1] : null, false, "EnemyTemplate_");
            templates[1] = CreateDummy(new Vector2(0f, -20f), enemies.Length > 3 ? enemies[3] : null, false, "EnemyTemplate_");
            templates[2] = CreateDummy(new Vector2(0f, -20f), enemies.Length > 2 ? enemies[2] : null, false, "EnemyTemplate_");

            foreach (var template in templates)
                if (template != null) template.transform.SetParent(root.transform, true);
            return templates;
        }

        public static PrototypeRoomLoopController CreateRoomLoop(Transform player, GameObject[] enemyTemplates)
        {
            var go = new GameObject("[RoomLoop]");
            var controller = go.AddComponent<PrototypeRoomLoopController>();
            controller.Configure(
                player,
                enemyTemplates,
                new[]
                {
                    new Vector2(4.0f, EnemyGroundY),
                    new Vector2(6.8f, EnemyGroundY),
                    new Vector2(9.6f, EnemyGroundY),
                    new Vector2(12.4f, EnemyGroundY),
                    new Vector2(15.2f, EnemyGroundY),
                    new Vector2(18.0f, EnemyGroundY)
                });
            return controller;
        }

        public static void CreateCamera(Transform target)
        {
            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(1.8f, 0.45f, 0f);
            rig.AddComponent<CameraFollow2D>().SetTarget(target);

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            camera.allowDynamicResolution = false;
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraShake2D>();
        }

        private static PrototypeEnemyArchetype ResolvePrototypeArchetype(PrtsAssetDescriptor descriptor)
        {
            if (descriptor == null)
                return PrototypeEnemyArchetype.Melee;
            return descriptor.Role switch
            {
                "FastMelee" => PrototypeEnemyArchetype.FastMelee,
                "Ranged" => PrototypeEnemyArchetype.Ranged,
                _ => PrototypeEnemyArchetype.Melee
            };
        }

        private static float ResolvePrototypeHealth(PrototypeEnemyArchetype archetype) => archetype switch
        {
            PrototypeEnemyArchetype.FastMelee => 45f,
            PrototypeEnemyArchetype.Ranged => 50f,
            _ => 60f
        };

        private static void CreateStaticBlock(string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            CreateVisualChild(go.transform, "Visual", size, color, 0, false);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static void CreateStaticCollider(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static GameObject CreateVisualChild(
            Transform parent,
            string name,
            Vector2 size,
            Color color,
            int sortingOrder,
            bool addHitFlash)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(color);
            if (addHitFlash)
                visual.AddComponent<HitFlash2D>();
            return visual;
        }
    }
}
#endif
