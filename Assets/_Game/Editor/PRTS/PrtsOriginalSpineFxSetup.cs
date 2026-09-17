#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Preserves the downloaded Ch'en character Spine render path.
    ///
    /// The BG/BG1..BG6 regions in the character battle Spine are character-level presentation
    /// attachments, not the independent chen_skill_02_* / chen_skill_03_* battle-effect prefabs.
    /// They are currently reserved for dash presentation. The actual skill slash FX are loaded
    /// separately from original client battle/prefabs/effects assets.
    /// </summary>
    internal static class PrtsOriginalSpineFxSetup
    {
        public static bool PrepareDownloadedChen()
        {
            var descriptor = PrtsPrototypeAssetCatalog.Chen;
            var skeletonDataAsset = FindSkeletonDataAsset(descriptor.TargetDirectory);
            if (skeletonDataAsset == null)
            {
                Debug.LogWarning(
                    "[ArknightsACT/PRTS] Ch'en SkeletonDataAsset is not available yet. " +
                    "Download Prototype Models and build the presentation prefabs first.");
                return false;
            }

            try
            {
                ConfigureNativePmaAdditivePath(skeletonDataAsset);
                RefreshBlendModeMaterials(skeletonDataAsset);
                EditorUtility.SetDirty(skeletonDataAsset);
                AssetDatabase.SaveAssets();

                var atlasFx = DescribeAtlasEffectRegions(descriptor);
                Debug.Log(
                    "[ArknightsACT/PRTS] Ch'en character-level Spine FX prepared with native PMA additive slots. " +
                    "These are not the independent skill slash FX. " + atlasFx,
                    skeletonDataAsset);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ArknightsACT/PRTS] Could not prepare Ch'en character-level Spine FX: " +
                    exception.GetBaseException().Message,
                    skeletonDataAsset);
                return false;
            }
        }

        private static UnityEngine.Object FindSkeletonDataAsset(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !AssetDatabase.IsValidFolder(directory))
                return null;

            var guids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { directory });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null && asset.GetType().FullName == "Spine.Unity.SkeletonDataAsset")
                    return asset;
            }

            guids = AssetDatabase.FindAssets(string.Empty, new[] { directory });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null && asset.GetType().FullName == "Spine.Unity.SkeletonDataAsset")
                    return asset;
            }

            return null;
        }

        private static void ConfigureNativePmaAdditivePath(UnityEngine.Object skeletonDataAsset)
        {
            var serialized = new SerializedObject(skeletonDataAsset);
            var blendModes = serialized.FindProperty("blendModeMaterials");
            var applyAdditive = blendModes?.FindPropertyRelative("applyAdditiveMaterial");
            if (applyAdditive == null)
                throw new MissingFieldException("SkeletonDataAsset.blendModeMaterials.applyAdditiveMaterial");

            if (applyAdditive.boolValue)
            {
                applyAdditive.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void RefreshBlendModeMaterials(UnityEngine.Object skeletonDataAsset)
        {
            var utilityType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Spine.Unity.Editor.BlendModeMaterialsUtility", false))
                .FirstOrDefault(type => type != null);
            if (utilityType == null)
                throw new TypeLoadException("Spine.Unity.Editor.BlendModeMaterialsUtility");

            var assetType = skeletonDataAsset.GetType();
            var method = utilityType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != "UpdateBlendModeMaterials")
                        return false;
                    var parameters = candidate.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(assetType);
                });

            if (method == null)
                throw new MissingMethodException(utilityType.FullName, "UpdateBlendModeMaterials(SkeletonDataAsset)");

            method.Invoke(null, new object[] { skeletonDataAsset });
        }

        private static string DescribeAtlasEffectRegions(PrtsAssetDescriptor descriptor)
        {
            var atlasPath = Path.Combine(descriptor.TargetDirectory, descriptor.BaseName + ".atlas.txt");
            if (!File.Exists(atlasPath))
                return "Atlas character-FX check: atlas text missing.";

            var lines = File.ReadAllLines(atlasPath);
            var regions = lines
                .Select(line => line.Trim())
                .Where(line =>
                    line.Equals("BG", StringComparison.OrdinalIgnoreCase) ||
                    (line.StartsWith("BG", StringComparison.OrdinalIgnoreCase) && !line.Contains(":")))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return regions.Length > 0
                ? "Character atlas BG regions: " + string.Join(", ", regions) + "."
                : "Atlas character-FX check: no BG-family regions found.";
        }
    }
}
#endif
