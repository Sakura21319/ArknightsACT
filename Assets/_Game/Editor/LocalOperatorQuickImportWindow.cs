#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class LocalOperatorQuickImportWindow : EditorWindow
    {
        private string _sourceFolder = string.Empty;
        private string _packageName = string.Empty;
        private LocalOperatorPackageScan _scan;
        private bool[] _selectedSkins = Array.Empty<bool>();
        private Vector2 _scroll;
        private bool _force;
        private float _targetWorldHeight = 1.64f;
        private float _feetLocalY = -0.72f;
        private float _safeInitialScale = 0.38f;

        internal static void Open()
        {
            var window = GetWindow<LocalOperatorQuickImportWindow>("角色素材快速导入");
            window.minSize = new Vector2(680f, 560f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("角色素材快速导入", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "用于把 ArkMod 导出的 Spine / Move / 头像 / 技能图标 / 语音 / FX 快速同步到项目。\n" +
                "这里只处理表现资源，不自动猜 Gameplay、技能时序或最终 FX 偏移。",
                MessageType.Info);

            DrawSource();
            EditorGUILayout.Space(8f);

            if (_scan == null)
            {
                EditorGUILayout.HelpBox(
                    "选择 D:\\Ark\\_Unpacked 下的角色目录后点击“扫描”。",
                    MessageType.None);
                return;
            }

            DrawScanResult();
            EditorGUILayout.Space(8f);
            DrawImportOptions();
        }

        private void DrawSource()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("素材源", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "当前解包根目录",
                    LocalOperatorAssetImportUtility.UnpackedRoot);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _sourceFolder = EditorGUILayout.TextField(
                        "角色目录",
                        _sourceFolder);

                    if (GUILayout.Button("选择目录...", GUILayout.Width(100f)))
                    {
                        var selected = EditorUtility.OpenFolderPanel(
                            "选择 ArkMod 角色导出目录",
                            LocalOperatorAssetImportUtility.UnpackedRoot,
                            string.Empty);
                        if (!string.IsNullOrWhiteSpace(selected))
                        {
                            var parent = Directory.GetParent(selected)?.FullName;
                            if (!string.IsNullOrWhiteSpace(parent))
                                LocalOperatorAssetImportUtility.UnpackedRoot = parent;

                            _sourceFolder = Path.GetFileName(selected.TrimEnd(
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar));
                            Scan();
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_sourceFolder)))
                {
                    if (GUILayout.Button("扫描", GUILayout.Height(28f)))
                        Scan();
                }
            }
        }

        private void DrawScanResult()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"{_scan.DisplayName}  [{_scan.CharacterId}]",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"角色代码: {_scan.CharacterCode}    " +
                    $"Spine 皮肤: {_scan.Skins.Count}    " +
                    $"技能图标: {_scan.IconRelativePaths.Count}    " +
                    $"FX: {_scan.FxTags.Count}");

                _packageName = EditorGUILayout.TextField(
                    "Unity 包名",
                    _packageName);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("选择皮肤", EditorStyles.boldLabel);

                _scroll = EditorGUILayout.BeginScrollView(
                    _scroll,
                    GUILayout.MinHeight(170f),
                    GUILayout.MaxHeight(260f));

                for (var i = 0; i < _scan.Skins.Count; i++)
                {
                    var skin = _scan.Skins[i];
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        _selectedSkins[i] = EditorGUILayout.ToggleLeft(
                            $"{skin.SkinId}  ·  {skin.CombatBaseName}",
                            _selectedSkins[i]);

                        EditorGUILayout.LabelField(
                            string.IsNullOrWhiteSpace(skin.MotionBaseName)
                                ? "Move Spine: 未发现"
                                : "Move Spine: " + skin.MotionBaseName,
                            EditorStyles.miniLabel);

                        var fxCount =
                            LocalOperatorPackageScanner.FxTagsForSkin(
                                _scan,
                                skin.SkinId).Length;
                        EditorGUILayout.LabelField(
                            $"可导入 FX（公共 + 该皮肤）: {fxCount}",
                            EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("全选皮肤"))
                    {
                        for (var i = 0; i < _selectedSkins.Length; i++)
                            _selectedSkins[i] = true;
                    }

                    if (GUILayout.Button("只选原版"))
                    {
                        for (var i = 0; i < _selectedSkins.Length; i++)
                            _selectedSkins[i] =
                                string.Equals(
                                    _scan.Skins[i].SkinId,
                                    "default",
                                    StringComparison.OrdinalIgnoreCase);
                    }

                    if (GUILayout.Button("清空"))
                    {
                        for (var i = 0; i < _selectedSkins.Length; i++)
                            _selectedSkins[i] = false;
                    }
                }
            }
        }

        private void DrawImportOptions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Presentation 默认布局", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "这里只是首次生成 Prefab 的基础尺寸，不是 FX 偏移。角色最终视觉尺寸/偏移仍可后续人工调。",
                    MessageType.None);

                _targetWorldHeight = EditorGUILayout.FloatField(
                    "Target World Height",
                    _targetWorldHeight);
                _feetLocalY = EditorGUILayout.FloatField(
                    "Feet Local Y",
                    _feetLocalY);
                _safeInitialScale = EditorGUILayout.FloatField(
                    "Initial Scale",
                    _safeInitialScale);
                _force = EditorGUILayout.ToggleLeft(
                    "强制重导选中皮肤（忽略增量检测）",
                    _force);

                EditorGUILayout.Space(6f);

                var selectedCount = _selectedSkins.Count(x => x);
                using (new EditorGUI.DisabledScope(
                           selectedCount == 0 ||
                           string.IsNullOrWhiteSpace(_packageName) ||
                           EditorApplication.isCompiling ||
                           EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(
                            $"导入选中的 {selectedCount} 套皮肤",
                            GUILayout.Height(36f)))
                    {
                        ImportSelected();
                    }
                }
            }
        }

        private void Scan()
        {
            try
            {
                _scan = LocalOperatorPackageScanner.Scan(_sourceFolder.Trim());
                if (_scan == null)
                    return;

                _packageName = LocalOperatorPackageScanner.SafeIdentifier(
                    _scan.CharacterCode,
                    "Operator");

                _selectedSkins = new bool[_scan.Skins.Count];
                for (var i = 0; i < _selectedSkins.Length; i++)
                {
                    _selectedSkins[i] = string.Equals(
                        _scan.Skins[i].SkinId,
                        "default",
                        StringComparison.OrdinalIgnoreCase);
                }

                if (!_selectedSkins.Any(x => x) && _selectedSkins.Length > 0)
                    _selectedSkins[0] = true;
            }
            catch (Exception exception)
            {
                _scan = null;
                _selectedSkins = Array.Empty<bool>();
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "扫描失败：\n" + exception.GetBaseException().Message,
                    "确定");
            }
        }

        private void ImportSelected()
        {
            if (_scan == null)
                return;

            try
            {
                var skinIds = _scan.Skins
                    .Where((skin, index) =>
                        index < _selectedSkins.Length && _selectedSkins[index])
                    .Select(skin => skin.SkinId)
                    .ToArray();

                var plan = LocalOperatorPackageScanner.CreateImportPlan(
                    _scan,
                    _packageName,
                    skinIds,
                    _targetWorldHeight,
                    _feetLocalY,
                    Mathf.Max(0.01f, _safeInitialScale));

                var summary = LocalOperatorPresentationImporter.Import(
                    plan,
                    skinId: null,
                    force: _force,
                    importAllSkins: true);

                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    $"导入完成。\n\n" +
                    $"皮肤: {summary.SkinCount}\n" +
                    $"Spine Prefab 重建: {summary.SpineSetsBuilt}\n" +
                    $"图片变化: {summary.SpriteCount}\n" +
                    $"语音变化: {summary.VoiceCount}\n" +
                    $"FX: {summary.FxRebuilt}/{summary.FxRequested} 重建\n" +
                    $"FX 帧复制: {summary.FxFramesCopied}\n\n" +
                    "Gameplay 与 FX 偏移没有被自动修改。",
                    "确定");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "导入失败：\n" + exception.GetBaseException().Message,
                    "确定");
            }
        }
    }
}
#endif
