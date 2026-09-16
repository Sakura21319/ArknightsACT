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
    /// Legacy MenuItem attributes are removed after the editor registers them, and only the current
    /// production workflow is re-exposed with short names.
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

        static ArknightsActMenuCleanup()
        {
            // MenuItem attributes are registered during script reload. Delay one editor tick so the
            // internal RemoveMenuItem call runs after registration has completed.
            EditorApplication.delayCall += RemoveLegacyItems;
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
            InvokeHidden(typeof(PrtsGameplayAudioDownloader), "DownloadLegacyEntry");
        }

        [MenuItem("ArknightsACT/Assets/PRTS/Verify Gameplay Audio", false, 121)]
        private static void VerifyGameplayAudio()
        {
            InvokeHidden(typeof(PrtsGameplayAudioDownloader), "VerifyLocalAudio");
        }

        private static void RemoveLegacyItems()
        {
            var method = typeof(Menu).GetMethod(
                "RemoveMenuItem",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);

            if (method == null)
            {
                Debug.LogWarning("[ArknightsACT/Menu] UnityEditor.Menu.RemoveMenuItem is unavailable; legacy menu entries could not be hidden.");
                return;
            }

            for (var i = 0; i < LegacyMenuItems.Length; i++)
            {
                try
                {
                    method.Invoke(null, new object[] { LegacyMenuItems[i] });
                }
                catch
                {
                    // Some entries may not exist in a given project state. Menu cleanup is best-effort
                    // and should never block compilation or scene building.
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