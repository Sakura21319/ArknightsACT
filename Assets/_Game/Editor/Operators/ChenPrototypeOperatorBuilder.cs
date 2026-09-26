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
            PrototypeOperatorDefinitionAssetUtility.Ensure(
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
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            return ChenPrototypePlayerFactory.Create25D(
                PrototypeSceneBuilder.BuildChenAttackDefinitions(),
                camera);
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
            PlayableOperatorPrototypeComposer.ApplyFormalCombatProfile(player, 100f, 2.2f, 5f);
        }
    }
}
#endif
