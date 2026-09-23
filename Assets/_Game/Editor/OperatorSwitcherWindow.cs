#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Data-driven PrototypeRun generator. The window does not know concrete operators or skins;
    /// it renders whatever PlayableOperatorDefinition assets the registry discovers.
    /// </summary>
    public sealed class OperatorSwitcherWindow : EditorWindow
    {
        private const float SkinCardWidth = 210f;
        private const float SkinCardHeight = 270f;

        private Vector2 _scroll;
        private readonly Dictionary<string, Vector2> _skinScroll = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        public static void Open()
        {
            var window = GetWindow<OperatorSwitcherWindow>("PrototypeRun");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("PrototypeRun 生成器", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "角色与皮肤来自 PlayableOperatorDefinition；新增角色无需修改此窗口。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);

            var current = FindFirstObjectByType<PlayableOperatorIdentity>();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    current != null
                        ? $"当前起始干员：{current.DisplayName}  [{current.OperatorId}]  " +
                          $"皮肤：{current.SkinDisplayName} ({current.SkinId})"
                        : "当前场景：未检测到激活的 Player 干员",
                    EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(8);
            var definitions = PrototypeOperatorRegistry.GetDefinitions();
            if (definitions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "没有发现可用的 PlayableOperatorDefinition / IPrototypeOperatorBuilder。",
                    MessageType.Warning);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null)
                        continue;

                    DrawOperatorSection(definition, current);
                    EditorGUILayout.Space(10);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Editor 这里只决定 PrototypeRun 的起始干员/皮肤；运行时按 Tab 可切换到场景内已生成的其他角色和皮肤。",
                MessageType.Info);
        }

        private void DrawOperatorSection(
            PlayableOperatorDefinition definition,
            PlayableOperatorIdentity current)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"{definition.DisplayName}  /  {definition.EnglishName}  [{definition.OperatorId}]",
                        EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("定位描述资产", GUILayout.Width(100f)))
                    {
                        Selection.activeObject = definition;
                        EditorGUIUtility.PingObject(definition);
                    }
                }

                EditorGUILayout.Space(4);
                var skinScroll = _skinScroll.TryGetValue(definition.OperatorId, out var savedScroll)
                    ? savedScroll
                    : Vector2.zero;
                skinScroll = EditorGUILayout.BeginScrollView(
                    skinScroll,
                    true,
                    false,
                    GUILayout.Height(SkinCardHeight + 12f));
                using (new EditorGUILayout.HorizontalScope())
                {
                    var skins = definition.Skins;
                    for (var skinIndex = 0; skinIndex < skins.Count; skinIndex++)
                    {
                        var skin = skins[skinIndex];
                        if (skin == null)
                            continue;

                        DrawSkinCard(
                            definition,
                            skin,
                            IsCurrent(current, definition.OperatorId, skin.SkinId));
                        GUILayout.Space(8);
                    }
                }
                EditorGUILayout.EndScrollView();
                _skinScroll[definition.OperatorId] = skinScroll;
            }
        }

        private static bool IsCurrent(
            PlayableOperatorIdentity current,
            string operatorId,
            string skinId)
        {
            return current != null &&
                   string.Equals(current.OperatorId, operatorId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(current.SkinId, skinId, StringComparison.OrdinalIgnoreCase);
        }

        private void DrawSkinCard(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin,
            bool selected)
        {
            using (new EditorGUILayout.VerticalScope(
                       selected ? "SelectionRect" : EditorStyles.helpBox,
                       GUILayout.Width(SkinCardWidth),
                       GUILayout.Height(SkinCardHeight)))
            {
                GUILayout.Space(8);
                GUILayout.Label(
                    skin.DisplayName,
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 17,
                        alignment = TextAnchor.MiddleCenter
                    });

                GUILayout.Label(
                    skin.IsReserved ? $"{skin.SkinId}  ·  预留" : skin.SkinId,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    });

                GUILayout.Space(4);
                var avatar = !string.IsNullOrWhiteSpace(skin.AvatarResourceKey)
                    ? Resources.Load<Texture2D>(skin.AvatarResourceKey)
                    : null;
                var avatarRect = GUILayoutUtility.GetRect(
                    150f,
                    150f,
                    GUILayout.ExpandWidth(true));

                if (avatar != null)
                    GUI.DrawTexture(avatarRect, avatar, ScaleMode.ScaleToFit, true);
                else
                    GUI.Box(avatarRect, "头像未绑定");

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(
                           EditorApplication.isPlayingOrWillChangePlaymode || skin.IsReserved))
                {
                    if (GUILayout.Button(
                            skin.IsReserved
                                ? "预留（未接入）"
                                : selected ? "刷新当前角色资源" : "设为起始并生成",
                            GUILayout.Height(34f)))
                    {
                        var operatorId = definition.OperatorId;
                        var skinId = skin.SkinId;
                        PrototypeOperatorEditorSelection.Remember(operatorId, skinId);
                        EditorApplication.delayCall += () =>
                        {
                            if (selected)
                                RefreshExistingOperator(definition, skin);
                            else
                                PrototypeRunSceneBuilder.Build(operatorId, skinId);
                            Repaint();
                        };
                    }
                }
            }
        }

        private static void RefreshExistingOperator(
            PlayableOperatorDefinition definition,
            PlayableOperatorSkinDefinition skin)
        {
            if (definition == null || skin == null)
                return;

            var current = PrototypeOperatorEditorSelection.FindCurrentSceneIdentity();
            PrototypeOperatorEditorSelection.Remember(definition.OperatorId, skin.SkinId);
            CurrentOperatorAssetRefreshService.Refresh(
                definition,
                skin,
                current,
                force: false,
                reason: "operator switcher");

            if (current != null)
            {
                Selection.activeGameObject = current.gameObject;
                EditorGUIUtility.PingObject(current.gameObject);
            }
        }
    }
}
#endif
