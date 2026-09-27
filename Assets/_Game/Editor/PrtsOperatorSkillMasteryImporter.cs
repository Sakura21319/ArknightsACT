#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ArknightsACT.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Imports official skill level 7 / mastery 1 / mastery 2 / mastery 3 snapshots
    /// from the locally extracted character_table.json + skill_table.json.
    ///
    /// Gameplay slot 1/2 currently map to each operator's original S2/S3.
    /// The full blackboard is preserved so character-specific runtime adapters can consume
    /// only keys whose semantics they explicitly understand.
    /// </summary>
    internal static class PrtsOperatorSkillMasteryImporter
    {
        private const string DefaultCharacterTablePath =
            "D:/Ark/ArkModExporter/runtime/data/character_table.json";
        private const string FallbackCharacterTablePath =
            "D:/Ark/ArkModExporter_PublicDeps/runtime/data/character_table.json";
        private const string DefaultSkillTablePath =
            "D:/Ark/ArkModExporter_PublicDeps/runtime/data/skill_table.json";
        private const string CharacterTablePathPref =
            "ArknightsACT.PRTS.CharacterTablePath";
        private const string SkillTablePathPref =
            "ArknightsACT.PRTS.SkillTablePath";

        private static string _cachedCharacterPath;
        private static DateTime _cachedCharacterWriteUtc;
        private static string _cachedCharacterText;
        private static string _cachedSkillPath;
        private static DateTime _cachedSkillWriteUtc;
        private static string _cachedSkillText;
        private static bool _missingSourceWarningLogged;

        [MenuItem("ArknightsACT/Characters/Progression/Refresh All PRTS Skill Mastery")]
        private static void RefreshAllMenu()
        {
            var definitions = PrototypeOperatorRegistry.GetDefinitions();
            var refreshed = 0;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.PrtsCharacterId) ||
                    !definition.PrtsCharacterId.StartsWith("char_", StringComparison.Ordinal))
                    continue;

                if (TryRefreshDefinition(
                        definition,
                        definition.PrtsCharacterId,
                        originalSkillIndexForGameplaySlot1: 2,
                        originalSkillIndexForGameplaySlot2: 3))
                    refreshed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[ArknightsACT/SkillMastery] Refreshed {refreshed} official skill mastery profile(s).");
        }

        internal static bool TryRefreshDefinition(
            PlayableOperatorDefinition definition,
            string prtsCharacterId,
            int originalSkillIndexForGameplaySlot1 = 2,
            int originalSkillIndexForGameplaySlot2 = 3)
        {
            if (definition == null || string.IsNullOrWhiteSpace(prtsCharacterId))
                return false;

            if (!TryLoadSources(out var characterTable, out var skillTable))
                return false;

            if (!TryExtractSkillIds(characterTable, prtsCharacterId, out var sourceSkillIds))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/SkillMastery] Could not find skills for '{prtsCharacterId}'.");
                return false;
            }

            var profiles = new List<OperatorSkillMasteryProfile>(2);
            if (TryBuildProfile(
                    skillTable,
                    sourceSkillIds,
                    gameplaySlot: 1,
                    sourceSkillIndex: originalSkillIndexForGameplaySlot1,
                    out var skill1))
                profiles.Add(skill1);

            if (TryBuildProfile(
                    skillTable,
                    sourceSkillIds,
                    gameplaySlot: 2,
                    sourceSkillIndex: originalSkillIndexForGameplaySlot2,
                    out var skill2))
                profiles.Add(skill2);

            if (profiles.Count == 0)
            {
                Debug.LogWarning(
                    $"[ArknightsACT/SkillMastery] No mastery snapshots imported for '{prtsCharacterId}'.");
                return false;
            }

            definition.ConfigureSkillMastery(new OperatorSkillMasterySet(profiles.ToArray()));
            EditorUtility.SetDirty(definition);
            return true;
        }

        internal static void SyncPlayerSkillMastery(
            GameObject player,
            PlayableOperatorDefinition definition)
        {
            if (player == null || definition == null || !definition.HasSkillMastery)
                return;

            var controller = player.GetComponent<OperatorSkillMasteryController>() ??
                             player.AddComponent<OperatorSkillMasteryController>();
            controller.Configure(
                definition.SkillMastery,
                definition.SkillMasteryCosts,
                applyLevel7: true);
            EditorUtility.SetDirty(controller);
        }

        internal static int SyncOpenScenePlayers(
            IReadOnlyList<PlayableOperatorDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                return 0;

            var identities = UnityEngine.Object.FindObjectsByType<PlayableOperatorIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var synced = 0;
            for (var identityIndex = 0; identityIndex < identities.Length; identityIndex++)
            {
                var identity = identities[identityIndex];
                if (identity == null)
                    continue;

                PlayableOperatorDefinition definition = null;
                for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
                {
                    var candidate = definitions[definitionIndex];
                    if (candidate != null &&
                        string.Equals(
                            candidate.OperatorId,
                            identity.OperatorId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        definition = candidate;
                        break;
                    }
                }

                if (definition == null || !definition.HasSkillMastery)
                    continue;

                SyncPlayerSkillMastery(identity.gameObject, definition);
                EditorUtility.SetDirty(identity.gameObject);
                synced++;
            }

            return synced;
        }

        internal static bool TryExtractSkillMasterySet(
            string characterTableJson,
            string skillTableJson,
            string prtsCharacterId,
            int originalSkillIndexForGameplaySlot1,
            int originalSkillIndexForGameplaySlot2,
            out OperatorSkillMasterySet set)
        {
            set = null;
            if (!TryExtractSkillIds(characterTableJson, prtsCharacterId, out var skillIds))
                return false;

            var profiles = new List<OperatorSkillMasteryProfile>(2);
            if (TryBuildProfile(
                    skillTableJson,
                    skillIds,
                    1,
                    originalSkillIndexForGameplaySlot1,
                    out var skill1))
                profiles.Add(skill1);
            if (TryBuildProfile(
                    skillTableJson,
                    skillIds,
                    2,
                    originalSkillIndexForGameplaySlot2,
                    out var skill2))
                profiles.Add(skill2);

            if (profiles.Count == 0)
                return false;
            set = new OperatorSkillMasterySet(profiles.ToArray());
            return true;
        }

        private static bool TryBuildProfile(
            string skillTable,
            IReadOnlyList<string> sourceSkillIds,
            int gameplaySlot,
            int sourceSkillIndex,
            out OperatorSkillMasteryProfile profile)
        {
            profile = null;
            var sourceArrayIndex = sourceSkillIndex - 1;
            if (sourceSkillIds == null ||
                sourceArrayIndex < 0 ||
                sourceArrayIndex >= sourceSkillIds.Count)
                return false;

            var skillId = sourceSkillIds[sourceArrayIndex];
            if (string.IsNullOrWhiteSpace(skillId) ||
                !TryExtractObjectByKey(skillTable, skillId, out var skillObject))
                return false;

            var levelsKey = skillObject.IndexOf("\"levels\"", StringComparison.Ordinal);
            if (levelsKey < 0)
                return false;
            var levelsStart = skillObject.IndexOf('[', levelsKey);
            if (levelsStart < 0)
                return false;
            var levelsEnd = FindMatching(skillObject, levelsStart, '[', ']');
            if (levelsEnd < 0)
                return false;

            var levels = ExtractTopLevelObjects(
                skillObject.Substring(levelsStart + 1, levelsEnd - levelsStart - 1));
            if (levels.Count < 7)
                return false;

            var snapshots = new List<OperatorSkillMasterySnapshot>(4);
            for (var mastery = 0; mastery <= 3; mastery++)
            {
                var sourceIndex = 6 + mastery;
                if (sourceIndex >= levels.Count)
                    break;

                if (TryParseSkillLevel(
                        skillId,
                        mastery,
                        sourceLevel: 7 + mastery,
                        levels[sourceIndex],
                        out var snapshot))
                    snapshots.Add(snapshot);
            }

            if (snapshots.Count == 0)
                return false;

            profile = new OperatorSkillMasteryProfile(
                gameplaySlot,
                sourceSkillIndex,
                skillId,
                snapshots.ToArray());
            return true;
        }

        private static bool TryParseSkillLevel(
            string skillId,
            int mastery,
            int sourceLevel,
            string levelObject,
            out OperatorSkillMasterySnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(levelObject))
                return false;

            TryReadString(levelObject, "name", out var displayName);
            TryReadString(levelObject, "description", out var description);
            TryReadString(levelObject, "rangeId", out var rangeId);
            TryReadFloat(levelObject, "duration", out var duration);

            var spCost = 0f;
            var initSp = 0f;
            var sourceSpType = string.Empty;
            var recoveryType = OperatorSkillPointRecoveryType.Natural;
            if (TryExtractObjectByKey(levelObject, "spData", out var spData))
            {
                TryReadFloat(spData, "spCost", out spCost);
                TryReadFloat(spData, "initSp", out initSp);
                TryReadString(spData, "spType", out sourceSpType);
                recoveryType = MapSkillPointRecoveryType(sourceSpType);
            }

            var blackboard = ExtractBlackboard(levelObject);
            snapshot = new OperatorSkillMasterySnapshot(
                mastery,
                sourceLevel,
                skillId,
                displayName,
                rangeId,
                description,
                spCost,
                initSp,
                duration,
                recoveryType,
                sourceSpType,
                blackboard.ToArray());
            return true;
        }

        private static OperatorSkillPointRecoveryType MapSkillPointRecoveryType(string sourceSpType)
        {
            return sourceSpType switch
            {
                "INCREASE_WHEN_ATTACK" => OperatorSkillPointRecoveryType.Attack,
                "INCREASE_WHEN_TAKEN_DAMAGE" => OperatorSkillPointRecoveryType.Defensive,
                "NO_RECOVER" => OperatorSkillPointRecoveryType.None,
                _ => OperatorSkillPointRecoveryType.Natural
            };
        }

        private static List<OperatorSkillBlackboardValue> ExtractBlackboard(string levelObject)
        {
            var result = new List<OperatorSkillBlackboardValue>();
            var keyIndex = levelObject.IndexOf("\"blackboard\"", StringComparison.Ordinal);
            if (keyIndex < 0)
                return result;

            var start = levelObject.IndexOf('[', keyIndex);
            if (start < 0)
                return result;
            var end = FindMatching(levelObject, start, '[', ']');
            if (end < 0)
                return result;

            var entries = ExtractTopLevelObjects(
                levelObject.Substring(start + 1, end - start - 1));
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!TryReadString(entry, "key", out var key) ||
                    string.IsNullOrWhiteSpace(key))
                    continue;

                TryReadFloat(entry, "value", out var value);
                TryReadString(entry, "valueStr", out var valueString);
                result.Add(new OperatorSkillBlackboardValue(key, value, valueString));
            }
            return result;
        }

        private static bool TryExtractSkillIds(
            string characterTable,
            string prtsCharacterId,
            out List<string> skillIds)
        {
            skillIds = new List<string>();
            if (string.IsNullOrWhiteSpace(characterTable) ||
                string.IsNullOrWhiteSpace(prtsCharacterId) ||
                !TryExtractObjectByKey(characterTable, prtsCharacterId, out var characterObject))
                return false;

            var skillsKey = characterObject.IndexOf("\"skills\"", StringComparison.Ordinal);
            if (skillsKey < 0)
                return false;
            var start = characterObject.IndexOf('[', skillsKey);
            if (start < 0)
                return false;
            var end = FindMatching(characterObject, start, '[', ']');
            if (end < 0)
                return false;

            var entries = ExtractTopLevelObjects(
                characterObject.Substring(start + 1, end - start - 1));
            for (var i = 0; i < entries.Count; i++)
            {
                if (TryReadString(entries[i], "skillId", out var skillId) &&
                    !string.IsNullOrWhiteSpace(skillId))
                    skillIds.Add(skillId);
            }
            return skillIds.Count > 0;
        }

        private static bool TryLoadSources(out string characterTable, out string skillTable)
        {
            characterTable = null;
            skillTable = null;

            var characterPath = ResolveCharacterTablePath();
            var skillPath = ResolveSkillTablePath();
            if (string.IsNullOrWhiteSpace(characterPath) ||
                string.IsNullOrWhiteSpace(skillPath) ||
                !File.Exists(characterPath) ||
                !File.Exists(skillPath))
            {
                if (!_missingSourceWarningLogged)
                {
                    _missingSourceWarningLogged = true;
                    Debug.LogWarning(
                        "[ArknightsACT/SkillMastery] Local official skill data is incomplete. " +
                        $"character_table='{characterPath}', skill_table='{skillPath}'.");
                }
                return false;
            }

            characterTable = ReadCached(
                characterPath,
                ref _cachedCharacterPath,
                ref _cachedCharacterWriteUtc,
                ref _cachedCharacterText);
            skillTable = ReadCached(
                skillPath,
                ref _cachedSkillPath,
                ref _cachedSkillWriteUtc,
                ref _cachedSkillText);

            _missingSourceWarningLogged = false;
            return !string.IsNullOrWhiteSpace(characterTable) &&
                   !string.IsNullOrWhiteSpace(skillTable);
        }

        private static string ReadCached(
            string path,
            ref string cachedPath,
            ref DateTime cachedWriteUtc,
            ref string cachedText)
        {
            var writeUtc = File.GetLastWriteTimeUtc(path);
            if (cachedText == null ||
                !string.Equals(cachedPath, path, StringComparison.OrdinalIgnoreCase) ||
                cachedWriteUtc != writeUtc)
            {
                cachedText = File.ReadAllText(path);
                cachedPath = path;
                cachedWriteUtc = writeUtc;
            }
            return cachedText;
        }

        private static string ResolveCharacterTablePath()
        {
            var overridePath = EditorPrefs.GetString(CharacterTablePathPref, string.Empty);
            if (!string.IsNullOrWhiteSpace(overridePath))
                return overridePath.Replace('\\', '/');
            if (File.Exists(DefaultCharacterTablePath))
                return DefaultCharacterTablePath;
            return FallbackCharacterTablePath;
        }

        private static string ResolveSkillTablePath()
        {
            var overridePath = EditorPrefs.GetString(SkillTablePathPref, string.Empty);
            return string.IsNullOrWhiteSpace(overridePath)
                ? DefaultSkillTablePath
                : overridePath.Replace('\\', '/');
        }

        private static bool TryExtractObjectByKey(string source, string key, out string objectText)
        {
            objectText = null;
            var token = "\"" + key + "\"";
            var searchAt = 0;
            while (searchAt < source.Length)
            {
                var keyIndex = source.IndexOf(token, searchAt, StringComparison.Ordinal);
                if (keyIndex < 0)
                    return false;

                var colon = source.IndexOf(':', keyIndex + token.Length);
                if (colon < 0)
                    return false;
                var objectStart = SkipWhitespace(source, colon + 1);
                if (objectStart < source.Length && source[objectStart] == '{')
                {
                    var objectEnd = FindMatching(source, objectStart, '{', '}');
                    if (objectEnd < 0)
                        return false;
                    objectText = source.Substring(objectStart, objectEnd - objectStart + 1);
                    return true;
                }

                searchAt = keyIndex + token.Length;
            }
            return false;
        }

        private static List<string> ExtractTopLevelObjects(string arrayBody)
        {
            var result = new List<string>();
            var depth = 0;
            var objectStart = -1;
            var inString = false;
            var escaped = false;

            for (var i = 0; i < arrayBody.Length; i++)
            {
                var c = arrayBody[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0) objectStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        result.Add(arrayBody.Substring(objectStart, i - objectStart + 1));
                        objectStart = -1;
                    }
                }
            }
            return result;
        }

        private static int FindMatching(string source, int start, char open, char close)
        {
            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var i = start; i < source.Length; i++)
            {
                var c = source[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open) depth++;
                else if (c == close && --depth == 0) return i;
            }
            return -1;
        }

        private static int SkipWhitespace(string source, int index)
        {
            while (index < source.Length && char.IsWhiteSpace(source[index]))
                index++;
            return index;
        }

        private static bool TryReadString(string source, string key, out string value)
        {
            value = null;
            var index = source.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (index < 0) return false;
            index = source.IndexOf(':', index);
            if (index < 0) return false;
            index = SkipWhitespace(source, index + 1);
            if (index >= source.Length || source[index] != '"')
                return false;

            var start = index + 1;
            var end = start;
            var escaped = false;
            while (end < source.Length)
            {
                var c = source[end];
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') break;
                end++;
            }
            if (end >= source.Length) return false;

            value = source.Substring(start, end - start)
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
            return true;
        }

        private static bool TryReadFloat(string source, string key, out float value)
        {
            value = 0f;
            var index = source.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (index < 0) return false;
            index = source.IndexOf(':', index);
            if (index < 0) return false;
            index = SkipWhitespace(source, index + 1);

            var end = index;
            while (end < source.Length)
            {
                var c = source[end];
                if (!(char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E'))
                    break;
                end++;
            }

            return end > index &&
                   float.TryParse(
                       source.Substring(index, end - index),
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out value);
        }
    }
}
#endif
