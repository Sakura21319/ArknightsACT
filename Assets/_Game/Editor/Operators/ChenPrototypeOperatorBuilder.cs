#if UNITY_EDITOR
using ArknightsACT.Editor.Effects;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.EditorTools;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class ChenPrototypeOperatorBuilder : IPrototypeOperatorBuilder
    {
        public string OperatorId => "Chen";

        public void EnsureDefinitionAsset()
        {
            var definition = PrototypeOperatorDefinitionAssetUtility.Ensure(
                "Operator_Chen",
                OperatorId,
                "陈",
                "CH'EN",
                10,
                new PlayableOperatorSkinDefinition(
                    "default",
                    "默认",
                    "UI/HUD/chen_avatar",
                    "UI/Skills/chen_badao",
                    "UI/Skills/chen_jueying",
                    true));
            PrtsOperatorProgressionImporter.TryRefreshDefinition(definition, "char_010_chen");
            PrtsOperatorSkillMasteryImporter.TryRefreshDefinition(definition, "char_010_chen", 2, 3);
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (definition == null || !definition.HasE2Progression)
                throw new System.InvalidOperationException("Chen is missing official E2 progression data.");

            return ChenPrototypePlayerFactory.Create25D(
                PrototypeSceneBuilder.BuildChenAttackDefinitions(),
                camera,
                definition.E2Progression.Evaluate(1));
        }

        public void RefreshAssets(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin,
            bool force)
        {
            PrtsCombatHudAssetBootstrap.RefreshAssets(force);
            PrtsOriginalSpineFxSetup.PrepareDownloadedChen();
        }

        public void RefreshExisting(
            GameObject player,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (player == null)
                return;

            PrtsOriginalSpineFxSetup.PrepareDownloadedChen();
            if (ChenExtractedFxSetup.HasImportedEffects())
                ChenExtractedFxSetup.Configure(player);

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
