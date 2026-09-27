#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Reads official operator base attributes from the locally extracted character_table.json.
    /// Only PHASE_2 base attributes are imported here: trust, potential, talents and modules remain
    /// separate modifier layers and must not be baked into the progression curve.
    /// </summary>
    internal static class PrtsOperatorProgressionImporter
    {
        private const string DefaultCharacterTablePath =
            "D:/Ark/ArkModExporter/runtime/data/character_table.json";
        private const string OverridePathPref =
            "ArknightsACT.PRTS.CharacterTablePath";

        private static readonly int[] CheckpointLevels = { 1, 30, 60, 90 };
        private static string _cachedPath;
        private static DateTime _cachedWriteUtc;
        private static string _cachedText;
        private static bool _missingSourceWarningLogged;

        [MenuItem("ArknightsACT/Characters/Progression/Refresh All PRTS E2 Stats")]
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
                if (TryRefreshDefinition(definition, definition.PrtsCharacterId))
                    refreshed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[ArknightsACT/Progression] Refreshed {refreshed} PRTS E2 progression profile(s).");
        }

        internal static bool TryRefreshDefinition(
            PlayableOperatorDefinition definition,
            string prtsCharacterId)
        {
            if (definition == null || string.IsNullOrWhiteSpace(prtsCharacterId))
                return false;

            if (!TryLoadCharacterTable(out var source))
            {
                EnsureSourceId(definition, prtsCharacterId);
                return false;
            }

            if (!TryExtractE2Progression(source, prtsCharacterId, out var progression))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/Progression] Could not extract PHASE_2 stats for " +
                    $"'{prtsCharacterId}' from {ResolveSourcePath()}.");
                EnsureSourceId(definition, prtsCharacterId);
                return false;
            }

            if (ProgressionMatches(definition, prtsCharacterId, progression))
                return true;

            definition.ConfigureProgression(prtsCharacterId, progression);
            EditorUtility.SetDirty(definition);
            return true;
        }

        internal static void SyncPlayerProgression(
            GameObject player,
            PlayableOperatorDefinition definition)
        {
            if (player == null || definition == null || !definition.HasE2Progression)
                return;

            // Old generated scenes predate OperatorRuntimeStats. Capture their authored prototype
            // values before adding the progression component, whose RequireComponent would otherwise
            // create a default 100 HP / 10 ATK profile and overwrite the legacy values on play.
            var runtimeStats = player.GetComponent<OperatorRuntimeStats>();
            if (runtimeStats == null)
            {
                var health = player.GetComponent<Health>();
                var combatStats = player.GetComponent<CombatStats>();
                var attack = player.GetComponent<PlayerAttackController>();

                var maxHealth = health != null ? health.MaxHealth : 100f;
                var baseAttack = attack != null ? attack.ConfiguredBaseAttack : 10f;
                var physicalDefense = combatStats != null ? combatStats.BasePhysicalDefense : 0f;
                var artsResistance = combatStats != null ? combatStats.BaseArtsResistance : 0f;

                runtimeStats = player.AddComponent<OperatorRuntimeStats>();
                runtimeStats.ConfigureCore(
                    maxHealth,
                    baseAttack,
                    physicalDefense,
                    artsResistance,
                    refillHealth: !Application.isPlaying);
                EditorUtility.SetDirty(runtimeStats);
            }

            var controller = player.GetComponent<OperatorProgressionController>();
            var preservedLevel = controller != null ? controller.EliteLevel : 1;
            if (controller == null)
                controller = player.AddComponent<OperatorProgressionController>();

            controller.Configure(
                definition.PrtsCharacterId,
                definition.E2Progression,
                initialEliteLevel: preservedLevel,
                applyImmediately: false);
            controller.ConfigureMetaProgression(definition.MetaProgression);
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

                if (definition == null || !definition.HasE2Progression)
                    continue;

                SyncPlayerProgression(identity.gameObject, definition);
                EditorUtility.SetDirty(identity.gameObject);
                synced++;
            }

            return synced;
        }

        internal static bool TryExtractE2Progression(
            string characterTableJson,
            string prtsCharacterId,
            out OperatorE2Progression progression)
        {
            progression = null;
            if (string.IsNullOrWhiteSpace(characterTableJson) ||
                string.IsNullOrWhiteSpace(prtsCharacterId) ||
                !TryExtractObjectByKey(characterTableJson, prtsCharacterId, out var characterObject))
                return false;

            var phasesKey = characterObject.IndexOf("\"phases\"", StringComparison.Ordinal);
            if (phasesKey < 0)
                return false;
            var arrayStart = characterObject.IndexOf('[', phasesKey);
            if (arrayStart < 0)
                return false;
            var arrayEnd = FindMatching(characterObject, arrayStart, '[', ']');
            if (arrayEnd < 0)
                return false;

            var phases = ExtractTopLevelObjects(
                characterObject.Substring(arrayStart + 1, arrayEnd - arrayStart - 1));
            if (phases.Count < 3)
                return false;

            var e2 = phases[2];
            if (!TryReadInt(e2, "maxLevel", out var maxLevel) ||
                !TryReadString(e2, "rangeId", out var rangeId))
                return false;

            var framesKey = e2.IndexOf("\"attributesKeyFrames\"", StringComparison.Ordinal);
            if (framesKey < 0)
                return false;
            var framesStart = e2.IndexOf('[', framesKey);
            if (framesStart < 0)
                return false;
            var framesEnd = FindMatching(e2, framesStart, '[', ']');
            if (framesEnd < 0)
                return false;

            var frameObjects = ExtractTopLevelObjects(
                e2.Substring(framesStart + 1, framesEnd - framesStart - 1));
            if (frameObjects.Count < 2 ||
                !TryParseFrame(frameObjects[0], out var from) ||
                !TryParseFrame(frameObjects[frameObjects.Count - 1], out var to))
                return false;

            var levels = BuildCheckpointLevels(maxLevel);
            var snapshots = new OperatorProgressionSnapshot[levels.Count];
            for (var i = 0; i < levels.Count; i++)
                snapshots[i] = new OperatorProgressionSnapshot(
                    levels[i],
                    EvaluateFrame(from, to, levels[i]));

            progression = new OperatorE2Progression(maxLevel, rangeId, snapshots);
            return progression.HasData;
        }

        private static List<int> BuildCheckpointLevels(int maxLevel)
        {
            var result = new List<int>(4);
            for (var i = 0; i < CheckpointLevels.Length; i++)
            {
                var level = Mathf.Clamp(CheckpointLevels[i], 1, Mathf.Max(1, maxLevel));
                if (!result.Contains(level))
                    result.Add(level);
            }

            if (!result.Contains(maxLevel))
                result.Add(Mathf.Max(1, maxLevel));
            result.Sort();
            return result;
        }

        private static OperatorBaseStats EvaluateFrame(PrtsAttributeFrame from, PrtsAttributeFrame to, int level)
        {
            var span = Mathf.Max(1, to.Level - from.Level);
            var t = Mathf.Clamp01((level - from.Level) / (float)span);
            return new OperatorBaseStats(
                Mathf.Round(Mathf.Lerp(from.MaxHealth, to.MaxHealth, t)),
                Mathf.Round(Mathf.Lerp(from.Attack, to.Attack, t)),
                Mathf.Round(Mathf.Lerp(from.Defense, to.Defense, t)),
                Mathf.Lerp(from.ArtsResistance, to.ArtsResistance, t),
                Mathf.Lerp(from.AttackInterval, to.AttackInterval, t),
                1f,
                1f);
        }

        private static bool TryParseFrame(string frameObject, out PrtsAttributeFrame frame)
        {
            frame = default;
            if (!TryReadInt(frameObject, "level", out var level) ||
                !TryReadFloat(frameObject, "maxHp", out var maxHp) ||
                !TryReadFloat(frameObject, "atk", out var attack) ||
                !TryReadFloat(frameObject, "def", out var defense) ||
                !TryReadFloat(frameObject, "magicResistance", out var resistance) ||
                !TryReadFloat(frameObject, "attackSpeed", out var attackSpeed) ||
                !TryReadFloat(frameObject, "baseAttackTime", out var baseAttackTime))
                return false;

            var effectiveInterval = attackSpeed > 0.001f
                ? baseAttackTime * 100f / attackSpeed
                : baseAttackTime;
            frame = new PrtsAttributeFrame(
                level,
                maxHp,
                attack,
                defense,
                resistance,
                Mathf.Max(0.05f, effectiveInterval));
            return true;
        }

        internal static bool TryExtractE2TalentBlackboard(
            string prtsCharacterId,
            int talentIndex,
            out Dictionary<string, float> blackboard)
        {
            if (!TryLoadCharacterTable(out var source))
            {
                blackboard = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                return false;
            }

            return TryExtractE2TalentBlackboard(
                source,
                prtsCharacterId,
                talentIndex,
                out blackboard);
        }

        internal static bool TryExtractE2TalentBlackboard(
            string characterTableJson,
            string prtsCharacterId,
            int talentIndex,
            out Dictionary<string, float> blackboard)
        {
            blackboard = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(characterTableJson) ||
                string.IsNullOrWhiteSpace(prtsCharacterId) ||
                talentIndex < 0 ||
                !TryExtractObjectByKey(characterTableJson, prtsCharacterId, out var characterObject))
                return false;

            var talentsKey = characterObject.IndexOf("\"talents\"", StringComparison.Ordinal);
            if (talentsKey < 0)
                return false;
            var talentsStart = characterObject.IndexOf('[', talentsKey);
            if (talentsStart < 0)
                return false;
            var talentsEnd = FindMatching(characterObject, talentsStart, '[', ']');
            if (talentsEnd < 0)
                return false;

            var talents = ExtractTopLevelObjects(
                characterObject.Substring(talentsStart + 1, talentsEnd - talentsStart - 1));
            if (talentIndex >= talents.Count)
                return false;

            var talentObject = talents[talentIndex];
            var candidatesKey = talentObject.IndexOf("\"candidates\"", StringComparison.Ordinal);
            if (candidatesKey < 0)
                return false;
            var candidatesStart = talentObject.IndexOf('[', candidatesKey);
            if (candidatesStart < 0)
                return false;
            var candidatesEnd = FindMatching(talentObject, candidatesStart, '[', ']');
            if (candidatesEnd < 0)
                return false;

            var candidates = ExtractTopLevelObjects(
                talentObject.Substring(candidatesStart + 1, candidatesEnd - candidatesStart - 1));
            string selected = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!TryExtractObjectByKey(candidate, "unlockCondition", out var unlock) ||
                    !TryReadString(unlock, "phase", out var phase) ||
                    !string.Equals(phase, "PHASE_2", StringComparison.Ordinal))
                    continue;

                if (!TryReadInt(candidate, "requiredPotentialRank", out var requiredPotentialRank) ||
                    requiredPotentialRank != 0)
                    continue;

                selected = candidate;
                break;
            }

            if (string.IsNullOrWhiteSpace(selected))
                return false;

            var blackboardKey = selected.IndexOf("\"blackboard\"", StringComparison.Ordinal);
            if (blackboardKey < 0)
                return false;
            var blackboardStart = selected.IndexOf('[', blackboardKey);
            if (blackboardStart < 0)
                return false;
            var blackboardEnd = FindMatching(selected, blackboardStart, '[', ']');
            if (blackboardEnd < 0)
                return false;

            var entries = ExtractTopLevelObjects(
                selected.Substring(blackboardStart + 1, blackboardEnd - blackboardStart - 1));
            for (var i = 0; i < entries.Count; i++)
            {
                if (!TryReadString(entries[i], "key", out var key) ||
                    string.IsNullOrWhiteSpace(key) ||
                    !TryReadFloat(entries[i], "value", out var value))
                    continue;
                blackboard[key] = value;
            }

            return blackboard.Count > 0;
        }

        private static bool TryLoadCharacterTable(out string source)
        {
            source = null;
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (!_missingSourceWarningLogged)
                {
                    _missingSourceWarningLogged = true;
                    Debug.LogWarning(
                        "[ArknightsACT/Progression] PRTS character_table.json was not found. " +
                        $"Expected '{path}'. Existing imported progression data will be preserved.");
                }
                return false;
            }

            var writeUtc = File.GetLastWriteTimeUtc(path);
            if (_cachedText == null ||
                !string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) ||
                _cachedWriteUtc != writeUtc)
            {
                _cachedText = File.ReadAllText(path);
                _cachedPath = path;
                _cachedWriteUtc = writeUtc;
            }

            _missingSourceWarningLogged = false;
            source = _cachedText;
            return !string.IsNullOrWhiteSpace(source);
        }

        private static string ResolveSourcePath()
        {
            var overridePath = EditorPrefs.GetString(OverridePathPref, string.Empty);
            return string.IsNullOrWhiteSpace(overridePath)
                ? DefaultCharacterTablePath
                : overridePath.Replace('\\', '/');
        }

        private static void EnsureSourceId(
            PlayableOperatorDefinition definition,
            string prtsCharacterId)
        {
            if (string.Equals(
                    definition.PrtsCharacterId,
                    prtsCharacterId,
                    StringComparison.Ordinal))
                return;

            definition.ConfigureProgression(prtsCharacterId, definition.E2Progression);
            EditorUtility.SetDirty(definition);
        }

        private static bool ProgressionMatches(
            PlayableOperatorDefinition definition,
            string prtsCharacterId,
            OperatorE2Progression expected)
        {
            if (definition == null || expected == null ||
                !string.Equals(
                    definition.PrtsCharacterId,
                    prtsCharacterId,
                    StringComparison.Ordinal) ||
                definition.E2Progression == null ||
                definition.E2Progression.MaxLevel != expected.MaxLevel ||
                !string.Equals(
                    definition.E2Progression.RangeId,
                    expected.RangeId,
                    StringComparison.Ordinal))
                return false;

            var actualPoints = definition.E2Progression.Snapshots;
            var expectedPoints = expected.Snapshots;
            if (actualPoints == null || expectedPoints == null ||
                actualPoints.Count != expectedPoints.Count)
                return false;

            for (var i = 0; i < expectedPoints.Count; i++)
            {
                var a = actualPoints[i];
                var e = expectedPoints[i];
                if (a.Level != e.Level || !StatsApproximatelyEqual(a.Stats, e.Stats))
                    return false;
            }

            return true;
        }

        private static bool StatsApproximatelyEqual(OperatorBaseStats a, OperatorBaseStats b)
        {
            return Mathf.Approximately(a.MaxHealth, b.MaxHealth) &&
                   Mathf.Approximately(a.Attack, b.Attack) &&
                   Mathf.Approximately(a.PhysicalDefense, b.PhysicalDefense) &&
                   Mathf.Approximately(a.ArtsResistance, b.ArtsResistance) &&
                   Mathf.Approximately(a.AttackInterval, b.AttackInterval);
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
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                        objectStart = i;
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
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open)
                    depth++;
                else if (c == close && --depth == 0)
                    return i;
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
            if (index < 0)
                return false;
            index = source.IndexOf(':', index);
            if (index < 0)
                return false;
            index = SkipWhitespace(source, index + 1);
            if (index >= source.Length || source[index] != '"')
                return false;

            var end = index + 1;
            var escaped = false;
            while (end < source.Length)
            {
                var c = source[end];
                if (escaped)
                    escaped = false;
                else if (c == '\\')
                    escaped = true;
                else if (c == '"')
                    break;
                end++;
            }

            if (end >= source.Length)
                return false;
            value = source.Substring(index + 1, end - index - 1);
            return true;
        }

        private static bool TryReadInt(string source, string key, out int value)
        {
            value = 0;
            if (!TryReadFloat(source, key, out var number))
                return false;
            value = Mathf.RoundToInt(number);
            return true;
        }

        private static bool TryReadFloat(string source, string key, out float value)
        {
            value = 0f;
            var index = source.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (index < 0)
                return false;
            index = source.IndexOf(':', index);
            if (index < 0)
                return false;
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

        private readonly struct PrtsAttributeFrame
        {
            public readonly int Level;
            public readonly float MaxHealth;
            public readonly float Attack;
            public readonly float Defense;
            public readonly float ArtsResistance;
            public readonly float AttackInterval;

            public PrtsAttributeFrame(
                int level,
                float maxHealth,
                float attack,
                float defense,
                float artsResistance,
                float attackInterval)
            {
                Level = level;
                MaxHealth = maxHealth;
                Attack = attack;
                Defense = defense;
                ArtsResistance = artsResistance;
                AttackInterval = attackInterval;
            }
        }
    }
}
#endif
