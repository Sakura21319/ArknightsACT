using UnityEngine;

namespace ArknightsACT.Gameplay.Bootstrap
{
    /// <summary>
    /// Keeps prototype standalone builds convenient for desktop testing.
    /// Editor Game view is intentionally left untouched.
    /// </summary>
    public static class RuntimeDisplayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureDisplay()
        {
#if !UNITY_EDITOR
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed, 60);
#endif
            Application.runInBackground = true;
        }
    }
}
