#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Chen;
using ArknightsACT.Gameplay.Characters.Schwarz;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Progression;
using ArknightsACT.Gameplay.Roguelite.SkillUpgrades;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class SchwarzPrototypePlayerFactory
    {
        public static GameObject Create25D(
            AttackDefinition[] attacks,
            Camera camera,
            SchwarzSkinVariant skin)
        {
            var go = new GameObject("Player_Schwarz");
            go.SetActive(false);
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one;

            var controller = go.AddComponent<CharacterController>();
            controller.radius = 0.34f;
            controller.height = 1.55f;
            controller.center = new Vector3(0f, 0.78f, 0f);
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 45f;

            AddSharedGameplay(go, attacks, skin);

            var motor = go.AddComponent<PlayerMotor25D>();
            motor.SetCamera(camera);

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(go.transform, false);
            billboard.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            billboard.AddComponent<BillboardPresentation25D>().Configure(camera);

            var combatDescriptor = GetCombatDescriptor(skin);
            GameObject combatPresentation = null;
            if (PrtsGeneratedPresentation.TryAttach(
                    combatDescriptor.BaseName,
                    billboard.transform,
                    out combatPresentation))
            {
                combatPresentation.transform.localPosition =
                    new Vector3(0f, -combatDescriptor.FeetLocalY, 0f);
                AttachMotionRetarget(go, billboard.transform, combatPresentation, skin);
            }
            else
            {
                CreatePlaceholder(billboard.transform);
                Debug.LogWarning(
                    $"[ArknightsACT/Schwarz] Missing generated presentation for {combatDescriptor.BaseName}. " +
                    "Run the Schwarz local asset importer or rebuild this scene.",
                    go);
            }

            go.AddComponent<SchwarzPresentationDriver25D>();
            PrepareCustomFxMountPoint(go);
            if (SchwarzExtractedFxSetup.HasVariantImported(skin))
                SchwarzExtractedFxSetup.Configure(go, skin);

            var trainingDummy = go.GetComponent<ChenTrainingDummySpawner>() ??
                                go.AddComponent<ChenTrainingDummySpawner>();
            trainingDummy.ConfigurePresentationPrefab(
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Generated/PRTS/Prefabs/enemy_1006_shield.prefab"));

            go.AddComponent<DamageTintFlash2D>();
            go.AddComponent<WorldHealthBar2D>();
            go.AddComponent<DamageNumberEmitter2D>();
            go.SetActive(true);
            return go;
        }

        private static void AddSharedGameplay(
            GameObject go,
            AttackDefinition[] attacks,
            SchwarzSkinVariant skin)
        {
            var health = go.GetComponent<Health>() ?? go.AddComponent<Health>();
            health.SetMaxHealth(100f);
            if (go.GetComponent<StatusController>() == null)
                go.AddComponent<StatusController>();

            var entity = go.GetComponent<CombatEntity>() ?? go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);

            go.AddComponent<PlayerInputReader>();
            go.AddComponent<PlayerDashController>();
            go.AddComponent<PlayerAttackController>().Configure(attacks, 18f);
            go.AddComponent<SchwarzRangedBasicAttack>();
            go.AddComponent<PlayerDamageGate>();

            go.AddComponent<SchwarzSkill1>();
            go.AddComponent<SchwarzSkill2>();
            go.AddComponent<SchwarzSniperModeController>();
            go.AddComponent<PlayerSkillController>();
            go.AddComponent<SchwarzSkillUpgradeApplier>();

            var profile = go.AddComponent<PlayerCombatProfile>();
            profile.SetProfession(OperatorProfession.Sniper);
            profile.Configure(
                CombatFeature.BasicAttack |
                CombatFeature.ActiveSkills |
                CombatFeature.Dash |
                CombatFeature.PhysicalDamage);

            var identity = go.AddComponent<PlayableOperatorIdentity>();
            identity.Configure(
                "Schwarz",
                "黑",
                SkinId(skin),
                AvatarResourceKey(skin),
                "UI/Skills/Schwarz/s2",
                "UI/Skills/Schwarz/s3");

            go.AddComponent<CollectibleInventory>();
            go.AddComponent<LevelUpgradeInventory>();
            go.AddComponent<CharacterSkillUpgradeInventory>();
            go.AddComponent<TemporaryCombatBuffs>();
        }

        private static void AttachMotionRetarget(
            GameObject owner,
            Transform presentationParent,
            GameObject combatPresentation,
            SchwarzSkinVariant skin)
        {
            var motionDescriptor = GetMotionDescriptor(skin);
            if (!PrtsGeneratedPresentation.TryAttach(
                    motionDescriptor.BaseName,
                    presentationParent,
                    out var motionSource))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/Schwarz] Missing base-motion source {motionDescriptor.BaseName}; " +
                    "locomotion will fall back to the combat presentation.",
                    owner);
                return;
            }

            motionSource.name = "MotionSource_Schwarz_" + SkinId(skin);
            var retarget = owner.AddComponent<SpineBoneMotionRetarget2D>();
            retarget.Configure(
                combatPresentation.transform,
                motionSource.transform,
                "Move",
                new[]
                {
                    // Keep Schwarz's combat-rig arms/hands/weapon bones untouched so the
                    // crossbow stays in the authored grip while legs/body inherit locomotion.
                    "arm", "hand", "weapon", "cross", "bow", "arrow"
                });
        }

        private static void PrepareCustomFxMountPoint(GameObject owner)
        {
            if (owner.transform.Find("CustomFxMountPoint") != null)
                return;
            var mount = new GameObject("CustomFxMountPoint");
            mount.transform.SetParent(owner.transform, false);
            mount.transform.localPosition = new Vector3(0f, 0.82f, 0f);
        }

        private static void CreatePlaceholder(Transform parent)
        {
            var visual = new GameObject("SchwarzPlaceholder");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.76f, 0f);
            visual.transform.localScale = new Vector3(0.72f, 1.52f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 30;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(new Color(0.30f, 0.40f, 0.34f));
        }

        private static string SkinId(SchwarzSkinVariant skin) =>
            skin switch
            {
                SchwarzSkinVariant.Snow => "snow#1",
                SchwarzSkinVariant.Striker => "striker#1",
                _ => "default"
            };

        private static string AvatarResourceKey(SchwarzSkinVariant skin) =>
            skin switch
            {
                SchwarzSkinVariant.Snow => "UI/HUD/Operators/schwarz_snow",
                SchwarzSkinVariant.Striker => "UI/HUD/Operators/schwarz_striker",
                _ => "UI/HUD/Operators/schwarz_default"
            };

        private static PrtsAssetDescriptor GetCombatDescriptor(SchwarzSkinVariant skin) =>
            skin switch
            {
                SchwarzSkinVariant.Snow => PrtsPrototypeAssetCatalog.SchwarzSnow,
                SchwarzSkinVariant.Striker => PrtsPrototypeAssetCatalog.SchwarzStriker,
                _ => PrtsPrototypeAssetCatalog.SchwarzDefault
            };

        private static PrtsAssetDescriptor GetMotionDescriptor(SchwarzSkinVariant skin) =>
            skin switch
            {
                SchwarzSkinVariant.Snow => PrtsPrototypeAssetCatalog.SchwarzSnowMotion,
                SchwarzSkinVariant.Striker => PrtsPrototypeAssetCatalog.SchwarzStrikerMotion,
                _ => PrtsPrototypeAssetCatalog.SchwarzDefaultMotion
            };
    }
}
#endif
