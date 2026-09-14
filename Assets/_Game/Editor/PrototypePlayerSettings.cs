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
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("ArknightsACT PlayerSettings applied: Windowed 1280x720, resizable.");
        }
    }
}
#endif
