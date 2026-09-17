#if UNITY_EDITOR
using System;
using System.Reflection;
using ArknightsACT.Editor.PRTS;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Single supported ArknightsACT editor menu. Old prototype MenuItem attributes are made inert by
    /// ArknightsActLegacyMenuItem.cs, so they can no longer reappear after a Unity domain reload.
    /// </summary>
    internal static class ArknightsActMenuCleanup
    {
        [UnityEditor.MenuItem("ArknightsACT/Build Prototype Scene", false, 10)]
        private static void BuildPrototypeScene()
        {
            // The combat Spine already contains Ch'en's authored Attack / Skill_2 / Skill_3 effects.
            // Refresh its special blend-mode materials before composing the scene so those original
            // attachments render at full intensity instead of being replaced by synthetic VFX.
            PrtsOriginalSpineFxSetup.PrepareDownloadedChen();
            PrototypeSceneBuilder.Build();
        }

        [UnityEditor.MenuItem("ArknightsACT/Assets/PRTS/Download Prototype Models", false, 100)]
        private static void DownloadPrototypeModels()
        {
            InvokeHidden(typeof(PrtsSpineSourceDownloader), "DownloadFullPrototypePack");
        }

        [UnityEditor.MenuItem("ArknightsACT/Assets/PRTS/Build Presentation Prefabs", false, 110)]
        private static void BuildPresentationPrefabs()
        {
            PrtsSpinePrefabBuilder.BuildAll();
            PrtsOriginalSpineFxSetup.PrepareDownloadedChen();
        }

        [UnityEditor.MenuItem("ArknightsACT/Assets/PRTS/Download Gameplay Audio", false, 120)]
        private static void DownloadGameplayAudio()
        {
            PrtsGameplayAudioDownloader.DownloadLegacyEntry();
        }

        [UnityEditor.MenuItem("ArknightsACT/Assets/PRTS/Verify Gameplay Audio", false, 121)]
        private static void VerifyGameplayAudio()
        {
            PrtsGameplayAudioDownloader.VerifyLocalAudio();
        }

        [UnityEditor.MenuItem("ArknightsACT/Assets/OHMS Effect Importer", false, 130)]
        private static void OpenOhmsEffectImporter()
        {
            OHMS.OhmsStructuredFxImporterWindow.Open();
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