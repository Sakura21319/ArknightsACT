#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// PRTS chibi atlases are relatively low-resolution source art. Bilinear filtering makes
    /// them look soft when enlarged in a 1080p ACT camera, so the prototype defaults to Point
    /// sampling for a crisper result. Mipmaps/compression/downscaling stay disabled.
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

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 1;
        }
    }
}
#endif
