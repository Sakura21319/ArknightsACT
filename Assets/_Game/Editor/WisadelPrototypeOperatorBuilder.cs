#if UNITY_EDITOR
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class WisadelPrototypeOperatorBuilder : IPrototypeOperatorBuilder
    {
        public string OperatorId => "Wisadel";

        public void EnsureDefinitionAsset()
        {
            const string defaultAvatar = "UI/HUD/Operators/wisadel_default";
            PrototypeOperatorDefinitionAssetUtility.Ensure(
                "Operator_Wisadel",
                OperatorId,
                "维什戴尔",
                "WISADEL",
                40,
                new PlayableOperatorSkinDefinition(
                    "default",
                    "原版",
                    defaultAvatar,
                    "UI/Skills/Wisadel/s2",
                    "UI/Skills/Wisadel/s3",
                    true),
                new PlayableOperatorSkinDefinition(
                    "game#9",
                    "game#9",
                    "UI/HUD/Operators/wisadel_game_9",
                    "UI/Skills/Wisadel/s2",
                    "UI/Skills/Wisadel/s3"));
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            var skinId = skin != null ? skin.SkinId : "default";
            WisadelLocalAssetBootstrap.PrepareForBuild(skinId);
            return WisadelPrototypePlayerFactory.Create25D(
                WisadelPrototypeSceneBuilder.BuildAttackDefinitions(),
                camera,
                skinId);
        }

        public void RefreshAssets(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin,
            bool force)
        {
            WisadelLocalAssetBootstrap.RefreshSelectedAssets(
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

            WisadelLocalAssetBootstrap.ConfigurePlayer(
                player,
                skin != null ? skin.SkinId : "default");
            SyncPresentationLayout(player);
            EditorUtility.SetDirty(player);
        }
        private static void SyncPresentationLayout(GameObject player)
        {
            var layouts = player.GetComponentsInChildren<SpineVisualAutoLayout2D>(true);
            for (var i = 0; i < layouts.Length; i++)
            {
                var layout = layouts[i];
                if (layout == null)
                    continue;

                var serialized = new SerializedObject(layout);
                serialized.FindProperty("targetWorldHeight").floatValue = 1.64f;
                serialized.FindProperty("feetLocalY").floatValue = -0.72f;
                serialized.FindProperty("safeInitialScale").floatValue = 0.38f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(layout);
            }
        }

    }
}
#endif
