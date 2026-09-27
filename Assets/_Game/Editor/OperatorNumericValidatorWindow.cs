#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ArknightsACT.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal enum OperatorNumericValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class OperatorNumericValidationIssue
    {
        public OperatorNumericValidationSeverity Severity;
        public string OperatorId;
        public string Category;
        public string Message;
        public string AssetPath;
        public int Line;

        public override string ToString() =>
            $"[{Severity}] {OperatorId} / {Category}: {Message}" +
            (string.IsNullOrWhiteSpace(AssetPath)
                ? string.Empty
                : $" ({AssetPath}{(Line > 0 ? ":" + Line : string.Empty)})");
    }

    internal sealed class OperatorNumericValidationReport
    {
        public readonly List<OperatorNumericValidationIssue> Issues = new();

        public int ErrorCount => Issues.Count(x => x.Severity == OperatorNumericValidationSeverity.Error);
        public int WarningCount => Issues.Count(x => x.Severity == OperatorNumericValidationSeverity.Warning);
        public int InfoCount => Issues.Count(x => x.Severity == OperatorNumericValidationSeverity.Info);

        public void Add(
            OperatorNumericValidationSeverity severity,
            string operatorId,
            string category,
            string message,
            string assetPath = null,
            int line = 0)
        {
            Issues.Add(new OperatorNumericValidationIssue
            {
                Severity = severity,
                OperatorId = operatorId ?? string.Empty,
                Category = category ?? string.Empty,
                Message = message ?? string.Empty,
                AssetPath = assetPath ?? string.Empty,
                Line = line
            });
        }
    }

    internal static class OperatorNumericValidator
    {
        private sealed class SkillFieldSpec
        {
            public readonly HashSet<string> Required;
            public readonly HashSet<string> Known;
            public readonly HashSet<string> Consumed;
            public readonly bool RequireRangeId;

            public SkillFieldSpec(
                IEnumerable<string> required,
                IEnumerable<string> known = null,
                IEnumerable<string> consumed = null,
                bool requireRangeId = false)
            {
                Required = new HashSet<string>(
                    required ?? Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                Known = new HashSet<string>(
                    known ?? required ?? Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                Consumed = new HashSet<string>(
                    consumed ?? required ?? Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                RequireRangeId = requireRangeId;
            }
        }

        private sealed class OperatorSpec
        {
            public readonly string OperatorId;
            public readonly SkillFieldSpec Slot1;
            public readonly SkillFieldSpec Slot2;
            public readonly bool RequireBaseRangeId;

            public OperatorSpec(
                string operatorId,
                SkillFieldSpec slot1,
                SkillFieldSpec slot2,
                bool requireBaseRangeId = true)
            {
                OperatorId = operatorId;
                Slot1 = slot1;
                Slot2 = slot2;
                RequireBaseRangeId = requireBaseRangeId;
            }

            public SkillFieldSpec GetSlot(int slot) => slot == 1 ? Slot1 : Slot2;
        }

        private static readonly Dictionary<string, OperatorSpec> Specs =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Chen"] = new OperatorSpec(
                    "Chen",
                    new SkillFieldSpec(
                        new[] { "atk_scale", "max_target" },
                        requireRangeId: true),
                    new SkillFieldSpec(
                        new[] { "atk_scale", "times", "stun" },
                        requireRangeId: true)),
                ["Schwarz"] = new OperatorSpec(
                    "Schwarz",
                    new SkillFieldSpec(
                        new[] { "atk", "talent@prob" }),
                    new SkillFieldSpec(
                        new[] { "atk", "base_attack_time", "talent@prob" },
                        requireRangeId: true)),
                ["Skadi"] = new OperatorSpec(
                    "Skadi",
                    new SkillFieldSpec(new[] { "atk", "duration" }),
                    new SkillFieldSpec(new[] { "atk", "def", "max_hp" })),
                ["Wisadel"] = new OperatorSpec(
                    "Wisadel",
                    new SkillFieldSpec(
                        new[] { "base_attack_time", "atk", "attack@atk_scale_ol" }),
                    new SkillFieldSpec(
                        new[] { "atk", "base_attack_time", "attack@atk_scale_3", "attack@trigger_time" },
                        new[] {
                            "atk", "max_cnt", "base_attack_time", "attack@atk_scale_3",
                            "attack@prob", "attack@trigger_time", "sp"
                        },
                        new[] { "atk", "base_attack_time", "attack@atk_scale_3", "attack@trigger_time" })),
                ["FrostNova"] = new OperatorSpec(
                    "FrostNova",
                    new SkillFieldSpec(new[] { "damage_scale", "radius_tiles", "cold_duration" }),
                    new SkillFieldSpec(new[] { "damage_scale", "radius_tiles", "cold_duration" }),
                    requireBaseRangeId: false)
            };

        private static readonly HashSet<string> SuspiciousRuntimeFields =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "skillPointCost",
                "initialSkillPoints",
                "physicalDamage",
                "artsDamage",
                "artsDamagePerPulse",
                "strikeDamage",
                "finalDamage",
                "autoDamagePerShot",
                "basicAttackDamageMultiplier",
                "basicAttackIntervalFlatDelta",
                "masteryAttackScale",
                "officialAttackBonus",
                "officialDefenseBonus",
                "officialMaxHealthBonus",
                "masteryAttackBonus",
                "masteryAttackIntervalFlatDelta",
                "masteryOverloadAttackScale",
                "masterySkill3AttackScale",
                "ammoCapacity"
            };

        private static readonly HashSet<string> SuspiciousFactoryFields =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "maxHealth",
                "physicalDefense",
                "artsResistance",
                "attackInterval",
                "baseAttackDamage"
            };

        private static readonly Regex AssignmentRegex =
            new(@"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\b\s*[:=]\s*(?<number>-?\d+(?:\.\d+)?)(?:f|d)?\b",
                RegexOptions.Compiled);

        public static OperatorNumericValidationReport Run()
        {
            var report = new OperatorNumericValidationReport();
            var definitions = PrototypeOperatorRegistry.GetDefinitions();

            foreach (var spec in Specs.Values)
            {
                var definition = definitions.FirstOrDefault(
                    x => x != null &&
                         string.Equals(x.OperatorId, spec.OperatorId, StringComparison.OrdinalIgnoreCase));
                if (definition == null)
                {
                    report.Add(
                        OperatorNumericValidationSeverity.Error,
                        spec.OperatorId,
                        "Definition",
                        "PlayableOperatorDefinition missing.");
                    continue;
                }

                ValidateProgression(definition, spec, report);
                ValidateMastery(definition, spec, report);
            }

            ValidateHardcodedOfficialNumbers(report);

            if (report.ErrorCount == 0 && report.WarningCount == 0)
            {
                report.Add(
                    OperatorNumericValidationSeverity.Info,
                    "ALL",
                    "Summary",
                    "All P9 numeric validation checks passed.");
            }

            LogReport(report);
            return report;
        }

        private static void ValidateProgression(
            PlayableOperatorDefinition definition,
            OperatorSpec spec,
            OperatorNumericValidationReport report)
        {
            var path = AssetDatabase.GetAssetPath(definition);
            var progression = definition.E2Progression;
            if (progression == null || !progression.HasData)
            {
                report.Add(
                    OperatorNumericValidationSeverity.Error,
                    spec.OperatorId,
                    "E2",
                    "Progression data missing.",
                    path);
                return;
            }

            var checkpoints = new[] { 1, 30, 60, 90 };
            for (var i = 0; i < checkpoints.Length; i++)
            {
                var level = checkpoints[i];
                if (!progression.HasCheckpoint(level))
                {
                    report.Add(
                        OperatorNumericValidationSeverity.Error,
                        spec.OperatorId,
                        "E2",
                        $"Missing explicit Lv{level} checkpoint.",
                        path);
                    continue;
                }

                var stats = progression.Evaluate(level);
                if (stats.MaxHealth <= 0f ||
                    stats.Attack < 0f ||
                    stats.PhysicalDefense < 0f ||
                    stats.ArtsResistance < 0f ||
                    stats.AttackInterval <= 0f)
                {
                    report.Add(
                        OperatorNumericValidationSeverity.Error,
                        spec.OperatorId,
                        "E2",
                        $"Lv{level} contains invalid HP/ATK/DEF/RES/attack interval.",
                        path);
                }
            }

            if (progression.MaxLevel < 90)
            {
                report.Add(
                    OperatorNumericValidationSeverity.Error,
                    spec.OperatorId,
                    "E2",
                    $"MaxLevel is {progression.MaxLevel}; expected at least 90.",
                    path);
            }

            if (spec.RequireBaseRangeId && string.IsNullOrWhiteSpace(progression.RangeId))
            {
                report.Add(
                    OperatorNumericValidationSeverity.Warning,
                    spec.OperatorId,
                    "Range",
                    "Base rangeId is empty.",
                    path);
            }
        }

        private static void ValidateMastery(
            PlayableOperatorDefinition definition,
            OperatorSpec spec,
            OperatorNumericValidationReport report)
        {
            var path = AssetDatabase.GetAssetPath(definition);
            var set = definition.SkillMastery;
            if (set == null || !set.HasData)
            {
                report.Add(
                    OperatorNumericValidationSeverity.Error,
                    spec.OperatorId,
                    "Mastery",
                    "Skill mastery set missing.",
                    path);
                return;
            }

            for (var slot = 1; slot <= 2; slot++)
            {
                var profile = set.FindSlot(slot);
                if (profile == null || !profile.HasData)
                {
                    report.Add(
                        OperatorNumericValidationSeverity.Error,
                        spec.OperatorId,
                        $"Skill{slot}",
                        "Mastery profile missing.",
                        path);
                    continue;
                }

                var exactTiers = new HashSet<int>();
                var snapshots = profile.Snapshots;
                for (var i = 0; i < snapshots.Count; i++)
                {
                    var snapshot = snapshots[i];
                    if (snapshot == null)
                        continue;
                    exactTiers.Add(snapshot.MasteryLevel);
                    ValidateSnapshot(spec, slot, snapshot, path, report);
                }

                for (var mastery = 0; mastery <= 3; mastery++)
                {
                    if (exactTiers.Contains(mastery))
                        continue;
                    report.Add(
                        OperatorNumericValidationSeverity.Error,
                        spec.OperatorId,
                        $"Skill{slot}",
                        $"Missing exact {(mastery == 0 ? "Skill7" : "M" + mastery)} snapshot.",
                        path);
                }
            }
        }

        private static void ValidateSnapshot(
            OperatorSpec spec,
            int slot,
            OperatorSkillMasterySnapshot snapshot,
            string assetPath,
            OperatorNumericValidationReport report)
        {
            var tier = snapshot.MasteryLevel == 0 ? "Skill7" : "M" + snapshot.MasteryLevel;
            var fieldSpec = spec.GetSlot(slot);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var blackboard = snapshot.Blackboard;

            for (var i = 0; i < blackboard.Count; i++)
            {
                var key = blackboard[i].Key;
                if (string.IsNullOrWhiteSpace(key))
                {
                    report.Add(
                        OperatorNumericValidationSeverity.Warning,
                        spec.OperatorId,
                        $"Skill{slot}/{tier}",
                        "Empty Blackboard key.",
                        assetPath);
                    continue;
                }

                if (!keys.Add(key))
                {
                    report.Add(
                        OperatorNumericValidationSeverity.Warning,
                        spec.OperatorId,
                        $"Skill{slot}/{tier}",
                        $"Duplicate Blackboard key '{key}'.",
                        assetPath);
                }

                if (!fieldSpec.Known.Contains(key))
                {
                    report.Add(
                        OperatorNumericValidationSeverity.Warning,
                        spec.OperatorId,
                        $"Skill{slot}/{tier}",
                        $"Unknown Blackboard field '{key}'.",
                        assetPath);
                }
                else if (!fieldSpec.Consumed.Contains(key))
                {
                    report.Add(
                        OperatorNumericValidationSeverity.Warning,
                        spec.OperatorId,
                        $"Skill{slot}/{tier}",
                        $"Official Blackboard field '{key}' is imported but not consumed by current Runtime.",
                        assetPath);
                }
            }

            foreach (var required in fieldSpec.Required)
            {
                if (keys.Contains(required))
                    continue;
                report.Add(
                    OperatorNumericValidationSeverity.Error,
                    spec.OperatorId,
                    $"Skill{slot}/{tier}",
                    $"Required Blackboard field '{required}' missing.",
                    assetPath);
            }

            if (fieldSpec.RequireRangeId && string.IsNullOrWhiteSpace(snapshot.RangeId))
            {
                report.Add(
                    OperatorNumericValidationSeverity.Warning,
                    spec.OperatorId,
                    $"Skill{slot}/{tier}",
                    "Expected skill rangeId is empty.",
                    assetPath);
            }

            if (snapshot.InitialSkillPoints > snapshot.SkillPointCost + 0.001f &&
                snapshot.SkillPointCost > 0f)
            {
                report.Add(
                    OperatorNumericValidationSeverity.Error,
                    spec.OperatorId,
                    $"Skill{slot}/{tier}",
                    $"Initial SP {snapshot.InitialSkillPoints} exceeds cost {snapshot.SkillPointCost}.",
                    assetPath);
            }

            if (string.IsNullOrWhiteSpace(snapshot.SourceSkillId))
            {
                report.Add(
                    OperatorNumericValidationSeverity.Error,
                    spec.OperatorId,
                    $"Skill{slot}/{tier}",
                    "Source skill id is empty.",
                    assetPath);
            }
        }

        private static void ValidateHardcodedOfficialNumbers(OperatorNumericValidationReport report)
        {
            var roots = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Chen"] = new[] {
                    "Assets/_Game/Scripts/Gameplay/Characters/Chen",
                    "Assets/_Game/Editor/ChenPrototypePlayerFactory.cs",
                    "Assets/_Game/Editor/Operators/ChenPrototypeOperatorBuilder.cs"
                },
                ["Schwarz"] = new[] {
                    "Assets/_Game/Scripts/Gameplay/Characters",
                    "Assets/_Game/Editor/SchwarzPrototypePlayerFactory.cs",
                    "Assets/_Game/Editor/Operators/SchwarzPrototypeOperatorBuilder.cs"
                },
                ["Skadi"] = new[] {
                    "Assets/_Game/Scripts/Gameplay/Characters/Skadi",
                    "Assets/_Game/Editor/SkadiPrototypePlayerFactory.cs",
                    "Assets/_Game/Editor/Operators/SkadiPrototypeOperatorBuilder.cs"
                },
                ["Wisadel"] = new[] {
                    "Assets/_Game/Scripts/Gameplay/Characters/Wisadel",
                    "Assets/_Game/Editor/WisadelPrototypePlayerFactory.cs",
                    "Assets/_Game/Editor/WisadelPrototypeOperatorBuilder.cs"
                },
                ["FrostNova"] = new[] {
                    "Assets/_Game/Scripts/Gameplay/Characters/FrostNova",
                    "Assets/_Game/Editor/FrostNovaPrototypePlayerFactory.cs",
                    "Assets/_Game/Editor/Operators/FrostNovaPrototypeOperatorBuilder.cs"
                }
            };

            foreach (var pair in roots)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < pair.Value.Length; i++)
                {
                    var path = pair.Value[i];
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { path });
                        for (var j = 0; j < guids.Length; j++)
                            ScanTextAsset(
                                pair.Key,
                                AssetDatabase.GUIDToAssetPath(guids[j]),
                                seen,
                                report);
                    }
                    else
                    {
                        ScanTextAsset(pair.Key, path, seen, report);
                    }
                }
            }

            ScanSceneYaml("Assets/_Game/Scenes/PrototypeRun.unity", report);
        }

        private static void ScanTextAsset(
            string operatorId,
            string assetPath,
            HashSet<string> seen,
            OperatorNumericValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(assetPath))
                return;

            // Schwarz runtime files live in the shared Characters folder. Restrict those by name.
            if (string.Equals(operatorId, "Schwarz", StringComparison.OrdinalIgnoreCase) &&
                assetPath.Contains("/Scripts/Gameplay/Characters/", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(assetPath).StartsWith("Schwarz", StringComparison.OrdinalIgnoreCase))
                return;

            var fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath))
                return;

            var lines = File.ReadAllLines(fullPath);
            var factoryLike =
                assetPath.Contains("PrototypePlayerFactory", StringComparison.OrdinalIgnoreCase) ||
                assetPath.Contains("PrototypeOperatorBuilder", StringComparison.OrdinalIgnoreCase);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    continue;

                foreach (Match match in AssignmentRegex.Matches(line))
                {
                    var name = match.Groups["name"].Value;
                    if (!float.TryParse(
                            match.Groups["number"].Value,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var value) ||
                        Mathf.Approximately(value, 0f))
                        continue;

                    var suspicious =
                        SuspiciousRuntimeFields.Contains(name) ||
                        (factoryLike && SuspiciousFactoryFields.Contains(name));
                    if (!suspicious)
                        continue;

                    report.Add(
                        OperatorNumericValidationSeverity.Warning,
                        operatorId,
                        "Hardcode",
                        $"Suspected official numeric initializer/argument '{name} = {value}'. Review whether it should come from progression/mastery data.",
                        assetPath,
                        i + 1);
                }
            }
        }

        private static void ScanSceneYaml(
            string assetPath,
            OperatorNumericValidationReport report)
        {
            var fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath))
                return;

            var lines = File.ReadAllLines(fullPath);
            for (var i = 0; i < lines.Length; i++)
            {
                var match = AssignmentRegex.Match(lines[i]);
                if (!match.Success)
                    continue;

                var name = match.Groups["name"].Value;
                if (!SuspiciousRuntimeFields.Contains(name))
                    continue;

                if (!float.TryParse(
                        match.Groups["number"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var value) ||
                    Mathf.Approximately(value, 0f))
                    continue;

                report.Add(
                    OperatorNumericValidationSeverity.Warning,
                    "Scene",
                    "Hardcode",
                    $"PrototypeRun contains non-zero serialized official-looking field '{name}: {value}'.",
                    assetPath,
                    i + 1);
            }
        }

        private static string ToFullPath(string assetPath)
        {
            var relative = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? assetPath.Substring("Assets/".Length)
                : assetPath;
            return Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void LogReport(OperatorNumericValidationReport report)
        {
            Debug.Log(
                $"[ArknightsACT/NumericValidator] Complete. " +
                $"errors={report.ErrorCount}, warnings={report.WarningCount}, info={report.InfoCount}");

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                switch (issue.Severity)
                {
                    case OperatorNumericValidationSeverity.Error:
                        Debug.LogError("[ArknightsACT/NumericValidator] " + issue);
                        break;
                    case OperatorNumericValidationSeverity.Warning:
                        Debug.LogWarning("[ArknightsACT/NumericValidator] " + issue);
                        break;
                    default:
                        Debug.Log("[ArknightsACT/NumericValidator] " + issue);
                        break;
                }
            }
        }
    }

    internal sealed class OperatorNumericValidatorWindow : EditorWindow
    {
        private OperatorNumericValidationReport _report;
        private Vector2 _scroll;
        private bool _showInfo = true;
        private bool _showWarnings = true;
        private bool _showErrors = true;

        public static void OpenAndRun()
        {
            var window = GetWindow<OperatorNumericValidatorWindow>("数值校验");
            window.minSize = new Vector2(860f, 480f);
            window.RunValidation();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新校验", GUILayout.Width(120f), GUILayout.Height(28f)))
                    RunValidation();

                GUILayout.Space(12f);
                _showErrors = GUILayout.Toggle(_showErrors, "Error", GUILayout.Width(70f));
                _showWarnings = GUILayout.Toggle(_showWarnings, "Warning", GUILayout.Width(85f));
                _showInfo = GUILayout.Toggle(_showInfo, "Info", GUILayout.Width(65f));

                GUILayout.FlexibleSpace();
                if (_report != null)
                {
                    GUILayout.Label(
                        $"Error {_report.ErrorCount}   Warning {_report.WarningCount}   Info {_report.InfoCount}",
                        EditorStyles.boldLabel);
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "P9 只检查数据完整性、字段消费和明显官方数值硬编码。动作时序、FX 偏移、世界距离/碰撞半径等 ACT 表现参数不会作为数值错误处理。",
                MessageType.Info);

            if (_report == null)
                return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _report.Issues.Count; i++)
            {
                var issue = _report.Issues[i];
                if (!ShouldShow(issue.Severity))
                    continue;

                DrawIssue(issue);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawIssue(OperatorNumericValidationIssue issue)
        {
            var type = issue.Severity switch
            {
                OperatorNumericValidationSeverity.Error => MessageType.Error,
                OperatorNumericValidationSeverity.Warning => MessageType.Warning,
                _ => MessageType.Info
            };

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    $"{issue.OperatorId} · {issue.Category}\n{issue.Message}",
                    type);

                if (!string.IsNullOrWhiteSpace(issue.AssetPath))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.SelectableLabel(
                            issue.AssetPath + (issue.Line > 0 ? ":" + issue.Line : string.Empty),
                            GUILayout.Height(18f));
                        if (GUILayout.Button("定位", GUILayout.Width(64f)))
                            Ping(issue);
                    }
                }
            }
        }

        private static void Ping(OperatorNumericValidationIssue issue)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            if (issue.Line > 0)
                AssetDatabase.OpenAsset(asset, issue.Line);
        }

        private bool ShouldShow(OperatorNumericValidationSeverity severity)
        {
            return severity switch
            {
                OperatorNumericValidationSeverity.Error => _showErrors,
                OperatorNumericValidationSeverity.Warning => _showWarnings,
                _ => _showInfo
            };
        }

        private void RunValidation()
        {
            _report = OperatorNumericValidator.Run();
            Repaint();
        }
    }
}
#endif
