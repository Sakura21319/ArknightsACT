#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.CameraSystem;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Texas;
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
        private static readonly Vector2 FloorSize = new(30f, 0.5f);
        private const float FloorCenterY = -1.35f;
        private const float FloorTopY = -1.10f;

        public static void CreateServices()
        {
            new GameObject("[Services]").AddComponent<HitStopService>();
        }

        public static GameObject CreatePlayer(AttackDefinition[] attacks)
        {
            var go = new GameObject("Player_Texas_Graybox");
            go.transform.position = new Vector3(0f, FloorTopY + 0.76f, 0f);
            go.transform.localScale = Vector3.one;

            CreateVisualChild(go.transform, "Visual", new Vector2(0.8f, 1.5f), new Color(0.05f, 0.72f, 1.00f), 20, addHitFlash: false);

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.gravityScale = 1f;

            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.72f, 1.45f);
            collider.isTrigger = false;

            go.AddComponent<Health>().SetMaxHealth(100f);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);

            go.AddComponent<PlayerInputReader>();
            go.AddComponent<PlayerMotor2D>();
            go.AddComponent<PlayerDashController>();
            go.AddComponent<AttackSlashPresentation2D>();
            go.AddComponent<SwordRainPresentation2D>();
            go.AddComponent<PlayerAttackController>().Configure(attacks, 10f);
            go.AddComponent<PlayerDamageGate>();
            go.AddComponent<TexasSwordRainSkill>();
            go.AddComponent<PlayerSkillController>();
            go.AddComponent<TexasBuildRuntime>();
            go.AddComponent<TexasPrototypeHud>();

            return go;
        }

        public static void CreateFloor()
        {
            CreateStaticBlock("Floor", new Vector2(6f, FloorCenterY), FloorSize, new Color(0.20f, 0.21f, 0.24f));
        }

        public static void CreatePlatform(Vector2 position, Vector2 size)
        {
            CreateStaticBlock("Platform", position, size, new Color(0.34f, 0.37f, 0.43f));
        }

        private static void CreateStaticBlock(string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = Vector3.one;
            CreateVisualChild(go.transform, "Visual", size, color, 0, addHitFlash: false);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = false;
        }

        public static void CreateDummy(Vector2 position)
        {
            var go = new GameObject("DummyEnemy");
            go.transform.position = new Vector3(position.x, FloorTopY + 0.73f, 0f);
            go.transform.localScale = Vector3.one;
            CreateVisualChild(go.transform, "Visual", new Vector2(0.8f, 1.45f), new Color(1.00f, 0.18f, 0.22f), 15, addHitFlash: true);

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.gravityScale = 1f;

            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.72f, 1.40f);
            collider.isTrigger = false;

            go.AddComponent<Health>().SetMaxHealth(180f);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);
            go.AddComponent<DummyEnemy>();
        }

        public static void CreateCamera(Transform target)
        {
            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(1.5f, 0.6f, 0f);
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

        private static GameObject CreateVisualChild(Transform parent, string name, Vector2 size, Color color, int sortingOrder, bool addHitFlash)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(color);
            if (addHitFlash) visual.AddComponent<HitFlash2D>();
            return visual;
        }
    }
}
#endif
