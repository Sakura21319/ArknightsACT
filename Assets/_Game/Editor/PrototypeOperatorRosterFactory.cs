#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class PrototypeOperatorRosterBuildResult
    {
        public GameObject ActivePlayer;
        public readonly List<GameObject> ReservePlayers = new();
        public readonly List<GameObject> AllPlayers = new();
    }

    /// <summary>
    /// Builds runtime switch targets from auto-discovered operator definitions and builders.
    /// Every authored skin becomes a switch target, so character switching and skin switching use
    /// one state-transfer path without central character-specific branches.
    /// </summary>
    internal static class PrototypeOperatorRosterFactory
    {
        public static PrototypeOperatorRosterBuildResult Build(
            Camera camera,
            string activeOperatorId,
            string activeSkinId = null)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));

            var definitions = PrototypeOperatorRegistry.GetDefinitions();
            var activeDefinition = FindDefinition(definitions, activeOperatorId);
            if (activeDefinition == null)
                throw new InvalidOperationException(
                    $"Cannot find operator definition '{activeOperatorId}'.");

            var selectedSkin = activeDefinition.FindSkin(activeSkinId);
            if (selectedSkin == null)
                throw new InvalidOperationException(
                    $"Operator '{activeOperatorId}' has no configured skins.");

            var result = new PrototypeOperatorRosterBuildResult();
            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var definition = definitions[definitionIndex];
                if (definition == null ||
                    !PrototypeOperatorRegistry.TryGetBuilder(definition.OperatorId, out var builder))
                    continue;

                var skins = definition.Skins;
                for (var skinIndex = 0; skinIndex < skins.Count; skinIndex++)
                {
                    var skin = skins[skinIndex];
                    if (skin == null || skin.IsReserved)
                        continue;

                    var isActive =
                        string.Equals(
                            definition.OperatorId,
                            activeDefinition.OperatorId,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            skin.SkinId,
                            selectedSkin.SkinId,
                            StringComparison.OrdinalIgnoreCase);

                    try
                    {
                        var player = builder.Build(camera, definition, skin);
                        if (player == null)
                        {
                            if (isActive)
                                throw new InvalidOperationException(
                                    $"Builder returned null for active target {definition.OperatorId}/{skin.SkinId}.");
                            continue;
                        }

                        ApplyDefinition(player, definition, skin);
                        result.AllPlayers.Add(player);

                        if (isActive)
                        {
                            result.ActivePlayer = player;
                        }
                        else
                        {
                            result.ReservePlayers.Add(player);
                            player.SetActive(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (isActive)
                            throw;

                        Debug.LogWarning(
                            $"[ArknightsACT/OperatorSwitch] Optional target " +
                            $"'{definition.OperatorId}/{skin.SkinId}' was skipped: {ex.Message}");
                    }
                }
            }

            if (result.ActivePlayer == null)
            {
                throw new InvalidOperationException(
                    $"Cannot create active prototype operator " +
                    $"'{activeDefinition.OperatorId}/{selectedSkin.SkinId}'.");
            }

            return result;
        }

        public static void RegisterReserves(
            GameObject rogueliteRoot,
            PrototypeOperatorRosterBuildResult roster)
        {
            if (rogueliteRoot == null || roster == null)
                return;

            var runtime = rogueliteRoot.GetComponent<PlayerRuntimeContext>();
            var switcher = rogueliteRoot.GetComponent<PlayableOperatorSwitchController>();
            if (runtime == null || switcher == null)
            {
                Debug.LogError(
                    "[ArknightsACT/OperatorRoster] Roguelite root is missing player runtime/switch controller.",
                    rogueliteRoot);
                return;
            }

            if (roster.ActivePlayer != null)
                runtime.RegisterPlayer(roster.ActivePlayer.transform);

            for (var i = 0; i < roster.ReservePlayers.Count; i++)
            {
                var reserve = roster.ReservePlayers[i];
                if (reserve != null)
                    switcher.RegisterReserve(reserve.transform);
            }
        }

        private static PlayableOperatorDefinition FindDefinition(
            IReadOnlyList<PlayableOperatorDefinition> definitions,
            string operatorId)
        {
            if (definitions == null || string.IsNullOrWhiteSpace(operatorId))
                return null;

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null &&
                    string.Equals(
                        definition.OperatorId,
                        operatorId,
                        StringComparison.OrdinalIgnoreCase))
                    return definition;
            }

            return null;
        }

        private static void ApplyDefinition(
            GameObject player,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            var identity = player.GetComponent<PlayableOperatorIdentity>() ??
                           player.AddComponent<PlayableOperatorIdentity>();
            identity.Configure(
                definition.OperatorId,
                definition.DisplayName,
                skin.SkinId,
                skin.AvatarResourceKey,
                skin.Skill1IconResourceKey,
                skin.Skill2IconResourceKey,
                skin.DisplayName);

            var safeSkinId = string.IsNullOrWhiteSpace(skin.SkinId)
                ? "default"
                : skin.SkinId.Replace('#', '_').Replace('/', '_');
            player.name = $"Player_{definition.OperatorId}_{safeSkinId}";
        }
    }
}
#endif
