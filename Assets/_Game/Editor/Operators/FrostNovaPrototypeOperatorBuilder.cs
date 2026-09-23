#if UNITY_EDITOR
using System;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.FrostNova;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class FrostNovaPrototypeOperatorBuilder : IPrototypeOperatorBuilder
    {
        public string OperatorId => "FrostNova";

        public void EnsureDefinitionAsset()
        {
            const string avatar = "UI/HUD/Operators/frostnova_default";
            PrototypeOperatorDefinitionAssetUtility.Ensure(
                "Operator_FrostNova",
                OperatorId,
                "霜星",
                "FROSTNOVA",
                30,
                new PlayableOperatorSkinDefinition(
                    "default",
                    "霜星",
                    avatar,
                    string.Empty,
                    string.Empty,
                    true),
                new PlayableOperatorSkinDefinition(
                    "winter#1",
                    "冬痕",
                    avatar,
                    string.Empty,
                    string.Empty),
                new PlayableOperatorSkinDefinition(
                    "new#1",
                    "霜星·新",
                    avatar,
                    string.Empty,
                    string.Empty),
                new PlayableOperatorSkinDefinition(
                    "winter_new#1",
                    "冬痕·新",
                    avatar,
                    string.Empty,
                    string.Empty));
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (skin == null)
                throw new InvalidOperationException("FrostNova skin is missing.");

            var variant = FrostNovaSkinVariantExtensions.FromSkinId(skin.SkinId);
            FrostNovaLocalAssetBootstrap.PrepareForBuild(variant);
            return FrostNovaPrototypePlayerFactory.Create25D(
                FrostNovaPrototypeSceneBuilder.BuildAttackDefinitions(),
                camera,
                variant);
        }

        public void RefreshAssets(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin,
            bool force)
        {
            if (skin == null)
                return;

            var variant = FrostNovaSkinVariantExtensions.FromSkinId(skin.SkinId);
            FrostNovaLocalAssetBootstrap.RefreshSelectedAssets(variant, force);
        }

        public void RefreshExisting(
            GameObject player,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (player == null || skin == null)
                return;

            var variant = FrostNovaSkinVariantExtensions.FromSkinId(skin.SkinId);
            if (FrostNovaExtractedFxSetup.HasImported(variant))
                FrostNovaExtractedFxSetup.Configure(player, variant);
            FrostNovaLocalAssetBootstrap.ConfigureAudioProfile(player, variant);
        }
    }
}
#endif
