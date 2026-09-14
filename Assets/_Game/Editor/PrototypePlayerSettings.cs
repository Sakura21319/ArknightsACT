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
            // Keep the prototype windowed, but give Spine chibis enough real screen pixels.
            // 1280x720 made a ~1.6-unit character occupy too few pixels on modern displays.
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("ArknightsACT PlayerSettings applied: Windowed 1600x900, resizable.");
        }
    }
}
#endif
