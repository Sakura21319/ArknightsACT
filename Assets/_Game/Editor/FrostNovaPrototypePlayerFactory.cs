#if UNITY_EDITOR
using System;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.FrostNova;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class FrostNovaPrototypePlayerFactory
    {
        public static GameObject Create25D(
            AttackDefinition[] attacks,
            Camera camera,
            FrostNovaSkinVariant skin)
        {
            var go = new GameObject("Player_FrostNova_" + skin.ToSkinId());
            go.SetActive(false);
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one;

            var controller = go.AddComponent<CharacterController>();
            controller.radius = 0.34f;
            controller.height = 1.62f;
            controller.center = new Vector3(0f, 0.81f, 0f);
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 45f;

            AddSharedGameplay(go, attacks, skin);
            go.AddComponent<ScavengingInventory25D>();

            var motor = go.AddComponent<PlayerMotor25D>();
            motor.SetCamera(camera);

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(go.transform, false);
            billboard.transform.localPosition = Vector3.zero;
            billboard.AddComponent<BillboardPresentation25D>().Configure(camera);

            var descriptor = GetDescriptor(skin);
            if (PrtsGeneratedPresentation.TryAttach(
                    descriptor.PrefabKey,
                    billboard.transform,
                    out var presentation))
            {
                presentation.transform.localPosition =
                    new Vector3(0f, -descriptor.FeetLocalY, 0f);
            }
            else
            {
                CreatePlaceholder(billboard.transform);
                Debug.LogWarning(
                    $"[ArknightsACT/FrostNova] Missing generated presentation for {descriptor.BaseName}. " +
                    "Generate FrostNova once after the local importer finishes.",
                    go);
            }

            var presentationDriver = go.AddComponent<FrostNovaPresentationDriver25D>();
            presentationDriver.Configure(skin);
            if (FrostNovaExtractedFxSetup.HasImported(skin))
                FrostNovaExtractedFxSetup.Configure(go, skin);

            go.AddComponent<DamageTintFlash2D>();
            go.AddComponent<WorldHealthBar2D>();
            go.AddComponent<DamageNumberEmitter2D>();

            go.SetActive(true);
            return go;
        }

        private static void AddSharedGameplay(
            GameObject go,
            AttackDefinition[] attacks,
            FrostNovaSkinVariant skin)
        {
            PlayableOperatorPrototypeComposer.AddFoundation(
                go,
                attacks,
                16f,
                OperatorProfession.Caster,
                CombatFeature.BasicAttack |
                CombatFeature.ActiveSkills |
                CombatFeature.Dash |
                CombatFeature.ArtsDamage,
                "FrostNova",
                "霜星",
                SkinId(skin),
                AvatarResourceKey(skin),
                string.Empty,
                string.Empty,
                maxHealth: 125f,
                physicalDefense: 2.0f,
                artsResistance: 20f);

            go.AddComponent<FrostNovaRangedBasicAttack>();
            var skill1 = go.AddComponent<FrostNovaSkill1>();
            var skill2 = go.AddComponent<FrostNovaSkill2>();
            skill1.ConfigureForSkin(skin);
            skill2.ConfigureForSkin(skin);
            PlayableOperatorPrototypeComposer.CompleteGameplay(go);

            FrostNovaLocalAssetBootstrap.ConfigureAudioProfile(go, skin);
        }

        private static void CreatePlaceholder(Transform parent)
        {
            var visual = new GameObject("FrostNovaPlaceholder");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.80f, 0f);
            visual.transform.localScale = new Vector3(0.72f, 1.60f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 30;
            visual.AddComponent<PlaceholderVisual2D>()
                .SetColor(new Color(0.62f, 0.84f, 0.95f));
        }

        private static string SkinId(FrostNovaSkinVariant skin) =>
            skin.ToSkinId();

        private static string AvatarResourceKey(FrostNovaSkinVariant skin) =>
            "UI/HUD/Operators/frostnova_default";

        private static PrtsAssetDescriptor GetDescriptor(FrostNovaSkinVariant skin) =>
            FrostNovaLocalAssetBootstrap.GetDescriptor(skin);
    }
}
#endif
