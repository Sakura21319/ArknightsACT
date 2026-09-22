#if UNITY_EDITOR
using ArknightsACT.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Minimal, safe operator switcher for the current prototype architecture.
    /// Switching rebuilds PrototypeRun.unity through the existing character factories so every
    /// serialized player reference (roguelite flow, stage runtime, rewards, inventory, HUD, etc.)
    /// stays consistent. This intentionally avoids fragile runtime hot-swapping.
    /// </summary>
    public sealed class OperatorSwitcherWindow : EditorWindow
    {
        private const float CardWidth = 220f;
        private const float CardHeight = 300f;

        private Vector2 _scroll;

        [UnityEditor.MenuItem("ArknightsACT/角色切换")]
        public static void Open()
        {
            var window = GetWindow<OperatorSwitcherWindow>("Operator Switcher");
            window.minSize = new Vector2(760f, 480f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("干员切换 / OPERATOR SWITCHER", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var current = FindFirstObjectByType<PlayableOperatorIdentity>();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    current != null
                        ? $"当前场景：{current.DisplayName}   [{current.OperatorId}]   Skin={current.SkinId}"
                        : "当前场景：未检测到 Player 干员",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "点击下方“切换”会使用对应角色 Factory 重建 PrototypeRun.unity。"
                    + " 这是当前项目最安全的换人方式，不会留下旧 Player 引用。",
                    EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space(10);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawOperatorCard(
                        "陈",
                        "CH'EN",
                        "默认",
                        "Assets/_Game/Resources/UI/HUD/chen_avatar.png",
                        current != null && current.OperatorId == "Chen",
                        () => PrototypeSceneBuilder.Build());

                    DrawOperatorCard(
                        "黑",
                        "SCHWARZ",
                        "原版",
                        "Assets/_Game/Resources/UI/HUD/Operators/schwarz_default.png",
                        IsCurrent(current, "Schwarz", "default"),
                        () => SchwarzPrototypeSceneBuilder.BuildDefault());

                    DrawOperatorCard(
                        "黑",
                        "SCHWARZ",
                        "Snow",
                        "Assets/_Game/Resources/UI/HUD/Operators/schwarz_snow.png",
                        IsCurrent(current, "Schwarz", "snow#1"),
                        () => SchwarzPrototypeSceneBuilder.BuildSnow());

                    DrawOperatorCard(
                        "黑",
                        "SCHWARZ",
                        "Striker",
                        "Assets/_Game/Resources/UI/HUD/Operators/schwarz_striker.png",
                        IsCurrent(current, "Schwarz", "striker#1"),
                        () => SchwarzPrototypeSceneBuilder.BuildStriker());
                }
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("黑特效调试 / Schwarz FX Tuning", GUILayout.Height(32)))
                SchwarzFxTuningWindow.Open();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "当前版本只允许在非 Play Mode 下切换。"
                + " 如果正在运行，请先停止 Play Mode，再点击切换。",
                MessageType.Info);
        }

        private static bool IsCurrent(PlayableOperatorIdentity current, string operatorId, string skinId)
        {
            return current != null &&
                   current.OperatorId == operatorId &&
                   current.SkinId == skinId;
        }

        private void DrawOperatorCard(
            string displayName,
            string englishName,
            string skinLabel,
            string avatarPath,
            bool selected,
            System.Action switchAction)
        {
            using (new EditorGUILayout.VerticalScope(
                       selected ? "SelectionRect" : EditorStyles.helpBox,
                       GUILayout.Width(CardWidth),
                       GUILayout.Height(CardHeight)))
            {
                GUILayout.Space(8);
                GUILayout.Label(displayName, new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter
                });
                GUILayout.Label(englishName, new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                });
                GUILayout.Space(4);

                var avatar = AssetDatabase.LoadAssetAtPath<Texture2D>(avatarPath);
                var rect = GUILayoutUtility.GetRect(160f, 160f, GUILayout.ExpandWidth(true));
                if (avatar != null)
                    GUI.DrawTexture(rect, avatar, ScaleMode.ScaleToFit, true);
                else
                    GUI.Box(rect, "头像未导入\n" + avatarPath);

                GUILayout.Space(4);
                GUILayout.Label("皮肤：" + skinLabel, new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter
                });

                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(selected ? "重新构建" : "切换", GUILayout.Height(34)))
                    {
                        // Rebuilding the scene mutates the editor hierarchy and can invalidate
                        // IMGUI's active layout stack if done inside OnGUI. Defer the entire action.
                        EditorApplication.delayCall += () =>
                        {
                            switchAction?.Invoke();
                            Repaint();
                            var identity = FindFirstObjectByType<PlayableOperatorIdentity>();
                            if (identity != null)
                                Selection.activeGameObject = identity.gameObject;
                        };
                    }
                }
            }
        }
    }
}
#endif
