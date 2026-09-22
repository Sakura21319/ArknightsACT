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
    /// Downloads selected PRTS Chernobog backgrounds/map previews for local prototype reference.
    /// The files are intentionally kept under an ignored PRTS folder and are not redistributed.
    /// </summary>
    internal static class PrtsEnvironmentReferenceDownloader
    {
        private static async void DownloadChernobogReferences()
        {
            var success = new List<string>();
            var failed = new List<string>();

            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ArknightsACT-Prototype/0.4");

                var assets = PrtsEnvironmentReferenceCatalog.All;
                for (var i = 0; i < assets.Length; i++)
                {
                    var asset = assets[i];
                    var cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "ArknightsACT · PRTS 环境素材",
                        $"{asset.DisplayName} ({i + 1}/{assets.Length})",
                        (float)i / assets.Length);
                    if (cancelled)
                    {
                        failed.Add("用户取消后续下载");
                        break;
                    }

                    try
                    {
                        await DownloadOne(client, asset);
                        success.Add(asset.DisplayName);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"PRTS environment reference failed: {asset.DisplayName}\n{exception}");
                        failed.Add($"{asset.DisplayName}: {exception.Message}");
                    }
                }

                AssetDatabase.Refresh();
                for (var i = 0; i < PrtsEnvironmentReferenceCatalog.All.Length; i++)
                    ConfigureTextureImporter(PrtsEnvironmentReferenceCatalog.All[i].LocalPath);
                AssetDatabase.SaveAssets();

                var message = new StringBuilder();
                message.AppendLine("切尔诺伯格环境参考下载完成。");
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
                message.AppendLine("重新执行 ArknightsACT > Build Prototype Scene 后，前三张切尔诺伯格背景会作为远景弱化叠层接入；关卡预览只用于布局参考。所有下载文件位于 .gitignore 排除目录。 ");
                EditorUtility.DisplayDialog("PRTS 环境素材", message.ToString(), "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static async Task DownloadOne(HttpClient client, PrtsEnvironmentReference asset)
        {
            var directory = Path.GetDirectoryName(asset.LocalPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using var response = await client.GetAsync(asset.RemoteUrl);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(asset.LocalPath, bytes);

            var sourcePath = Path.Combine(directory ?? string.Empty, Path.GetFileNameWithoutExtension(asset.LocalPath) + "_SOURCE.txt");
            File.WriteAllText(
                sourcePath,
                "Display name: " + asset.DisplayName + Environment.NewLine +
                "PRTS page: " + asset.SourcePage + Environment.NewLine +
                "PRTS media: " + asset.RemoteUrl + Environment.NewLine +
                "Runtime backdrop: " + asset.RuntimeBackdrop + Environment.NewLine +
                "Local prototype/reference use only. Do not commit or redistribute from this repository." + Environment.NewLine,
                Encoding.UTF8);
        }

        private static void ConfigureTextureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
#endif
