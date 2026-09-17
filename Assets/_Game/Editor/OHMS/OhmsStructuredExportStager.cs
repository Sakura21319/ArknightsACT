#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ArknightsACT.Editor.OHMS
{
    /// <summary>
    /// Builds a temporary copy of an OHMS structured export under Library/ and normalizes PPtrs whose
    /// targets are present in the same combined export. AssetStudio keeps the original non-zero m_FileID
    /// even when dependency bundles were loaded and exported together, while the generic importer indexes
    /// objects by PathID. Rewriting those resolvable references to FileID 0 lets the normal importer follow
    /// dependencies without touching the user's source export.
    /// </summary>
    internal static class OhmsStructuredExportStager
    {
        private static readonly Regex PathIdRegex = new(
            "\\\"PathID\\\"\\s*:\\s*(?<id>-?\\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PointerRegex = new(
            "(?<fileKey>\\\"m_FileID\\\"\\s*:\\s*)(?<file>-?\\d+)(?<middle>\\s*,\\s*\\\"m_PathID\\\"\\s*:\\s*)(?<path>-?\\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal sealed class StageResult
        {
            public string SourceRoot;
            public string StagingRoot;
            public int KnownPathIds;
            public int JsonPayloads;
            public int RewrittenPointers;
        }

        internal static StageResult Prepare(string sourceRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot))
                throw new ArgumentException("OHMS source root is empty.", nameof(sourceRoot));

            sourceRoot = Path.GetFullPath(sourceRoot);
            var assetsPath = Path.Combine(sourceRoot, "assets.json");
            var thingsPath = Path.Combine(sourceRoot, "things");
            if (!File.Exists(assetsPath) || !Directory.Exists(thingsPath))
                throw new InvalidOperationException($"Not an OHMS structured export: {sourceRoot}");

            var knownPathIds = ReadPathIds(assetsPath);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var stagingRoot = Path.Combine(projectRoot, "Library", "ArknightsACT", "OHMS", "Normalized");
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, true);
            Directory.CreateDirectory(stagingRoot);

            File.Copy(assetsPath, Path.Combine(stagingRoot, "assets.json"), true);
            CopyOptionalIndexFiles(sourceRoot, stagingRoot);

            var stagingThings = Path.Combine(stagingRoot, "things");
            Directory.CreateDirectory(stagingThings);

            var result = new StageResult
            {
                SourceRoot = sourceRoot,
                StagingRoot = stagingRoot,
                KnownPathIds = knownPathIds.Count
            };

            foreach (var sourceFile in Directory.EnumerateFiles(thingsPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(thingsPath, sourceFile);
                var destination = Path.Combine(stagingThings, relative);
                var destinationDir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(destinationDir))
                    Directory.CreateDirectory(destinationDir);

                var bytes = File.ReadAllBytes(sourceFile);
                if (!LooksLikeJson(bytes))
                {
                    File.WriteAllBytes(destination, bytes);
                    continue;
                }

                var text = DecodeUtf8(bytes);
                if (string.IsNullOrWhiteSpace(text))
                {
                    File.WriteAllBytes(destination, bytes);
                    continue;
                }

                result.JsonPayloads++;
                var rewritten = PointerRegex.Replace(text, match =>
                {
                    if (!long.TryParse(match.Groups["file"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileId) || fileId == 0)
                        return match.Value;
                    if (!long.TryParse(match.Groups["path"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pathId) || pathId == 0)
                        return match.Value;
                    if (!knownPathIds.Contains(pathId))
                        return match.Value;

                    result.RewrittenPointers++;
                    return match.Groups["fileKey"].Value + "0" + match.Groups["middle"].Value + match.Groups["path"].Value;
                });

                File.WriteAllText(destination, rewritten, new UTF8Encoding(false));
            }

            return result;
        }

        private static HashSet<long> ReadPathIds(string assetsPath)
        {
            var result = new HashSet<long>();
            var json = File.ReadAllText(assetsPath);
            foreach (Match match in PathIdRegex.Matches(json))
            {
                if (long.TryParse(match.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    result.Add(id);
            }
            return result;
        }

        private static void CopyOptionalIndexFiles(string sourceRoot, string stagingRoot)
        {
            foreach (var fileName in new[] { "assets.xml" })
            {
                var source = Path.Combine(sourceRoot, fileName);
                if (File.Exists(source))
                    File.Copy(source, Path.Combine(stagingRoot, fileName), true);
            }
        }

        private static bool LooksLikeJson(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return false;

            var index = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                index = 3;
            while (index < bytes.Length && IsAsciiWhitespace(bytes[index]))
                index++;
            if (index >= bytes.Length)
                return false;
            return bytes[index] == (byte)'{' || bytes[index] == (byte)'[';
        }

        private static bool IsAsciiWhitespace(byte value)
            => value == 0x20 || value == 0x09 || value == 0x0A || value == 0x0D;

        private static string DecodeUtf8(byte[] bytes)
        {
            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
