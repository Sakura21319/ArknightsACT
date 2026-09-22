#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Imports the approved generated Chernobog-style home background from a temporary base64 payload.
    /// This is a one-time local conversion; runtime reads only the resulting Resources texture.
    /// </summary>
    public static class HomeShellAssetBootstrap
    {
        private const string SourcePath = "Assets/_Game/Editor/Temp/home_chernobog_800.jpg.b64";
        private const string TargetPath = "Assets/_Game/Resources/UI/Shell/home_chernobog.jpg";

        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            EditorApplication.delayCall += ImportIfNeeded;
        }

        private static void ImportFromMenu()
        {
            ImportIfNeeded(force: true);
        }

        private static void ImportIfNeeded() => ImportIfNeeded(false);

        private static void ImportIfNeeded(bool force)
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += ImportIfNeeded;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var targetAbsolute = Absolute(TargetPath);
            if (!force && File.Exists(targetAbsolute) &&
                AssetDatabase.LoadAssetAtPath<Sprite>(TargetPath) != null)
                return;

            var sourceAbsolute = Absolute(SourcePath);
            if (!File.Exists(sourceAbsolute))
            {
                if (!File.Exists(targetAbsolute))
                    Debug.LogWarning("[ArknightsACT/UI] Home background payload is missing: " + SourcePath);
                return;
            }

            try
            {
                var payload = File.ReadAllText(sourceAbsolute).Trim();
                var bytes = Convert.FromBase64String(payload);
                Directory.CreateDirectory(Path.GetDirectoryName(targetAbsolute) ?? Application.dataPath);
                File.WriteAllBytes(targetAbsolute, bytes);

                AssetDatabase.ImportAsset(TargetPath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(TargetPath) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }

                if (AssetDatabase.LoadAssetAtPath<Sprite>(TargetPath) == null)
                    throw new InvalidDataException("Imported home background did not produce a Sprite.");

                AssetDatabase.DeleteAsset(SourcePath);
                AssetDatabase.Refresh();
                Debug.Log("[ArknightsACT/UI] Home background imported: " + TargetPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ArknightsACT/UI] Home background import failed: " + exception);
            }
        }

        private static string Absolute(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif
