#if UNITY_EDITOR
using ArknightsACT.Gameplay.Debugging;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PresentationQualityDiagnosticsMenu
    {
        [MenuItem("ArknightsACT/Diagnostics/Log Presentation Quality Now")]
        private static void LogNow()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "[ArknightsACT/PresentationQuality] Enter Play Mode first, then run " +
                    "ArknightsACT > Diagnostics > Log Presentation Quality Now.");
                return;
            }

            PresentationQualityDiagnostics2D.LogNow();
        }
    }
}
#endif
