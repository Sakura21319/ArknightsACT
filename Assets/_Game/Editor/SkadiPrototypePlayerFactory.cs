#if UNITY_EDITOR
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Skadi;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class SkadiPrototypePlayerFactory
    {
        internal static GameObject Create25D(
            AttackDefinition[] attacks,
            Camera camera,
            string skinId)
        {
            var normalizedSkin = NormalizeSkinId(skinId);
            var combatDescriptor = SkadiLocalAssetBootstrap.GetCombatDescriptor(normalizedSkin);
            var motionDescriptor = SkadiLocalAssetBootstrap.GetMotionDescriptor(normalizedSkin);

            var player = new GameObject("Player_Skadi_" + SafeSkinName(normalizedSkin));
            player.SetActive(false);
            player.transform.position = Vector3.zero;
            player.transform.localScale = Vector3.one;

            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.62f;
            controller.center = new Vector3(0f, 0.81f, 0f);
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 45f;

            PlayableOperatorPrototypeComposer.AddFoundation(
                player,
                attacks,
                17f,
                OperatorProfession.Guard,
                CombatFeature.BasicAttack |
                CombatFeature.Dash |
                CombatFeature.PhysicalDamage,
                "Skadi",
                "斯卡蒂",
                normalizedSkin,
                AvatarResourceKey(normalizedSkin),
                "UI/Skills/Skadi/s2",
                "UI/Skills/Skadi/s3",
                maxHealth: 120f,
                physicalDefense: 2.0f,
                artsResistance: 0f);
            PlayableOperatorPrototypeComposer.CompleteGameplay(player);
            player.AddComponent<ScavengingInventory25D>();

            var motor = player.AddComponent<PlayerMotor25D>();
            motor.SetCamera(camera);

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(player.transform, false);
            billboard.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            billboard.AddComponent<BillboardPresentation25D>().Configure(camera);

            if (PrtsGeneratedPresentation.TryAttach(
                    combatDescriptor.PrefabKey,
                    billboard.transform,
                    out var combatPresentation))
            {
                combatPresentation.transform.localPosition =
                    new Vector3(0f, -combatDescriptor.FeetLocalY, 0f);
                AttachMotionRetarget(
                    player,
                    billboard.transform,
                    combatPresentation,
                    motionDescriptor,
                    normalizedSkin);
            }
            else
            {
                CreatePlaceholder(billboard.transform);
                Debug.LogWarning(
                    $"[ArknightsACT/Skadi] Missing generated presentation '{combatDescriptor.PrefabKey}'. " +
                    "Refresh Skadi assets and rebuild the current operator.",
                    player);
            }

            player.AddComponent<SkadiPresentationDriver25D>();
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
            PrtsAssetDescriptor motionDescriptor,
            string skinId)
        {
            if (motionDescriptor == null ||
                !PrtsGeneratedPresentation.TryAttach(
                    motionDescriptor.PrefabKey,
                    presentationParent,
                    out var motionSource))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/Skadi] Missing Move source for skin '{skinId}'; " +
                    "locomotion will use the combat presentation fallback.",
                    owner);
                return;
            }

            motionSource.name = "MotionSource_Skadi_" + SafeSkinName(skinId);
            var retarget = owner.AddComponent<SpineBoneMotionRetarget2D>();
            retarget.Configure(
                combatPresentation.transform,
                motionSource.transform,
                "Move",
                new[] { "arm", "hand", "weapon", "sword", "blade" });
        }

        private static string NormalizeSkinId(string skinId)
        {
            if (string.Equals(skinId, "marthe_5", System.StringComparison.OrdinalIgnoreCase))
                return "marthe#5";
            if (string.Equals(skinId, "summer_3", System.StringComparison.OrdinalIgnoreCase))
                return "summer#3";
            if (string.Equals(skinId, "marthe#5", System.StringComparison.OrdinalIgnoreCase))
                return "marthe#5";
            if (string.Equals(skinId, "summer#3", System.StringComparison.OrdinalIgnoreCase))
                return "summer#3";
            return "default";
        }

        private static string AvatarResourceKey(string skinId)
        {
            if (string.Equals(skinId, "marthe#5", System.StringComparison.OrdinalIgnoreCase))
                return "UI/HUD/Operators/skadi_marthe_5";
            if (string.Equals(skinId, "summer#3", System.StringComparison.OrdinalIgnoreCase))
                return "UI/HUD/Operators/skadi_summer_3";
            return "UI/HUD/Operators/skadi_default";
        }

        private static string SafeSkinName(string skinId) =>
            (skinId ?? "default").Replace('#', '_').Replace(' ', '_');

        private static void CreatePlaceholder(Transform parent)
        {
            var visual = new GameObject("SkadiPlaceholder");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            visual.transform.localScale = new Vector3(0.74f, 1.56f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 30;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(new Color(0.45f, 0.52f, 0.64f));
        }
    }
}
#endif
