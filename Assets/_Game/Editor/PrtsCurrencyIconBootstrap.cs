#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// One-time, non-blocking import of the PRTS Longmen Coin icon.
    /// The runtime UI reads only the local Resources asset and never performs network I/O.
    /// </summary>
    public static class PrtsCurrencyIconBootstrap
    {
        private const string AssetPath = "Assets/_Game/Resources/UI/Currency/lmd.png";
        private static readonly string[] SourceUrls =
        {
            "https://media.prts.wiki/f/f2/%E5%9B%BE%E6%A0%87_%E9%BE%99%E9%97%A8%E5%B8%81.png",
            "https://prts.wiki/index.php?title=Special:Redirect/file/%E5%9B%BE%E6%A0%87_%E9%BE%99%E9%97%A8%E5%B8%81.png"
        };

        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            if (File.Exists(ToAbsolutePath(AssetPath)) &&
                AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath) != null)
                return;
            EditorApplication.delayCall += BeginDownload;
        }

        private static void RefreshFromMenu()
        {
            BeginDownload(force: true);
        }

        private static void BeginDownload() => BeginDownload(false);

        private static async void BeginDownload(bool force)
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += () => BeginDownload(force);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var absolute = ToAbsolutePath(AssetPath);
            if (!force && File.Exists(absolute) &&
                AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath) != null)
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? Application.dataPath);

                using var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    AllowAutoRedirect = true
                };
                using var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(20)
                };
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ArknightsACT/1.0");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://prts.wiki/");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

                byte[] bytes = null;
                Exception lastError = null;
                for (var i = 0; i < SourceUrls.Length; i++)
                {
                    try
                    {
                        using var response = await client.GetAsync(SourceUrls[i]);
                        response.EnsureSuccessStatusCode();
                        var candidate = await response.Content.ReadAsByteArrayAsync();
                        if (candidate != null && candidate.Length >= 128)
                        {
                            bytes = candidate;
                            break;
                        }
                    }
                    catch (Exception sourceError)
                    {
                        lastError = sourceError;
                    }
                }

                if (bytes == null)
                    throw new InvalidDataException("PRTS LMD image download failed from all known sources.", lastError);

                await Task.Run(() => File.WriteAllBytes(absolute, bytes));

                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(AssetPath) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
                if (sprite == null)
                    throw new InvalidDataException("Downloaded PRTS LMD icon did not import as a Sprite.");

                Debug.Log("[ArknightsACT/UI] PRTS Longmen Coin icon imported: " + AssetPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArknightsACT/UI] PRTS LMD icon import failed; UI fallback icon will be used. " + exception.Message);
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif
