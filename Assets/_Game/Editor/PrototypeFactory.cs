#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.CameraSystem;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
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
            go.transform.position = new Vector3(0f, 1.1f, 0f);
            go.transform.localScale = new Vector3(0.75f, 1.45f, 1f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<PlaceholderVisual2D>().SetColor(new Color(0.18f, 0.50f, 0.95f));

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
            return go;
        }

        public static void CreateFloor()
        {
            CreateStaticBlock("Floor", new Vector2(6f, -0.15f), new Vector2(30f, 0.5f), new Color(0.18f, 0.18f, 0.20f));
        }

        public static void CreatePlatform(Vector2 position, Vector2 size)
        {
            CreateStaticBlock("Platform", position, size, new Color(0.30f, 0.32f, 0.36f));
        }

        private static void CreateStaticBlock(string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<PlaceholderVisual2D>().SetColor(color);
            go.AddComponent<BoxCollider2D>();
        }

        public static void CreateDummy(Vector2 position)
        {
            var go = new GameObject("DummyEnemy");
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.8f, 1.35f, 1f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<PlaceholderVisual2D>().SetColor(new Color(0.85f, 0.28f, 0.28f));
            go.AddComponent<HitFlash2D>();

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CapsuleCollider2D>();
            go.AddComponent<Health>().SetMaxHealth(100f);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);
            go.AddComponent<DummyEnemy>();
        }

        public static void CreateCamera(Transform target)
        {
            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(2f, 2.5f, 0f);
            rig.AddComponent<CameraFollow2D>().SetTarget(target);

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraShake2D>();
        }
    }
}
#endif
