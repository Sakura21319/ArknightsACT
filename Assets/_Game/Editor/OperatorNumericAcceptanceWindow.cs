#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Chen;
using ArknightsACT.Gameplay.Characters.FrostNova;
using ArknightsACT.Gameplay.Characters.Schwarz;
using ArknightsACT.Gameplay.Characters.Skadi;
using ArknightsACT.Gameplay.Characters.Wisadel;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class OperatorNumericAcceptanceRow
    {
        public string OperatorId;
        public string Case;
        public string Field;
        public float Expected;
        public float Actual;
        public float Delta;
        public bool Passed;
        public string Source;

        public override string ToString() =>
            $"[{(Passed ? "PASS" : "FAIL")}] {OperatorId} / {Case} / {Field}: " +
            $"expected={Expected:0.####}, actual={Actual:0.####}, delta={Delta:0.####}" +
            (string.IsNullOrWhiteSpace(Source) ? string.Empty : $" [{Source}]");
    }

    internal sealed class OperatorNumericAcceptanceReport
    {
        public readonly List<OperatorNumericAcceptanceRow> Rows = new();

        public int PassedCount => Rows.Count(x => x.Passed);
        public int FailedCount => Rows.Count(x => !x.Passed);

        public void Compare(
            string operatorId,
            string caseName,
            string field,
            float expected,
            float actual,
            float tolerance,
            string source = null)
        {
            var delta = actual - expected;
            Rows.Add(new OperatorNumericAcceptanceRow
            {
                OperatorId = operatorId ?? string.Empty,
                Case = caseName ?? string.Empty,
                Field = field ?? string.Empty,
                Expected = expected,
                Actual = actual,
                Delta = delta,
                Passed = Mathf.Abs(delta) <= Mathf.Max(0.0001f, tolerance),
                Source = source ?? string.Empty
            });
        }
    }

    internal static class OperatorNumericAcceptanceRunner
    {
        private static readonly int[] Levels = { 1, 30, 60, 90 };

        public static OperatorNumericAcceptanceReport Run()
        {
            var report = new OperatorNumericAcceptanceReport();
            var definitions = PrototypeOperatorRegistry.GetDefinitions();

            foreach (var definition in definitions)
            {
                if (definition == null ||
                    !IsTargetOperator(definition.OperatorId))
                    continue;

                AcceptBaseProgression(definition, report);
                AcceptMastery(definition, report);
            }

            Debug.Log(
                $"[ArknightsACT/NumericAcceptance] Complete. " +
                $"passed={report.PassedCount}, failed={report.FailedCount}");

            for (var i = 0; i < report.Rows.Count; i++)
            {
                var row = report.Rows[i];
                if (row.Passed)
                    Debug.Log("[ArknightsACT/NumericAcceptance] " + row);
                else
                    Debug.LogError("[ArknightsACT/NumericAcceptance] " + row);
            }

            return report;
        }

        private static bool IsTargetOperator(string operatorId) =>
            string.Equals(operatorId, "Chen", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operatorId, "Schwarz", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operatorId, "Skadi", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operatorId, "Wisadel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operatorId, "FrostNova", StringComparison.OrdinalIgnoreCase);

        private static void AcceptBaseProgression(
            PlayableOperatorDefinition definition,
            OperatorNumericAcceptanceReport report)
        {
            if (!definition.HasE2Progression)
                return;

            for (var i = 0; i < Levels.Length; i++)
            {
                var level = Levels[i];
                var expected = definition.E2Progression.Evaluate(level);
                var go = CreateHarness(definition.OperatorId + "_Lv" + level, expected);

                try
                {
                    var runtime = go.GetComponent<OperatorRuntimeStats>();
                    var caseName = "E2 Lv" + level;

                    CompareStat(report, definition, caseName, "MaxHP", expected.MaxHealth, runtime.MaxHealth);
                    CompareStat(report, definition, caseName, "ATK", expected.Attack, runtime.Attack);
                    CompareStat(report, definition, caseName, "DEF", expected.PhysicalDefense, runtime.PhysicalDefense);
                    CompareStat(report, definition, caseName, "RES", expected.ArtsResistance, runtime.ArtsResistance);
                    CompareStat(report, definition, caseName, "AttackInterval", expected.AttackInterval, runtime.AttackInterval);
                    CompareStat(report, definition, caseName, "BasicAttackRange", expected.BasicAttackRange, runtime.BasicAttackRange);
                    CompareStat(report, definition, caseName, "SkillRange", expected.SkillRange, runtime.SkillRange);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        private static void AcceptMastery(
            PlayableOperatorDefinition definition,
            OperatorNumericAcceptanceReport report)
        {
            if (!definition.HasE2Progression || !definition.HasSkillMastery)
                return;

            for (var mastery = 0; mastery <= 3; mastery++)
            {
                var go = CreateHarness(
                    definition.OperatorId + "_M" + mastery,
                    definition.E2Progression.Evaluate(1));

                try
                {
                    AddSkillAdapters(definition.OperatorId, go);

                    var progression = go.AddComponent<OperatorProgressionController>();
                    progression.Configure(
                        definition.PrtsCharacterId,
                        definition.E2Progression,
                        1,
                        applyImmediately: true);

                    var controller = go.AddComponent<OperatorSkillMasteryController>();
                    controller.Configure(
                        definition.SkillMastery,
                        definition.SkillMasteryCosts,
                        applyLevel7: false);

                    for (var slot = 1; slot <= 2; slot++)
                    {
                        var profile = definition.SkillMastery.FindSlot(slot);
                        var snapshot = profile?.GetSnapshot(mastery);
                        if (snapshot == null)
                            continue;

                        controller.ApplySlot(slot, mastery);
                        var target = go.GetComponents<MonoBehaviour>()
                            .OfType<IOperatorSkillMasteryTarget>()
                            .FirstOrDefault(x => x.MasterySlot == slot);
                        if (target is not MonoBehaviour behaviour)
                            continue;

                        AcceptCommonSkillFields(
                            definition,
                            snapshot,
                            slot,
                            mastery,
                            behaviour,
                            controller,
                            report);

                        AcceptOperatorSpecific(
                            definition,
                            snapshot,
                            slot,
                            mastery,
                            behaviour,
                            go.GetComponent<OperatorRuntimeStats>(),
                            report);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        private static void AcceptCommonSkillFields(
            PlayableOperatorDefinition definition,
            OperatorSkillMasterySnapshot snapshot,
            int slot,
            int mastery,
            MonoBehaviour behaviour,
            OperatorSkillMasteryController controller,
            OperatorNumericAcceptanceReport report)
        {
            var caseName = SkillCase(slot, mastery);
            var source = snapshot.SourceSkillId;

            if (behaviour is IPlayerSkill playerSkill)
            {
                report.Compare(
                    definition.OperatorId,
                    caseName,
                    "SP Cost",
                    snapshot.SkillPointCost,
                    playerSkill.SkillPointCost,
                    0.001f,
                    source);
                report.Compare(
                    definition.OperatorId,
                    caseName,
                    "Initial SP",
                    snapshot.InitialSkillPoints,
                    playerSkill.SkillPoints,
                    0.001f,
                    source);
            }

            if (behaviour is IPlayerSkillLifecycleState lifecycle)
            {
                var expectedDuration = ResolveExpectedDuration(definition.OperatorId, slot, snapshot);
                if (expectedDuration >= 0f)
                {
                    report.Compare(
                        definition.OperatorId,
                        caseName,
                        "Duration",
                        expectedDuration,
                        lifecycle.ActiveDurationSeconds,
                        0.001f,
                        source);
                }
            }

            var expectedRangeRatio = OperatorRangeCatalog.GetForwardReachRatio(
                definition.E2Progression.RangeId,
                snapshot.RangeId,
                1f);
            var actualRangeRatio = OperatorRangeUtility.ResolveSkillRangeMultiplier(
                behaviour,
                slot,
                1f);
            report.Compare(
                definition.OperatorId,
                caseName,
                "RangeMultiplier",
                expectedRangeRatio,
                actualRangeRatio,
                0.001f,
                source);

            var expectedRecovery = (float)snapshot.SkillPointRecoveryType;
            var actualRecovery = (float)controller.GetRecoveryType(slot);
            report.Compare(
                definition.OperatorId,
                caseName,
                "SP Recovery Type",
                expectedRecovery,
                actualRecovery,
                0f,
                source);
        }

        private static void AcceptOperatorSpecific(
            PlayableOperatorDefinition definition,
            OperatorSkillMasterySnapshot snapshot,
            int slot,
            int mastery,
            MonoBehaviour behaviour,
            OperatorRuntimeStats runtime,
            OperatorNumericAcceptanceReport report)
        {
            var id = definition.OperatorId;
            var caseName = SkillCase(slot, mastery);
            var source = snapshot.SourceSkillId;

            if (string.Equals(id, "Chen", StringComparison.OrdinalIgnoreCase))
            {
                var expectedScale = snapshot.GetFirstBlackboard(0f, "atk_scale");
                ComparePrivate(report, id, caseName, "ATK Scale", expectedScale, behaviour, "masteryAttackScale", source);

                if (slot == 1)
                {
                    var expectedTargets = snapshot.GetFirstBlackboardInt(0, "max_target");
                    ComparePrivate(report, id, caseName, "Max Targets", expectedTargets, behaviour, "masteryMaxTargets", source);
                    report.Compare(
                        id,
                        caseName,
                        "Computed Skill Damage",
                        runtime.Attack * expectedScale,
                        runtime.Attack * ReadPrivateFloat(behaviour, "masteryAttackScale"),
                        0.01f,
                        source);
                }
                else
                {
                    var expectedTimes = snapshot.GetFirstBlackboardInt(0, "times");
                    var expectedStun = snapshot.GetFirstBlackboard(0f, "stun");
                    ComparePrivate(report, id, caseName, "Strike Count", expectedTimes, behaviour, "strikeCount", source);
                    ComparePrivate(report, id, caseName, "Final Stun", expectedStun, behaviour, "finalStrikeStunSeconds", source);
                    report.Compare(
                        id,
                        caseName,
                        "Computed Per-Strike Damage",
                        runtime.Attack * expectedScale,
                        runtime.Attack * ReadPrivateFloat(behaviour, "masteryAttackScale"),
                        0.01f,
                        source);
                }

                return;
            }

            if (string.Equals(id, "Schwarz", StringComparison.OrdinalIgnoreCase))
            {
                var atkBonus = snapshot.GetFirstBlackboard(0f, "atk");
                ComparePrivate(
                    report,
                    id,
                    caseName,
                    "Official ATK Multiplier",
                    1f + atkBonus,
                    behaviour,
                    "officialAttackMultiplier",
                    source);

                SetAutoBool(behaviour, "IsBuffActive", true);
                report.Compare(
                    id,
                    caseName,
                    "Resolved ATK While Active",
                    definition.E2Progression.Evaluate(1).Attack * (1f + atkBonus),
                    runtime.Attack,
                    0.01f,
                    source);

                if (slot == 2)
                {
                    var delta = snapshot.GetFirstBlackboard(0f, "base_attack_time");
                    ComparePrivate(
                        report,
                        id,
                        caseName,
                        "AttackInterval Delta",
                        delta,
                        behaviour,
                        "basicAttackIntervalFlatDelta",
                        source);
                    report.Compare(
                        id,
                        caseName,
                        "Resolved Attack Interval",
                        Mathf.Max(0.05f, definition.E2Progression.Evaluate(1).AttackInterval + delta),
                        runtime.AttackInterval,
                        0.001f,
                        source);
                }

                SetAutoBool(behaviour, "IsBuffActive", false);
                return;
            }

            if (string.Equals(id, "Skadi", StringComparison.OrdinalIgnoreCase))
            {
                var atkBonus = snapshot.GetFirstBlackboard(0f, "atk");

                if (slot == 1 && behaviour is SkadiSkill1 s2)
                {
                    report.Compare(id, caseName, "ATK Bonus", atkBonus, s2.OfficialAttackBonus, 0.001f, source);
                    SetAutoBool(s2, "IsActive", true);
                    report.Compare(
                        id,
                        caseName,
                        "Resolved ATK While Active",
                        definition.E2Progression.Evaluate(1).Attack * (1f + atkBonus),
                        runtime.Attack,
                        0.01f,
                        source);
                    SetAutoBool(s2, "IsActive", false);
                }
                else if (slot == 2 && behaviour is SkadiSkill2 s3)
                {
                    var defBonus = snapshot.GetFirstBlackboard(0f, "def");
                    var hpBonus = snapshot.GetFirstBlackboard(0f, "max_hp");
                    report.Compare(id, caseName, "ATK Bonus", atkBonus, s3.OfficialAttackBonus, 0.001f, source);
                    report.Compare(id, caseName, "DEF Bonus", defBonus, s3.OfficialDefenseBonus, 0.001f, source);
                    report.Compare(id, caseName, "MaxHP Bonus", hpBonus, s3.OfficialMaxHealthBonus, 0.001f, source);

                    SetAutoBool(s3, "IsActive", true);
                    var baseStats = definition.E2Progression.Evaluate(1);
                    report.Compare(id, caseName, "Resolved ATK While Active", baseStats.Attack * (1f + atkBonus), runtime.Attack, 0.01f, source);
                    report.Compare(id, caseName, "Resolved DEF While Active", baseStats.PhysicalDefense * (1f + defBonus), runtime.PhysicalDefense, 0.01f, source);
                    report.Compare(id, caseName, "Resolved MaxHP While Active", baseStats.MaxHealth * (1f + hpBonus), runtime.MaxHealth, 0.01f, source);
                    SetAutoBool(s3, "IsActive", false);
                }

                return;
            }

            if (string.Equals(id, "Wisadel", StringComparison.OrdinalIgnoreCase) &&
                behaviour is WisadelSkill wisadel)
            {
                var atkBonus = snapshot.GetFirstBlackboard(0f, "atk");
                var intervalDelta = snapshot.GetFirstBlackboard(0f, "base_attack_time");
                ComparePrivate(report, id, caseName, "ATK Bonus", atkBonus, wisadel, "masteryAttackBonus", source);
                ComparePrivate(report, id, caseName, "AttackInterval Delta", intervalDelta, wisadel, "masteryAttackIntervalFlatDelta", source);

                SetAutoBool(wisadel, "IsActive", true);
                var baseStats = definition.E2Progression.Evaluate(1);
                report.Compare(id, caseName, "Resolved ATK While Active", baseStats.Attack * (1f + atkBonus), runtime.Attack, 0.01f, source);
                report.Compare(id, caseName, "Resolved Attack Interval", Mathf.Max(0.05f, baseStats.AttackInterval + intervalDelta), runtime.AttackInterval, 0.001f, source);

                if (slot == 1)
                {
                    var overloadScale = snapshot.GetFirstBlackboard(0f, "attack@atk_scale_ol");
                    ComparePrivate(report, id, caseName, "Overload ATK Scale", overloadScale, wisadel, "masteryOverloadAttackScale", source);
                    report.Compare(
                        id,
                        caseName,
                        "Computed Overload Shot Damage",
                        baseStats.Attack * (1f + atkBonus) * overloadScale,
                        runtime.Attack * ReadPrivateFloat(wisadel, "masteryOverloadAttackScale"),
                        0.01f,
                        source);
                }
                else
                {
                    var attackScale = snapshot.GetFirstBlackboard(0f, "attack@atk_scale_3");
                    var ammo = snapshot.GetFirstBlackboardInt(0, "attack@trigger_time");
                    ComparePrivate(report, id, caseName, "S3 ATK Scale", attackScale, wisadel, "masterySkill3AttackScale", source);
                    report.Compare(id, caseName, "Ammo", ammo, wisadel.AmmoCapacity, 0f, source);
                    report.Compare(
                        id,
                        caseName,
                        "Computed S3 Shot Damage",
                        baseStats.Attack * (1f + atkBonus) * attackScale,
                        runtime.Attack * ReadPrivateFloat(wisadel, "masterySkill3AttackScale"),
                        0.01f,
                        source);
                }

                SetAutoBool(wisadel, "IsActive", false);
                return;
            }

            if (string.Equals(id, "FrostNova", StringComparison.OrdinalIgnoreCase))
            {
                var damageScale = snapshot.GetFirstBlackboard(0f, "damage_scale");
                var radius = snapshot.GetFirstBlackboard(0f, "radius_tiles");
                var cold = snapshot.GetFirstBlackboard(0f, "cold_duration");

                ComparePrivate(report, id, caseName, "Damage Scale", damageScale, behaviour, "officialDamageScale", source);
                ComparePrivate(report, id, caseName, "Radius Tiles", radius, behaviour, "officialRadiusTiles", source);
                ComparePrivate(report, id, caseName, "Cold Duration", cold, behaviour, "officialColdDurationSeconds", source);
                report.Compare(
                    id,
                    caseName,
                    "Computed Arts Damage",
                    definition.E2Progression.Evaluate(1).Attack * damageScale,
                    runtime.Attack * ReadPrivateFloat(behaviour, "officialDamageScale"),
                    0.01f,
                    source);
            }
        }

        private static float ResolveExpectedDuration(
            string operatorId,
            int slot,
            OperatorSkillMasterySnapshot snapshot)
        {
            if (string.Equals(operatorId, "Skadi", StringComparison.OrdinalIgnoreCase) && slot == 1)
                return snapshot.GetFirstBlackboard(-1f, "duration");
            return snapshot.Duration;
        }

        private static void AddSkillAdapters(string operatorId, GameObject go)
        {
            if (string.Equals(operatorId, "Chen", StringComparison.OrdinalIgnoreCase))
            {
                go.AddComponent<ChenSkill1>();
                go.AddComponent<ChenSkill2>();
                return;
            }

            if (string.Equals(operatorId, "Schwarz", StringComparison.OrdinalIgnoreCase))
            {
                go.AddComponent<SchwarzArmorBreakTalent>();
                go.AddComponent<SchwarzSkill1>();
                go.AddComponent<SchwarzSkill2>();
                return;
            }

            if (string.Equals(operatorId, "Skadi", StringComparison.OrdinalIgnoreCase))
            {
                go.AddComponent<SkadiSkill1>();
                go.AddComponent<SkadiSkill2>();
                return;
            }

            if (string.Equals(operatorId, "Wisadel", StringComparison.OrdinalIgnoreCase))
            {
                var s2 = go.AddComponent<WisadelSkill>();
                s2.ConfigureRuntimeSlot(1, "饱和复仇", 0f);
                var s3 = go.AddComponent<WisadelSkill>();
                s3.ConfigureRuntimeSlot(2, "爆裂黎明", 0f);
                return;
            }

            if (string.Equals(operatorId, "FrostNova", StringComparison.OrdinalIgnoreCase))
            {
                var s1 = go.AddComponent<FrostNovaSkill1>();
                s1.ConfigureForSkin(FrostNovaSkinVariant.Winter);
                var s2 = go.AddComponent<FrostNovaSkill2>();
                s2.ConfigureForSkin(FrostNovaSkinVariant.Winter);
            }
        }

        private static GameObject CreateHarness(string name, OperatorBaseStats baseStats)
        {
            var go = new GameObject("__P10_" + name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<Health>();
            go.AddComponent<StatusController>();
            go.AddComponent<CombatStats>();
            go.AddComponent<CombatEntity>();
            var runtime = go.AddComponent<OperatorRuntimeStats>();
            runtime.ConfigureBase(baseStats, refillHealth: true);
            return go;
        }

        private static void CompareStat(
            OperatorNumericAcceptanceReport report,
            PlayableOperatorDefinition definition,
            string caseName,
            string field,
            float expected,
            float actual)
        {
            report.Compare(
                definition.OperatorId,
                caseName,
                field,
                expected,
                actual,
                field.Contains("Interval", StringComparison.OrdinalIgnoreCase) ? 0.001f : 0.01f,
                definition.PrtsCharacterId);
        }

        private static void ComparePrivate(
            OperatorNumericAcceptanceReport report,
            string operatorId,
            string caseName,
            string field,
            float expected,
            object instance,
            string privateField,
            string source)
        {
            report.Compare(
                operatorId,
                caseName,
                field,
                expected,
                ReadPrivateFloat(instance, privateField),
                0.001f,
                source);
        }

        private static float ReadPrivateFloat(object instance, string fieldName)
        {
            if (instance == null)
                return float.NaN;

            var field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return float.NaN;

            var value = field.GetValue(instance);
            return value switch
            {
                float f => f,
                int i => i,
                _ => float.NaN
            };
        }

        private static void SetAutoBool(object instance, string propertyName, bool value)
        {
            if (instance == null)
                return;

            var field = instance.GetType().GetField(
                "<" + propertyName + ">k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(instance, value);
        }

        private static string SkillCase(int slot, int mastery) =>
            $"Skill{slot} / {(mastery == 0 ? "Skill7" : "M" + mastery)}";
    }

    internal sealed class OperatorNumericAcceptanceWindow : EditorWindow
    {
        private OperatorNumericAcceptanceReport _report;
        private Vector2 _scroll;
        private bool _showPassed;
        private bool _showFailed = true;

        public static void OpenAndRun()
        {
            var window = GetWindow<OperatorNumericAcceptanceWindow>("数值自动验收");
            window.minSize = new Vector2(980f, 520f);
            window.RunAcceptance();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新验收", GUILayout.Width(120f), GUILayout.Height(28f)))
                    RunAcceptance();

                GUILayout.Space(12f);
                _showFailed = GUILayout.Toggle(_showFailed, "Fail", GUILayout.Width(60f));
                _showPassed = GUILayout.Toggle(_showPassed, "Pass", GUILayout.Width(60f));

                GUILayout.FlexibleSpace();
                if (_report != null)
                {
                    GUILayout.Label(
                        $"Pass {_report.PassedCount}   Fail {_report.FailedCount}",
                        EditorStyles.boldLabel);
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "P10 将 OperatorDefinition / SkillMastery 作为官方输入，通过真实 RuntimeStats 与技能 Adapter 计算实际结果，再比较 Expected / Actual / Delta。默认只显示失败项。",
                MessageType.Info);

            if (_report == null)
                return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _report.Rows.Count; i++)
            {
                var row = _report.Rows[i];
                if (row.Passed && !_showPassed)
                    continue;
                if (!row.Passed && !_showFailed)
                    continue;

                var type = row.Passed ? MessageType.Info : MessageType.Error;
                EditorGUILayout.HelpBox(
                    $"{row.OperatorId} · {row.Case} · {row.Field}\n" +
                    $"Expected {row.Expected:0.####}   Actual {row.Actual:0.####}   " +
                    $"Delta {row.Delta:0.####}\n{row.Source}",
                    type);
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunAcceptance()
        {
            _report = OperatorNumericAcceptanceRunner.Run();
            Repaint();
        }
    }
}
#endif
