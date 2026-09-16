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
    /// Downloads the small local-only PRTS audio set used by the playable prototype. Audio is kept
    /// below the already ignored PRTS art root and is never committed by this workflow.
    /// </summary>
    internal static class PrtsGameplayAudioDownloader
    {
        [MenuItem("ArknightsACT/Assets/PRTS/Download Gameplay Audio (BGM + Combat + Chen)")]
        [MenuItem("ArknightsACT/Assets/PRTS/Download Gameplay Audio (BGM + Chen)")]
        private static async void DownloadGameplayAudio()
        {
            var success = new List<string>();
            var failed = new List<string>();

            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(35)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ArknightsACT-Prototype/0.6");

                var assets = PrtsGameplayAudioCatalog.All;
                for (var i = 0; i < assets.Length; i++)
                {
                    var asset = assets[i];
                    var cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "ArknightsACT · PRTS 游戏音频",
                        $"{asset.DisplayName} ({i + 1}/{assets.Length})",
                        (float)i / assets.Length);
                    if (cancelled)
                    {
                        failed.Add("用户取消后续下载");
                        break;
                    }

                    try
                    {
                        var selectedUrl = await DownloadOne(client, asset);
                        WriteSourceNote(asset, selectedUrl);
                        success.Add(asset.DisplayName);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[ArknightsACT/PRTS Audio] Download failed: {asset.DisplayName}\n{exception}");
                        failed.Add($"{asset.DisplayName}: {exception.Message}");
                    }
                }

                AssetDatabase.Refresh();
                for (var i = 0; i < PrtsGameplayAudioCatalog.All.Length; i++)
                    ConfigureAudioImporter(PrtsGameplayAudioCatalog.All[i]);
                AssetDatabase.SaveAssets();

                var message = new StringBuilder();
                message.AppendLine("PRTS 游戏音频下载完成。");
                message.AppendLine();
                message.AppendLine($"成功：{success.Count}/{PrtsGameplayAudioCatalog.All.Length}");
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
                message.AppendLine("音频保存在 Assets/_Game/Art/Audio/PRTS（已被 .gitignore 排除）。");
                message.AppendLine("接着执行 ArknightsACT > Build Prototype Scene，然后进入 Play 验证 BGM、技能/普攻/命中/受击/死亡/敌人攻击音效与陈的战斗语音。");
                EditorUtility.DisplayDialog("PRTS 游戏音频", message.ToString(), "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static async Task<string> DownloadOne(HttpClient client, PrtsGameplayAudioAsset asset)
        {
            if (asset.RemoteUrls == null || asset.RemoteUrls.Length == 0)
                throw new InvalidOperationException("No candidate media URL configured.");

            var directory = Path.GetDirectoryName(asset.LocalPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var errors = new List<string>(asset.RemoteUrls.Length);
            for (var i = 0; i < asset.RemoteUrls.Length; i++)
            {
                var url = asset.RemoteUrls[i];
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                try
                {
                    using var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        errors.Add($"{(int)response.StatusCode} {url}");
                        continue;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes == null || bytes.Length < 128)
                    {
                        errors.Add($"empty/invalid response {url}");
                        continue;
                    }

                    File.WriteAllBytes(asset.LocalPath, bytes);
                    return url;
                }
                catch (Exception exception)
                {
                    errors.Add($"{url}: {exception.Message}");
                }
            }

            throw new InvalidOperationException("All candidate URLs failed: " + string.Join(" | ", errors));
        }

        private static void WriteSourceNote(PrtsGameplayAudioAsset asset, string selectedUrl)
        {
            var directory = Path.GetDirectoryName(asset.LocalPath) ?? string.Empty;
            var notePath = Path.Combine(
                directory,
                Path.GetFileNameWithoutExtension(asset.LocalPath) + "_SOURCE.txt");
            File.WriteAllText(
                notePath,
                "Display name: " + asset.DisplayName + Environment.NewLine +
                "PRTS page: " + asset.SourcePage + Environment.NewLine +
                "PRTS media: " + selectedUrl + Environment.NewLine +
                "Kind: " + asset.Kind + Environment.NewLine +
                "Local prototype/reference use only. Do not commit or redistribute from this repository." + Environment.NewLine,
                Encoding.UTF8);
        }

        private static void ConfigureAudioImporter(PrtsGameplayAudioAsset asset)
        {
            var importer = AssetImporter.GetAtPath(asset.LocalPath) as AudioImporter;
            if (importer == null)
                return;

            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = asset.Kind == PrtsGameplayAudioKind.Music ? 0.78f : 0.86f;
            settings.loadType = asset.Kind == PrtsGameplayAudioKind.Music
                ? AudioClipLoadType.CompressedInMemory
                : AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;

            importer.forceToMono = false;
            importer.loadInBackground = asset.Kind == PrtsGameplayAudioKind.Music;
            importer.SaveAndReimport();
        }
    }
}
#endif
