#if UNITY_EDITOR
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Skadi;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class SkadiPrototypeOperatorBuilder : IPrototypeOperatorBuilder
    {
        public string OperatorId => "Skadi";

        public void EnsureDefinitionAsset()
        {
            var definition = PrototypeOperatorDefinitionAssetUtility.Ensure(
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
            PrtsOperatorProgressionImporter.TryRefreshDefinition(definition, "char_263_skadi");
            PrtsOperatorSkillMasteryImporter.TryRefreshDefinition(definition, "char_263_skadi", 2, 3);
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (definition == null || !definition.HasE2Progression)
                throw new System.InvalidOperationException("Skadi is missing official E2 progression data.");

            var skinId = skin != null ? skin.SkinId : "default";
            SkadiLocalAssetBootstrap.RefreshSkin(skinId, false);
            return SkadiPrototypePlayerFactory.Create25D(
                SkadiPrototypeSceneBuilder.BuildAttackDefinitions(),
                camera,
                skinId,
                definition.E2Progression.Evaluate(1));
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

            if (player.GetComponent<SkadiSkill1>() == null)
                player.AddComponent<SkadiSkill1>();
            if (player.GetComponent<SkadiSkill2>() == null)
                player.AddComponent<SkadiSkill2>();
            if (player.GetComponent<SkadiSkillAudioCue>() == null)
                player.AddComponent<SkadiSkillAudioCue>();
            PlayableOperatorPrototypeComposer.CompleteGameplay(player);
            var combatProfile = player.GetComponent<ArknightsACT.Gameplay.Roguelite.PlayerCombatProfile>();
            if (combatProfile != null)
                combatProfile.Configure(
                    combatProfile.Features |
                    ArknightsACT.Gameplay.Roguelite.CombatFeature.ActiveSkills);

            SkadiLocalAssetBootstrap.ConfigurePlayer(
                player,
                skin != null ? skin.SkinId : "default");
            if (definition != null && definition.HasE2Progression)
            {
                var progression = player.GetComponent<OperatorProgressionController>();
                var level = progression != null ? progression.EliteLevel : 1;
                PlayableOperatorPrototypeComposer.ApplyFormalCombatProfile(
                    player,
                    definition.E2Progression.Evaluate(level));
            }
            EditorUtility.SetDirty(player);
        }
    }
}
#endif
