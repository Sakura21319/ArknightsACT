#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Keeps PRTS atlas pages deterministic on import/reimport.
    /// Original pages use Point to preserve source pixels; generated local 2x HD pages use
    /// Bilinear because they are already upscaled/sharpened and need smoother sub-pixel motion.
    /// </summary>
    internal sealed class PrtsTextureImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                assetPath.IndexOf("/PRTS/", StringComparison.OrdinalIgnoreCase) < 0 ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return;

            if (assetImporter is not TextureImporter importer)
                return;

            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var isHd = !string.IsNullOrWhiteSpace(directory) &&
                       File.Exists(Path.Combine(directory, PrtsHdAtlasUpscaler.MarkerFileName));

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = isHd ? FilterMode.Bilinear : FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 1;
        }
    }
}
#endif
