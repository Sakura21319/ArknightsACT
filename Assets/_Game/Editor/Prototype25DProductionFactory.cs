#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.CameraSystem;
using ArknightsACT.Gameplay.Enemies;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Facilities;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite.Progression;
using ArknightsACT.Gameplay.Rooms;
using UnityEditor;
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

        public static TrainingDummyFacility CreateTrainingDummy(Transform playerSpawn)
        {
            var root = new GameObject("[TrainingDummyFacility]");
            var facility = root.AddComponent<TrainingDummyFacility>();
            var spawnPosition = playerSpawn != null ? playerSpawn.position : Vector3.zero;
            var motor = playerSpawn != null
                ? playerSpawn.GetComponent<ArknightsACT.Gameplay.Characters.PlayerMotor25D>()
                : null;
            var forward = motor != null ? motor.PlanarForward : Vector3.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;

            facility.Configure(
                spawnPosition +
                forward.normalized * TrainingDummyFacility.SpawnTestDistance +
                Vector3.up * 0.03f,
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Generated/PRTS/Prefabs/enemy_1006_shield.prefab"),
                showBar: true);
            return facility;
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
                    new Vector3(4.1f, 0.03f, 2.0f),
                    new Vector3(6.2f, 0.03f, -3.3f),
                    new Vector3(2.6f, 0.03f, -4.5f),
                    new Vector3(-4.4f, 0.03f, 4.2f),
                    new Vector3(9.0f, 0.03f, -2.4f),
                    new Vector3(-5.7f, 0.03f, -3.7f)
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

            var health = go.GetComponent<Health>() ?? go.AddComponent<Health>();
            health.SetMaxHealth(healthOverride ?? ResolveHealth(archetype));
            if (go.GetComponent<StatusController>() == null)
                go.AddComponent<StatusController>();

            var entity = go.GetComponent<CombatEntity>() ?? go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);

            var stats = go.GetComponent<CombatStats>() ?? go.AddComponent<CombatStats>();
            stats.SetBasePhysicalDefense(healthOverride.HasValue ? 4f : ResolveDefense(archetype));
            stats.SetBaseArtsResistance(healthOverride.HasValue ? 10f : ResolveResistance(archetype));

            if (healthOverride.HasValue)
            {
                var resistance = go.GetComponent<StatusResistanceProfile>() ??
                                 go.AddComponent<StatusResistanceProfile>();
                resistance.ClearRules();
                resistance.ConfigureTagRule(
                    CombatStatusTags.HardCrowdControl,
                    immune: false,
                    durationMultiplier: 0.35f);
                resistance.ConfigureTagRule(
                    CombatStatusTags.MovementImpair,
                    immune: false,
                    durationMultiplier: 0.65f);
                resistance.ConfigureIdRule(CombatStatusIds.Disarm, immune: true);
            }

            var experience = go.AddComponent<EnemyExperienceReward>();
            experience.Configure(healthOverride.HasValue ? 120 : ResolveExperience(archetype));

            var brain = go.AddComponent<PrototypeEnemyCombatBrain25D>();
            brain.Configure(archetype, Vector3.back);
            go.AddComponent<EnemyVisionCone25D>();
            go.AddComponent<EnemyDeathCleanup25D>();

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

        private static float ResolveDefense(PrototypeEnemyArchetype archetype) => archetype switch
        {
            PrototypeEnemyArchetype.FastMelee => 0.5f,
            PrototypeEnemyArchetype.Ranged => 0.75f,
            _ => 1.0f
        };

        private static float ResolveResistance(PrototypeEnemyArchetype archetype) => archetype switch
        {
            PrototypeEnemyArchetype.Ranged => 5f,
            _ => 0f
        };

        private static int ResolveExperience(PrototypeEnemyArchetype archetype) => archetype switch
        {
            PrototypeEnemyArchetype.FastMelee => 22,
            PrototypeEnemyArchetype.Ranged => 32,
            _ => 28
        };
    }
}
#endif