#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// One-shot migration requested while introducing the shared local-operator importer.
    /// It imports the two validation packages once, then stays dormant on future reloads.
    /// </summary>
    [InitializeOnLoad]
    internal static class LocalOperatorImportMigration20260924
    {
        private const string MigrationId = "2026-09-24-wisadel-skadi-v3-render-fix";
        private const string CompletedKey =
            "ArknightsACT.LocalImportMigration.Completed.20260924";
        private static bool _scheduled;

        static LocalOperatorImportMigration20260924()
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

            var wisadelRoot =
                LocalOperatorAssetImportUtility.GetCharacterSourceRoot("wisdel");
            var skadiRoot =
                LocalOperatorAssetImportUtility.GetCharacterSourceRoot("skadi");
            if (!Directory.Exists(wisadelRoot) || !Directory.Exists(skadiRoot))
            {
                Debug.LogWarning(
                    "[ArknightsACT/LocalImport] One-shot Wisadel/Skadi import skipped because " +
                    "one or both unpack folders are missing. Set the unpack root from the " +
                    "ArknightsACT/角色资源 menu and use the explicit import commands.");
                return;
            }

            try
            {
                WisadelLocalAssetBootstrap.ImportOriginalAndGame9(
                    force: false,
                    showDialog: false);
                SkadiLocalAssetBootstrap.ImportAll(
                    force: false,
                    showDialog: false);
                new SkadiPrototypeOperatorBuilder().EnsureDefinitionAsset();
                AssetDatabase.SaveAssets();

                EditorPrefs.SetString(CompletedKey, MigrationId);
                Debug.Log(
                    "[ArknightsACT/LocalImport] One-shot Wisadel + Skadi presentation import completed.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
#endif
