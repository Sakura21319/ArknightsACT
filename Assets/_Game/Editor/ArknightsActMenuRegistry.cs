#if UNITY_EDITOR
using UnityEditor;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// The only supported top-level ArknightsACT menu registry.
    /// Character/editor tools expose callable methods but do not register their own scattered menus.
    /// </summary>
    internal static class ArknightsActMenuRegistry
    {
        [UnityEditor.MenuItem("ArknightsACT/PrototypeRun/角色与皮肤", false, 10)]
        private static void OpenOperatorSwitcher() => OperatorSwitcherWindow.Open();

        [UnityEditor.MenuItem("ArknightsACT/PrototypeRun/重新生成当前角色", false, 11)]
        private static void RebuildCurrentOperator()
        {
            if (!PrototypeOperatorEditorSelection.TryResolve(
                    out var definition,
                    out var skin,
                    out _))
                return;

            PrototypeRunSceneBuilder.Build(definition.OperatorId, skin.SkinId);
        }

        [UnityEditor.MenuItem("ArknightsACT/角色资源/快速导入角色素材...", false, 29)]
        private static void OpenLocalOperatorQuickImport() => LocalOperatorQuickImportWindow.Open();

        [UnityEditor.MenuItem("ArknightsACT/角色资源/刷新当前角色资源", false, 30)]
        private static void RefreshCurrentOperatorAssets() =>
            CurrentOperatorAssetRefreshService.RefreshCurrent(false, "menu");

        [UnityEditor.MenuItem("ArknightsACT/角色资源/强制刷新当前角色资源", false, 31)]
        private static void ForceRefreshCurrentOperatorAssets() =>
            CurrentOperatorAssetRefreshService.RefreshCurrent(true, "menu force");

        [UnityEditor.MenuItem("ArknightsACT/角色资源/导入维什戴尔（原版 + game#9）", false, 32)]
        private static void ImportWisadelLocalAssets() =>
            WisadelLocalAssetBootstrap.ImportOriginalAndGame9(true);

        [UnityEditor.MenuItem("ArknightsACT/角色资源/导入斯卡蒂（原版 + marthe#5 + summer#3）", false, 33)]
        private static void ImportSkadiLocalAssets() =>
            SkadiLocalAssetBootstrap.ImportAll(true);

        [UnityEditor.MenuItem("ArknightsACT/角色资源/设置本地解包根目录...", false, 39)]
        private static void SetLocalUnpackedRoot()
        {
            var selected = EditorUtility.OpenFolderPanel(
                "选择 ArkMod 解包根目录",
                LocalOperatorAssetImportUtility.UnpackedRoot,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selected))
                return;

            LocalOperatorAssetImportUtility.UnpackedRoot = selected;
            UnityEngine.Debug.Log(
                "[ArknightsACT/LocalImport] Unpacked root = " +
                LocalOperatorAssetImportUtility.UnpackedRoot);
        }

        [UnityEditor.MenuItem("ArknightsACT/角色调试/霜星·冬痕调节", false, 50)]
        private static void OpenFrostNovaTuning() => FrostNovaTuningWindow.Open();

        [UnityEditor.MenuItem("ArknightsACT/角色调试/黑·技能特效调节", false, 51)]
        private static void OpenSchwarzTuning() => SchwarzFxTuningWindow.Open();

        [UnityEditor.MenuItem("ArknightsACT/角色调试/维什戴尔 FX 偏移", false, 52)]
        private static void OpenWisadelFxTuning() => WisadelFxTuningWindow.Open();

        [UnityEditor.MenuItem("ArknightsACT/战斗调试/P1 状态调试器", false, 70)]
        private static void OpenP1CombatDebugger() => P1CombatStatusDebuggerWindow.Open();
    }
}
#endif
