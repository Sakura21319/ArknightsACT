#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Keeps PRTS atlas pages deterministic on import/reimport.
    /// Mirrors the web viewer's linear sampling while disabling mipmaps, compression and
    /// importer downscaling that can soften the source unnecessarily.
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
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 1;
        }
    }
}
#endif
