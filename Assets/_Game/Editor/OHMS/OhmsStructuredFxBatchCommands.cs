#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        // Bump this when the reconstruction contract changes in a way that requires the checked-in
        // Ch'en prefabs to be refreshed.  On the developer machine that still has the staged OHMS
        // export, Unity performs the refresh once after the scripts compile.  Stable asset paths in
        // OhmsStructuredFxImporter preserve prefab/material/mesh GUIDs during that refresh.
        private const int ChenReconstructionRevision = 4;

        [UnityEditor.MenuItem("ArknightsACT/OHMS/Effect Importer")]
        private static void OpenEffectImporter()
        {
            OhmsStructuredFxImporterWindow.Open();
        }

        [InitializeOnLoadMethod]
        private static void ScheduleChenReconstructionRefresh()
        {
            if (Application.isBatchMode)
                return;
            EditorApplication.delayCall += TryRefreshChenReconstructionOnce;
        }

        private static void TryRefreshChenReconstructionOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var sourceRoot = ResolveChenSourceRoot();
            if (!HasStructuredExport(sourceRoot))
                return;

            var projectKey = Application.dataPath.Replace('\\', '_').Replace('/', '_').Replace(':', '_');
            var revisionKey = $"ArknightsACT.OHMS.ChenReconstructionRevision.{projectKey}";
            if (EditorPrefs.GetInt(revisionKey, 0) >= ChenReconstructionRevision)
                return;

            try
            {
                ImportStagedChenCombatFx();
                EditorPrefs.SetInt(revisionKey, ChenReconstructionRevision);
                Debug.Log($"[ArknightsACT/OHMS] Applied Ch'en FX reconstruction revision {ChenReconstructionRevision}.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [UnityEditor.MenuItem("ArknightsACT/OHMS/Import staged Ch'en combat FX")]
        public static void ImportStagedChenCombatFx()
        {
            var sourceRoot = ResolveChenSourceRoot();
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException($"Staged OHMS export was not found: {sourceRoot}");

            const string outputRoot = OhmsStructuredFxImporter.DefaultOutputRoot;
            const string packageName = "Chen";
            var packageRoot = $"{outputRoot}/{packageName}";

            var report = OhmsStructuredFxImporter.Import(new OhmsStructuredFxImporter.ImportOptions
            {
                SourceRoot = sourceRoot,
                OutputRoot = outputRoot,
                PackageName = packageName,
                IncludeTokens = "chen_skill_02_,chen_skill_03_,chen_attack_01_",
                ExcludeTokens = "_sale#10,_nian#2,chen_skill_03_start_03"
            });
            BindChenBladeFallbacks(packageRoot);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArknightsACT/OHMS] Staged Ch'en combat FX import finished. {report.ToSummary()}");
        }

        [UnityEditor.MenuItem("ArknightsACT/OHMS/Repair Ch'en skill 3 blade textures")]
        public static void RepairChenSkill3BladeTextures()
        {
            var packageRoot = string.Concat(OhmsStructuredFxImporter.DefaultOutputRoot, "/Chen");
            BindChenBladeFallbacks(packageRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArknightsACT/OHMS] Ch'en skill 3 blade texture repair finished.");
        }

        private static string ResolveChenSourceRoot()
        {
            var sourceRoot = Environment.GetEnvironmentVariable("OHMS_SOURCE_ROOT");
            return string.IsNullOrWhiteSpace(sourceRoot)
                ? Path.GetFullPath("Library/ArknightsACT/OHMS/Normalized")
                : Path.GetFullPath(sourceRoot);
        }

        private static bool HasStructuredExport(string sourceRoot)
        {
            if (!Directory.Exists(sourceRoot))
                return false;

            return File.Exists(Path.Combine(sourceRoot, "assets.json")) &&
                   Directory.Exists(Path.Combine(sourceRoot, "things"));
        }

        /// <summary>
        /// Resolve Ch'en's external blade textures after import.  The original chen.ab bundle
        /// keeps the two blade materials in the bundle but stores their Texture2D payloads in
        /// refs/fx/texture/trail.ab and refs/fx/texture/flow.ab.  AssetStudio exports these as
        /// external FileID pointers, so a chen.ab-only import otherwise falls back to the red
        /// diagnostic texture.  The three exact textures are staged in this package and rebound
        /// here by the source material contract.
        /// </summary>
        internal static void BindChenBladeFallbacks(string packageRoot)
        {
            var blade = AssetDatabase.LoadAssetAtPath<Material>(
                $"{packageRoot}/Materials/chen_skill_03_hit_1_5065494637834020258.mat");
            var bladeHighlight = AssetDatabase.LoadAssetAtPath<Material>(
                $"{packageRoot}/Materials/trail_51_C_add_1154816863944289969.mat");
            var fallbackBlade = AssetDatabase.LoadAssetAtPath<Material>(
                $"{packageRoot}/Materials/daofeng_01_add_6470594128073098110.mat");
            var missing = AssetDatabase.LoadAssetAtPath<Material>(
                $"{packageRoot}/Materials/OHMS_MissingExternalMaterial.mat");
            var trail52 = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{packageRoot}/Textures/trail_52_C_-8341173122533851548.png");
            var trail51 = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{packageRoot}/Textures/trail_51_C_-4617581226512930796.png");
            var flow02 = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{packageRoot}/Textures/flow_02_ab_02_1329491080043089029.png");
            if (blade == null || bladeHighlight == null || fallbackBlade == null || missing == null ||
                trail52 == null || trail51 == null || flow02 == null)
            {
                Debug.LogWarning(
                    $"[ArknightsACT/OHMS] Could not bind Ch'en blade textures. " +
                    $"blade={blade != null}, highlight={bladeHighlight != null}, fallback={fallbackBlade != null}, " +
                    $"missing={missing != null}, trail52={trail52 != null}, trail51={trail51 != null}, flow02={flow02 != null}");
                return;
            }

            if (blade.HasProperty("_MainTex"))
                blade.SetTexture("_MainTex", trail52);
            if (blade.HasProperty("_DissolveTex"))
                blade.SetTexture("_DissolveTex", flow02);
            if (bladeHighlight.HasProperty("_MainTex"))
                bladeHighlight.SetTexture("_MainTex", trail51);
            if (bladeHighlight.HasProperty("_DissolveTex"))
                bladeHighlight.SetTexture("_DissolveTex", null);
            EditorUtility.SetDirty(blade);
            EditorUtility.SetDirty(bladeHighlight);

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { $"{packageRoot}/Prefabs" });
            var slashNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "rotation_y",
                "rotation_z",
                "fixed",
                "static_offset"
            };
            var changed = 0;
            foreach (var prefabGuid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                var isAttackPrefab = prefabName.Equals("chen_attack_01_start", StringComparison.OrdinalIgnoreCase) ||
                                      prefabName.Equals("chen_attack_01_hit", StringComparison.OrdinalIgnoreCase);
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var prefabChanged = false;
                try
                {
                    foreach (var renderer in contents.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer == null)
                            continue;
                        var rendererName = renderer.gameObject.name;
                        var isSkill3Hit = prefabName.StartsWith("chen_skill_03_hit_", StringComparison.OrdinalIgnoreCase);
                        var isBlade = rendererName.StartsWith("daoguang", StringComparison.OrdinalIgnoreCase);
                        var isBladeHighlight = isBlade && rendererName.IndexOf("liang", StringComparison.OrdinalIgnoreCase) >= 0;
                        var useBlade = isSkill3Hit && isBlade;
                        var useFallback = !useBlade && (isAttackPrefab || slashNodes.Contains(rendererName));
                        if (!useBlade && !useFallback)
                            continue;

                        var materials = renderer.sharedMaterials;
                        if (materials == null || materials.Length == 0)
                            continue;
                        var replaced = false;
                        for (var i = 0; i < materials.Length; i++)
                        {
                            if (useBlade)
                            {
                                var expected = isBladeHighlight ? bladeHighlight : blade;
                                if (materials[i] == expected)
                                    continue;
                                materials[i] = expected;
                                replaced = true;
                                continue;
                            }
                            if (materials[i] != missing)
                                continue;
                            materials[i] = fallbackBlade;
                            replaced = true;
                        }
                        if (!replaced)
                            continue;
                        renderer.sharedMaterials = materials;
                        changed++;
                        prefabChanged = true;
                    }
                    if (prefabChanged)
                        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log($"[ArknightsACT/OHMS] Bound original Ch'en skill-3 blade textures and fallback attack material on {changed} renderer(s).");
        }
    }
}
#endif
