#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    internal static class PrtsPresentationSetupValidator
    {
        [MenuItem("ArknightsACT/Assets/PRTS/4. Validate Presentation Setup")]
        public static void ValidateSetup()
        {
            var lines = new List<string>();
            var runtime = FindType("Spine.Unity.SkeletonAnimation") != null;
            lines.Add("Spine runtime: " + (runtime ? "OK" : "MISSING"));

            var downloaded = 0;
            var generated = 0;
            var pack = PrtsPrototypeAssetCatalog.GetFullPrototypePack();
            foreach (var descriptor in pack)
            {
                var hasSource = Directory.Exists(descriptor.TargetDirectory) &&
                                (File.Exists(Path.Combine(descriptor.TargetDirectory, descriptor.BaseName + ".skel.bytes")) ||
                                 File.Exists(Path.Combine(descriptor.TargetDirectory, descriptor.BaseName + ".json")));
                var hasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrtsSpinePrefabBuilder.GetPrefabPath(descriptor.BaseName)) != null;

                if (hasSource) downloaded++;
                if (hasPrefab) generated++;
                lines.Add($"{descriptor.DisplayName}: source={(hasSource ? "OK" : "missing")}, prefab={(hasPrefab ? "OK" : "missing")}");
            }

            lines.Insert(1, $"PRTS source models: {downloaded}/{pack.Length}");
            lines.Insert(2, $"Generated presentation prefabs: {generated}/{pack.Length}");

            var message = string.Join("\n", lines);
            Debug.Log("[ArknightsACT/PRTS] Setup validation\n" + message);
            EditorUtility.DisplayDialog("PRTS Presentation Setup", message, "OK");
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
    }
}
#endif
