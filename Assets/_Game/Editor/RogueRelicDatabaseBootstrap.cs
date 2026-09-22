#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Editor-only bootstrap for building a complete Integrated Strategies relic database.
    /// Runtime code never depends on the external client or the network.
    ///
    /// Preferred path:
    /// 1) decode locally when the client data keeps original FlatBuffer filenames;
    /// 2) otherwise use a structured CN gamedata snapshot as a fallback.
    ///
    /// The current PC client stores many anon/*.bin files under hash-only names. ArkUnpacker
    /// selects the FBS schema from the original filename, so hash-only anon files cannot be
    /// decoded reliably without first restoring the filename map. In that case we intentionally
    /// fall back to the same structured game table from an automated gamedata snapshot.
    /// </summary>
    public static class RogueRelicDatabaseBootstrap
    {
        // Export schema v8: fully local icon cache + embedded-image XLSX + IS1 current pool.
        private const string LogPrefix = "[ArknightsACT/RogueRelicDB] ";
        private const int ExportSchemaVersion = 4; // Excel image-formula export + compact transport

        private static readonly string[] GameWindowsCandidates =
        {
            @"D:\SteamLibrary\Hypergryph Launcher\games\Arknights\Arknights_Data\StreamingAssets\AB\Windows",
            @"D:\Arknights bilibili\games\Arknights Game\Arknights_Data\StreamingAssets\AB\Windows"
        };

        private static readonly string[] ArkUnpackerCandidates =
        {
            @"D:\AK_Extract\ArkUnpacker-v5.1.0.exe",
            @"D:\Effect\_bin\ArkUnpacker-v5.1.0.exe",
            @"D:\Effect\ArkUnpacker-v5.1.0.exe"
        };

        private static readonly string[] PythonCandidates =
        {
            @"D:\Effect\.venv\Scripts\python.exe",
            @"C:\ProgramData\Anaconda3\python.exe",
            @"C:\Users\21613\anaconda3\python.exe"
        };

        private static readonly string[] StructuredDataUrls =
        {
            "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/master/cn/gamedata/excel/roguelike_topic_table.json",
            "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/excel/roguelike_topic_table.json"
        };

        private static readonly string[] LegacyDataUrls =
        {
            "https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/master/cn/gamedata/excel/roguelike_table.json",
            "https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/excel/roguelike_table.json"
        };

        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            // A curated IS1 workbook is now edited manually. Never auto-regenerate it on script
            // reload, otherwise user-deleted rows/columns would be restored. Rebuild remains
            // available explicitly through the menu command below.
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var curated = string.IsNullOrWhiteSpace(projectRoot)
                ? null
                : Path.Combine(projectRoot, "Docs", "RogueRelics", "RogueRelicDatabase_IS1_CurrentPool.xlsx");
            if (!string.IsNullOrWhiteSpace(curated) && File.Exists(curated))
                return;

            EditorApplication.delayCall += TryBuildSchemaSummary;
        }

        private static void RebuildFromMenu()
        {
            TryBuildSchemaSummary(forceDecode: true);
        }

        private static void TryBuildSchemaSummary() => TryBuildSchemaSummary(false);

        private static void TryBuildSchemaSummary(bool forceDecode)
        {
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot)) return;

                var workRoot = Path.Combine(projectRoot, "Library", "ArknightsACT", "RogueRelicDB");
                var decodedRoot = Path.Combine(workRoot, "Decoded");
                var downloadedRoot = Path.Combine(workRoot, "Downloaded");
                var summaryPath = Path.Combine(projectRoot, "Logs", "RogueRelicSchemaSummary.json");
                var sourceLogPath = Path.Combine(projectRoot, "Logs", "RogueRelicDataSource.txt");
                var scriptPath = Path.Combine(Application.dataPath, "_Game", "Editor", "Tools", "inspect_rogue_topic.py");
                var exporterPath = Path.Combine(Application.dataPath, "_Game", "Editor", "Tools", "export_rogue_relic_database.py");
                var preprocessorPath = Path.Combine(Application.dataPath, "_Game", "Editor", "Tools", "preprocess_rogue_relic_database.py");
                var exportRoot = Path.Combine(projectRoot, "Docs", "RogueRelics");
                var iconRoot = Path.Combine(Application.dataPath, "_Game", "Resources", "RogueRelics", "Icons");
                var exportCsv = Path.Combine(exportRoot, "RogueRelicDatabase.csv");

                if (!forceDecode &&
                    File.Exists(summaryPath) && new FileInfo(summaryPath).Length > 128 &&
                    File.Exists(exportCsv) && new FileInfo(exportCsv).Length > 256 &&
                    File.GetLastWriteTimeUtc(exportCsv) >= File.GetLastWriteTimeUtc(exporterPath) &&
                    File.Exists(Path.Combine(exportRoot, "RogueRelicDatabase_IS1_CurrentPool.xlsx")) &&
                    File.GetLastWriteTimeUtc(Path.Combine(exportRoot, "RogueRelicDatabase_IS1_CurrentPool.xlsx")) >=
                        File.GetLastWriteTimeUtc(preprocessorPath))
                    return;

                var python = PythonCandidates.FirstOrDefault(File.Exists);
                if (python == null || !File.Exists(scriptPath) || !File.Exists(exporterPath) || !File.Exists(preprocessorPath))
                {
                    Debug.LogError(LogPrefix +
                        $"export prerequisites missing: python={python ?? "<missing>"}, " +
                        $"inspector={File.Exists(scriptPath)}, exporter={File.Exists(exporterPath)}, " +
                        $"preprocessor={File.Exists(preprocessorPath)}");
                    return;
                }

                Directory.CreateDirectory(workRoot);
                Directory.CreateDirectory(downloadedRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(summaryPath) ?? workRoot);

                string tablePath = null;
                string sourceDescription = null;

                var gameRoot = GameWindowsCandidates.FirstOrDefault(Directory.Exists);
                var unpacker = ArkUnpackerCandidates.FirstOrDefault(File.Exists);
                var anonRoot = gameRoot == null ? null : Path.Combine(gameRoot, "anon");

                if (gameRoot != null && unpacker != null && Directory.Exists(anonRoot))
                {
                    var hashedAnon = LooksLikeHashOnlyAnon(anonRoot);
                    if (hashedAnon)
                    {
                        Debug.LogWarning(LogPrefix +
                            "PC anon files are hash-only names. ArkUnpacker chooses FlatBuffer schemas from the original filename, " +
                            "so direct decode is skipped and the structured CN gamedata fallback will be used.");
                    }
                    else
                    {
                        tablePath = FindRogueTopicTable(decodedRoot);
                        if (forceDecode || tablePath == null)
                        {
                            if (Directory.Exists(decodedRoot))
                                Directory.Delete(decodedRoot, true);
                            Directory.CreateDirectory(decodedRoot);

                            var args =
                                "-m fb -i " + Quote(anonRoot) +
                                " -o " + Quote(decodedRoot) +
                                " -d -l 3";

                            Debug.Log(LogPrefix + "decoding local anon FlatBuffers...");
                            var exit = RunProcess(
                                unpacker,
                                args,
                                Path.GetDirectoryName(unpacker),
                                out var stdout,
                                out var stderr,
                                240000);

                            File.WriteAllText(
                                Path.Combine(workRoot, "ArkUnpacker_stdout.txt"),
                                stdout ?? string.Empty,
                                Encoding.UTF8);
                            File.WriteAllText(
                                Path.Combine(workRoot, "ArkUnpacker_stderr.txt"),
                                stderr ?? string.Empty,
                                Encoding.UTF8);

                            if (exit != 0)
                            {
                                Debug.LogWarning(LogPrefix +
                                    $"local ArkUnpacker decode failed ({exit}); using structured fallback.\n{stderr}");
                            }
                            else
                            {
                                tablePath = FindRogueTopicTable(decodedRoot);
                            }
                        }

                        if (tablePath != null)
                            sourceDescription = "Local installed client decoded with ArkUnpacker: " + tablePath;
                    }
                }

                if (tablePath == null)
                {
                    tablePath = DownloadStructuredFallback(downloadedRoot, out sourceDescription);
                }

                if (tablePath == null)
                {
                    Debug.LogError(LogPrefix +
                        "roguelike_topic_table.json could not be obtained from either the local client or structured CN gamedata fallback.");
                    return;
                }

                var legacyPath = DownloadLegacyFallback(downloadedRoot, out var legacySourceDescription);

                File.WriteAllText(
                    sourceLogPath,
                    sourceDescription + Environment.NewLine +
                    (legacySourceDescription ?? "Legacy IS1 source: unavailable") + Environment.NewLine +
                    "GeneratedUtc=" + DateTime.UtcNow.ToString("O") + Environment.NewLine,
                    Encoding.UTF8);

                var pyArgs = Quote(scriptPath) + " " + Quote(tablePath) + " " + Quote(summaryPath);
                var pyExit = RunProcess(python, pyArgs, projectRoot, out var pyOut, out var pyErr, 120000);
                if (pyExit != 0)
                {
                    Debug.LogError(LogPrefix + $"schema inspector failed ({pyExit})\n{pyOut}\n{pyErr}");
                    return;
                }

                Directory.CreateDirectory(exportRoot);
                Directory.CreateDirectory(iconRoot);

                var exportArgs =
                    Quote(exporterPath) + " " +
                    Quote(tablePath) + " " +
                    Quote(legacyPath ?? "-") + " " +
                    Quote(exportRoot) + " " +
                    Quote(iconRoot);
                var exportExit = RunProcess(
                    python,
                    exportArgs,
                    projectRoot,
                    out var exportOut,
                    out var exportErr,
                    900000);
                if (exportExit != 0)
                {
                    Debug.LogError(LogPrefix +
                        $"relic database exporter failed ({exportExit})\n{exportOut}\n{exportErr}");
                    return;
                }

                var preprocessArgs = Quote(preprocessorPath) + " " + Quote(exportRoot);
                var preprocessExit = RunProcess(
                    python,
                    preprocessArgs,
                    projectRoot,
                    out var preprocessOut,
                    out var preprocessErr,
                    180000);
                if (preprocessExit != 0)
                {
                    Debug.LogError(LogPrefix +
                        $"relic database preprocessor failed ({preprocessExit})\n{preprocessOut}\n{preprocessErr}");
                    return;
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                Debug.Log(LogPrefix +
                    "database ready: " + exportCsv + "\n" +
                    "preprocessed IS1 pool: " + Path.Combine(exportRoot, "RogueRelicDatabase_IS1_CurrentPool.xlsx") + "\n" +
                    "source: " + sourceDescription + "\n" +
                    (legacySourceDescription ?? "legacy IS1 source unavailable") + "\n" +
                    exportOut + "\n" +
                    preprocessOut);
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + e);
            }
        }

        private static bool LooksLikeHashOnlyAnon(string anonRoot)
        {
            try
            {
                var files = Directory.GetFiles(anonRoot, "*.bin", SearchOption.TopDirectoryOnly);
                if (files.Length == 0) return false;

                var checkedCount = Math.Min(files.Length, 32);
                var hashedCount = 0;
                for (var i = 0; i < checkedCount; i++)
                {
                    var stem = Path.GetFileNameWithoutExtension(files[i]);
                    if (Regex.IsMatch(stem ?? string.Empty, "^[0-9a-fA-F]{32}$"))
                        hashedCount++;
                }

                return hashedCount >= Math.Max(1, checkedCount * 3 / 4);
            }
            catch
            {
                return false;
            }
        }

        private static string DownloadStructuredFallback(string downloadedRoot, out string sourceDescription)
        {
            sourceDescription = null;
            Directory.CreateDirectory(downloadedRoot);
            var destination = Path.Combine(downloadedRoot, "roguelike_topic_table.json");

            using var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true
            });
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 ArknightsACT RogueRelicDB Editor Importer");

            Exception last = null;
            foreach (var url in StructuredDataUrls)
            {
                try
                {
                    Debug.Log(LogPrefix + "downloading structured roguelike data: " + url);
                    var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    if (bytes == null || bytes.Length < 1024)
                        throw new InvalidDataException("downloaded file is unexpectedly small");

                    var json = Encoding.UTF8.GetString(bytes);
                    if (!json.Contains("\"details\"") || !json.Contains("rogue"))
                        throw new InvalidDataException("downloaded JSON does not look like roguelike_topic_table");

                    File.WriteAllBytes(destination, bytes);
                    sourceDescription = "Structured CN gamedata snapshot: " + url;
                    return destination;
                }
                catch (Exception e)
                {
                    last = e;
                    Debug.LogWarning(LogPrefix + "structured source failed: " + url + "\n" + e.Message);
                }
            }

            Debug.LogError(LogPrefix + "all structured fallback sources failed: " + last);
            return null;
        }

        private static string DownloadLegacyFallback(string downloadedRoot, out string sourceDescription)
        {
            sourceDescription = null;
            Directory.CreateDirectory(downloadedRoot);
            var destination = Path.Combine(downloadedRoot, "roguelike_table.json");

            using var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true
            });
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 ArknightsACT RogueRelicDB Legacy Importer");

            foreach (var url in LegacyDataUrls)
            {
                try
                {
                    var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    if (bytes == null || bytes.Length < 512)
                        continue;

                    var json = Encoding.UTF8.GetString(bytes);
                    if (!json.Contains("relic") && !json.Contains("collect"))
                        continue;

                    File.WriteAllBytes(destination, bytes);
                    sourceDescription = "Legacy IS1 structured gamedata snapshot: " + url;
                    return destination;
                }
                catch (Exception e)
                {
                    Debug.LogWarning(LogPrefix + "legacy source failed: " + url + "\n" + e.Message);
                }
            }

            return File.Exists(destination) && new FileInfo(destination).Length > 512
                ? destination
                : null;
        }

        private static string FindRogueTopicTable(string root)
        {
            if (!Directory.Exists(root)) return null;

            var exact = Directory.GetFiles(root, "roguelike_topic_table.json", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (exact != null) return exact;

            return Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
                .FirstOrDefault(path =>
                    Path.GetFileName(path).IndexOf("roguelike", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    Path.GetFileName(path).IndexOf("topic", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int RunProcess(
            string fileName,
            string arguments,
            string workingDirectory,
            out string stdout,
            out string stderr,
            int timeoutMs)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                stdout = string.Empty;
                stderr = "Process.Start returned null.";
                return -1;
            }

            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                stdout = outTask.IsCompleted ? outTask.Result : string.Empty;
                stderr = (errTask.IsCompleted ? errTask.Result : string.Empty) + "\nTimed out.";
                return -2;
            }

            System.Threading.Tasks.Task.WaitAll(outTask, errTask);
            stdout = outTask.Result;
            stderr = errTask.Result;
            return process.ExitCode;
        }

        private static string Quote(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
#endif
