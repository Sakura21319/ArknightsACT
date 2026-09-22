#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    internal static class PrtsSpinePrefabBuilder
    {
        internal const string GeneratedPrefabDirectory = "Assets/_Game/Generated/PRTS/Prefabs";

        public static void BuildAll()
        {
            var skeletonAnimationType = FindType("Spine.Unity.SkeletonAnimation");
            if (skeletonAnimationType == null)
            {
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "Spine.Unity.SkeletonAnimation is not available. Run '1. Install Spine 3.8-Compatible Runtime' first and wait for Unity to finish recompiling.",
                    "OK");
                return;
            }

            EnsureFolder(GeneratedPrefabDirectory);
            ForceImportDownloadedSources();
            AssetDatabase.Refresh();

            var built = 0;
            var failed = new List<string>();
            var pack = PrtsPrototypeAssetCatalog.GetFullPrototypePack();
            for (var i = 0; i < pack.Length; i++)
            {
                var descriptor = pack[i];
                EditorUtility.DisplayProgressBar(
                    "ArknightsACT",
                    "Building " + descriptor.DisplayName,
                    (float)i / Mathf.Max(1, pack.Length));

                if (TryBuild(descriptor, skeletonAnimationType, out var error))
                    built++;
                else
                    failed.Add(descriptor.DisplayName + ": " + error);
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var message = $"Built {built}/{pack.Length} PRTS presentation prefabs.";
            if (failed.Count > 0)
                message += "\n\nNot built:\n- " + string.Join("\n- ", failed);

            Debug.Log("[ArknightsACT/PRTS] " + message);
            EditorUtility.DisplayDialog("ArknightsACT", message, "OK");
        }

        public static bool TryBuild(PrtsAssetDescriptor descriptor, Type skeletonAnimationType, out string error)
        {
            error = string.Empty;
            var skeletonDataAsset = FindSkeletonDataAsset(descriptor);
            if (skeletonDataAsset == null)
            {
                error = "SkeletonDataAsset not found. Download the PRTS pack and ensure the Spine 3.8-compatible importer completed successfully.";
                return false;
            }

            GameObject root = null;
            try
            {
                root = new GameObject("PRTS_" + descriptor.BaseName);
                root.transform.localScale = Vector3.one;

                var visual = new GameObject("SpineVisual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * descriptor.SafeInitialScale;

                var skeletonComponent = visual.AddComponent(skeletonAnimationType);
                if (!AssignSkeletonDataAsset(skeletonComponent, skeletonDataAsset))
                {
                    error = "Could not assign SkeletonDataAsset to SkeletonAnimation.";
                    return false;
                }

                InvokeInitialize(skeletonComponent);

                var animationNames = ReadAnimationNames(skeletonDataAsset);
                var resolved = PrtsAnimationResolver.Resolve(animationNames);

                var presentation = root.AddComponent<SpineCharacterPresentation2D>();
                presentation.SetVisualRoot(visual.transform);
                presentation.Configure(
                    resolved.Idle,
                    resolved.Move,
                    resolved.Attacks,
                    resolved.Skill,
                    resolved.Hit,
                    resolved.Die);

                // Do not measure Renderer.bounds in the editor. Spine mesh bounds are not stable
                // until the runtime has actually rendered frames, and using them here previously
                // allowed a bad measurement to shrink/move every character out of view.
                var layout = root.AddComponent<SpineVisualAutoLayout2D>();
                layout.Configure(
                    visual.transform,
                    descriptor.TargetWorldHeight,
                    descriptor.FeetLocalY,
                    descriptor.SafeInitialScale);
                presentation.SetVisualRoot(visual.transform);

                var renderer = visual.GetComponent<Renderer>() ?? visual.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                    renderer.sortingOrder = descriptor.Role == "Player" ? 30 : 20;

                var path = GetPrefabPath(descriptor.BaseName);
                PrefabUtility.SaveAsPrefabAsset(root, path);

                var animationSummary = animationNames.Count > 0
                    ? string.Join(", ", animationNames)
                    : "<none discovered>";
                Debug.Log(
                    $"[ArknightsACT/PRTS] Built {descriptor.DisplayName} ({descriptor.BaseName}) -> {path}\n" +
                    $"Safe scale: {descriptor.SafeInitialScale:0.###}; target height: {descriptor.TargetWorldHeight:0.00}\n" +
                    $"Animations: {animationSummary}");
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static string GetPrefabPath(string baseName)
        {
            return GeneratedPrefabDirectory + "/" + baseName + ".prefab";
        }

        private static UnityEngine.Object FindSkeletonDataAsset(PrtsAssetDescriptor descriptor)
        {
            if (!AssetDatabase.IsValidFolder(descriptor.TargetDirectory))
                return null;

            var guids = AssetDatabase.FindAssets(descriptor.BaseName, new[] { descriptor.TargetDirectory });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null && asset.GetType().FullName == "Spine.Unity.SkeletonDataAsset")
                    return asset;
            }

            // Some Spine importers generate asset names that do not preserve the original base name.
            guids = AssetDatabase.FindAssets(string.Empty, new[] { descriptor.TargetDirectory });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null && asset.GetType().FullName == "Spine.Unity.SkeletonDataAsset")
                    return asset;
            }

            return null;
        }

        private static bool AssignSkeletonDataAsset(Component skeletonAnimation, UnityEngine.Object skeletonDataAsset)
        {
            var type = skeletonAnimation.GetType();
            var field = type.GetField("skeletonDataAsset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType.IsInstanceOfType(skeletonDataAsset))
            {
                field.SetValue(skeletonAnimation, skeletonDataAsset);
                return true;
            }

            var property = type.GetProperty("SkeletonDataAsset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(skeletonDataAsset))
            {
                property.SetValue(skeletonAnimation, skeletonDataAsset);
                return true;
            }

            return false;
        }

        private static void InvokeInitialize(Component skeletonAnimation)
        {
            var method = skeletonAnimation.GetType().GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);
            method?.Invoke(skeletonAnimation, new object[] { true });
        }

        private static List<string> ReadAnimationNames(UnityEngine.Object skeletonDataAsset)
        {
            var result = new List<string>();
            var method = skeletonDataAsset.GetType().GetMethod(
                "GetSkeletonData",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);
            if (method == null)
                return result;

            var skeletonData = method.Invoke(skeletonDataAsset, new object[] { true });
            if (skeletonData == null)
                return result;

            var animationsProperty = skeletonData.GetType().GetProperty("Animations", BindingFlags.Instance | BindingFlags.Public);
            if (animationsProperty?.GetValue(skeletonData) is not IEnumerable animations)
                return result;

            foreach (var animation in animations)
            {
                if (animation == null)
                    continue;

                var nameProperty = animation.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
                var name = nameProperty?.GetValue(animation) as string;
                if (!string.IsNullOrWhiteSpace(name))
                    result.Add(name);
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void ForceImportDownloadedSources()
        {
            foreach (var descriptor in PrtsPrototypeAssetCatalog.GetFullPrototypePack())
            {
                if (!Directory.Exists(descriptor.TargetDirectory))
                    continue;

                var files = Directory.GetFiles(descriptor.TargetDirectory, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith("SOURCE.txt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var assetPath = file.Replace('\\', '/');
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
