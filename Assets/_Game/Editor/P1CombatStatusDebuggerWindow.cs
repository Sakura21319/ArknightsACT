#if UNITY_EDITOR
using System.Linq;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class P1CombatStatusDebuggerWindow : EditorWindow
    {
        private enum TargetMode
        {
            ActivePlayer,
            NearestEnemy,
            Selection
        }

        private TargetMode _targetMode = TargetMode.NearestEnemy;
        private float _damageAmount = 100f;
        private float _burnPerTick = 8f;
        private float _defenseDown = 0.20f;
        private float _resistanceDown = 20f;
        private float _fragile = 0.25f;
        private DamageResult? _lastDamageResult;
        internal static void Open()
        {
            var window = GetWindow<P1CombatStatusDebuggerWindow>("P1 Combat Debug");
            window.minSize = new Vector2(420f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange _)
        {
            _lastDamageResult = null;
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("P1 Combat / Status Debugger", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "仅用于 Play Mode 验证。不会写入场景或正式运行时输入。",
                MessageType.Info);

            _targetMode = (TargetMode)EditorGUILayout.EnumPopup("Target", _targetMode);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后可用。", MessageType.Warning);
                return;
            }

            var target = ResolveTarget();
            var source = ResolveSource(target);

            DrawTargetSummary(target);

            using (new EditorGUI.DisabledScope(target == null))
            {
                EditorGUILayout.Space(8f);
                DrawDamageControls(source, target);

                EditorGUILayout.Space(8f);
                DrawStatusControls(source, target);

                EditorGUILayout.Space(8f);
                DrawActiveStatuses(target);
            }
        }

        private void DrawTargetSummary(CombatEntity target)
        {
            EditorGUILayout.LabelField("Target State", EditorStyles.boldLabel);
            if (target == null)
            {
                EditorGUILayout.HelpBox("当前目标不存在。NearestEnemy 需要场景中有存活 Enemy。", MessageType.Warning);
                return;
            }

            EditorGUILayout.ObjectField("Entity", target, typeof(CombatEntity), true);
            EditorGUILayout.LabelField("Team", target.Team.ToString());

            if (target.Health != null)
            {
                EditorGUILayout.LabelField(
                    "HP",
                    target.Health.CurrentHealth.ToString("0.##") + " / " +
                    target.Health.MaxHealth.ToString("0.##"));
            }

            if (target.Stats != null)
            {
                EditorGUILayout.LabelField("DEF", target.Stats.PhysicalDefense.ToString("0.##"));
                EditorGUILayout.LabelField("RES", target.Stats.ArtsResistance.ToString("0.##"));
                EditorGUILayout.LabelField("Move x", target.Stats.MoveSpeedMultiplier.ToString("0.###"));
                EditorGUILayout.LabelField("AttackSpeed x", target.Stats.AttackSpeedMultiplier.ToString("0.###"));
            }

            var blocked = target.Status != null
                ? target.Status.BlockedActions
                : CombatActionMask.None;
            EditorGUILayout.LabelField("Blocked", blocked.ToString());
        }

        private void DrawDamageControls(CombatEntity source, CombatEntity target)
        {
            EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);
            _damageAmount = Mathf.Max(0f, EditorGUILayout.FloatField("Base Damage", _damageAmount));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Physical"))
                    ApplyDamage(source, target, DamageType.Physical);
                if (GUILayout.Button("Arts"))
                    ApplyDamage(source, target, DamageType.Arts);
                if (GUILayout.Button("True"))
                    ApplyDamage(source, target, DamageType.True);
            }

            if (_lastDamageResult.HasValue)
            {
                var result = _lastDamageResult.Value;
                EditorGUILayout.LabelField(
                    "Last",
                    "Raw " + result.RawDamage.ToString("0.##") +
                    " | Pre " + result.PreMitigationDamage.ToString("0.##") +
                    " | Mitigated " + result.MitigatedDamage.ToString("0.##") +
                    " | Final " + result.FinalDamage.ToString("0.##"));
                EditorGUILayout.LabelField(
                    "Mitigation",
                    "DEF " + result.EffectiveDefense.ToString("0.##") +
                    " | RES " + result.EffectiveResistance.ToString("0.##") +
                    " | Killed " + result.Killed);
            }
        }

        private void DrawStatusControls(CombatEntity source, CombatEntity target)
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cold"))
                    target.Status?.Apply(CombatStatusIds.Cold, 4f, source, source);
                if (GUILayout.Button("Cold Again"))
                    target.Status?.Apply(CombatStatusIds.Cold, 4f, source, source);
                if (GUILayout.Button("Freeze"))
                    target.Status?.Apply(CombatStatusIds.Freeze, 2.5f, source, source);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Disarm"))
                    target.Status?.Apply(CombatStatusIds.Disarm, 4f, source, source);
                if (GUILayout.Button("Stun"))
                    target.Status?.Apply(CombatStatusIds.Stun, 2f, source, source);
                if (GUILayout.Button("Root"))
                    target.Status?.Apply(CombatStatusIds.Root, 3f, source, source);
            }

            _burnPerTick = Mathf.Max(0f, EditorGUILayout.FloatField("Burn / tick", _burnPerTick));
            if (GUILayout.Button("Apply Burn"))
            {
                target.Status?.Apply(
                    CombatStatusIds.Burn,
                    duration: 2.05f,
                    source: source,
                    owner: source,
                    magnitude: _burnPerTick,
                    periodicDamageType: DamageType.Arts,
                    sourceId: "P1Debug_Burn");
            }

            _defenseDown = Mathf.Clamp01(EditorGUILayout.FloatField("DEF Down", _defenseDown));
            if (GUILayout.Button("Apply DEF Down"))
            {
                target.Status?.Apply(
                    CombatStatusIds.DefenseDown,
                    duration: 5f,
                    source: source,
                    owner: source,
                    magnitude: _defenseDown,
                    sourceId: "P1Debug_DefenseDown");
            }

            _resistanceDown = Mathf.Max(0f, EditorGUILayout.FloatField("RES Down", _resistanceDown));
            if (GUILayout.Button("Apply RES Down"))
            {
                target.Status?.Apply(
                    CombatStatusIds.ResistanceDown,
                    duration: 5f,
                    source: source,
                    owner: source,
                    magnitude: _resistanceDown,
                    sourceId: "P1Debug_ResistanceDown");
            }

            _fragile = Mathf.Max(0f, EditorGUILayout.FloatField("Fragile", _fragile));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Fragile"))
                {
                    target.Status?.Apply(
                        CombatStatusIds.Fragile,
                        duration: 5f,
                        source: source,
                        owner: source,
                        magnitude: _fragile,
                        sourceId: "P1Debug_Fragile");
                }

                if (GUILayout.Button("Apply Weaken"))
                {
                    target.Status?.Apply(
                        CombatStatusIds.Weaken,
                        duration: 5f,
                        source: source,
                        owner: source,
                        magnitude: _fragile,
                        sourceId: "P1Debug_Weaken");
                }
            }

            if (GUILayout.Button("Clear All Statuses"))
                target.Status?.ClearAll();
        }

        private void DrawActiveStatuses(CombatEntity target)
        {
            EditorGUILayout.LabelField("Active Statuses", EditorStyles.boldLabel);
            if (target?.Status == null || target.Status.ActiveStatuses.Count == 0)
            {
                EditorGUILayout.LabelField("None");
                return;
            }

            foreach (var status in target.Status.ActiveStatuses)
            {
                if (status == null || status.Definition == null)
                    continue;

                var remaining = Mathf.Max(0f, status.ExpiresAt - Time.time);
                EditorGUILayout.LabelField(
                    status.Definition.DisplayName,
                    remaining.ToString("0.00") + "s | stack " + status.Stacks +
                    " | magnitude " + status.Magnitude.ToString("0.###"));
            }
        }

        private void ApplyDamage(CombatEntity source, CombatEntity target, DamageType type)
        {
            if (target == null)
                return;

            _lastDamageResult = DamageSystem.Apply(new DamageContext(
                source,
                source,
                target,
                _damageAmount,
                type,
                Vector2.zero,
                sourceId: "P1CombatStatusDebugger",
                tags: DamageTags.SecondaryProc));
        }

        private CombatEntity ResolveTarget()
        {
            switch (_targetMode)
            {
                case TargetMode.ActivePlayer:
                {
                    var active = PlayerRuntimeContext.Resolve();
                    return active != null ? active.GetComponent<CombatEntity>() : null;
                }

                case TargetMode.Selection:
                {
                    if (Selection.activeGameObject == null)
                        return null;
                    return Selection.activeGameObject.GetComponentInParent<CombatEntity>() ??
                           Selection.activeGameObject.GetComponentInChildren<CombatEntity>();
                }

                default:
                    return FindNearestEnemy();
            }
        }

        private static CombatEntity ResolveSource(CombatEntity target)
        {
            var active = PlayerRuntimeContext.Resolve();
            var player = active != null ? active.GetComponent<CombatEntity>() : null;
            if (player != null && player != target)
                return player;

            // World/null source intentionally bypasses friendly-fire rejection for self testing.
            return null;
        }

        private static CombatEntity FindNearestEnemy()
        {
            var active = PlayerRuntimeContext.Resolve();
            var origin = active != null ? active.position : Vector3.zero;

            return Object.FindObjectsByType<CombatEntity>(FindObjectsSortMode.None)
                .Where(entity =>
                    entity != null &&
                    entity.Team == Team.Enemy &&
                    entity.Health != null &&
                    !entity.Health.IsDead)
                .OrderBy(entity => (entity.transform.position - origin).sqrMagnitude)
                .FirstOrDefault();
        }
    }
}
#endif
