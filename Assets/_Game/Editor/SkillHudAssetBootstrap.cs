#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.EditorTools
{
    /// <summary>
    /// Legacy local skill-icon helper. Automatic execution is disabled because the combat HUD
    /// now waits for manually supplied original icon assets instead of substituting FX frames.
    /// </summary>
    public static class SkillHudAssetBootstrap
    {
        private const string TargetFolder = "Assets/_Game/Resources/UI/Skills";
        private const string Skill1Target = TargetFolder + "/chen_skill1.png";
        private const string Skill2Target = TargetFolder + "/chen_skill2.png";

        private const string Skill1Fallback =
            "Assets/_Game/Art/FX/Extracted/Chen/Frames/skill_02_start/f0025.png";
        private const string Skill2Fallback =
            "Assets/_Game/Art/FX/Extracted/Chen/Frames/skill_03_start/f0020.png";

        private static void RefreshLocalSkillIcons()
        {
            if (File.Exists(Skill1Target))
                AssetDatabase.DeleteAsset(Skill1Target);
            if (File.Exists(Skill2Target))
                AssetDatabase.DeleteAsset(Skill2Target);
            EnsureLocalSkillIcons();
        }

        private static void EnsureLocalSkillIcons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EnsureFolder("Assets/_Game/Resources/UI");
            EnsureFolder(TargetFolder);

            EnsureOne(
                Skill1Target,
                Skill1Fallback,
                new[] { "badao", "chixiao", "skill_02", "skill2" });

            EnsureOne(
                Skill2Target,
                Skill2Fallback,
                new[] { "jueying", "skill_03", "skill3" });

            AssetDatabase.SaveAssets();
        }

        private static void EnsureOne(string target, string fallback, IReadOnlyList<string> slotTokens)
        {
            if (File.Exists(target))
            {
                ConfigureSprite(target);
                return;
            }

            var source = FindBestExistingIcon(slotTokens);
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                source = fallback;

            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                Debug.LogWarning($"[ArknightsACT/HUD] No local skill icon source found for {target}.");
                return;
            }

            if (!AssetDatabase.CopyAsset(source, target))
            {
                Debug.LogWarning($"[ArknightsACT/HUD] Failed to copy local skill icon: {source} -> {target}");
                return;
            }

            ConfigureSprite(target);
            Debug.Log($"[ArknightsACT/HUD] Local skill icon ready: {target} (source: {source})");
        }

        private static string FindBestExistingIcon(IReadOnlyList<string> slotTokens)
        {
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("chen t:Texture2D", new[] { "Assets/_Game" }))
                guids.Add(guid);
            foreach (var guid in AssetDatabase.FindAssets("skill t:Texture2D", new[] { "Assets/_Game" }))
                guids.Add(guid);

            string best = null;
            var bestScore = int.MinValue;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var lower = path.ToLowerInvariant();
                if (lower.Contains("/resources/ui/skills/"))
                    continue;

                var score = 0;
                if (lower.Contains("chen")) score += 25;
                if (lower.Contains("skill")) score += 20;
                if (lower.Contains("icon")) score += 120;
                if (lower.Contains("/ui/")) score += 55;
                if (lower.Contains("portrait")) score -= 80;
                if (lower.Contains("/frames/")) score -= 45;
                if (lower.Contains("/fx/")) score -= 20;

                var slotMatched = false;
                for (var i = 0; i < slotTokens.Count; i++)
                {
                    if (!lower.Contains(slotTokens[i]))
                        continue;
                    slotMatched = true;
                    score += i < 2 ? 90 : 50;
                }

                if (!slotMatched || score <= bestScore)
                    continue;
                bestScore = score;
                best = path;
            }

            // Avoid choosing an arbitrary FX texture when there is no convincing local icon match.
            return bestScore >= 100 ? best : null;
        }

        private static void ConfigureSprite(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return;

            var dirty =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Compressed;

            if (dirty)
                importer.SaveAndReimport();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
