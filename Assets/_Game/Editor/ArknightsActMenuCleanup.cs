#if UNITY_EDITOR
using System;
using System.Reflection;
using ArknightsACT.Editor.PRTS;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Keeps the production menu small while preserving old editor utilities in code for debugging.
    /// Unity rebuilds attribute-backed MenuItem entries during domain/script reload, so a single delayed
    /// removal is not reliable. We repeatedly remove legacy entries for a short post-reload window and
    /// expose only the current production workflow below.
    /// </summary>
    [InitializeOnLoad]
    internal static class ArknightsActMenuCleanup
    {
        private static readonly string[] LegacyMenuItems =
        {
            "ArknightsACT/Build 2.5D Demo Scene",
            "ArknightsACT/Assets/Rebuild Chernobog Modular Kit",
            "ArknightsACT/Assets/Apply Chernobog Floor Composition Assets",
            "ArknightsACT/Assets/Apply Chernobog Material Production Pass",
            "ArknightsACT/Assets/Apply Chernobog Fine Material Detail",
            "ArknightsACT/Assets/Apply Chernobog Production Detail Pass",
            "ArknightsACT/Diagnostics/Dump Ch'en Animation Catalog",

            "ArknightsACT/Assets/PRTS/Download Chernobog Environment References",
            "ArknightsACT/Assets/PRTS/1. Install Spine 3.8-Compatible Runtime",
            "ArknightsACT/Assets/PRTS/Open Runtime Fork on GitHub",
            "ArknightsACT/Assets/PRTS/Download Full Prototype Pack",
            "ArknightsACT/Assets/PRTS/Download Ch'en",
            "ArknightsACT/Assets/PRTS/Download Ch'en Base Motion Source",
            "ArknightsACT/Assets/PRTS/Download Prototype Enemies",
            "ArknightsACT/Assets/PRTS/Download Roguelite Treasure",
            "ArknightsACT/Assets/PRTS/Open Ch'en Source Page",
            "ArknightsACT/Assets/PRTS/2.5 Apply High Quality Texture Settings",
            "ArknightsACT/Assets/PRTS/3. Build Presentation Prefabs",
            "ArknightsACT/Assets/PRTS/4. Validate Presentation Setup",

            "ArknightsACT/Assets/PRTS/Gameplay Audio/Download Missing Only",
            "ArknightsACT/Assets/PRTS/Gameplay Audio/Download All",
            "ArknightsACT/Assets/PRTS/Gameplay Audio/Download BGM",
            "ArknightsACT/Assets/PRTS/Gameplay Audio/Download Combat + Skill SFX",
            "ArknightsACT/Assets/PRTS/Gameplay Audio/Download Chen Voices",
            "ArknightsACT/Assets/PRTS/Gameplay Audio/Verify Local Audio",
            "ArknightsACT/Assets/PRTS/Download Gameplay Audio (BGM + Combat + Chen)",
            "ArknightsACT/Assets/PRTS/Download Gameplay Audio (BGM + Chen)"
        };

        private static readonly MethodInfo RemoveMenuItemMethod = typeof(Menu).GetMethod(
            "RemoveMenuItem",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(string) },
            null);

        private static double _cleanupUntil;
        private static double _nextCleanupAt;
        private static bool _warnedUnavailable;

        static ArknightsActMenuCleanup()
        {
            BeginCleanupWindow();
            AssemblyReloadEvents.afterAssemblyReload += BeginCleanupWindow;
        }

        [MenuItem("ArknightsACT/Assets/PRTS/Download Prototype Models", false, 100)]
        private static void DownloadPrototypeModels()
        {
            InvokeHidden(typeof(PrtsSpineSourceDownloader), "DownloadFullPrototypePack");
        }

        [MenuItem("ArknightsACT/Assets/PRTS/Build Presentation Prefabs", false, 110)]
        private static void BuildPresentationPrefabs()
        {
            PrtsSpinePrefabBuilder.BuildAll();
        }

        [MenuItem("ArknightsACT/Assets/PRTS/Download Gameplay Audio", false, 120)]
        private static void DownloadGameplayAudio()
        {
            PrtsGameplayAudioDownloader.DownloadLegacyEntry();
        }

        [MenuItem("ArknightsACT/Assets/PRTS/Verify Gameplay Audio", false, 121)]
        private static void VerifyGameplayAudio()
        {
            PrtsGameplayAudioDownloader.VerifyLocalAudio();
        }

        private static void BeginCleanupWindow()
        {
            _cleanupUntil = EditorApplication.timeSinceStartup + 20.0d;
            _nextCleanupAt = 0d;
            EditorApplication.update -= MaintainLegacyMenuRemoval;
            EditorApplication.update += MaintainLegacyMenuRemoval;
            EditorApplication.delayCall += RemoveLegacyItems;
        }

        private static void MaintainLegacyMenuRemoval()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now >= _cleanupUntil)
            {
                EditorApplication.update -= MaintainLegacyMenuRemoval;
                return;
            }

            if (now < _nextCleanupAt)
                return;

            _nextCleanupAt = now + 0.5d;
            RemoveLegacyItems();
        }

        private static void RemoveLegacyItems()
        {
            if (RemoveMenuItemMethod == null)
            {
                if (!_warnedUnavailable)
                {
                    _warnedUnavailable = true;
                    Debug.LogWarning("[ArknightsACT/Menu] Unity internal RemoveMenuItem API is unavailable; legacy menu entries could not be hidden.");
                }
                return;
            }

            for (var i = 0; i < LegacyMenuItems.Length; i++)
            {
                try
                {
                    RemoveMenuItemMethod.Invoke(null, new object[] { LegacyMenuItems[i] });
                }
                catch
                {
                    // Best-effort cleanup: some entries do not exist in every editor state.
                }
            }
        }

        private static void InvokeHidden(Type type, string methodName)
        {
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null)
            {
                Debug.LogError($"[ArknightsACT/Menu] Could not find editor action {type.Name}.{methodName}.");
                return;
            }

            method.Invoke(null, null);
        }
    }
}
#endif