#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Applies deterministic high-quality import settings to local PRTS atlas pages.
    /// PRTS web viewers render these Spine assets with linear sampling; Point made the Unity
    /// result look blocky and did not restore detail, so the prototype now mirrors that choice.
    /// </summary>
    internal static class PrtsTextureQualityUtility
    {
        public static void ApplyToPrototypePack()
        {
            var changed = ApplyToPack(PrtsPrototypeAssetCatalog.GetFullPrototypePack());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArknightsACT/PRTS] Linear high-quality settings applied to {changed} atlas texture(s). Bilinear, no mipmaps, no compression/downscale.");
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
