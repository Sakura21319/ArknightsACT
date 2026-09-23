#if UNITY_EDITOR
using ArknightsACT.Gameplay.Characters.Schwarz;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    public sealed class SchwarzFxTuningWindow : EditorWindow
    {
        private const string ProfileAssetPath =
            "Assets/_Game/Resources/Config/SchwarzFxTuningProfile.asset";

        private SchwarzFxTuningProfile _profile;
        private Vector2 _scroll;
        public static void Open()
        {
            var window = GetWindow<SchwarzFxTuningWindow>("黑特效调试");
            window.minSize = new Vector2(760f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            SchwarzLocalAssetBootstrap.EnsureS2CompositeFx();
            _profile = LoadOrCreateProfile();
        }

        private void OnGUI()
        {
            if (_profile == null)
                _profile = LoadOrCreateProfile();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("黑 / SCHWARZ FX TUNING", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "每个特效分别保存【朝右】与【朝左】两套 X/Y Offset、Scale、Angle。\n" +
                "右向保留当前已调好的值；左向可以单独微调，不再依赖数学镜像。保存后运行时立即重新加载。",
                MessageType.Info);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                DrawGroup("普通攻击", new[]
                {
                    (SchwarzFxSlot.BasicStart, "普攻 Start"),
                    (SchwarzFxSlot.BasicTrail, "普攻箭矢 / Tracer"),
                    (SchwarzFxSlot.BasicHit, "普攻 Hit")
                });

                DrawGroup("二技能 / 暮眼锐瞳", new[]
                {
                    (SchwarzFxSlot.Skill2IgniteRed, "common_064_ignite_attack_red"),
                    (SchwarzFxSlot.Skill2Combustion, "common_combustion_buff_02"),
                    (SchwarzFxSlot.Skill2Trail, "S2 强化箭矢")
                });

                DrawGroup("三技能 / 战术的终结", new[]
                {
                    (SchwarzFxSlot.Skill3Start, "S3 Shot FX / skill_03_start（点击才播放）"),
                    (SchwarzFxSlot.Skill3Trail, "S3 专用箭矢 / skill_03_trail"),
                    (SchwarzFxSlot.Skill3Hit, "S3 Hit / skill_01_hit（按皮肤）"),
                    (SchwarzFxSlot.Skill3Buff02, "S3 Buff 02 / 全程持续"),
                    (SchwarzFxSlot.Skill3Buff03, "S3 Buff 03 / 全程持续")
                });
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存并应用", GUILayout.Height(34)))
                    SaveAndApply();

                if (GUILayout.Button("恢复默认", GUILayout.Height(34)))
                {
                    Undo.RecordObject(_profile, "Reset Schwarz FX Tuning");
                    _profile.ResetDefaults();
                    EditorUtility.SetDirty(_profile);
                    Repaint();
                }
            }

            EditorGUILayout.LabelField(
                "配置文件：" + ProfileAssetPath,
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawGroup(string title, (SchwarzFxSlot slot, string label)[] items)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            foreach (var item in items)
                DrawDirectionalSetting(item.slot, item.label);
        }

        private void DrawDirectionalSetting(SchwarzFxSlot slot, string label)
        {
            var setting = _profile.Get(slot);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSide(setting, true);
                    GUILayout.Space(10);
                    DrawSide(setting, false);
                }
            }
        }

        private void DrawSide(SchwarzFxTuningSetting setting, bool right)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(330f)))
            {
                EditorGUILayout.LabelField(
                    right ? "朝右 / RIGHT" : "朝左 / LEFT",
                    EditorStyles.boldLabel);

                var offset = right ? setting.rightOffset : setting.leftOffset;
                var scale = right ? setting.rightScale : setting.leftScale;
                var angle = right ? setting.rightAngleDegrees : setting.leftAngleDegrees;

                EditorGUI.BeginChangeCheck();
                offset = EditorGUILayout.Vector2Field("X / Y Offset", offset);
                scale = EditorGUILayout.FloatField("Scale", scale);
                angle = EditorGUILayout.FloatField("Angle", angle);

                if (!EditorGUI.EndChangeCheck())
                    return;

                Undo.RecordObject(_profile, "Tune Schwarz FX");
                if (right)
                {
                    setting.rightOffset = offset;
                    setting.rightScale = Mathf.Max(0.05f, scale);
                    setting.rightAngleDegrees = angle;
                }
                else
                {
                    setting.leftOffset = offset;
                    setting.leftScale = Mathf.Max(0.05f, scale);
                    setting.leftAngleDegrees = angle;
                }

                EditorUtility.SetDirty(_profile);
            }
        }

        private void SaveAndApply()
        {
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var controllers = Object.FindObjectsByType<SchwarzExtractedFxController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < controllers.Length; i++)
                controllers[i]?.ApplySavedTuning();

            SceneView.RepaintAll();
            ShowNotification(new GUIContent("已保存并应用黑的左右特效参数"));
        }

        private static SchwarzFxTuningProfile LoadOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<SchwarzFxTuningProfile>(ProfileAssetPath);
            if (profile != null)
                return profile;

            EnsureFolder("Assets/_Game/Resources");
            EnsureFolder("Assets/_Game/Resources/Config");

            profile = CreateInstance<SchwarzFxTuningProfile>();
            profile.ResetDefaults();
            AssetDatabase.CreateAsset(profile, ProfileAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            var name = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
