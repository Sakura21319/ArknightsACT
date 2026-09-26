#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArknightsACT.Editor.PRTS;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class LocalOperatorPackageSkin
    {
        internal string SkinId;
        internal string CombatBaseName;
        internal string MotionBaseName;
        internal string AvatarRelativePath;
    }

    internal sealed class LocalOperatorPackageScan
    {
        internal string SourceFolder;
        internal string SourceRoot;
        internal string CharacterCode;
        internal string CharacterId;
        internal string DisplayName;
        internal readonly List<LocalOperatorPackageSkin> Skins = new();
        internal readonly List<string> IconRelativePaths = new();
        internal readonly List<string> FxTags = new();
        internal string VoiceRelativePath;
    }

    internal static class LocalOperatorPackageScanner
    {
        [Serializable]
        private sealed class ManifestHeader
        {
            public string @char;
            public string charId;
            public string displayName;
        }

        internal static LocalOperatorPackageScan Scan(string sourceFolder)
        {
            if (!LocalOperatorAssetImportUtility.EnsureCharacterSource(
                    sourceFolder,
                    sourceFolder,
                    out var sourceRoot))
                return null;

            var result = new LocalOperatorPackageScan
            {
                SourceFolder = sourceFolder,
                SourceRoot = sourceRoot,
                CharacterCode = sourceFolder,
                DisplayName = sourceFolder
            };

            ReadManifestHeader(result);

            var spineRoot = Path.Combine(sourceRoot, "spine");
            if (!Directory.Exists(spineRoot))
                throw new InvalidOperationException("Spine folder is missing: " + spineRoot);

            var allAtlas = Directory.GetFiles(spineRoot, "*.atlas", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (string.IsNullOrWhiteSpace(result.CharacterId))
            {
                result.CharacterId = allAtlas
                    .Where(x => x.StartsWith("char_", StringComparison.OrdinalIgnoreCase))
                    .Where(x => !x.StartsWith("build_", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Length)
                    .FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(result.CharacterId))
                throw new InvalidOperationException(
                    "Could not determine charId from unity_import_manifest.json or spine/*.atlas.");

            var combatBases = allAtlas
                .Where(x =>
                    string.Equals(x, result.CharacterId, StringComparison.OrdinalIgnoreCase) ||
                    x.StartsWith(result.CharacterId + "_", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            for (var i = 0; i < combatBases.Length; i++)
            {
                var combat = combatBases[i];
                var suffix = combat.Length == result.CharacterId.Length
                    ? string.Empty
                    : combat.Substring(result.CharacterId.Length + 1);
                var skinId = string.IsNullOrWhiteSpace(suffix) ? "default" : suffix;
                var motion = "build_" + combat;
                var avatarRelative = "ui_assets/avatars/" + combat + ".png";

                result.Skins.Add(new LocalOperatorPackageSkin
                {
                    SkinId = skinId,
                    CombatBaseName = combat,
                    MotionBaseName = allAtlas.Any(x =>
                        string.Equals(x, motion, StringComparison.OrdinalIgnoreCase))
                        ? motion
                        : string.Empty,
                    AvatarRelativePath = File.Exists(Path.Combine(
                        sourceRoot,
                        avatarRelative.Replace('/', Path.DirectorySeparatorChar)))
                        ? avatarRelative
                        : string.Empty
                });
            }

            if (result.Skins.Count == 0)
                throw new InvalidOperationException(
                    $"No combat Spine sets matching '{result.CharacterId}' were found.");

            var iconRoot = Path.Combine(sourceRoot, "icons");
            if (Directory.Exists(iconRoot))
            {
                result.IconRelativePaths.AddRange(
                    Directory.GetFiles(iconRoot, "*.png", SearchOption.TopDirectoryOnly)
                        .Select(path => "icons/" + Path.GetFileName(path))
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            }

            var framesRoot = Path.Combine(sourceRoot, "effects", "frames");
            if (Directory.Exists(framesRoot))
            {
                foreach (var directory in Directory.GetDirectories(framesRoot)
                             .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    if (HasNumberedFrame(directory))
                        result.FxTags.Add(Path.GetFileName(directory));
                }
            }

            var voice = Path.Combine(sourceRoot, "voice", "voice");
            if (Directory.Exists(voice))
                result.VoiceRelativePath = "voice/voice";

            return result;
        }

        internal static LocalOperatorImportPlan CreateImportPlan(
            LocalOperatorPackageScan scan,
            string packageName,
            IEnumerable<string> selectedSkinIds,
            float targetWorldHeight,
            float feetLocalY,
            float safeInitialScale)
        {
            if (scan == null)
                throw new ArgumentNullException(nameof(scan));

            var selected = new HashSet<string>(
                selectedSkinIds ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            var safePackage = SafeIdentifier(
                string.IsNullOrWhiteSpace(packageName)
                    ? scan.CharacterCode
                    : packageName,
                "Operator");

            var commonSprites = scan.IconRelativePaths
                .Select(relative => new LocalOperatorSpriteImport(
                    relative,
                    $"Assets/_Game/Resources/UI/Skills/{safePackage}/Raw/{Path.GetFileName(relative)}"))
                .ToArray();

            var skins = new List<LocalOperatorSkinImportPlan>();
            for (var i = 0; i < scan.Skins.Count; i++)
            {
                var sourceSkin = scan.Skins[i];
                if (sourceSkin == null || !selected.Contains(sourceSkin.SkinId))
                    continue;

                var safeSkin = SafeIdentifier(sourceSkin.SkinId, "default");
                var characterRoot =
                    $"Assets/_Game/Art/Characters/{safePackage}/Local/{safeSkin}";
                var prefabPrefix =
                    SafeIdentifier(scan.CharacterCode, "operator").ToLowerInvariant() +
                    "_" + safeSkin.ToLowerInvariant();

                var combat = new PrtsAssetDescriptor(
                    $"{scan.DisplayName}·{sourceSkin.SkinId}",
                    "local",
                    string.Empty,
                    sourceSkin.CombatBaseName,
                    characterRoot + "/Spine",
                    "Player",
                    targetWorldHeight,
                    feetLocalY,
                    safeInitialScale,
                    prefabPrefix);

                PrtsAssetDescriptor motion = null;
                if (!string.IsNullOrWhiteSpace(sourceSkin.MotionBaseName))
                {
                    motion = new PrtsAssetDescriptor(
                        $"{scan.DisplayName}·{sourceSkin.SkinId} 动作源",
                        "local",
                        string.Empty,
                        sourceSkin.MotionBaseName,
                        characterRoot + "/BaseMotion",
                        "MotionSource",
                        targetWorldHeight,
                        feetLocalY,
                        safeInitialScale,
                        prefabPrefix + "_motion");
                }

                var sprites = string.IsNullOrWhiteSpace(sourceSkin.AvatarRelativePath)
                    ? Array.Empty<LocalOperatorSpriteImport>()
                    : new[]
                    {
                        new LocalOperatorSpriteImport(
                            sourceSkin.AvatarRelativePath,
                            $"Assets/_Game/Resources/UI/HUD/Operators/" +
                            $"{SafeIdentifier(scan.CharacterCode, "operator").ToLowerInvariant()}_" +
                            $"{safeSkin.ToLowerInvariant()}.png")
                    };

                skins.Add(new LocalOperatorSkinImportPlan(
                    sourceSkin.SkinId,
                    combat,
                    motion,
                    sprites,
                    FxTagsForSkin(scan, sourceSkin.SkinId)));
            }

            return new LocalOperatorImportPlan(
                scan.DisplayName,
                scan.SourceFolder,
                safePackage,
                commonSprites,
                skins,
                scan.VoiceRelativePath,
                string.IsNullOrWhiteSpace(scan.VoiceRelativePath)
                    ? string.Empty
                    : $"Assets/_Game/Art/Audio/{safePackage}/Voices");
        }

        internal static string[] FxTagsForSkin(
            LocalOperatorPackageScan scan,
            string skinId)
        {
            if (scan == null)
                return Array.Empty<string>();

            var nonDefaultSkins = scan.Skins
                .Where(x => x != null &&
                            !string.Equals(x.SkinId, "default", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.SkinId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Length)
                .ToArray();

            var result = new List<string>();
            for (var i = 0; i < scan.FxTags.Count; i++)
            {
                var tag = scan.FxTags[i];
                string ownerSkin = null;
                for (var skinIndex = 0; skinIndex < nonDefaultSkins.Length; skinIndex++)
                {
                    var candidate = nonDefaultSkins[skinIndex];
                    if (tag.EndsWith("_" + candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        ownerSkin = candidate;
                        break;
                    }
                }

                if (ownerSkin == null ||
                    string.Equals(ownerSkin, skinId, StringComparison.OrdinalIgnoreCase))
                    result.Add(tag);
            }

            return result.ToArray();
        }

        private static void ReadManifestHeader(LocalOperatorPackageScan result)
        {
            var manifest = Path.Combine(result.SourceRoot, "unity_import_manifest.json");
            if (!File.Exists(manifest))
                return;

            try
            {
                var header = JsonUtility.FromJson<ManifestHeader>(File.ReadAllText(manifest));
                if (header == null)
                    return;

                if (!string.IsNullOrWhiteSpace(header.@char))
                    result.CharacterCode = header.@char;
                if (!string.IsNullOrWhiteSpace(header.charId))
                    result.CharacterId = header.charId;
                if (!string.IsNullOrWhiteSpace(header.displayName))
                    result.DisplayName = header.displayName;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ArknightsACT/LocalImport] Could not parse unity_import_manifest.json: " +
                    exception.Message);
            }
        }

        private static bool HasNumberedFrame(string directory)
        {
            foreach (var path in Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name) ||
                    name.Length < 2 ||
                    (name[0] != 'f' && name[0] != 'F'))
                    continue;

                var numeric = true;
                for (var i = 1; i < name.Length; i++)
                {
                    if (char.IsDigit(name[i]))
                        continue;
                    numeric = false;
                    break;
                }

                if (numeric)
                    return true;
            }

            return false;
        }

        internal static string SafeIdentifier(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                value = fallback;

            var chars = value.Trim()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();
            var normalized = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }
    }
}
#endif
