#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// One-shot sync for the 2026-09-24 Wisadel re-export. This migration is intentionally
    /// Wisadel-only: the new package changed FX metadata/binding semantics and newly referenced
    /// trail/composite prefabs must exist before the runtime controller can use them.
    /// </summary>
    [InitializeOnLoad]
    internal static class WisadelReexportImportMigration20260924
    {
        private const string MigrationId = "2026-09-24-wisadel-basemotion-fullsource-v5";
        private const string CompletedKey =
            "ArknightsACT.WisadelReexportMigration.Completed.20260924.v5";
        private static bool _scheduled;

        static WisadelReexportImportMigration20260924()
        {
            if (string.Equals(
                    EditorPrefs.GetString(CompletedKey, string.Empty),
                    MigrationId,
                    StringComparison.Ordinal))
                return;

            Schedule();
        }

        private static void Schedule()
        {
            if (_scheduled)
                return;

            _scheduled = true;
            EditorApplication.update -= TryRun;
            EditorApplication.update += TryRun;
        }

        private static void TryRun()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= TryRun;
            _scheduled = false;

            var sourceRoot =
                LocalOperatorAssetImportUtility.GetCharacterSourceRoot("wisdel");
            var manifest = Path.Combine(sourceRoot, "unity_import_manifest.json");
            if (!Directory.Exists(sourceRoot) || !File.Exists(manifest))
            {
                Debug.LogWarning(
                    "[ArknightsACT/Wisadel] Re-export sync skipped: " +
                    "wisdel/unity_import_manifest.json is missing.");
                return;
            }

            try
            {
                // This package was freshly re-exported. Rebuild once even when destination
                // names already exist so old frame sequences/controllers cannot survive.
                WisadelLocalAssetBootstrap.ImportOriginalAndGame9(
                    force: true,
                    showDialog: false);
                AssetDatabase.SaveAssets();
                EditorPrefs.SetString(CompletedKey, MigrationId);
                Debug.Log(
                    "[ArknightsACT/Wisadel] Re-export sync completed; " +
                    "BaseMotion full-source presentation, vfx bindings and battle audio are refreshed.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
#endif
