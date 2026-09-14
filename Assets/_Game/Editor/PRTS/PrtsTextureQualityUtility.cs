#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// PRTS chibi atlases are screen-space 2D art. Unity's generic texture defaults can enable
    /// mipmaps and platform compression, which makes the already-small source art look soft.
    /// Keep high-quality bilinear sampling but remove mip/downsample/compression blur.
    /// </summary>
    internal static class PrtsTextureQualityUtility
    {
        [MenuItem("ArknightsACT/Assets/PRTS/2.5 Apply High Quality Texture Settings")]
        public static void ApplyToPrototypePack()
        {
            var changed = ApplyToPack(PrtsPrototypeAssetCatalog.GetFullPrototypePack());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArknightsACT/PRTS] High-quality texture settings applied to {changed} atlas texture(s).");
        }

        public static int ApplyToPack(PrtsAssetDescriptor[] descriptors)
        {
            var changed = 0;
            if (descriptors == null)
                return changed;

            foreach (var descriptor in descriptors)
            {
                if (descriptor == null || !Directory.Exists(descriptor.TargetDirectory))
                    continue;

                var pngFiles = Directory.GetFiles(descriptor.TargetDirectory, "*.png", SearchOption.AllDirectories);
                foreach (var file in pngFiles)
                {
                    var assetPath = file.Replace('\\', '/');
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                        continue;

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
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            return changed;
        }
    }
}
#endif
