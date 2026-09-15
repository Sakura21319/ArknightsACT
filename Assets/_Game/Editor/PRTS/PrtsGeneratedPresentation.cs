#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    internal static class PrtsGeneratedPresentation
    {
        public static bool TryAttach(string baseName, Transform parent, out GameObject instance)
        {
            instance = null;
            if (parent == null || string.IsNullOrWhiteSpace(baseName))
                return false;

            var prefabPath = PrtsSpinePrefabBuilder.GetPrefabPath(baseName);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return false;

            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return false;

            instance.name = "Presentation_" + baseName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return true;
        }
    }
}
#endif
