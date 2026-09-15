#if UNITY_EDITOR
using System;
using System.Reflection;
using ArknightsACT.Gameplay.Debugging;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PresentationQualityDiagnosticsMenu
    {
        [MenuItem("ArknightsACT/Diagnostics/Set Game View 1920x1080")]
        private static void SetGameView1080p()
        {
            const int width = 1920;
            const int height = 1080;
            const string label = "ArknightsACT 1920x1080";

            try
            {
                var editorAssembly = typeof(UnityEditor.Editor).Assembly;
                var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                var gameViewSizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                var gameViewSizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                var gameViewSizeTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                var groupTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");

                if (gameViewType == null || gameViewSizesType == null || gameViewSizeType == null ||
                    gameViewSizeTypeEnum == null || groupTypeEnum == null)
                    throw new MissingMemberException("Unity GameView editor types were not found.");

                var instanceProperty = gameViewSizesType.GetProperty(
                    "instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                var sizesInstance = instanceProperty?.GetValue(null);
                if (sizesInstance == null)
                    throw new MissingMemberException("GameViewSizes.instance was not found.");

                var standaloneValue = Enum.Parse(groupTypeEnum, "Standalone");
                var getGroup = gameViewSizesType.GetMethod(
                    "GetGroup",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var group = getGroup?.Invoke(sizesInstance, new[] { standaloneValue });
                if (group == null)
                    throw new MissingMemberException("Standalone GameView size group was not found.");

                var groupType = group.GetType();
                var getTotalCount = groupType.GetMethod("GetTotalCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var getGameViewSize = groupType.GetMethod("GetGameViewSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var addCustomSize = groupType.GetMethod("AddCustomSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getTotalCount == null || getGameViewSize == null || addCustomSize == null)
                    throw new MissingMemberException("GameView size group methods were not found.");

                var total = (int)getTotalCount.Invoke(group, null);
                var targetIndex = -1;
                for (var i = 0; i < total; i++)
                {
                    var size = getGameViewSize.Invoke(group, new object[] { i });
                    if (size == null)
                        continue;

                    if (ReadInt(size, "width") == width && ReadInt(size, "height") == height)
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex < 0)
                {
                    var fixedResolution = Enum.Parse(gameViewSizeTypeEnum, "FixedResolution");
                    var constructor = gameViewSizeType.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) },
                        null);
                    if (constructor == null)
                        throw new MissingMethodException("GameViewSize constructor was not found.");

                    var newSize = constructor.Invoke(new object[] { fixedResolution, width, height, label });
                    addCustomSize.Invoke(group, new[] { newSize });
                    total = (int)getTotalCount.Invoke(group, null);
                    targetIndex = total - 1;
                }

                var gameView = EditorWindow.GetWindow(gameViewType);
                var selectedSizeIndex = gameViewType.GetProperty(
                    "selectedSizeIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (selectedSizeIndex != null && selectedSizeIndex.CanWrite)
                {
                    selectedSizeIndex.SetValue(gameView, targetIndex);
                }
                else
                {
                    var callback = gameViewType.GetMethod(
                        "SizeSelectionCallback",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (callback == null)
                        throw new MissingMemberException("GameView resolution selector was not found.");
                    callback.Invoke(gameView, new object[] { targetIndex, null });
                }

                gameView.Repaint();
                Debug.Log(
                    "[ArknightsACT/PresentationQuality] Game View set to fixed 1920x1080. " +
                    "This gives the PRTS Spine presentation a stable 1080p backbuffer instead of Free Aspect panel pixels.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ArknightsACT/PresentationQuality] Could not set the internal Unity Game View resolution automatically: " +
                    exception.GetBaseException().Message +
                    ". Select a fixed 1920x1080 resolution manually from the Game View resolution dropdown.");
            }
        }

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

        private static int ReadInt(object target, string memberName)
        {
            if (target == null)
                return -1;

            var type = target.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                           ?? type.GetProperty(char.ToUpperInvariant(memberName[0]) + memberName.Substring(1),
                               BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(target) is int propertyValue)
                return propertyValue;

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? type.GetField("m_" + char.ToUpperInvariant(memberName[0]) + memberName.Substring(1),
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(target) is int fieldValue ? fieldValue : -1;
        }
    }
}
#endif
