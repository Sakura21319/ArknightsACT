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
    /// Downloads the local-only audio set used by the playable prototype.
    /// PRTS-hosted SFX/voices are downloaded directly. Tracks that PRTS identifies but does not mirror
    /// under /assets/audio can use an msr:<songId> candidate, which resolves the official Monster Siren
    /// API source URL before downloading the media. No async Editor GUI is used.
    /// </summary>
    internal static class PrtsGameplayAudioDownloader
    {
        private const string MonsterSirenSongApi = "https://monster-siren.hypergryph.com/api/song/";
        private static bool _downloadRunning;

        [Serializable]
        private sealed class MonsterSirenSongResponse
        {
            public int code;
            public string msg;
            public MonsterSirenSongData data;
        }

        [Serializable]
        private sealed class MonsterSirenSongData
        {
            public string cid;
            public string name;
            public string sourceUrl;
        }

        private static void DownloadMissingOnly()
        {
            StartDownload("缺失音频", PrtsGameplayAudioCatalog.All, skipExisting: true);
        }

        private static void DownloadAll()
        {
            StartDownload("全部音频", PrtsGameplayAudioCatalog.All, skipExisting: false);
        }

        private static void DownloadBgm()
        {
            StartDownload("BGM", PrtsGameplayAudioCatalog.Bgm, skipExisting: false);
        }

        private static void DownloadCombatAndSkills()
        {
            StartDownload("战斗与技能音效", PrtsGameplayAudioCatalog.CombatAndSkills, skipExisting: false);
        }

        private static void DownloadChenVoices()
        {
            StartDownload("陈日语语音", PrtsGameplayAudioCatalog.ChenVoices, skipExisting: false);
        }

        internal static void VerifyLocalAudio()
        {
            var valid = new List<string>();
            var missing = new List<string>();
            foreach (var asset in PrtsGameplayAudioCatalog.All)
            {
                if (IsValidLocalFile(asset.LocalPath))
                    valid.Add(asset.DisplayName);
                else
                    missing.Add(asset.DisplayName);
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[ArknightsACT/PRTS Audio] 本地音频检查：{valid.Count}/{PrtsGameplayAudioCatalog.All.Length} 可用。");
            if (missing.Count > 0)
            {
                builder.AppendLine("缺失/无效：");
                foreach (var item in missing)
                    builder.AppendLine("- " + item);
            }
            else
            {
                builder.AppendLine("全部音频文件均已存在。可以执行 ArknightsACT > Build Prototype Scene。");
            }
            Debug.Log(builder.ToString());
        }

        /// <summary>
        /// Single production entry used by the compact ArknightsACT menu.
        /// Existing valid files are preserved; new BGM / Japanese voice filenames are fetched as needed.
        /// </summary>
        internal static void DownloadLegacyEntry()
        {
            DownloadMissingOnly();
        }

        private static async void StartDownload(
            string label,
            PrtsGameplayAudioAsset[] assets,
            bool skipExisting)
        {
            if (_downloadRunning)
            {
                Debug.LogWarning("[ArknightsACT/PRTS Audio] 已经有一个音频下载任务在运行，请等待当前任务结束。");
                return;
            }

            _downloadRunning = true;
            try
            {
                await DownloadSet(label, assets, skipExisting);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ArknightsACT/PRTS Audio] 下载流程发生未处理异常：\n{exception}");
            }
            finally
            {
                _downloadRunning = false;
            }
        }

        private static async Task DownloadSet(
            string label,
            PrtsGameplayAudioAsset[] assets,
            bool skipExisting)
        {
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"[ArknightsACT/PRTS Audio] {label} 没有配置资源。");
                return;
            }

            var success = new List<string>();
            var skipped = new List<string>();
            var failed = new List<string>();

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ArknightsACT-Prototype/0.8");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");

            Debug.Log($"[ArknightsACT/PRTS Audio] 开始下载 {label}，共 {assets.Length} 项。" +
                      (skipExisting ? " 已存在的有效文件会跳过。" : string.Empty));

            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                if (asset == null)
                    continue;

                if (skipExisting && IsValidLocalFile(asset.LocalPath))
                {
                    skipped.Add(asset.DisplayName);
                    Debug.Log($"[ArknightsACT/PRTS Audio] 跳过 {i + 1}/{assets.Length}: {asset.DisplayName}");
                    continue;
                }

                try
                {
                    var selectedUrl = await DownloadOne(client, asset);
                    WriteSourceNote(asset, selectedUrl);
                    success.Add(asset.DisplayName);
                    Debug.Log($"[ArknightsACT/PRTS Audio] 完成 {i + 1}/{assets.Length}: {asset.DisplayName}");
                }
                catch (Exception exception)
                {
                    failed.Add($"{asset.DisplayName}: {exception.Message}");
                    Debug.LogWarning($"[ArknightsACT/PRTS Audio] 下载失败 {i + 1}/{assets.Length}: {asset.DisplayName}\n{exception.Message}");
                }
            }

            AssetDatabase.Refresh();
            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                if (asset != null && IsValidLocalFile(asset.LocalPath))
                    ConfigureAudioImporter(asset);
            }
            AssetDatabase.SaveAssets();

            var message = new StringBuilder();
            message.AppendLine($"[ArknightsACT/PRTS Audio] {label} 下载结束。");
            message.AppendLine($"成功：{success.Count}，跳过：{skipped.Count}，失败：{failed.Count}。");
            if (success.Count > 0)
                message.AppendLine("成功：" + string.Join("、", success));
            if (skipped.Count > 0)
                message.AppendLine("已存在：" + string.Join("、", skipped));
            if (failed.Count > 0)
            {
                message.AppendLine("失败：");
                foreach (var item in failed)
                    message.AppendLine("- " + item);
            }
            message.AppendLine("音频目录：Assets/_Game/Art/Audio/PRTS（.gitignore 已排除）。");
            message.AppendLine("全部所需音频准备好后，执行 ArknightsACT > Build Prototype Scene。");
            Debug.Log(message.ToString());
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
                var candidate = asset.RemoteUrls[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                try
                {
                    if (candidate.StartsWith("msr:", StringComparison.OrdinalIgnoreCase))
                    {
                        var songId = candidate.Substring("msr:".Length).Trim();
                        return await DownloadMonsterSirenSong(client, songId, asset.LocalPath);
                    }

                    await DownloadMediaUrl(client, candidate, asset.LocalPath);
                    return candidate;
                }
                catch (Exception exception)
                {
                    errors.Add($"{candidate}: {exception.Message}");
                }
            }

            throw new InvalidOperationException("All candidate URLs failed: " + string.Join(" | ", errors));
        }

        private static async Task<string> DownloadMonsterSirenSong(
            HttpClient client,
            string songId,
            string localPath)
        {
            if (string.IsNullOrWhiteSpace(songId))
                throw new InvalidOperationException("Monster Siren song id is empty.");

            var apiUrl = MonsterSirenSongApi + songId;
            using var apiResponse = await client.GetAsync(apiUrl);
            if (!apiResponse.IsSuccessStatusCode)
                throw new InvalidOperationException($"Monster Siren API returned {(int)apiResponse.StatusCode} for {apiUrl}");

            var json = await apiResponse.Content.ReadAsStringAsync();
            var payload = JsonUtility.FromJson<MonsterSirenSongResponse>(json);
            if (payload == null || payload.code != 0 || payload.data == null || string.IsNullOrWhiteSpace(payload.data.sourceUrl))
                throw new InvalidOperationException("Monster Siren API did not return a usable sourceUrl.");

            await DownloadMediaUrl(client, payload.data.sourceUrl, localPath);
            return payload.data.sourceUrl;
        }

        private static async Task DownloadMediaUrl(HttpClient client, string url, string localPath)
        {
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {url}");

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes == null || bytes.Length < 128)
                throw new InvalidOperationException($"empty/invalid response {url}");

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (LooksLikeTextOrHtml(mediaType, bytes))
                throw new InvalidOperationException($"non-audio response ({mediaType}) {url}");

            File.WriteAllBytes(localPath, bytes);
        }

        private static bool LooksLikeTextOrHtml(string mediaType, byte[] bytes)
        {
            if (!string.IsNullOrWhiteSpace(mediaType))
            {
                var lowerType = mediaType.ToLowerInvariant();
                if (lowerType.StartsWith("text/") || lowerType.Contains("html") || lowerType.Contains("json"))
                    return true;
            }

            var length = Math.Min(bytes.Length, 96);
            var prefix = Encoding.UTF8.GetString(bytes, 0, length).TrimStart().ToLowerInvariant();
            return prefix.StartsWith("<!doctype") ||
                   prefix.StartsWith("<html") ||
                   prefix.StartsWith("<?xml") ||
                   prefix.StartsWith("{");
        }

        private static bool IsValidLocalFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                return new FileInfo(path).Length >= 128;
            }
            catch
            {
                return false;
            }
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
                "Reference page: " + asset.SourcePage + Environment.NewLine +
                "Resolved media: " + selectedUrl + Environment.NewLine +
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