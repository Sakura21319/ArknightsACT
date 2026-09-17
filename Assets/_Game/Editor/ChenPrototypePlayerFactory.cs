#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Chen;
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
    /// <summary>
    /// Ch'en-specific composition. The legacy side-view factory is retained as a fallback,
    /// while Create25D is the production Phase 06 path.
    /// </summary>
    internal static class ChenPrototypePlayerFactory
    {
        public static GameObject Create(AttackDefinition[] attacks, float spawnY)
        {
            var go = new GameObject("Player_Chen");
            go.transform.position = new Vector3(0f, spawnY, 0f);
            go.transform.localScale = Vector3.one;

            GameObject combatPresentation = null;
            var hasCombatPresentation = PrtsGeneratedPresentation.TryAttach(
                PrtsPrototypeAssetCatalog.Chen.BaseName,
                go.transform,
                out combatPresentation);

            if (!hasCombatPresentation)
            {
                CreatePlaceholder(go.transform);
                Debug.LogWarning(
                    "[ArknightsACT/Spine] Ch'en combat presentation prefab is missing; using placeholder. " +
                    "Download Ch'en and run '3. Build Presentation Prefabs' before rebuilding Prototype Scene.",
                    go);
            }
            else
            {
                AttachMotionRetarget(go, go.transform, combatPresentation);
            }

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.gravityScale = 1f;

            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.72f, 1.45f);

            AddSharedGameplay(go, attacks);
            go.AddComponent<PlayerMotor2D>();
            go.AddComponent<ChenPresentationDriver2D>();
            AttachOriginalFx(go);
            go.AddComponent<DamageTintFlash2D>();
            go.AddComponent<WorldHealthBar2D>();
            go.AddComponent<DamageNumberEmitter2D>();
            return go;
        }

        public static GameObject Create25D(AttackDefinition[] attacks, Camera camera)
        {
            var go = new GameObject("Player_Chen");
            go.SetActive(false);
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one;

            var controller = go.AddComponent<CharacterController>();
            controller.radius = 0.34f;
            controller.height = 1.55f;
            controller.center = new Vector3(0f, 0.78f, 0f);
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 45f;

            AddSharedGameplay(go, attacks);

            var motor = go.AddComponent<PlayerMotor25D>();
            motor.SetCamera(camera);

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(go.transform, false);
            billboard.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            billboard.AddComponent<BillboardPresentation25D>().Configure(camera);

            GameObject combatPresentation = null;
            if (PrtsGeneratedPresentation.TryAttach(
                    PrtsPrototypeAssetCatalog.Chen.BaseName,
                    billboard.transform,
                    out combatPresentation))
            {
                combatPresentation.transform.localPosition =
                    new Vector3(0f, -PrtsPrototypeAssetCatalog.Chen.FeetLocalY, 0f);

                AttachMotionRetarget(go, billboard.transform, combatPresentation);
            }
            else
            {
                CreatePlaceholder(billboard.transform);
                Debug.LogWarning(
                    "[ArknightsACT/Spine] Ch'en combat presentation prefab is missing; using 2.5D placeholder.",
                    go);
            }

            go.AddComponent<ChenPresentationDriver25D>();
            AttachOriginalFx(go);
            go.AddComponent<DamageTintFlash2D>();
            go.AddComponent<WorldHealthBar2D>();
            go.AddComponent<DamageNumberEmitter2D>();
            go.SetActive(true);
            return go;
        }

        private static void AttachOriginalFx(GameObject go)
        {
            // Character-level Spine BG effects are not the skill slash effects. Keep that visual
            // family reserved for dash only, as a light character after-image/accent.
            var characterFx = go.AddComponent<ChenOriginalSpineFxVisibilityController>();
            characterFx.enabled = false;
            go.AddComponent<ChenDashSpineFxGateController>().Configure(characterFx);

            // Actual skill slashes come from independent game-client battle/prefabs/effects assets.
            // The local catalog wires them when extracted prefabs are present; there is no synthetic
            // LineRenderer or generated slash fallback.
            ChenOriginalSkillFxCatalog.Configure(go);
        }

        private static void AttachMotionRetarget(GameObject owner, Transform presentationParent, GameObject combatPresentation)
        {
            var hasMotionSource = PrtsGeneratedPresentation.TryAttach(
                PrtsPrototypeAssetCatalog.ChenBaseMotion.BaseName,
                presentationParent,
                out var motionSource);

            if (!hasMotionSource)
            {
                Debug.LogWarning(
                    "[ArknightsACT/Spine] Ch'en base motion source prefab is missing. " +
                    "Movement will use the combat presentation fallback until it is downloaded/built.",
                    owner);
                return;
            }

            motionSource.name = "MotionSource_Chen_Base";
            var retarget = owner.GetComponent<SpineBoneMotionRetarget2D>() ??
                           owner.AddComponent<SpineBoneMotionRetarget2D>();
            retarget.Configure(combatPresentation.transform, motionSource.transform, "Move");
        }

        private static void AddSharedGameplay(GameObject go, AttackDefinition[] attacks)
        {
            var health = go.GetComponent<Health>() ?? go.AddComponent<Health>();
            health.SetMaxHealth(100f);
            if (go.GetComponent<StatusController>() == null)
                go.AddComponent<StatusController>();

            var entity = go.GetComponent<CombatEntity>() ?? go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);

            go.AddComponent<PlayerInputReader>();
            go.AddComponent<PlayerDashController>();
            go.AddComponent<PlayerAttackController>().Configure(attacks, 10f);
            go.AddComponent<PlayerDamageGate>();

            go.AddComponent<ChenSkill1>();
            go.AddComponent<ChenSkill2>();
            go.AddComponent<PlayerSkillController>();
            go.AddComponent<ChenSkillUpgradeApplier>();

            var profile = go.AddComponent<PlayerCombatProfile>();
            profile.Configure(
                CombatFeature.BasicAttack |
                CombatFeature.ActiveSkills |
                CombatFeature.Dash |
                CombatFeature.PhysicalDamage |
                CombatFeature.ArtsDamage);
            go.AddComponent<CollectibleInventory>();
            go.AddComponent<LevelUpgradeInventory>();
            go.AddComponent<CharacterSkillUpgradeInventory>();
            go.AddComponent<TemporaryCombatBuffs>();
        }

        private static void CreatePlaceholder(Transform parent)
        {
            var visual = new GameObject("ChenPlaceholder");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.76f, 0f);
            visual.transform.localScale = new Vector3(0.78f, 1.52f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 30;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(new Color(0.60f, 0.70f, 0.82f));
        }
    }
}
#endif
