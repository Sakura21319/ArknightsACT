#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Downloads raw PRTS Spine source files for local prototype use.
    /// No PRTS binary assets are committed to the repository; downloaded files live under ignored PRTS folders.
    /// </summary>
    internal static class PrtsSpineSourceDownloader
    {
        [MenuItem("ArknightsACT/Assets/PRTS/Download Full Prototype Pack")]
        private static async void DownloadFullPrototypePack()
        {
            await DownloadPack(PrtsPrototypeAssetCatalog.GetFullPrototypePack(), "完整 Demo 素材包");
        }

        [MenuItem("ArknightsACT/Assets/PRTS/Download Texas")]
        private static async void DownloadTexas()
        {
            await DownloadPack(new[] { PrtsPrototypeAssetCatalog.Texas }, "德克萨斯");
        }

        [MenuItem("ArknightsACT/Assets/PRTS/Download Prototype Enemies")]
        private static async void DownloadPrototypeEnemies()
        {
            await DownloadPack(PrtsPrototypeAssetCatalog.PrototypeEnemies, "Demo 小兵素材包");
        }

        [MenuItem("ArknightsACT/Assets/PRTS/Open Texas Source Page")]
        private static void OpenTexasPage() => Application.OpenURL(PrtsPrototypeAssetCatalog.Texas.SourcePage);

        private static async Task DownloadPack(IReadOnlyList<PrtsAssetDescriptor> assets, string packName)
        {
            if (assets == null || assets.Count == 0)
                return;

            var success = new List<string>();
            var failed = new List<string>();

            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ArknightsACT-Prototype/0.2");

                for (var i = 0; i < assets.Count; i++)
                {
                    var asset = assets[i];
                    var cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "ArknightsACT · PRTS",
                        $"{asset.DisplayName} ({i + 1}/{assets.Count})",
                        (float)i / assets.Count);

                    if (cancelled)
                    {
                        failed.Add("用户取消后续下载");
                        break;
                    }

                    try
                    {
                        await DownloadAsset(client, asset);
                        success.Add(asset.DisplayName);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"PRTS asset failed: {asset.DisplayName}\n{exception}");
                        failed.Add($"{asset.DisplayName}: {exception.Message}");
                    }
                }

                AssetDatabase.Refresh();

                var message = new StringBuilder();
                message.AppendLine($"{packName} 下载完成。");
                message.AppendLine();
                message.AppendLine($"成功：{success.Count}");
                if (success.Count > 0)
                    message.AppendLine(string.Join("、", success));

                if (failed.Count > 0)
                {
                    message.AppendLine();
                    message.AppendLine($"失败/跳过：{failed.Count}");
                    foreach (var item in failed)
                        message.AppendLine("- " + item);
                }

                message.AppendLine();
                message.AppendLine("这些是 Spine 源素材；实际播放动画仍由 Presentation 层接入兼容的 Spine Runtime。下载目录已被 .gitignore 排除。 ");
                EditorUtility.DisplayDialog("PRTS 素材下载", message.ToString(), "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static async Task DownloadAsset(HttpClient client, PrtsAssetDescriptor asset)
        {
            Directory.CreateDirectory(asset.TargetDirectory);

            var atlasUrl = asset.RemoteDirectory + asset.BaseName + ".atlas";
            var atlasText = await GetStringRequired(client, atlasUrl);
            File.WriteAllText(
                Path.Combine(asset.TargetDirectory, asset.BaseName + ".atlas.txt"),
                atlasText,
                Encoding.UTF8);

            var pages = ParseAtlasPages(atlasText);
            if (pages.Count == 0)
                throw new InvalidOperationException("Atlas 中未找到 PNG 页面。");

            foreach (var page in pages)
            {
                var bytes = await GetBytesRequired(client, asset.RemoteDirectory + page);
                File.WriteAllBytes(Path.Combine(asset.TargetDirectory, page), bytes);
            }

            var skeletonType = await DownloadSkeleton(client, asset);

            File.WriteAllText(
                Path.Combine(asset.TargetDirectory, "SOURCE.txt"),
                "Display name: " + asset.DisplayName + Environment.NewLine +
                "Prototype role: " + asset.Role + Environment.NewLine +
                "PRTS page: " + asset.SourcePage + Environment.NewLine +
                "PRTS asset root: " + asset.RemoteDirectory + Environment.NewLine +
                "Internal model id: " + asset.BaseName + Environment.NewLine +
                "Skeleton source: " + skeletonType + Environment.NewLine +
                "Local prototype only. Keep gameplay code independent from these paths." + Environment.NewLine,
                Encoding.UTF8);
        }

        private static async Task<string> DownloadSkeleton(HttpClient client, PrtsAssetDescriptor asset)
        {
            var skelUrl = asset.RemoteDirectory + asset.BaseName + ".skel";
            var skel = await TryGetBytes(client, skelUrl);
            if (skel != null)
            {
                File.WriteAllBytes(Path.Combine(asset.TargetDirectory, asset.BaseName + ".skel.bytes"), skel);
                return ".skel";
            }

            var jsonUrl = asset.RemoteDirectory + asset.BaseName + ".json";
            var json = await TryGetBytes(client, jsonUrl);
            if (json != null)
            {
                File.WriteAllBytes(Path.Combine(asset.TargetDirectory, asset.BaseName + ".json"), json);
                return ".json";
            }

            throw new InvalidOperationException("未找到 .skel 或 .json skeleton 文件。Atlas/PNG 可能已经下载。");
        }

        private static List<string> ParseAtlasPages(string atlasText)
        {
            var pages = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = atlasText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (!line.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.Contains(":"))
                    continue;
                if (seen.Add(line))
                    pages.Add(line);
            }

            return pages;
        }

        private static async Task<string> GetStringRequired(HttpClient client, string url)
        {
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private static async Task<byte[]> GetBytesRequired(HttpClient client, string url)
        {
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        private static async Task<byte[]> TryGetBytes(HttpClient client, string url)
        {
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}
#endif
