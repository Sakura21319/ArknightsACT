#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.CameraSystem;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Debugging;
using ArknightsACT.Gameplay.Enemies;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypeFactory
    {
        public static void CreateServices()
        {
            new GameObject("[Services]").AddComponent<HitStopService>();
        }

        public static GameObject CreatePlayer(AttackDefinition[] attacks)
        {
            var go = new GameObject("Player_Texas_Graybox");
            go.transform.position = new Vector3(0f, 0.15f, 0f);
            go.transform.localScale = new Vector3(0.95f, 1.65f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 20;
            go.AddComponent<PlaceholderVisual2D>().SetColor(new Color(0.05f, 0.72f, 1.00f));
            go.AddComponent<WorldLabel2D>().Configure("德克萨斯 / PLAYER", new Vector2(0f, 0.85f));

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = Vector2.one;

            go.AddComponent<Health>().SetMaxHealth(100f);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);
            go.AddComponent<PlayerInputReader>();
            go.AddComponent<PlayerDashController>();
            go.AddComponent<PlayerMotor2D>();
            go.AddComponent<PlayerAttackController>().Configure(attacks, 10f);
            go.AddComponent<PlayerDamageGate>();
            go.AddComponent<WorldBoundsRespawner>();

            var hud = new GameObject("PrototypeDebugHUD").AddComponent<PrototypeDebugHud>();
            hud.BindPlayer(go);
            return go;
        }

        public static void CreateFloor()
        {
            CreateStaticBlock("Floor", new Vector2(6f, -1.35f), new Vector2(30f, 0.5f), new Color(0.20f, 0.21f, 0.24f));
        }

        public static void CreatePlatform(Vector2 position, Vector2 size)
        {
            CreateStaticBlock("Platform", position, size, new Color(0.34f, 0.37f, 0.43f));
        }

        private static void CreateStaticBlock(string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 0;
            go.AddComponent<PlaceholderVisual2D>().SetColor(color);

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;
            go.AddComponent<BoxCollider2D>();
        }

        public static void CreateDummy(Vector2 position)
        {
            var go = new GameObject("DummyEnemy");
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.95f, 1.55f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 15;
            go.AddComponent<PlaceholderVisual2D>().SetColor(new Color(1.00f, 0.18f, 0.22f));
            go.AddComponent<HitFlash2D>();
            go.AddComponent<WorldLabel2D>().Configure("训练敌人", new Vector2(0f, 0.85f));

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            go.AddComponent<CapsuleCollider2D>();
            go.AddComponent<Health>().SetMaxHealth(100f);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);
            go.AddComponent<DummyEnemy>();
            go.AddComponent<WorldBoundsRespawner>();
        }

        public static void CreateCamera(Transform target)
        {
            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(1.5f, 1.1f, 0f);
            rig.AddComponent<CameraFollow2D>().SetTarget(target);

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraShake2D>();
        }
    }
}
#endif
