#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Keeps locally downloaded PRTS atlas pages sharp whenever Unity imports/reimports them.
    /// The source art is not pixel art, so Bilinear stays enabled; blur-inducing mipmaps,
    /// downscaling and compression are disabled instead.
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
