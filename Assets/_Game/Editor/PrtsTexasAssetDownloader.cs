#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    public static class PrtsTexasAssetDownloader
    {
        private const string SourcePage = "https://prts.wiki/w/%E5%BE%B7%E5%85%8B%E8%90%A8%E6%96%AF/spine";
        private const string RemoteDirectory = "https://static.prts.wiki/spine/char/char_102_texas/char_102_texas/";
        private const string RemoteBaseName = "char_102_texas";
        private const string TargetDirectory = "Assets/_Game/Art/Characters/Texas/PRTS/Spine";

        [MenuItem("ArknightsACT/Assets/Open Texas PRTS Spine Page")]
        public static void OpenSourcePage() => Application.OpenURL(SourcePage);

        [MenuItem("ArknightsACT/Assets/Download Texas PRTS Spine Source")]
        public static async void DownloadTexasSpineSource()
        {
            try
            {
                Directory.CreateDirectory(TargetDirectory);
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ArknightsACT-Prototype/0.1");

                EditorUtility.DisplayProgressBar("ArknightsACT", "Downloading Texas atlas...", 0.15f);
                var atlasUrl = RemoteDirectory + RemoteBaseName + ".atlas";
                var atlasText = await client.GetStringAsync(atlasUrl);
                File.WriteAllText(Path.Combine(TargetDirectory, RemoteBaseName + ".atlas.txt"), atlasText);

                var pages = ParseAtlasPages(atlasText);
                var progress = 0;
                foreach (var page in pages)
                {
                    progress++;
                    EditorUtility.DisplayProgressBar(
                        "ArknightsACT",
                        "Downloading " + page,
                        0.20f + 0.45f * progress / Mathf.Max(1f, pages.Count));
                    var bytes = await client.GetByteArrayAsync(RemoteDirectory + page);
                    File.WriteAllBytes(Path.Combine(TargetDirectory, page), bytes);
                }

                EditorUtility.DisplayProgressBar("ArknightsACT", "Downloading Texas skeleton...", 0.75f);
                var skeletonDownloaded = await TryDownloadSkeleton(client);
                if (!skeletonDownloaded)
                    throw new InvalidOperationException("PRTS skeleton endpoint was not found (.skel/.json). Atlas and textures may still have been downloaded.");

                File.WriteAllText(
                    Path.Combine(TargetDirectory, "SOURCE.txt"),
                    "PRTS page: " + SourcePage + Environment.NewLine +
                    "PRTS asset root: " + RemoteDirectory + Environment.NewLine +
                    "Internal character id: char_102_texas" + Environment.NewLine);

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "Texas PRTS Spine source downloaded to:\n" + TargetDirectory +
                    "\n\nThese are source assets only. Runtime rendering stays isolated from gameplay and requires a compatible Spine runtime or an offline frame-baking step.",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Texas asset download failed", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<string> ParseAtlasPages(string atlasText)
        {
            var pages = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = atlasText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (!line.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !seen.Add(line))
                    continue;
                pages.Add(line);
            }
            return pages;
        }

        private static async Task<bool> TryDownloadSkeleton(HttpClient client)
        {
            var skel = await TryDownloadBytes(client, RemoteDirectory + RemoteBaseName + ".skel");
            if (skel != null)
            {
                File.WriteAllBytes(Path.Combine(TargetDirectory, RemoteBaseName + ".skel.bytes"), skel);
                return true;
            }

            var json = await TryDownloadBytes(client, RemoteDirectory + RemoteBaseName + ".json");
            if (json == null)
                return false;

            File.WriteAllBytes(Path.Combine(TargetDirectory, RemoteBaseName + ".json"), json);
            return true;
        }

        private static async Task<byte[]> TryDownloadBytes(HttpClient client, string url)
        {
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}
#endif
