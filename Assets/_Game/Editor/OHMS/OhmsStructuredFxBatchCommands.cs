#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.OHMS
{
    /// <summary>
    /// Rebuilds the checked-in Ch'en FX package from the last staged OHMS export.
    /// This is exposed as a menu command so the importer can be rerun without
    /// relying on a window-local Refresh button.
    /// </summary>
    public static class OhmsStructuredFxBatchCommands
    {
        [MenuItem("ArknightsACT/OHMS/Import staged Ch'en combat FX")]
        public static void ImportStagedChenCombatFx()
        {
            var sourceRoot = Environment.GetEnvironmentVariable("OHMS_SOURCE_ROOT");
            if (string.IsNullOrWhiteSpace(sourceRoot))
                sourceRoot = Path.GetFullPath("Library/ArknightsACT/OHMS/Normalized");
            else
                sourceRoot = Path.GetFullPath(sourceRoot);
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException($"Staged OHMS export was not found: {sourceRoot}");

            const string outputRoot = OhmsStructuredFxImporter.DefaultOutputRoot;
            const string packageName = "Chen";
            var packageRoot = $"{outputRoot}/{packageName}";
            if (AssetDatabase.IsValidFolder(packageRoot))
            {
                AssetDatabase.DeleteAsset(packageRoot);
                AssetDatabase.Refresh();
            }

            var report = OhmsStructuredFxImporter.Import(new OhmsStructuredFxImporter.ImportOptions
            {
                SourceRoot = sourceRoot,
                OutputRoot = outputRoot,
                PackageName = packageName,
                IncludeTokens = "chen_skill_02_,chen_skill_03_,chen_attack_01_",
                ExcludeTokens = "_sale#10,_nian#2,chen_skill_03_start_03"
            });
            Debug.Log($"[ArknightsACT/OHMS] Staged Ch'en combat FX import finished. {report.ToSummary()}");
        }
    }
}
#endif
