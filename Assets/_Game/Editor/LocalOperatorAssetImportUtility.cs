#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArknightsACT.Editor.Effects;
using ArknightsACT.Editor.PRTS;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Shared local-unpack import helpers for playable operators.
    /// Character bootstraps should describe what they need; this class owns the repetitive
    /// file-copy, Spine presentation and incremental extracted-FX work.
    /// </summary>
    internal static class LocalOperatorAssetImportUtility
    {
        private const string SourceRootPreference = "ArknightsACT.LocalAssets.UnpackedRoot";
        private const string DefaultSourceRoot = @"D:\Ark\_Unpacked";

        internal static string UnpackedRoot
        {
            get => EditorPrefs.GetString(SourceRootPreference, DefaultSourceRoot);
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? DefaultSourceRoot
                    : Path.GetFullPath(value.Trim());
                EditorPrefs.SetString(SourceRootPreference, normalized);
            }
        }

        internal static string GetCharacterSourceRoot(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                throw new ArgumentException("Character source folder is required.", nameof(folderName));

            return Path.Combine(UnpackedRoot, folderName.Trim());
        }

        internal static bool EnsureCharacterSource(string folderName, string label, out string sourceRoot)
        {
            sourceRoot = GetCharacterSourceRoot(folderName);
            if (Directory.Exists(sourceRoot))
                return true;

            Debug.LogWarning(
                $"[ArknightsACT/LocalImport] {label} local unpack is missing: {sourceRoot}. " +
                $"Current unpack root: {UnpackedRoot}");
            return false;
        }

        internal static bool CopyRaw(string source, string assetPath, bool force, bool warnIfMissing = true)
        {
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                if (warnIfMissing)
                    Debug.LogWarning("[ArknightsACT/LocalImport] Missing local asset: " + source);
                return false;
            }

            var target = ToAbsoluteProjectPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? Application.dataPath);

            var sourceInfo = new FileInfo(source);
            var targetInfo = new FileInfo(target);
            var needsCopy =
                force ||
                !targetInfo.Exists ||
                sourceInfo.Length != targetInfo.Length ||
                sourceInfo.LastWriteTimeUtc != targetInfo.LastWriteTimeUtc;

            if (!needsCopy)
                return false;

            File.Copy(source, target, true);
            File.SetLastWriteTimeUtc(target, sourceInfo.LastWriteTimeUtc);
            return true;
        }

        internal static bool CopySprite(string source, string assetPath, bool force)
        {
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                Debug.LogWarning("[ArknightsACT/LocalImport] Missing local sprite: " + source);
                return false;
            }

            var changed = CopyRaw(source, assetPath, force);
            if (changed || AssetImporter.GetAtPath(assetPath) == null)
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return changed;

            var importerChanged =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !importer.alphaIsTransparency ||
                importer.mipmapEnabled ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.maxTextureSize != 2048;

            if (importerChanged)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            return changed || importerChanged;
        }

        internal static bool CopySpineSet(
            string sourceSpineDirectory,
            PrtsAssetDescriptor descriptor,
            bool force)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            var atlasSource = Path.Combine(sourceSpineDirectory, descriptor.BaseName + ".atlas");
            var skeletonSource = Path.Combine(sourceSpineDirectory, descriptor.BaseName + ".skel");
            if (!File.Exists(atlasSource) || !File.Exists(skeletonSource))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/LocalImport] Missing Spine pair for {descriptor.DisplayName}: " +
                    $"{atlasSource} / {skeletonSource}");
                return false;
            }

            var changed = false;
            changed |= CopyRaw(
                atlasSource,
                descriptor.TargetDirectory + "/" + descriptor.BaseName + ".atlas.txt",
                force);
            changed |= CopyRaw(
                skeletonSource,
                descriptor.TargetDirectory + "/" + descriptor.BaseName + ".skel.bytes",
                force);

            foreach (var page in File.ReadAllLines(atlasSource)
                         .Select(line => line.Trim())
                         .Where(line => line.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                                        line.IndexOf(':') < 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var textureAssetPath =
                    descriptor.TargetDirectory + "/" + Path.GetFileName(page);
                changed |= CopyRaw(
                    Path.Combine(sourceSpineDirectory, Path.GetFileName(page)),
                    textureAssetPath,
                    force);

                // Keep locally exported Spine pages aligned with the validated PRTS import
                // settings. Unity's default TextureImporter path otherwise produces dark/dirty
                // transparent edges (linear texture + Repeat + alpha transparency disabled).
                changed |= ConfigureSpineTexture(textureAssetPath);
            }

            return changed;
        }

        internal static bool CopyCombatSpineSetWithHighResolutionMotionAtlas(
            string sourceSpineDirectory,
            PrtsAssetDescriptor combatDescriptor,
            PrtsAssetDescriptor motionDescriptor,
            bool force)
        {
            if (combatDescriptor == null)
                return false;
            if (motionDescriptor == null)
                return CopySpineSet(sourceSpineDirectory, combatDescriptor, force);

            var combatAtlasPath =
                Path.Combine(sourceSpineDirectory, combatDescriptor.BaseName + ".atlas");
            var combatSkeletonPath =
                Path.Combine(sourceSpineDirectory, combatDescriptor.BaseName + ".skel");
            var motionAtlasPath =
                Path.Combine(sourceSpineDirectory, motionDescriptor.BaseName + ".atlas");

            if (!File.Exists(combatAtlasPath) ||
                !File.Exists(combatSkeletonPath) ||
                !File.Exists(motionAtlasPath))
                return CopySpineSet(sourceSpineDirectory, combatDescriptor, force);

            var combatAtlas = File.ReadAllText(combatAtlasPath);
            var motionAtlas = File.ReadAllText(motionAtlasPath);
            var combatRegions = GetAtlasRegionNames(combatAtlas);
            var motionRegions = GetAtlasRegionNames(motionAtlas);

            if (combatRegions.Count == 0 || motionRegions.Count == 0)
                return CopySpineSet(sourceSpineDirectory, combatDescriptor, force);

            var shared = 0;
            foreach (var region in combatRegions)
                if (motionRegions.Contains(region))
                    shared++;

            var coverage = (float)shared / combatRegions.Count;
            var combatArea = GetLargestAtlasPageArea(combatAtlas);
            var motionArea = GetLargestAtlasPageArea(motionAtlas);

            // Base/build skeletons are sometimes authored from a higher-resolution version of
            // exactly the same character art. When at least 90% of battle attachment names match
            // and the source page is materially larger, place that atlas first and keep the battle
            // atlas as a fallback page. Spine Atlas.FindRegion returns the first matching region,
            // so combat animations keep their original .skel while common face/body/weapon regions
            // transparently use the sharper source. Missing battle-only regions still resolve from
            // the second page.
            if (coverage < 0.90f ||
                motionArea <= combatArea * 1.20f)
                return CopySpineSet(sourceSpineDirectory, combatDescriptor, force);

            var changed = false;
            changed |= CopyRaw(
                combatSkeletonPath,
                combatDescriptor.TargetDirectory + "/" +
                combatDescriptor.BaseName + ".skel.bytes",
                force);

            foreach (var page in ExtractAtlasPageNames(motionAtlas)
                         .Concat(ExtractAtlasPageNames(combatAtlas))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var pageAssetPath =
                    combatDescriptor.TargetDirectory + "/" + Path.GetFileName(page);
                changed |= CopyRaw(
                    Path.Combine(sourceSpineDirectory, Path.GetFileName(page)),
                    pageAssetPath,
                    force);
                changed |= ConfigureSpineTexture(pageAssetPath);
            }

            var mergedAtlas =
                motionAtlas.Trim() + Environment.NewLine + Environment.NewLine +
                combatAtlas.Trim() + Environment.NewLine;
            var targetAtlasAssetPath =
                combatDescriptor.TargetDirectory + "/" +
                combatDescriptor.BaseName + ".atlas.txt";
            var targetAtlasPath = ToAbsoluteProjectPath(targetAtlasAssetPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(targetAtlasPath) ?? Application.dataPath);

            var currentAtlas = File.Exists(targetAtlasPath)
                ? File.ReadAllText(targetAtlasPath)
                : string.Empty;
            if (force || !string.Equals(
                    NormalizeLineEndings(currentAtlas),
                    NormalizeLineEndings(mergedAtlas),
                    StringComparison.Ordinal))
            {
                File.WriteAllText(
                    targetAtlasPath,
                    mergedAtlas,
                    new UTF8Encoding(false));
                changed = true;
            }

            Debug.Log(
                $"[ArknightsACT/LocalImport] {combatDescriptor.DisplayName}: " +
                $"using high-resolution motion atlas first " +
                $"({shared}/{combatRegions.Count} battle regions, {coverage:P0} coverage; " +
                $"page area {combatArea} -> {motionArea}).");

            return changed;
        }

        private static HashSet<string> GetAtlasRegionNames(string atlasText)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(atlasText))
                return result;

            var lines = NormalizeLineEndings(atlasText).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var value = raw.Trim();
                if (string.IsNullOrWhiteSpace(value) ||
                    char.IsWhiteSpace(raw[0]) ||
                    value.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    value.IndexOf(':') >= 0)
                    continue;

                result.Add(value);
            }

            return result;
        }

        private static IEnumerable<string> ExtractAtlasPageNames(string atlasText)
        {
            if (string.IsNullOrWhiteSpace(atlasText))
                yield break;

            var lines = NormalizeLineEndings(atlasText).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var value = raw.Trim();
                if (string.IsNullOrWhiteSpace(value) ||
                    char.IsWhiteSpace(raw[0]) ||
                    !value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return value;
            }
        }

        private static long GetLargestAtlasPageArea(string atlasText)
        {
            if (string.IsNullOrWhiteSpace(atlasText))
                return 0;

            long largest = 0;
            var lines = NormalizeLineEndings(atlasText).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var value = lines[i].Trim();
                if (!value.StartsWith("size:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = value.Substring(5).Split(',');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0].Trim(), out var width) ||
                    !int.TryParse(parts[1].Trim(), out var height))
                    continue;

                largest = Math.Max(largest, (long)width * height);
            }

            return largest;
        }

        private static string NormalizeLineEndings(string value) =>
            (value ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

        private static bool ConfigureSpineTexture(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            if (AssetImporter.GetAtPath(assetPath) == null)
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return false;

            var changed =
                importer.textureType != TextureImporterType.Default ||
                !importer.sRGBTexture ||
                !importer.alphaIsTransparency ||
                importer.mipmapEnabled ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.maxTextureSize != 8192;

            if (!changed)
                return false;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
            return true;
        }

        internal static bool BuildPresentation(PrtsAssetDescriptor descriptor, bool force, bool sourceChanged)
        {
            if (descriptor == null)
                return false;

            // ArkMod exporter merges the game's separate <texture>[alpha] mask into an ordinary
            // RGBA PNG without premultiplying RGB. The resulting atlas pages are straight-alpha.
            // Spine's default Skeleton material is PMA, so local packages must explicitly enable
            // Straight Alpha Texture or tiny translucent parts (eyes, eyelashes, hair edges) get
            // bright/dirty halos. Always repair existing generated materials as well, even when
            // the presentation prefab itself does not need rebuilding.
            ConfigureStraightAlphaMaterials(descriptor);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrtsSpinePrefabBuilder.GetPrefabPath(descriptor.PrefabKey));
            if (!force && !sourceChanged && existing != null)
                return false;

            var skeletonType = FindType("Spine.Unity.SkeletonAnimation");
            if (skeletonType == null)
            {
                Debug.LogWarning(
                    $"[ArknightsACT/LocalImport] Spine runtime unavailable; cannot build {descriptor.DisplayName}.");
                return false;
            }

            if (!PrtsSpinePrefabBuilder.TryBuild(descriptor, skeletonType, out var error))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/LocalImport] Could not build {descriptor.DisplayName}: {error}");
                return false;
            }

            // TryBuild/Spine import may create or recreate materials, so enforce the local-package
            // alpha convention once more after generation.
            ConfigureStraightAlphaMaterials(descriptor);
            return true;
        }

        internal static bool ConfigureStraightAlphaMaterials(PrtsAssetDescriptor descriptor)
        {
            if (descriptor == null ||
                string.IsNullOrWhiteSpace(descriptor.TargetDirectory) ||
                !AssetDatabase.IsValidFolder(descriptor.TargetDirectory))
                return false;

            var changed = false;
            var guids = AssetDatabase.FindAssets(
                "t:Material",
                new[] { descriptor.TargetDirectory });

            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null || !material.HasProperty("_StraightAlphaInput"))
                    continue;

                var needsFloat = material.GetFloat("_StraightAlphaInput") < 0.5f;
                var needsKeyword = !material.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT");
                if (!needsFloat && !needsKeyword)
                    continue;

                material.SetFloat("_StraightAlphaInput", 1f);
                material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                EditorUtility.SetDirty(material);
                changed = true;
            }

            return changed;
        }

        internal static ExtractedFrameFxImporter.ImportResult ImportFxTags(
            string sourceRoot,
            string packageName,
            IEnumerable<string> tags,
            bool force)
        {
            if (tags == null)
                throw new ArgumentNullException(nameof(tags));

            var framesRoot = ExtractedFrameFxImporter.ResolveFramesRoot(sourceRoot);
            if (string.IsNullOrWhiteSpace(framesRoot))
                throw new InvalidOperationException(
                    $"FX frames root was not found under '{sourceRoot}'.");

            var requested = tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var missing = new List<string>();
            var changedFolders = new List<string>();
            var packageRoot =
                ExtractedFrameFxImporter.DefaultOutputRoot + "/" + SanitizePackageName(packageName);

            foreach (var tag in requested)
            {
                var sourceFolder = Path.Combine(framesRoot, tag);
                if (!Directory.Exists(sourceFolder) || !HasAnimationFrames(sourceFolder))
                {
                    missing.Add(tag);
                    continue;
                }

                if (force || NeedsFxImport(sourceFolder, packageRoot, tag))
                    changedFolders.Add(sourceFolder);
            }

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    $"[ArknightsACT/LocalImport] {packageName} missing FX tags: " +
                    string.Join(", ", missing));
            }

            if (changedFolders.Count == 0)
            {
                return new ExtractedFrameFxImporter.ImportResult
                {
                    FramesRoot = framesRoot,
                    PackageRoot = packageRoot
                };
            }

            var result = ExtractedFrameFxImporter.ImportSelected(
                changedFolders,
                ExtractedFrameFxImporter.DefaultOutputRoot,
                packageName);

            Debug.Log(
                $"[ArknightsACT/LocalImport] {packageName} incremental FX import: " +
                $"{changedFolders.Count}/{requested.Length} groups changed, " +
                $"{result.ImportedEffects} rebuilt, {result.ImportedFrames} frames copied.");

            return result;
        }

        internal static int PruneGeneratedFx(
            string packageName,
            IEnumerable<string> keepTags)
        {
            var keep = new HashSet<string>(
                (keepTags ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var packageRoot =
                ExtractedFrameFxImporter.DefaultOutputRoot + "/" +
                SanitizePackageName(packageName);
            var removed = 0;

            foreach (var assetFolder in new[] { "Prefabs", "Animations", "Controllers" })
            {
                var folder = packageRoot + "/" + assetFolder;
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                var guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
                for (var i = 0; i < guids.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (AssetDatabase.IsValidFolder(assetPath))
                        continue;

                    var name = Path.GetFileNameWithoutExtension(assetPath);
                    if (keep.Contains(name))
                        continue;

                    if (AssetDatabase.DeleteAsset(assetPath))
                        removed++;
                }
            }

            var framesRoot = packageRoot + "/Frames";
            if (AssetDatabase.IsValidFolder(framesRoot))
            {
                var subFolders = AssetDatabase.GetSubFolders(framesRoot);
                for (var i = 0; i < subFolders.Length; i++)
                {
                    var folder = subFolders[i];
                    var name = Path.GetFileName(folder);
                    if (keep.Contains(name))
                        continue;

                    if (AssetDatabase.DeleteAsset(folder))
                        removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log(
                    $"[ArknightsACT/LocalImport] {packageName}: pruned {removed} stale generated FX assets/groups.");
            }

            return removed;
        }

        internal static GameObject LoadFx(string packageName, string tag)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                ExtractedFrameFxImporter.DefaultOutputRoot + "/" +
                SanitizePackageName(packageName) + "/Prefabs/" + tag + ".prefab");
        }

        internal static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, (assetPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool NeedsFxImport(string sourceFolder, string packageRoot, string tag)
        {
            var prefabPath = packageRoot + "/Prefabs/" + tag + ".prefab";
            var clipPath = packageRoot + "/Animations/" + tag + ".anim";
            var controllerPath = packageRoot + "/Controllers/" + tag + ".controller";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null ||
                clip == null ||
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) == null)
                return true;

            var sourceFps = ExtractedFrameFxImporter.ReadSourceFps(sourceFolder);
            if (Mathf.Abs(clip.frameRate - sourceFps) > 0.01f)
                return true;

            var targetFrames = ToAbsoluteProjectPath(packageRoot + "/Frames/" + tag);
            if (!Directory.Exists(targetFrames))
                return true;

            var sourceFiles = Directory.GetFiles(sourceFolder, "*.png", SearchOption.TopDirectoryOnly)
                .Where(IsAnimationFrameFile)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var targetFiles = Directory.GetFiles(targetFrames, "*.png", SearchOption.TopDirectoryOnly)
                .Where(IsAnimationFrameFile)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (sourceFiles.Length == 0 || sourceFiles.Length != targetFiles.Length)
                return true;

            for (var i = 0; i < sourceFiles.Length; i++)
            {
                var source = new FileInfo(sourceFiles[i]);
                var target = new FileInfo(targetFiles[i]);
                if (!string.Equals(source.Name, target.Name, StringComparison.OrdinalIgnoreCase) ||
                    source.Length != target.Length ||
                    source.LastWriteTimeUtc > target.LastWriteTimeUtc)
                    return true;
            }

            return false;
        }

        private static bool HasAnimationFrames(string folder) =>
            Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
                .Any(IsAnimationFrameFile);

        private static bool IsAnimationFrameFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return false;

            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name) ||
                name.Length < 2 ||
                (name[0] != 'f' && name[0] != 'F'))
                return false;

            for (var i = 1; i < name.Length; i++)
                if (!char.IsDigit(name[i]))
                    return false;

            return true;
        }

        private static string SanitizePackageName(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "Imported" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid.ToString(), string.Empty);
            return string.IsNullOrWhiteSpace(value) ? "Imported" : value;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
#endif
