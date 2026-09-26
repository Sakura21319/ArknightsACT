#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArknightsACT.Editor.Effects;
using ArknightsACT.Editor.PRTS;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal sealed class LocalOperatorSpriteImport
    {
        internal string SourceRelativePath { get; }
        internal string TargetAssetPath { get; }

        internal LocalOperatorSpriteImport(string sourceRelativePath, string targetAssetPath)
        {
            SourceRelativePath = sourceRelativePath;
            TargetAssetPath = targetAssetPath;
        }
    }

    internal sealed class LocalOperatorSkinImportPlan
    {
        internal string SkinId { get; }
        internal PrtsAssetDescriptor Combat { get; }
        internal PrtsAssetDescriptor Motion { get; }
        internal IReadOnlyList<LocalOperatorSpriteImport> Sprites { get; }
        internal IReadOnlyList<string> FxTags { get; }

        internal LocalOperatorSkinImportPlan(
            string skinId,
            PrtsAssetDescriptor combat,
            PrtsAssetDescriptor motion,
            IEnumerable<LocalOperatorSpriteImport> sprites,
            IEnumerable<string> fxTags)
        {
            SkinId = string.IsNullOrWhiteSpace(skinId) ? "default" : skinId;
            Combat = combat;
            Motion = motion;
            Sprites = (sprites ?? Array.Empty<LocalOperatorSpriteImport>()).ToArray();
            FxTags = (fxTags ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    internal sealed class LocalOperatorImportPlan
    {
        internal string Label { get; }
        internal string SourceFolder { get; }
        internal string FxPackageName { get; }
        internal IReadOnlyList<LocalOperatorSpriteImport> CommonSprites { get; }
        internal IReadOnlyList<LocalOperatorSkinImportPlan> Skins { get; }
        internal string VoiceSourceRelativePath { get; }
        internal string VoiceTargetAssetRoot { get; }

        internal LocalOperatorImportPlan(
            string label,
            string sourceFolder,
            string fxPackageName,
            IEnumerable<LocalOperatorSpriteImport> commonSprites,
            IEnumerable<LocalOperatorSkinImportPlan> skins,
            string voiceSourceRelativePath = null,
            string voiceTargetAssetRoot = null)
        {
            Label = label;
            SourceFolder = sourceFolder;
            FxPackageName = fxPackageName;
            CommonSprites = (commonSprites ?? Array.Empty<LocalOperatorSpriteImport>()).ToArray();
            Skins = (skins ?? Array.Empty<LocalOperatorSkinImportPlan>()).ToArray();
            VoiceSourceRelativePath = voiceSourceRelativePath ?? string.Empty;
            VoiceTargetAssetRoot = voiceTargetAssetRoot ?? string.Empty;
        }

        internal LocalOperatorSkinImportPlan FindSkin(string skinId)
        {
            var requested = string.IsNullOrWhiteSpace(skinId) ? "default" : skinId;
            for (var i = 0; i < Skins.Count; i++)
            {
                var skin = Skins[i];
                if (skin != null &&
                    string.Equals(skin.SkinId, requested, StringComparison.OrdinalIgnoreCase))
                    return skin;
            }

            return Skins.Count > 0 ? Skins[0] : null;
        }
    }

    internal sealed class LocalOperatorImportSummary
    {
        internal int SkinCount;
        internal int SpineSetsBuilt;
        internal int SpriteCount;
        internal int VoiceCount;
        internal int FxRequested;
        internal int FxRebuilt;
        internal int FxFramesCopied;
    }

    /// <summary>
    /// Data-driven presentation importer shared by local playable-operator packages.
    /// It deliberately stops before gameplay mapping and manual FX offset tuning.
    /// </summary>
    internal static class LocalOperatorPresentationImporter
    {
        internal static LocalOperatorImportSummary Import(
            LocalOperatorImportPlan plan,
            string skinId,
            bool force,
            bool importAllSkins)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            if (!LocalOperatorAssetImportUtility.EnsureCharacterSource(
                    plan.SourceFolder,
                    plan.Label,
                    out var sourceRoot))
                return new LocalOperatorImportSummary();

            var selectedSkins = importAllSkins
                ? plan.Skins.Where(x => x != null).ToArray()
                : new[] { plan.FindSkin(skinId) }.Where(x => x != null).ToArray();

            if (selectedSkins.Length == 0)
                throw new InvalidOperationException(plan.Label + " has no importable skin plan.");

            var summary = new LocalOperatorImportSummary { SkinCount = selectedSkins.Length };
            var sourceSpine = Path.Combine(sourceRoot, "spine");
            var buildRequests = new List<(PrtsAssetDescriptor descriptor, bool sourceChanged)>();

            for (var i = 0; i < selectedSkins.Length; i++)
            {
                var skin = selectedSkins[i];

                bool motionChanged = false;
                if (skin.Motion != null)
                {
                    motionChanged = LocalOperatorAssetImportUtility.CopySpineSet(
                        sourceSpine,
                        skin.Motion,
                        force);
                }

                if (skin.Combat != null)
                {
                    var combatChanged = skin.Motion != null
                        ? LocalOperatorAssetImportUtility.CopyCombatSpineSetWithHighResolutionMotionAtlas(
                            sourceSpine,
                            skin.Combat,
                            skin.Motion,
                            force)
                        : LocalOperatorAssetImportUtility.CopySpineSet(
                            sourceSpine,
                            skin.Combat,
                            force);

                    buildRequests.Add((skin.Combat, combatChanged));
                }

                if (skin.Motion != null)
                    buildRequests.Add((skin.Motion, motionChanged));
            }

            for (var i = 0; i < plan.CommonSprites.Count; i++)
            {
                var sprite = plan.CommonSprites[i];
                if (ImportSprite(sourceRoot, sprite, force))
                    summary.SpriteCount++;
            }

            for (var skinIndex = 0; skinIndex < selectedSkins.Length; skinIndex++)
            {
                var sprites = selectedSkins[skinIndex].Sprites;
                for (var spriteIndex = 0; spriteIndex < sprites.Count; spriteIndex++)
                {
                    if (ImportSprite(sourceRoot, sprites[spriteIndex], force))
                        summary.SpriteCount++;
                }
            }

            summary.VoiceCount = ImportVoices(plan, sourceRoot, force);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            for (var i = 0; i < buildRequests.Count; i++)
            {
                var request = buildRequests[i];
                if (LocalOperatorAssetImportUtility.BuildPresentation(
                        request.descriptor,
                        force,
                        request.sourceChanged))
                    summary.SpineSetsBuilt++;
            }

            var fxTags = selectedSkins
                .SelectMany(x => x.FxTags)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            summary.FxRequested = fxTags.Length;

            if (fxTags.Length > 0)
            {
                var fx = LocalOperatorAssetImportUtility.ImportFxTags(
                    sourceRoot,
                    plan.FxPackageName,
                    fxTags,
                    force);
                summary.FxRebuilt = fx.ImportedEffects;
                summary.FxFramesCopied = fx.ImportedFrames;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log(
                $"[ArknightsACT/LocalImport] {plan.Label}: skins={summary.SkinCount}, " +
                $"Spine rebuilt={summary.SpineSetsBuilt}, sprites changed={summary.SpriteCount}, " +
                $"voices changed={summary.VoiceCount}, FX requested={summary.FxRequested}, " +
                $"FX rebuilt={summary.FxRebuilt}, frames copied={summary.FxFramesCopied}.");

            return summary;
        }

        private static bool ImportSprite(
            string sourceRoot,
            LocalOperatorSpriteImport sprite,
            bool force)
        {
            if (sprite == null ||
                string.IsNullOrWhiteSpace(sprite.SourceRelativePath) ||
                string.IsNullOrWhiteSpace(sprite.TargetAssetPath))
                return false;

            return LocalOperatorAssetImportUtility.CopySprite(
                Path.Combine(
                    sourceRoot,
                    sprite.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)),
                sprite.TargetAssetPath,
                force);
        }

        private static int ImportVoices(
            LocalOperatorImportPlan plan,
            string sourceRoot,
            bool force)
        {
            if (string.IsNullOrWhiteSpace(plan.VoiceSourceRelativePath) ||
                string.IsNullOrWhiteSpace(plan.VoiceTargetAssetRoot))
                return 0;

            var sourceDirectory = Path.Combine(
                sourceRoot,
                plan.VoiceSourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(sourceDirectory))
                return 0;

            var changed = 0;
            foreach (var source in Directory.GetFiles(sourceDirectory, "*.wav", SearchOption.TopDirectoryOnly))
            {
                if (LocalOperatorAssetImportUtility.CopyRaw(
                        source,
                        plan.VoiceTargetAssetRoot.TrimEnd('/') + "/" + Path.GetFileName(source),
                        force))
                    changed++;
            }

            return changed;
        }
    }
}
#endif
