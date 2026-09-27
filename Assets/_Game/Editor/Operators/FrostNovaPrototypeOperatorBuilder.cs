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
            var definition = PrototypeOperatorDefinitionAssetUtility.Ensure(
                "Operator_FrostNova",
                OperatorId,
                "霜星",
                "FROSTNOVA",
                30,
                new PlayableOperatorSkinDefinition(
                    "winter#1",
                    "冬痕",
                    avatar,
                    string.Empty,
                    string.Empty,
                    true));

            if (!FrostNovaWinterTraceCombatProfile.TryLoad(out var winterTrace))
                throw new InvalidOperationException(
                    "Missing FrostNovaWinterTraceCombatProfile.json.");

            definition.ConfigureProgression(
                FrostNovaWinterTraceCombatProfile.ProgressionSourceId,
                FrostNovaWinterTraceCombatProfile.BuildProjectProgression(winterTrace));
            definition.ConfigureSkillMastery(
                FrostNovaWinterTraceCombatProfile.BuildSkillMasterySet(winterTrace));
            UnityEditor.EditorUtility.SetDirty(definition);
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (skin == null)
                throw new InvalidOperationException("FrostNova skin is missing.");
            if (definition == null || !definition.HasE2Progression)
                throw new InvalidOperationException("FrostNova WinterTrace progression is missing.");

            var variant = FrostNovaSkinVariantExtensions.FromSkinId(skin.SkinId);
            FrostNovaLocalAssetBootstrap.PrepareForBuild(variant);
            return FrostNovaPrototypePlayerFactory.Create25D(
                FrostNovaPrototypeSceneBuilder.BuildAttackDefinitions(),
                camera,
                variant,
                definition.E2Progression.Evaluate(1));
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

            if (definition != null && definition.HasE2Progression)
            {
                var progression = player.GetComponent<OperatorProgressionController>();
                var level = progression != null ? progression.EliteLevel : 1;
                PlayableOperatorPrototypeComposer.ApplyFormalCombatProfile(
                    player,
                    definition.E2Progression.Evaluate(level));
            }
        }
    }
}
#endif
