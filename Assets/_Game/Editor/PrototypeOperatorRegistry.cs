#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal interface IPrototypeOperatorBuilder
    {
        string OperatorId { get; }
        void EnsureDefinitionAsset();
        GameObject Build(
            Camera camera,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin);
        void RefreshAssets(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin,
            bool force);
        void RefreshExisting(
            GameObject player,
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin);
    }

    internal static class PrototypeOperatorDefinitionAssetUtility
    {
        internal const string Root = "Assets/_Game/Data/Operators";

        public static PlayableOperatorDefinition Ensure(
            string assetName,
            string operatorId,
            string displayName,
            string englishName,
            int sortOrder,
            params PlayableOperatorSkinDefinition[] skins)
        {
            EnsureFolder(Root);
            var path = $"{Root}/{assetName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<PlayableOperatorDefinition>(path);
            if (definition != null)
                return definition;

            definition = ScriptableObject.CreateInstance<PlayableOperatorDefinition>();
            definition.name = assetName;
            definition.Configure(operatorId, displayName, englishName, sortOrder, skins);
            AssetDatabase.CreateAsset(definition, path);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    /// <summary>
    /// Auto-discovered operator catalog.
    /// There is deliberately no central list of operator ids and no operator-specific switch statement.
    /// A new character supplies one PlayableOperatorDefinition asset and one IPrototypeOperatorBuilder.
    /// </summary>
    internal static class PrototypeOperatorRegistry
    {
        private static Dictionary<string, IPrototypeOperatorBuilder> _builders;

        public static IReadOnlyList<PlayableOperatorDefinition> GetDefinitions()
        {
            EnsureBuilders();

            foreach (var builder in _builders.Values)
                builder.EnsureDefinitionAsset();

            var guids = AssetDatabase.FindAssets(
                "t:PlayableOperatorDefinition",
                new[] { PrototypeOperatorDefinitionAssetUtility.Root });

            var definitions = new List<PlayableOperatorDefinition>(guids.Length);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var definition = AssetDatabase.LoadAssetAtPath<PlayableOperatorDefinition>(path);
                if (definition == null || string.IsNullOrWhiteSpace(definition.OperatorId))
                    continue;

                if (!ids.Add(definition.OperatorId))
                {
                    Debug.LogError(
                        $"[ArknightsACT/OperatorRegistry] Duplicate operator id '{definition.OperatorId}' at {path}.");
                    continue;
                }

                if (!_builders.ContainsKey(definition.OperatorId))
                {
                    Debug.LogWarning(
                        $"[ArknightsACT/OperatorRegistry] Definition '{definition.OperatorId}' has no builder and will be skipped.");
                    continue;
                }

                if (!ValidateDefinition(definition, path))
                    continue;

                definitions.Add(definition);
            }

            definitions.Sort((a, b) =>
            {
                var order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(a.OperatorId, b.OperatorId, StringComparison.OrdinalIgnoreCase);
            });
            return definitions;
        }

        private static bool ValidateDefinition(
            PlayableOperatorDefinition definition,
            string assetPath)
        {
            var skins = definition.Skins;
            if (skins == null || skins.Count == 0)
            {
                Debug.LogError(
                    $"[ArknightsACT/OperatorRegistry] '{definition.OperatorId}' has no skins: {assetPath}");
                return false;
            }

            var skinIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validSkinCount = 0;
            for (var i = 0; i < skins.Count; i++)
            {
                var skin = skins[i];
                if (skin == null || string.IsNullOrWhiteSpace(skin.SkinId))
                {
                    Debug.LogError(
                        $"[ArknightsACT/OperatorRegistry] '{definition.OperatorId}' has an invalid skin at index {i}: {assetPath}");
                    return false;
                }

                if (!skinIds.Add(skin.SkinId))
                {
                    Debug.LogError(
                        $"[ArknightsACT/OperatorRegistry] Duplicate skin id " +
                        $"'{definition.OperatorId}/{skin.SkinId}' in {assetPath}.");
                    return false;
                }

                validSkinCount++;
            }

            return validSkinCount > 0;
        }

        public static bool TryGetBuilder(string operatorId, out IPrototypeOperatorBuilder builder)
        {
            EnsureBuilders();
            return _builders.TryGetValue(operatorId ?? string.Empty, out builder);
        }

        private static void EnsureBuilders()
        {
            if (_builders != null)
                return;

            _builders = new Dictionary<string, IPrototypeOperatorBuilder>(StringComparer.OrdinalIgnoreCase);
            var types = TypeCache.GetTypesDerivedFrom<IPrototypeOperatorBuilder>();
            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                IPrototypeOperatorBuilder builder;
                try
                {
                    builder = Activator.CreateInstance(type) as IPrototypeOperatorBuilder;
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[ArknightsACT/OperatorRegistry] Could not create builder {type.FullName}: {ex.Message}");
                    continue;
                }

                if (builder == null || string.IsNullOrWhiteSpace(builder.OperatorId))
                    continue;

                if (_builders.ContainsKey(builder.OperatorId))
                {
                    Debug.LogError(
                        $"[ArknightsACT/OperatorRegistry] Duplicate builder id '{builder.OperatorId}'.");
                    continue;
                }

                _builders.Add(builder.OperatorId, builder);
            }
        }
    }

}
#endif
