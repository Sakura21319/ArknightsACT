#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Keeps the downloaded PRTS Spine data authoritative for Ch'en's combat effects.
    ///
    /// Arknights stores the normal-attack / Skill_2 / Skill_3 effect sprites and their animation
    /// timelines inside the combat Spine itself. Several of those slots use Spine blend modes.
    /// The default Spine importer can keep additive slots in the single-batch PMA path; for this
    /// prototype we explicitly materialize Additive / Multiply / Screen replacement materials so
    /// the authored bright slash sprites are rendered with their original blend intent instead of
    /// being replaced by hand-authored Unity LineRenderer effects.
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
                EnableExplicitAdditiveMaterials(skeletonDataAsset);
                RefreshBlendModeMaterials(skeletonDataAsset);
                EditorUtility.SetDirty(skeletonDataAsset);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    "[ArknightsACT/PRTS] Refreshed Ch'en original Spine FX blend materials " +
                    "(Additive / Multiply / Screen). Attack, Skill_2 and Skill_3 keep the authored PRTS attachments.",
                    skeletonDataAsset);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ArknightsACT/PRTS] Could not refresh Ch'en original Spine FX materials: " +
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

            // The Spine importer may not expose the custom ScriptableObject type to Unity's t: query
            // on every runtime version, so keep the same broad fallback used by the prefab builder.
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

        private static void EnableExplicitAdditiveMaterials(UnityEngine.Object skeletonDataAsset)
        {
            var serialized = new SerializedObject(skeletonDataAsset);
            var blendModes = serialized.FindProperty("blendModeMaterials");
            var applyAdditive = blendModes?.FindPropertyRelative("applyAdditiveMaterial");
            if (applyAdditive == null)
                throw new MissingFieldException("SkeletonDataAsset.blendModeMaterials.applyAdditiveMaterial");

            if (!applyAdditive.boolValue)
            {
                applyAdditive.boolValue = true;
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
    }
}
#endif
