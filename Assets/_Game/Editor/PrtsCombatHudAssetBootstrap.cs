#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace ArknightsACT.EditorTools
{
    /// <summary>
    /// Imports Chen's already-unpacked local HUD assets from D:\Ark into the Unity project.
    /// No network access is used.
    /// </summary>
    public static class PrtsCombatHudAssetBootstrap
    {
        private const string SourceIcons = @"D:\Ark\_Unpacked\chen\icons";
        private const string SourceReady = @"D:\Ark\_Unpacked\chen\ui_assets\battle_skill_ready";
        private const string SourceAvatars = @"D:\Ark\_Unpacked\chen\ui_assets\avatars";

        private const string AvatarSource = SourceAvatars + @"\char_010_chen.png";
        private const string Skill1Source = SourceIcons + @"\skill_icon_skchr_chen_2.png";
        private const string Skill2Source = SourceIcons + @"\skill_icon_skchr_chen_3.png";
        private const string ReadyMarkSource = SourceReady + @"\sprite_skill_ready__-3542339109505237889.png";
        private const string ReadyPulseSource = SourceReady + @"\sprite_skill_bg.png";
        private const string ReadyCircleSource = SourceReady + @"\sprite_circle.png";

        private const string AvatarTarget = "Assets/_Game/Resources/UI/HUD/chen_avatar.png";
        private const string Skill1Target = "Assets/_Game/Resources/UI/Skills/chen_badao.png";
        private const string Skill2Target = "Assets/_Game/Resources/UI/Skills/chen_jueying.png";
        private const string ReadyMarkTarget = "Assets/_Game/Resources/UI/HUD/BattleSkillReady/sprite_skill_ready.png";
        private const string ReadyPulseTarget = "Assets/_Game/Resources/UI/HUD/BattleSkillReady/sprite_skill_bg.png";
        private const string ReadyCircleTarget = "Assets/_Game/Resources/UI/HUD/BattleSkillReady/sprite_circle.png";

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += () => ImportAll(false);
        }

        private static void ImportFromMenu() => ImportAll(true);

        private static void ImportAll(bool force)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            CopyAndImportExact(AvatarSource, AvatarTarget, force, "Chen avatar");
            CopyAndImportExact(Skill1Source, Skill1Target, force, "Chen S2 / 赤霄·拔刀");
            CopyAndImportExact(Skill2Source, Skill2Target, force, "Chen S3 / 赤霄·绝影");
            CopyAndImportExact(ReadyMarkSource, ReadyMarkTarget, force, "battle ready mark");
            CopyAndImportExact(ReadyPulseSource, ReadyPulseTarget, force, "battle ready pulse");
            CopyAndImportExact(ReadyCircleSource, ReadyCircleTarget, force, "auto-skill ready circle");

            AssetDatabase.SaveAssets();
        }

        private static void CopyAndImportExact(string source, string targetAssetPath, bool force, string label)
        {
            if (!File.Exists(source))
            {
                Debug.LogWarning($"[ArknightsACT/HUD] Missing local {label}: {source}");
                return;
            }

            Debug.Log($"[ArknightsACT/HUD] Exact source for {label}: {source}");
            CopyAndImport(source, targetAssetPath, force);
        }

        private static void CopyAndImport(string source, string targetAssetPath, bool force)
        {
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                return;

            var targetAbsolute = ToAbsolutePath(targetAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetAbsolute) ?? Application.dataPath);

            var needsCopy = force || !File.Exists(targetAbsolute) ||
                            File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(targetAbsolute) ||
                            new FileInfo(source).Length != new FileInfo(targetAbsolute).Length;
            if (needsCopy)
                File.Copy(source, targetAbsolute, true);

            AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(targetAssetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif
