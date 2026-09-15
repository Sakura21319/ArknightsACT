#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypePlayerSettings
    {
        [MenuItem("ArknightsACT/Apply Prototype Player Settings")]
        public static void Apply()
        {
            PlayerSettings.productName = "ArknightsACT";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            // Use a stable 1080p prototype backbuffer. The Editor Game View must still be
            // switched to a fixed 1920x1080 size separately; Free Aspect follows dock pixels.
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("ArknightsACT PlayerSettings applied: Windowed 1920x1080, resizable.");
        }
    }
}
#endif
