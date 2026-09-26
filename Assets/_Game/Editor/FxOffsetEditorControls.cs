#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Shared direction-specific FX offset controls. New operator tuning windows should use
    /// this control so manual values and sliders keep the same default range.
    /// </summary>
    internal static class FxOffsetEditorControls
    {
        internal const float DefaultLimit = 8f;

        internal static bool DrawOffsetFields(string title, ref Vector2 offset)
        {
            var changed = false;
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            changed |= DrawAxis("X", ref offset.x);
            changed |= DrawAxis("Y", ref offset.y);
            return changed;
        }

        private static bool DrawAxis(string axis, ref float value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(axis, GUILayout.Width(16f));
            var next = EditorGUILayout.FloatField(
                Mathf.Clamp(value, -DefaultLimit, DefaultLimit),
                GUILayout.Width(64f));
            next = GUILayout.HorizontalSlider(next, -DefaultLimit, DefaultLimit);
            EditorGUILayout.EndHorizontal();

            next = Mathf.Clamp(next, -DefaultLimit, DefaultLimit);
            if (Mathf.Approximately(value, next))
                return false;

            value = next;
            return true;
        }
    }
}
#endif
