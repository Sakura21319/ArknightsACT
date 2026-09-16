#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite.Progression;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypeTreasureFactory
    {
        public static void Create(Camera camera, Transform player)
        {
            if (camera == null || player == null)
                return;

            var playerEntity = player.GetComponent<CombatEntity>();
            var root = new GameObject("[Treasure25D]");

            CreateChest(
                root.transform,
                "Treasure_Normal",
                TreasureChestKind.Normal,
                PrtsPrototypeAssetCatalog.NormalTreasureChest,
                new Vector3(-1.2f, 0.03f, 3.25f),
                45f,
                camera,
                playerEntity);

            CreateChest(
                root.transform,
                "Treasure_Spike",
                TreasureChestKind.Spike,
                PrtsPrototypeAssetCatalog.SpikeTreasureChest,
                new Vector3(2.85f, 0.03f, -2.85f),
                70f,
                camera,
                playerEntity);

            CreateChest(
                root.transform,
                "Treasure_Monster",
                TreasureChestKind.Monster,
                PrtsPrototypeAssetCatalog.ChestSeaborn,
                new Vector3(8.75f, 0.03f, 1.75f),
                135f,
                camera,
                playerEntity);
        }

        private static GameObject CreateChest(
            Transform parent,
            string objectName,
            TreasureChestKind kind,
            PrtsAssetDescriptor descriptor,
            Vector3 position,
            float maxHealth,
            Camera camera,
            CombatEntity player)
        {
            var go = new GameObject(objectName);
            go.SetActive(false);
            go.transform.SetParent(parent, true);
            go.transform.position = position;

            if (kind == TreasureChestKind.Monster)
            {
                var controller = go.AddComponent<CharacterController>();
                controller.radius = 0.32f;
                controller.height = 0.90f;
                controller.center = new Vector3(0f, 0.45f, 0f);
                controller.stepOffset = 0.20f;
                controller.slopeLimit = 45f;
            }
            else
            {
                var collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.72f, 0.62f, 0.72f);
                collider.center = new Vector3(0f, 0.31f, 0f);
            }

            var health = go.AddComponent<Health>();
            health.SetMaxHealth(maxHealth);
            go.AddComponent<StatusController>();
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(go.transform, false);
            billboard.AddComponent<BillboardPresentation25D>().Configure(camera);

            GameObject dormantVisual = null;
            GameObject monsterVisual = null;
            if (kind == TreasureChestKind.Monster)
            {
                dormantVisual = AttachPresentationOrFallback(
                    billboard.transform,
                    PrtsPrototypeAssetCatalog.NormalTreasureChest,
                    TreasureChestKind.Normal,
                    "DormantChest");
                monsterVisual = AttachPresentationOrFallback(
                    billboard.transform,
                    PrtsPrototypeAssetCatalog.ChestSeaborn,
                    TreasureChestKind.Monster,
                    "ActiveMonster");
                if (monsterVisual != null)
                    monsterVisual.SetActive(false);
            }
            else
            {
                AttachPresentationOrFallback(billboard.transform, descriptor, kind, kind + "Visual");
            }

            if (kind == TreasureChestKind.Monster)
            {
                go.AddComponent<TreasureMonsterBrain25D>();
                var monsterPresentation = go.AddComponent<TreasureMonsterPresentation25D>();
                monsterPresentation.Configure(dormantVisual, monsterVisual);
                go.AddComponent<EnemyExperienceReward>().Configure(90);
            }

            go.AddComponent<TreasureChest25D>().Configure(kind, player);
            go.AddComponent<DamageTintFlash2D>();
            var healthBar = go.AddComponent<WorldHealthBar2D>();
            healthBar.ConfigureWorldLayout(
                kind == TreasureChestKind.Monster ? 1.02f : 0.84f,
                kind == TreasureChestKind.Monster ? 0.88f : 0.78f,
                0.065f);
            go.AddComponent<DamageNumberEmitter2D>();

            go.SetActive(true);
            return go;
        }

        private static GameObject AttachPresentationOrFallback(
            Transform parent,
            PrtsAssetDescriptor descriptor,
            TreasureChestKind kind,
            string objectName)
        {
            if (PrtsGeneratedPresentation.TryAttach(descriptor.BaseName, parent, out var presentation))
            {
                presentation.name = objectName;
                presentation.transform.localPosition = new Vector3(0f, -descriptor.FeetLocalY, 0f);
                return presentation;
            }

            var fallback = CreateFallback(parent, kind);
            fallback.name = objectName + "_Fallback";
            Debug.LogWarning(
                $"[ArknightsACT/Treasure] PRTS presentation missing for {descriptor.DisplayName}. " +
                "Run 'Download Roguelite Treasure' then '3. Build Presentation Prefabs'.",
                parent);
            return fallback;
        }

        private static GameObject CreateFallback(Transform parent, TreasureChestKind kind)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = kind + "_Fallback";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, kind == TreasureChestKind.Monster ? 0.42f : 0.30f, 0f);
            visual.transform.localScale = kind == TreasureChestKind.Monster
                ? new Vector3(0.68f, 0.82f, 0.62f)
                : new Vector3(0.68f, 0.52f, 0.58f);
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            var renderer = visual.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            var color = kind switch
            {
                TreasureChestKind.Spike => new Color(0.62f, 0.20f, 0.16f),
                TreasureChestKind.Monster => new Color(0.25f, 0.48f, 0.52f),
                _ => new Color(0.62f, 0.52f, 0.24f)
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            renderer.sharedMaterial = material;
            return visual;
        }
    }
}
#endif
