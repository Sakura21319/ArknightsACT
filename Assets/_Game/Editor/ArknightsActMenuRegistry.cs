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

        [UnityEditor.MenuItem("ArknightsACT/角色资源/刷新当前角色资源", false, 30)]
        private static void RefreshCurrentOperatorAssets() =>
            CurrentOperatorAssetRefreshService.RefreshCurrent(false, "menu");

        [UnityEditor.MenuItem("ArknightsACT/角色资源/强制刷新当前角色资源", false, 31)]
        private static void ForceRefreshCurrentOperatorAssets() =>
            CurrentOperatorAssetRefreshService.RefreshCurrent(true, "menu force");

        [UnityEditor.MenuItem("ArknightsACT/角色调试/霜星·冬痕调节", false, 50)]
        private static void OpenFrostNovaTuning() => FrostNovaTuningWindow.Open();

        [UnityEditor.MenuItem("ArknightsACT/角色调试/黑·技能特效调节", false, 51)]
        private static void OpenSchwarzTuning() => SchwarzFxTuningWindow.Open();

        [UnityEditor.MenuItem("ArknightsACT/战斗调试/P1 状态调试器", false, 70)]
        private static void OpenP1CombatDebugger() => P1CombatStatusDebuggerWindow.Open();
    }
}
#endif
