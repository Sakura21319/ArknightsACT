#if UNITY_EDITOR
using ArknightsACT.Gameplay.Characters.Wisadel;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class WisadelFxTuningWindow : EditorWindow
    {
        private const string ProfilePath = "Assets/_Game/Resources/Config/WisadelFxTuningProfile.asset";
        private static readonly string[] Labels =
        {
            "普攻 Start A/B/C",
            "普攻 Trail",
            "普攻 Hit",
            "二技能 Start",
            "二技能 Buff",
            "二技能 Hit",
            "三技能 Start",
            "三技能 Trail",
            "三技能 Hit",
            "普攻 Down Start A/B/C",
            "普攻 Hit 02",
            "二技能 Buff 02",
            "二技能 Hit 02",
            "二技能 Overload Start（条件层）",
            "三技能 Up Start",
            "三技能 Down Start",
            "三技能 Buff Back",
            "三技能 Buff Front",
            "三技能 Buff 02 Back",
            "三技能 Buff 02 Front",
            "三技能 Hit 02",
            "三技能 Hit 03"
        };

        private WisadelFxTuningProfile _profile;
        private Vector2 _scroll;

        internal static void Open()
        {
            var window = GetWindow<WisadelFxTuningWindow>("维什戴尔 FX 调参");
            window.minSize = new Vector2(520f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            _profile = LoadOrCreateProfile();
        }

        private void OnGUI()
        {
            _profile ??= LoadOrCreateProfile();
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("无法加载或创建 WisadelFxTuningProfile。", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("维什戴尔 · 已绑定 FX 配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "此面板不会生成或预览任何 FX。角色 Runtime 已经按 Gameplay 事件绑定特效；这里控制启用状态、左右朝向 XY 偏移，以及少数需要单独校准的缩放/时间参数。\n" +
                "Hit 类偏移以实际受击目标为基准；Trail 类偏移只调整弹道起点。Hit 02 Scale 中 1.0 为当前大小；三技能 Trail Delay 只延迟视觉启动，不改变实际命中时刻。",
                MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < Labels.Length; i++)
                DrawFxItem((WisadelFxSlot)i, i);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("恢复默认：全部启用 / 偏移 0 / Scale 1 / Delay 0", GUILayout.Height(28f)))
            {
                Undo.RecordObject(_profile, "Reset Wisadel FX tuning");
                _profile.ResetDefaults();
                SaveAndApply();
            }
        }

        private void DrawFxItem(WisadelFxSlot slot, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var setting = _profile.Get(slot);
            var enabled = setting.Enabled;
            var nextEnabled = EditorGUILayout.ToggleLeft(
                "启用 FX  " + Labels[index],
                enabled);

            if (nextEnabled != enabled)
            {
                Undo.RecordObject(_profile, "Toggle Wisadel FX");
                setting.disabled = !nextEnabled;
                SaveAndApply();
            }

            var rightOffset = setting.rightOffset;
            var leftOffset = setting.leftOffset;
            var scaleMultiplier = setting.ScaleMultiplier;
            var startDelaySeconds = setting.StartDelaySeconds;
            var changed = false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(220f));
            changed |= FxOffsetEditorControls.DrawOffsetFields(
                "朝右 / RIGHT",
                ref rightOffset);
            EditorGUILayout.EndVertical();
            GUILayout.Space(12f);
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(220f));
            changed |= FxOffsetEditorControls.DrawOffsetFields(
                "朝左 / LEFT",
                ref leftOffset);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (slot == WisadelFxSlot.Skill2Hit02 || slot == WisadelFxSlot.Skill3Hit02)
            {
                var nextScale = EditorGUILayout.FloatField("大小缩放 / Scale", scaleMultiplier);
                nextScale = Mathf.Clamp(nextScale, 0.05f, 10f);
                if (!Mathf.Approximately(nextScale, scaleMultiplier))
                {
                    scaleMultiplier = nextScale;
                    changed = true;
                }
            }

            if (slot == WisadelFxSlot.Skill3Trail)
            {
                var nextDelay = EditorGUILayout.FloatField("启动延迟 / Delay (s)", startDelaySeconds);
                nextDelay = Mathf.Clamp(nextDelay, 0f, 2f);
                if (!Mathf.Approximately(nextDelay, startDelaySeconds))
                {
                    startDelaySeconds = nextDelay;
                    changed = true;
                }
            }

            if (changed)
            {
                Undo.RecordObject(_profile, "Tune Wisadel FX");
                setting.rightOffset = rightOffset;
                setting.leftOffset = leftOffset;
                setting.scaleMultiplier = scaleMultiplier;
                setting.startDelaySeconds = startDelaySeconds;
                SaveAndApply();
            }

            EditorGUILayout.EndVertical();
        }

        private void SaveAndApply()
        {
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();

            var controllers = Object.FindObjectsByType<WisadelExtractedFxController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < controllers.Length; i++)
                controllers[i]?.ApplySavedTuning();

            SceneView.RepaintAll();
        }

        private static WisadelFxTuningProfile LoadOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WisadelFxTuningProfile>(ProfilePath);
            if (profile != null)
                return profile;

            EnsureFolder("Assets/_Game/Resources/Config");
            profile = CreateInstance<WisadelFxTuningProfile>();
            profile.ResetDefaults();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;
            var parent = path.Substring(0, slash);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
#endif
