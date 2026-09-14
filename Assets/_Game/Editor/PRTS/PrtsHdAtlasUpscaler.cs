#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Local-only prototype upscaler for the low-resolution PRTS chibi atlas pages.
    ///
    /// It keeps an untouched *.original backup, writes a 2x bilinear + mild-unsharp PNG,
    /// and scales pixel coordinates in the matching Spine atlas by the same factor. Skeleton
    /// world geometry is unchanged because atlas region ratios remain identical.
    ///
    /// This cannot invent true source detail; it is a pragmatic readability improvement for
    /// the prototype. Production art should still be replaced with licensed/high-resolution art.
    /// </summary>
    internal static class PrtsHdAtlasUpscaler
    {
        private const int ScaleFactor = 2;
        private const float SharpenStrength = 0.16f;
        internal const string MarkerFileName = "HD_ATLAS_GENERATED.txt";

        [MenuItem("ArknightsACT/Assets/PRTS/2.6 Build 2x Sharp Local Atlases")]
        private static void BuildHdAtlases()
        {
            var success = new List<string>();
            var failed = new List<string>();

            foreach (var descriptor in PrtsPrototypeAssetCatalog.GetFullPrototypePack())
            {
                try
                {
                    if (BuildDescriptor(descriptor))
                        success.Add(descriptor.DisplayName);
                    else
                        failed.Add(descriptor.DisplayName + ": source atlas/png missing");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    failed.Add(descriptor.DisplayName + ": " + exception.GetBaseException().Message);
                }
            }

            AssetDatabase.Refresh();
            PrtsTextureQualityUtility.ApplyToPack(PrtsPrototypeAssetCatalog.GetFullPrototypePack());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var message = $"2x local HD atlas generated for {success.Count} model(s).";
            if (failed.Count > 0)
                message += "\n\nFailed / skipped:\n- " + string.Join("\n- ", failed);
            message += "\n\nNext: run '3. Build Presentation Prefabs', then rebuild Prototype Scene.";

            Debug.Log("[ArknightsACT/PRTS] " + message);
            EditorUtility.DisplayDialog("PRTS Local HD Atlas", message, "OK");
        }

        [MenuItem("ArknightsACT/Assets/PRTS/2.7 Restore Original Local Atlases")]
        private static void RestoreOriginalAtlases()
        {
            var restored = 0;
            foreach (var descriptor in PrtsPrototypeAssetCatalog.GetFullPrototypePack())
            {
                if (!Directory.Exists(descriptor.TargetDirectory))
                    continue;

                var atlasPath = Path.Combine(descriptor.TargetDirectory, descriptor.BaseName + ".atlas.txt");
                var atlasBackup = atlasPath + ".original";
                if (File.Exists(atlasBackup))
                {
                    File.Copy(atlasBackup, atlasPath, true);
                    restored++;
                }

                foreach (var backup in Directory.GetFiles(descriptor.TargetDirectory, "*.png.original", SearchOption.TopDirectoryOnly))
                {
                    var destination = backup.Substring(0, backup.Length - ".original".Length);
                    File.Copy(backup, destination, true);
                }

                var marker = Path.Combine(descriptor.TargetDirectory, MarkerFileName);
                if (File.Exists(marker))
                    File.Delete(marker);
            }

            AssetDatabase.Refresh();
            PrtsTextureQualityUtility.ApplyToPack(PrtsPrototypeAssetCatalog.GetFullPrototypePack());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("PRTS Local HD Atlas", $"Restored original local atlases for {restored} model(s).", "OK");
        }

        private static bool BuildDescriptor(PrtsAssetDescriptor descriptor)
        {
            if (descriptor == null || !Directory.Exists(descriptor.TargetDirectory))
                return false;

            var atlasPath = Path.Combine(descriptor.TargetDirectory, descriptor.BaseName + ".atlas.txt");
            if (!File.Exists(atlasPath))
                return false;

            var atlasBackup = atlasPath + ".original";
            if (!File.Exists(atlasBackup))
                File.Copy(atlasPath, atlasBackup, true);

            var originalAtlas = File.ReadAllText(atlasBackup, Encoding.UTF8);
            var pageNames = ParseAtlasPages(originalAtlas);
            if (pageNames.Count == 0)
                return false;

            foreach (var page in pageNames)
            {
                var pngPath = Path.Combine(descriptor.TargetDirectory, page);
                if (!File.Exists(pngPath))
                    throw new FileNotFoundException("Atlas page missing", pngPath);

                var pngBackup = pngPath + ".original";
                if (!File.Exists(pngBackup))
                    File.Copy(pngPath, pngBackup, true);

                var hdBytes = UpscalePng(File.ReadAllBytes(pngBackup), ScaleFactor, SharpenStrength);
                File.WriteAllBytes(pngPath, hdBytes);
            }

            var scaledAtlas = ScaleAtlasCoordinates(originalAtlas, ScaleFactor);
            File.WriteAllText(atlasPath, scaledAtlas, Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(descriptor.TargetDirectory, MarkerFileName),
                $"Generated by ArknightsACT local prototype tool.\nScale: {ScaleFactor}x\nSharpen: {SharpenStrength:0.00}\n",
                Encoding.UTF8);

            foreach (var page in pageNames)
                AssetDatabase.ImportAsset(Path.Combine(descriptor.TargetDirectory, page).Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(atlasPath.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            return true;
        }

        private static byte[] UpscalePng(byte[] sourceBytes, int factor, float sharpen)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!source.LoadImage(sourceBytes, false))
                throw new InvalidOperationException("Could not decode PRTS PNG.");

            var src = source.GetPixels32();
            var srcWidth = source.width;
            var srcHeight = source.height;
            var dstWidth = srcWidth * factor;
            var dstHeight = srcHeight * factor;
            var scaled = new Color32[dstWidth * dstHeight];

            for (var y = 0; y < dstHeight; y++)
            {
                var sy = ((y + 0.5f) / factor) - 0.5f;
                var y0 = Mathf.Clamp(Mathf.FloorToInt(sy), 0, srcHeight - 1);
                var y1 = Mathf.Min(y0 + 1, srcHeight - 1);
                var ty = Mathf.Clamp01(sy - Mathf.Floor(sy));

                for (var x = 0; x < dstWidth; x++)
                {
                    var sx = ((x + 0.5f) / factor) - 0.5f;
                    var x0 = Mathf.Clamp(Mathf.FloorToInt(sx), 0, srcWidth - 1);
                    var x1 = Mathf.Min(x0 + 1, srcWidth - 1);
                    var tx = Mathf.Clamp01(sx - Mathf.Floor(sx));

                    var c00 = src[y0 * srcWidth + x0];
                    var c10 = src[y0 * srcWidth + x1];
                    var c01 = src[y1 * srcWidth + x0];
                    var c11 = src[y1 * srcWidth + x1];
                    scaled[y * dstWidth + x] = Bilinear(c00, c10, c01, c11, tx, ty);
                }
            }

            var sharpened = sharpen > 0.001f
                ? ApplyCrossSharpen(scaled, dstWidth, dstHeight, sharpen)
                : scaled;

            var destination = new Texture2D(dstWidth, dstHeight, TextureFormat.RGBA32, false, false);
            destination.SetPixels32(sharpened);
            destination.Apply(false, false);
            var result = destination.EncodeToPNG();

            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(destination);
            return result;
        }

        private static Color32 Bilinear(Color32 c00, Color32 c10, Color32 c01, Color32 c11, float tx, float ty)
        {
            var r0 = Mathf.Lerp(c00.r, c10.r, tx);
            var g0 = Mathf.Lerp(c00.g, c10.g, tx);
            var b0 = Mathf.Lerp(c00.b, c10.b, tx);
            var a0 = Mathf.Lerp(c00.a, c10.a, tx);
            var r1 = Mathf.Lerp(c01.r, c11.r, tx);
            var g1 = Mathf.Lerp(c01.g, c11.g, tx);
            var b1 = Mathf.Lerp(c01.b, c11.b, tx);
            var a1 = Mathf.Lerp(c01.a, c11.a, tx);

            return new Color32(
                ToByte(Mathf.Lerp(r0, r1, ty)),
                ToByte(Mathf.Lerp(g0, g1, ty)),
                ToByte(Mathf.Lerp(b0, b1, ty)),
                ToByte(Mathf.Lerp(a0, a1, ty)));
        }

        private static Color32[] ApplyCrossSharpen(Color32[] input, int width, int height, float strength)
        {
            var output = new Color32[input.Length];
            for (var y = 0; y < height; y++)
            {
                var ym = Mathf.Max(0, y - 1);
                var yp = Mathf.Min(height - 1, y + 1);
                for (var x = 0; x < width; x++)
                {
                    var xm = Mathf.Max(0, x - 1);
                    var xp = Mathf.Min(width - 1, x + 1);
                    var center = input[y * width + x];
                    var left = input[y * width + xm];
                    var right = input[y * width + xp];
                    var down = input[ym * width + x];
                    var up = input[yp * width + x];

                    output[y * width + x] = new Color32(
                        SharpenChannel(center.r, left.r, right.r, down.r, up.r, strength),
                        SharpenChannel(center.g, left.g, right.g, down.g, up.g, strength),
                        SharpenChannel(center.b, left.b, right.b, down.b, up.b, strength),
                        SharpenChannel(center.a, left.a, right.a, down.a, up.a, strength * 0.7f));
                }
            }
            return output;
        }

        private static byte SharpenChannel(byte center, byte left, byte right, byte down, byte up, float strength)
        {
            var value = center * (1f + 4f * strength) - strength * (left + right + down + up);
            return ToByte(value);
        }

        private static byte ToByte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);

        private static string ScaleAtlasCoordinates(string atlasText, int factor)
        {
            var scalableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "size", "xy", "orig", "offset", "split", "pad"
            };

            var lines = atlasText.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var trimmed = raw.TrimStart();
                var colon = trimmed.IndexOf(':');
                if (colon <= 0)
                    continue;

                var key = trimmed.Substring(0, colon).Trim();
                if (!scalableKeys.Contains(key))
                    continue;

                var values = trimmed.Substring(colon + 1).Split(',');
                var scaled = new string[values.Length];
                var valid = true;
                for (var j = 0; j < values.Length; j++)
                {
                    if (!int.TryParse(values[j].Trim(), out var number))
                    {
                        valid = false;
                        break;
                    }
                    scaled[j] = (number * factor).ToString();
                }

                if (!valid)
                    continue;

                var indentLength = raw.Length - trimmed.Length;
                var indent = indentLength > 0 ? raw.Substring(0, indentLength) : string.Empty;
                lines[i] = indent + key + ": " + string.Join(",", scaled);
            }

            return string.Join("\n", lines);
        }

        private static List<string> ParseAtlasPages(string atlasText)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = atlasText.Replace("\r\n", "\n").Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains(":") &&
                    seen.Add(line))
                    result.Add(line);
            }
            return result;
        }
    }
}
#endif
