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
            var definition = PrototypeOperatorDefinitionAssetUtility.Ensure(
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
            PrtsOperatorProgressionImporter.TryRefreshDefinition(definition, "char_340_shwaz");
            PrtsOperatorSkillMasteryImporter.TryRefreshDefinition(definition, "char_340_shwaz", 2, 3);
        }

        public GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (definition == null || !definition.HasE2Progression)
                throw new InvalidOperationException("Schwarz is missing official E2 progression data.");

            var variant = ParseSkin(skin?.SkinId);
            SchwarzLocalAssetBootstrap.PrepareForBuild(variant);
            var player = SchwarzPrototypePlayerFactory.Create25D(
                SchwarzPrototypeSceneBuilder.BuildAttackDefinitions(),
                camera,
                variant,
                definition.E2Progression.Evaluate(1));
            ConfigureOfficialTalent(player);
            return player;
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
            player.GetComponent<SchwarzSkill1>()?.ConfigureFormalLifecycle();
            player.GetComponent<SchwarzSkill2>()?.ConfigureFormalLifecycle();
            if (definition != null && definition.HasE2Progression)
            {
                var progression = player.GetComponent<OperatorProgressionController>();
                var level = progression != null ? progression.EliteLevel : 1;
                PlayableOperatorPrototypeComposer.ApplyFormalCombatProfile(
                    player,
                    definition.E2Progression.Evaluate(level));
            }
            ConfigureOfficialTalent(player);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(player);
#endif
        }

        private static void ConfigureOfficialTalent(GameObject player)
        {
            var talent = player != null ? player.GetComponent<SchwarzArmorBreakTalent>() : null;
            if (talent == null)
                return;

            if (!PrtsOperatorProgressionImporter.TryExtractE2TalentBlackboard(
                    "char_340_shwaz",
                    talentIndex: 0,
                    out var blackboard) ||
                !blackboard.TryGetValue("prob", out var probability) ||
                !blackboard.TryGetValue("atk_scale", out var attackScale) ||
                !blackboard.TryGetValue("def", out var defenseDelta) ||
                !blackboard.TryGetValue("defdown_duration", out var duration))
            {
                Debug.LogError(
                    "[ArknightsACT/Schwarz] Missing PHASE_2 rank-0 official armor-break talent data; talent disabled.",
                    player);
                return;
            }

            talent.ConfigureOfficialBaseTalent(
                probability,
                attackScale,
                defenseDelta,
                duration);
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
