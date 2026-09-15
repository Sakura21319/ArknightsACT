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
    internal static class Prototype25DProductionFactory
    {
        private static readonly Vector3 CameraOffset = new(-10.5f, 6.8f, -10.5f);

        public static void ConfigureCamera(Camera camera, Transform player)
        {
            if (camera == null || player == null)
                return;

            var demoFollow = camera.GetComponent<ArknightsACT.Gameplay.Prototype25D.Prototype25DCameraFollow>();
            if (demoFollow != null)
                Object.DestroyImmediate(demoFollow);

            var rig = new GameObject("CameraRig25D");
            rig.transform.position = camera.transform.position;
            rig.transform.rotation = camera.transform.rotation;

            camera.transform.SetParent(rig.transform, true);
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;

            var follow = rig.AddComponent<CameraFollow25D>();
            follow.Configure(player, CameraOffset);

            if (camera.GetComponent<CameraShake2D>() == null)
                camera.gameObject.AddComponent<CameraShake2D>();
        }

        public static GameObject[] CreateEnemyTemplates(Camera camera)
        {
            var enemies = PrtsPrototypeAssetCatalog.PrototypeEnemies;
            var root = new GameObject("[EnemyTemplates25D]");
            var templates = new GameObject[4];
            templates[0] = CreateEnemyTemplate(enemies.Length > 1 ? enemies[1] : null, PrototypeEnemyArchetype.Melee, camera, "Soldier");
            templates[1] = CreateEnemyTemplate(enemies.Length > 3 ? enemies[3] : null, PrototypeEnemyArchetype.FastMelee, camera, "Hound");
            templates[2] = CreateEnemyTemplate(enemies.Length > 2 ? enemies[2] : null, PrototypeEnemyArchetype.Ranged, camera, "Crossbowman");
            templates[3] = CreateEnemyTemplate(enemies.Length > 5 ? enemies[5] : null, PrototypeEnemyArchetype.Melee, camera, "HeavyDefender", 180f);

            for (var i = 0; i < templates.Length; i++)
            {
                if (templates[i] != null)
                    templates[i].transform.SetParent(root.transform, true);
            }
            return templates;
        }

        public static PrototypeRoomLoopController CreateRoomLoop(Transform player, GameObject[] enemyTemplates)
        {
            var go = new GameObject("[RoomLoop]");
            var controller = go.AddComponent<PrototypeRoomLoopController>();
            controller.Configure25D(
                player,
                enemyTemplates,
                new[]
                {
                    new Vector3(4.5f, 0.03f, 2.8f),
                    new Vector3(6.2f, 0.03f, -2.3f),
                    new Vector3(2.7f, 0.03f, -4.1f),
                    new Vector3(-3.8f, 0.03f, 4.0f),
                    new Vector3(7.8f, 0.03f, 4.1f),
                    new Vector3(-5.0f, 0.03f, -3.0f)
                });
            return controller;
        }

        private static GameObject CreateEnemyTemplate(
            PrtsAssetDescriptor descriptor,
            PrototypeEnemyArchetype archetype,
            Camera camera,
            string fallbackName,
            float? healthOverride = null)
        {
            var displayName = descriptor != null ? descriptor.DisplayName : fallbackName;
            var go = new GameObject("EnemyTemplate25D_" + displayName);
            go.SetActive(false);
            go.transform.position = new Vector3(0f, -20f, 0f);

            var controller = go.AddComponent<CharacterController>();
            controller.radius = archetype == PrototypeEnemyArchetype.FastMelee ? 0.30f : 0.36f;
            controller.height = archetype == PrototypeEnemyArchetype.FastMelee ? 1.0f : 1.5f;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            controller.stepOffset = 0.25f;
            controller.slopeLimit = 45f;

            var health = go.AddComponent<Health>();
            health.SetMaxHealth(healthOverride ?? ResolveHealth(archetype));
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);

            var brain = go.AddComponent<PrototypeEnemyCombatBrain25D>();
            brain.Configure(archetype, Vector3.back);
            go.AddComponent<EnemyVisionCone25D>();

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(go.transform, false);
            billboard.transform.localPosition = Vector3.zero;
            billboard.AddComponent<BillboardPresentation25D>().Configure(camera);

            if (descriptor != null && PrtsGeneratedPresentation.TryAttach(descriptor.BaseName, billboard.transform, out var presentation))
            {
                presentation.transform.localPosition = new Vector3(0f, -descriptor.FeetLocalY, 0f);
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.name = fallbackName + "_Fallback";
                visual.transform.SetParent(billboard.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                visual.transform.localScale = new Vector3(0.8f, 1.5f, 1f);
                var collider = visual.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);
            }

            go.AddComponent<EnemyPresentationDriver25D>();
            go.AddComponent<DamageTintFlash2D>();
            go.AddComponent<WorldHealthBar2D>();
            go.AddComponent<DamageNumberEmitter2D>();
            return go;
        }

        private static float ResolveHealth(PrototypeEnemyArchetype archetype) => archetype switch
        {
            PrototypeEnemyArchetype.FastMelee => 45f,
            PrototypeEnemyArchetype.Ranged => 50f,
            _ => 60f
        };
    }
}
#endif
