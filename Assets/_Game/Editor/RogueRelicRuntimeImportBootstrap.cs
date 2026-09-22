#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ArknightsACT.Editor
{
    public static class RogueRelicRuntimeImportBootstrap
    {
        private const string Prefix = "[ArknightsACT/RogueRelicRuntime] ";
        private const string SessionKey = "ArknightsACT.RogueRelicRuntimeImportRunning";

        private static readonly string[] PythonCandidates =
        {
            @"D:\Effect\.venv\Scripts\python.exe",
            @"C:\ProgramData\Anaconda3\python.exe",
            @"C:\Users\21613\anaconda3\python.exe",
            @"C:\Users\21613\AppData\Roaming\uv\python\cpython-3.12.14-windows-x86_64-none\python.exe"
        };

        [InitializeOnLoadMethod]
        private static void AutoRun()
        {
            EditorApplication.delayCall += () => Import(false);
        }

        private static void RunFromMenu()
        {
            Import(true);
        }

        private static void Import(bool force)
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return;

            var xlsx = Path.Combine(projectRoot, "Docs", "RogueRelics", "RogueRelicDatabase_IS1_CurrentPool.xlsx");
            var script = Path.Combine(Application.dataPath, "_Game", "Editor", "Tools", "import_is1_runtime_catalog.py");
            var outputJson = Path.Combine(Application.dataPath, "_Game", "Resources", "ScavengingCatalog.json");
            var iconDir = Path.Combine(Application.dataPath, "_Game", "Resources", "RogueRelics", "RuntimeIcons");
            var report = Path.Combine(projectRoot, "Logs", "RogueRelicRuntimeImport.json");

            if (!File.Exists(xlsx) || !File.Exists(script))
                return;

            if (!force && File.Exists(outputJson) &&
                File.GetLastWriteTimeUtc(outputJson) >= File.GetLastWriteTimeUtc(xlsx) &&
                File.GetLastWriteTimeUtc(outputJson) >= File.GetLastWriteTimeUtc(script))
                return;

            var python = PythonCandidates.FirstOrDefault(File.Exists);
            if (python == null)
            {
                Debug.LogError(Prefix + "Python not found. Use the manual import batch file under Docs/RogueRelics.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(report) ?? projectRoot);
            Directory.CreateDirectory(iconDir);
            SessionState.SetBool(SessionKey, true);

            try
            {
                var args = Quote(script) + " " + Quote(projectRoot) + " " + Quote(xlsx) + " " +
                           Quote(outputJson) + " " + Quote(iconDir) + " " + Quote(report);
                var psi = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = args,
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null)
                    throw new InvalidOperationException("Process.Start returned null.");

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(180000))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("Runtime catalog import timed out.");
                }

                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Importer failed (" + process.ExitCode + ")\n" + stdout + "\n" + stderr);

                AssetDatabase.Refresh();
                Debug.Log(Prefix + "curated IS1 workbook imported.\n" + stdout);
            }
            catch (Exception e)
            {
                Debug.LogError(Prefix + e);
            }
            finally
            {
                SessionState.SetBool(SessionKey, false);
            }
        }

        private static string Quote(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
#endif
