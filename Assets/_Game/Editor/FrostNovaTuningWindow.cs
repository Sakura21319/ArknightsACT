#if UNITY_EDITOR
using ArknightsACT.Gameplay.Characters.FrostNova;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class FrostNovaTuningAssetUtility
    {
        internal const string AssetPath =
            "Assets/_Game/Resources/Config/FrostNovaTuningProfile.asset";

        internal static FrostNovaTuningProfile Ensure()
        {
            var profile = AssetDatabase.LoadAssetAtPath<FrostNovaTuningProfile>(AssetPath);
            if (profile != null)
                return profile;

            EnsureFolder("Assets/_Game/Resources");
            EnsureFolder("Assets/_Game/Resources/Config");

            profile = ScriptableObject.CreateInstance<FrostNovaTuningProfile>();
            profile.ResetDefaults();
            AssetDatabase.CreateAsset(profile, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profile;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    internal sealed class FrostNovaTuningWindow : EditorWindow
    {
        private FrostNovaTuningProfile _profile;
        private Vector2 _scroll;
        public static void Open()
        {
            var window = GetWindow<FrostNovaTuningWindow>("霜星·冬痕调节");
            window.minSize = new Vector2(560f, 660f);
            window.Show();
        }

        private void OnEnable()
        {
            _profile = FrostNovaTuningAssetUtility.Ensure();
        }

        private void OnGUI()
        {
            _profile ??= FrostNovaTuningAssetUtility.Ensure();
            if (_profile == null)
            {
                EditorGUILayout.HelpBox(
                    "无法创建 FrostNovaTuningProfile。",
                    MessageType.Error);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("冬痕动画速度", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "当前只维护冬痕这一套数值。霜星原皮后续直接复用这里的参数，不再保留第二套重复调参。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(_profile, "Tune FrostNova Winter");

            _profile.MoveAnimationSpeed = EditorGUILayout.Slider(
                "走路 Move",
                _profile.MoveAnimationSpeed,
                0.1f,
                4f);
            _profile.BasicAttackAnimationSpeed = EditorGUILayout.Slider(
                "普攻 Attack",
                _profile.BasicAttackAnimationSpeed,
                0.1f,
                4f);
            _profile.Skill2AnimationSpeed = EditorGUILayout.Slider(
                "2技能动作 / Skill_1",
                _profile.Skill2AnimationSpeed,
                0.1f,
                3f);
            _profile.Skill3AnimationSpeed = EditorGUILayout.Slider(
                "Skill_3",
                _profile.Skill3AnimationSpeed,
                0.1f,
                3f);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("普攻弹道 / 出伤", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "实际出伤时机自动等于：Trail 出现延迟 + 弹道飞行时间。这样调整弹道后，伤害会自动跟着视觉命中，不需要再单独维护一份伤害延迟。",
                MessageType.None);

            _profile.BasicProjectileFlightSeconds = EditorGUILayout.Slider(
                "弹道飞行时间",
                _profile.BasicProjectileFlightSeconds,
                0.02f,
                1.50f);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(
                    "当前实际出伤时间(s)",
                    _profile.BasicAttackImpactSeconds);
            }

            DrawFx("普攻 Start", FrostNovaFxSlot.BasicStart);
            DrawDirectionalFx(
                "普攻 Trail / 弹道",
                FrostNovaFxSlot.BasicTrail);
            DrawFx("普攻 Hit", FrostNovaFxSlot.BasicHit);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("冬痕 Skill_2 · 冰环", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "动作使用 Spine Skill_1；frstar2_skill_02_range 只在锁定到有效敌人时出现并跟随目标。空放只播动作，不出打击特效。",
                MessageType.None);
            DrawFx("冰环 / Skill_2 Range", FrostNovaFxSlot.WinterSkill2Range);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("冬痕 Skill_3 · 冰暴", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "已按原版拆成两段：Start = 第一段手部特效；Range = 第一段聚气；Range 02 = 第二段爆开。",
                MessageType.None);
            DrawFx("第一段 · 手部 Start", FrostNovaFxSlot.WinterSkill3Start);
            DrawFx("第一段 · 聚气 Range", FrostNovaFxSlot.WinterSkill3Range);
            DrawFx("第二段 · 爆开 Range 02", FrostNovaFxSlot.WinterSkill3Range2);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("冬痕角色附着 FX", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "已确认：Buff 03 = 眼部特效，Buff 05 = 背部特效；Buff 04 用途暂未确认。Buff 01/02 已移出运行时。",
                MessageType.Info);
            DrawActorFx("Buff 03 · 眼部", FrostNovaFxSlot.WinterActorBuff03);
            DrawActorFx("Buff 04 · 未确认", FrostNovaFxSlot.WinterActorBuff04);
            DrawPersistentActorFx(
                "Buff 05 · 常驻背部",
                FrostNovaFxSlot.WinterActorBuff05);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_profile);
                AssetDatabase.SaveAssets();
                ApplyToOpenScene();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(12f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存并立即应用", GUILayout.Height(30f)))
                {
                    EditorUtility.SetDirty(_profile);
                    AssetDatabase.SaveAssets();
                    ApplyToOpenScene();
                }

                if (GUILayout.Button("恢复默认", GUILayout.Height(30f)))
                {
                    Undo.RecordObject(_profile, "Reset FrostNova Winter Tuning");
                    _profile.ResetDefaults();
                    EditorUtility.SetDirty(_profile);
                    AssetDatabase.SaveAssets();
                    ApplyToOpenScene();
                    Repaint();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawFx(string title, FrostNovaFxSlot slot)
        {
            var setting = _profile.Get(slot);
            if (setting == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

            setting.delaySeconds = Mathf.Max(
                0f,
                EditorGUILayout.FloatField(
                    "出现延迟 Delay(s)",
                    setting.delaySeconds));

            setting.playbackSpeed = Mathf.Max(
                0.05f,
                EditorGUILayout.Slider(
                    "播放速度",
                    setting.playbackSpeed,
                    0.05f,
                    4f));

            setting.offset = EditorGUILayout.Vector2Field(
                "Offset",
                setting.offset);
            setting.scale = Mathf.Max(
                0.05f,
                EditorGUILayout.FloatField(
                    "Scale",
                    setting.scale));
            setting.angleDegrees = EditorGUILayout.FloatField(
                "Angle",
                setting.angleDegrees);

            EditorGUILayout.EndVertical();
        }

        private void DrawDirectionalFx(
            string title,
            FrostNovaFxSlot slot)
        {
            var setting = _profile.Get(slot);
            if (setting == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Offset 只修正弹道起点，终点始终使用目标真实位置，因此不会因偏移而多飞一截。",
                MessageType.None);

            setting.delaySeconds = Mathf.Max(
                0f,
                EditorGUILayout.FloatField(
                    "出现延迟 Delay(s)",
                    setting.delaySeconds));
            setting.playbackSpeed = Mathf.Max(
                0.05f,
                EditorGUILayout.Slider(
                    "播放速度",
                    setting.playbackSpeed,
                    0.05f,
                    4f));
            setting.scale = Mathf.Max(
                0.05f,
                EditorGUILayout.FloatField(
                    "Scale",
                    setting.scale));

            EditorGUILayout.LabelField("朝右", EditorStyles.miniBoldLabel);
            setting.offset = EditorGUILayout.Vector2Field(
                "右 Offset",
                setting.offset);
            setting.angleDegrees = EditorGUILayout.FloatField(
                "右 Angle",
                setting.angleDegrees);

            EditorGUILayout.LabelField("朝左", EditorStyles.miniBoldLabel);
            setting.leftOffset = EditorGUILayout.Vector2Field(
                "左 Offset",
                setting.leftOffset);
            setting.leftAngleDegrees = EditorGUILayout.FloatField(
                "左 Angle",
                setting.leftAngleDegrees);

            EditorGUILayout.EndVertical();
        }

        private void DrawActorFx(string title, FrostNovaFxSlot slot)
        {
            var setting = _profile.Get(slot);
            if (setting == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            setting.enabled = EditorGUILayout.ToggleLeft(
                title + " 启用",
                setting.enabled);

            using (new EditorGUI.DisabledScope(!setting.enabled))
            {
                var options = new[] { "Skill_2 + Skill_3", "Skill_2", "Skill_3" };
                var selected = setting.triggerSkill switch
                {
                    2 => 1,
                    3 => 2,
                    _ => 0
                };
                selected = EditorGUILayout.Popup("触发技能", selected, options);
                setting.triggerSkill = selected switch
                {
                    1 => 2,
                    2 => 3,
                    _ => 0
                };

                setting.delaySeconds = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        "出现延迟 Delay(s)",
                        setting.delaySeconds));
                setting.playbackSpeed = Mathf.Max(
                    0.05f,
                    EditorGUILayout.Slider(
                        "播放速度",
                        setting.playbackSpeed,
                        0.05f,
                        4f));
                setting.offset = EditorGUILayout.Vector2Field(
                    "Offset",
                    setting.offset);
                setting.scale = Mathf.Max(
                    0.05f,
                    EditorGUILayout.FloatField(
                        "Scale",
                        setting.scale));
                setting.angleDegrees = EditorGUILayout.FloatField(
                    "Angle",
                    setting.angleDegrees);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPersistentActorFx(
            string title,
            FrostNovaFxSlot slot)
        {
            var setting = _profile.Get(slot);
            if (setting == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "该层在冬痕角色启用期间始终存在并循环播放，不跟随 Skill_2 / Skill_3 重复生成。",
                MessageType.None);

            setting.playbackSpeed = Mathf.Max(
                0.05f,
                EditorGUILayout.Slider(
                    "播放速度",
                    setting.playbackSpeed,
                    0.05f,
                    4f));
            setting.scale = Mathf.Max(
                0.05f,
                EditorGUILayout.FloatField(
                    "Scale",
                    setting.scale));

            EditorGUILayout.LabelField("朝右", EditorStyles.miniBoldLabel);
            setting.offset = EditorGUILayout.Vector2Field(
                "右 Offset",
                setting.offset);
            setting.angleDegrees = EditorGUILayout.FloatField(
                "右 Angle",
                setting.angleDegrees);

            EditorGUILayout.LabelField("朝左", EditorStyles.miniBoldLabel);
            setting.leftOffset = EditorGUILayout.Vector2Field(
                "左 Offset",
                setting.leftOffset);
            setting.leftAngleDegrees = EditorGUILayout.FloatField(
                "左 Angle",
                setting.leftAngleDegrees);

            EditorGUILayout.EndVertical();
        }

        private static void ApplyToOpenScene()
        {
            var drivers = Object.FindObjectsByType<FrostNovaPresentationDriver25D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < drivers.Length; i++)
                drivers[i]?.RefreshTuning();

            var rangedAttacks = Object.FindObjectsByType<FrostNovaRangedBasicAttack>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < rangedAttacks.Length; i++)
                rangedAttacks[i]?.RefreshTuning();

            var fxControllers = Object.FindObjectsByType<FrostNovaExtractedFxController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < fxControllers.Length; i++)
                fxControllers[i]?.RefreshTuning();
        }
    }
}
#endif
