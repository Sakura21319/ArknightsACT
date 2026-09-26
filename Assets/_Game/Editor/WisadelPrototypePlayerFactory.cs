#if UNITY_EDITOR
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Wisadel;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class WisadelPrototypePlayerFactory
    {
        internal static GameObject Create25D(
            AttackDefinition[] attacks,
            Camera camera,
            string skinId)
        {
            var game9 = string.Equals(skinId, "game#9", System.StringComparison.OrdinalIgnoreCase);
            var descriptor = game9
                ? PrtsPrototypeAssetCatalog.WisadelGame9
                : PrtsPrototypeAssetCatalog.WisadelDefault;

            var player = new GameObject("Player_Wisadel_" + (game9 ? "game_9" : "default"));
            player.SetActive(false);
            player.transform.position = Vector3.zero;
            player.transform.localScale = Vector3.one;

            var characterController = player.AddComponent<CharacterController>();
            characterController.radius = 0.34f;
            characterController.height = 1.68f;
            characterController.center = new Vector3(0f, 0.84f, 0f);
            characterController.stepOffset = 0.28f;
            characterController.slopeLimit = 45f;

            PlayableOperatorPrototypeComposer.AddFoundation(
                player,
                attacks,
                15f,
                OperatorProfession.Sniper,
                CombatFeature.BasicAttack |
                CombatFeature.ActiveSkills |
                CombatFeature.Dash |
                CombatFeature.PhysicalDamage,
                "Wisadel",
                "维什戴尔",
                game9 ? "game#9" : "default",
                game9 ? "UI/HUD/Operators/wisadel_game_9" : "UI/HUD/Operators/wisadel_default",
                "UI/Skills/Wisadel/s2",
                "UI/Skills/Wisadel/s3",
                maxHealth: 105f,
                physicalDefense: 1.5f,
                artsResistance: 0f);

            player.AddComponent<WisadelRangedBasicAttack>();
            var skill2 = player.AddComponent<WisadelSkill>();
            skill2.ConfigurePrototype(1, "Hell's Siege", 24f, 10f, 0.62f, 25f, 1);
            var skill3 = player.AddComponent<WisadelSkill>();
            skill3.ConfigurePrototype(2, "Final Will", 32f, 12f, 0.72f, 0.5f, 6);
            PlayableOperatorPrototypeComposer.CompleteGameplay(player);
            player.AddComponent<ScavengingInventory25D>();

            var motor = player.AddComponent<PlayerMotor25D>();
            motor.SetCamera(camera);

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(player.transform, false);
            billboard.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            billboard.AddComponent<BillboardPresentation25D>().Configure(camera);

            if (PrtsGeneratedPresentation.TryAttach(descriptor.PrefabKey, billboard.transform, out var presentation))
            {
                presentation.transform.localPosition = new Vector3(0f, -descriptor.FeetLocalY, 0f);
                AttachMotionRetarget(player, billboard.transform, presentation, game9);
            }
            else
            {
                CreatePlaceholder(billboard.transform);
                Debug.LogWarning(
                    $"[ArknightsACT/Wisadel] Missing generated presentation {descriptor.PrefabKey}. " +
                    "Run the Wisadel local asset importer, then rebuild the active operator.",
                    player);
            }

            player.AddComponent<WisadelPresentationDriver25D>();
            PrepareCustomFxMountPoint(player);
            WisadelLocalAssetBootstrap.ConfigurePlayer(player, game9 ? "game#9" : "default");
            player.AddComponent<DamageTintFlash2D>();
            player.AddComponent<WorldHealthBar2D>();
            player.AddComponent<DamageNumberEmitter2D>();
            player.SetActive(true);
            return player;
        }

        private static void AttachMotionRetarget(
            GameObject owner,
            Transform presentationParent,
            GameObject combatPresentation,
            bool game9)
        {
            var descriptor = game9
                ? PrtsPrototypeAssetCatalog.WisadelGame9Motion
                : PrtsPrototypeAssetCatalog.WisadelDefaultMotion;
            if (!PrtsGeneratedPresentation.TryAttach(descriptor.PrefabKey, presentationParent, out var motionSource))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/Wisadel] Missing Move source {descriptor.PrefabKey}; " +
                    "the combat rig will use procedural locomotion.",
                    owner);
                return;
            }

            motionSource.name = "MotionSource_Wisadel_" + (game9 ? "game_9" : "default");
            // The build skeleton must occupy the same presentation origin as the combat rig when
            // it becomes the visible BaseMotion source (Move / Relax / Sit / Sleep / etc.).
            motionSource.transform.localPosition = combatPresentation.transform.localPosition;
            motionSource.transform.localRotation = combatPresentation.transform.localRotation;
            var retarget = owner.AddComponent<SpineBoneMotionRetarget2D>();
            retarget.Configure(
                combatPresentation.transform,
                motionSource.transform,
                "Move",
                new[] { "arm", "hand", "weapon", "gun", "launcher" },
                showFullSourceVisuals: true);

            // BaseMotion is also the source of Interact / Sit / Sleep / Special. Keep those
            // authored actions available on both default and game#9 instead of importing only Move.
            if (owner.GetComponent<BaseMotionActionShortcutController>() == null)
                owner.AddComponent<BaseMotionActionShortcutController>();
        }

        private static void PrepareCustomFxMountPoint(GameObject owner)
        {
            var mount = owner.transform.Find("CustomFxMountPoint");
            if (mount == null)
            {
                mount = new GameObject("CustomFxMountPoint").transform;
                mount.SetParent(owner.transform, false);
                mount.localPosition = new Vector3(0f, 0.82f, 0f);
            }
        }

        private static void CreatePlaceholder(Transform parent)
        {
            var visual = new GameObject("WisadelPlaceholder");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            visual.transform.localScale = new Vector3(0.72f, 1.56f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 30;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(new Color(0.58f, 0.28f, 0.27f));
        }
    }
}
#endif
