#if UNITY_EDITOR
using System;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Schwarz;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class SchwarzPrototypeOperatorBuilder : IPrototypeOperatorBuilder
    {
        public string OperatorId => "Schwarz";

        public void EnsureDefinitionAsset()
        {
            PrototypeOperatorDefinitionAssetUtility.Ensure(
                "Operator_Schwarz",
                OperatorId,
                "黑",
                "SCHWARZ",
                20,
                new PlayableOperatorSkinDefinition(
                    "default",
                    "原版",
                    "UI/HUD/Operators/schwarz_default",
                    "UI/Skills/Schwarz/s2",
                    "UI/Skills/Schwarz/s3",
                    true),
                new PlayableOperatorSkinDefinition(
                    "snow#1",
                    "Snow",
                    "UI/HUD/Operators/schwarz_snow",
                    "UI/Skills/Schwarz/s2",
                    "UI/Skills/Schwarz/s3"),
                new PlayableOperatorSkinDefinition(
                    "striker#1",
                    "Striker",
                    "UI/HUD/Operators/schwarz_striker",
                    "UI/Skills/Schwarz/s2",
                    "UI/Skills/Schwarz/s3"));
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            var variant = ParseSkin(skin?.SkinId);
            SchwarzLocalAssetBootstrap.PrepareForBuild(variant);
            return SchwarzPrototypePlayerFactory.Create25D(
                SchwarzPrototypeSceneBuilder.BuildAttackDefinitions(),
                camera,
                variant);
        }

        public void RefreshAssets(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin,
            bool force)
        {
            SchwarzLocalAssetBootstrap.RefreshSelectedAssets(ParseSkin(skin?.SkinId), force);
        }

        public void RefreshExisting(
            GameObject player,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (player == null)
                return;

            var variant = ParseSkin(skin?.SkinId);
            SchwarzExtractedFxSetup.Configure(player, variant);
            SchwarzLocalAssetBootstrap.ConfigureAudioProfile(player);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(player);
#endif
        }

        private static SchwarzSkinVariant ParseSkin(string skinId)
        {
            if (string.Equals(skinId, "snow#1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skinId, "snow", StringComparison.OrdinalIgnoreCase))
                return SchwarzSkinVariant.Snow;

            if (string.Equals(skinId, "striker#1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skinId, "striker", StringComparison.OrdinalIgnoreCase))
                return SchwarzSkinVariant.Striker;

            return SchwarzSkinVariant.Default;
        }
    }
}
#endif
