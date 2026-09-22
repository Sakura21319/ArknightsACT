#if UNITY_EDITOR
using System;
using System.Reflection;
using ArknightsACT.Editor.PRTS;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Single supported ArknightsACT editor menu. Old prototype MenuItem attributes are made inert by
    /// ArknightsActLegacyMenuItem.cs, so they can no longer reappear after a Unity domain reload.
    /// </summary>
    internal static class ArknightsActMenuCleanup
    {
        private static void BuildPrototypeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "不能在 Play Mode 中构建场景。请先点击 Unity 顶部的停止按钮，再重新执行构建。",
                    "确定");
                Debug.LogWarning("[ArknightsACT/25D] 已阻止 Play Mode 内的场景构建。请先退出 Play Mode。");
                return;
            }

            // The combat Spine already contains Ch'en's authored Attack / Skill_2 / Skill_3 effects.
            // Refresh its special blend-mode materials before composing the scene so those original
            // attachments render at full intensity instead of being replaced by synthetic VFX.
            PrtsOriginalSpineFxSetup.PrepareDownloadedChen();
            PrototypeSceneBuilder.Build();
        }

        private static void DownloadPrototypeModels()
        {
            InvokeHidden(typeof(PrtsSpineSourceDownloader), "DownloadFullPrototypePack");
        }

        private static void BuildPresentationPrefabs()
        {
            PrtsSpinePrefabBuilder.BuildAll();
            PrtsOriginalSpineFxSetup.PrepareDownloadedChen();
        }

        private static void DownloadGameplayAudio()
        {
            PrtsGameplayAudioDownloader.DownloadLegacyEntry();
        }

        private static void VerifyGameplayAudio()
        {
            PrtsGameplayAudioDownloader.VerifyLocalAudio();
        }

        private static void OpenOhmsEffectImporter()
        {
            OHMS.OhmsStructuredFxImporterWindow.Open();
        }

        private static void ImportStagedChenCombatFx()
        {
            OHMS.OhmsStructuredFxBatchCommands.ImportStagedChenCombatFx();
        }

        private static void RepairChenSkill3BladeTextures()
        {
            OHMS.OhmsStructuredFxBatchCommands.RepairChenSkill3BladeTextures();
        }

        private static void OpenExtractedFrameFxImporter()
        {
            Effects.ExtractedFrameFxImporterWindow.Open();
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
