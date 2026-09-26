#if UNITY_EDITOR
using ArknightsACT.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class SkadiPrototypeOperatorBuilder : IPrototypeOperatorBuilder
    {
        public string OperatorId => "Skadi";

        public void EnsureDefinitionAsset()
        {
            PrototypeOperatorDefinitionAssetUtility.Ensure(
                "Operator_Skadi",
                OperatorId,
                "斯卡蒂",
                "SKADI",
                50,
                new PlayableOperatorSkinDefinition(
                    "default",
                    "原版",
                    "UI/HUD/Operators/skadi_default",
                    "UI/Skills/Skadi/s2",
                    "UI/Skills/Skadi/s3",
                    true),
                new PlayableOperatorSkinDefinition(
                    "marthe#5",
                    "marthe#5",
                    "UI/HUD/Operators/skadi_marthe_5",
                    "UI/Skills/Skadi/s2",
                    "UI/Skills/Skadi/s3"),
                new PlayableOperatorSkinDefinition(
                    "summer#3",
                    "summer#3",
                    "UI/HUD/Operators/skadi_summer_3",
                    "UI/Skills/Skadi/s2",
                    "UI/Skills/Skadi/s3"));
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            var skinId = skin != null ? skin.SkinId : "default";
            SkadiLocalAssetBootstrap.RefreshSkin(skinId, false);
            return SkadiPrototypePlayerFactory.Create25D(
                SkadiPrototypeSceneBuilder.BuildAttackDefinitions(),
                camera,
                skinId);
        }

        public void RefreshAssets(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin,
            bool force)
        {
            SkadiLocalAssetBootstrap.RefreshSkin(
                skin != null ? skin.SkinId : "default",
                force);
        }

        public void RefreshExisting(
            GameObject player,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (player == null)
                return;

            PlayableOperatorPrototypeComposer.ApplyFormalCombatProfile(
                player,
                maxHealth: 120f,
                physicalDefense: 2.0f,
                artsResistance: 0f);
            EditorUtility.SetDirty(player);
        }
    }
}
#endif
