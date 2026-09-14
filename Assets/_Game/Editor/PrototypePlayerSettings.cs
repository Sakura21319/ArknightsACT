#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypePlayerSettings
    {
        public static void Apply()
        {
            PlayerSettings.productName = "ArknightsACT";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
        }
    }
}
#endif
