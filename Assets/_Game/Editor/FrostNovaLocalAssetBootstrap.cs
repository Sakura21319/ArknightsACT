#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArknightsACT.Editor.Effects;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.FrostNova;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Imports four FrostNova presentations from D:\Effect\_output\frostn:
    /// original FrostNova/Winter from spine, plus two additional reskins from spine_new.
    /// Gameplay/FX are shared by skill family, not duplicated per presentation skin.
    /// </summary>
    internal static class FrostNovaLocalAssetBootstrap
    {
        private const string SourceRoot = @"D:\Effect\_output\frostn";
        private const string SourceOriginalSpine = SourceRoot + @"\spine";
        private const string SourceNewSpine = SourceRoot + @"\spine_new";
        private const string SourceFrames = SourceRoot + @"\effects\frames";
        private const string SourceSoundRoot = @"D:\Ark\_Unpacked\frstar2\sound";
        private const string Skill2SfxSource = SourceSoundRoot + @"\e_aoe_frost.wav";
        private const string Skill3StartSfxSource = SourceSoundRoot + @"\e_aoe_frostnovaice_h1.wav";
        private const string Skill3ImpactSfxSource = SourceSoundRoot + @"\e_aoe_frostnovaice_h2.wav";
        private const string AudioTargetRoot = "Assets/_Game/Art/Audio/FrostNova";
        private const string Skill2SfxTarget = AudioTargetRoot + "/FrostNova_IceRing.wav";
        private const string Skill3StartSfxTarget = AudioTargetRoot + "/FrostNova_IceBurst_Start.wav";
        private const string Skill3ImpactSfxTarget = AudioTargetRoot + "/FrostNova_IceBurst_Impact.wav";
        private const string SourceWinterSkill3Frames =
            SourceRoot + @"\spine\frames\enemy_1510_frstar2\Skill_3";
        private const string WinterSkill3FramesAssetDirectory =
            "Assets/_Game/Art/Characters/FrostNova/Local/Winter/Skill3Frames";
        private const string WinterSkill3SequenceAssetPath =
            "Assets/_Game/Resources/Config/FrostNovaWinterSkill3Frames.asset";
        private const string SourcePortrait =
            SourceRoot + @"\portrait\剧情立绘_霜星_罗德岛制服_1024.png";
        private static void ImportAllFromMenu()
        {
            ImportCoreAssets(true);
            foreach (FrostNovaSkinVariant skin in Enum.GetValues(typeof(FrostNovaSkinVariant)))
                BuildPresentationPrefab(skin);

            ImportEffectFamily(false);
            ImportEffectFamily(true);
            FrostNovaExtractedFxSetup.TryApplyToOpenScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "ArknightsACT",
                "霜星 4 套 Spine 与原皮/冬痕两组技能特效已重新导入。",
                "OK");
        }

        internal static void PrepareForBuild(
            FrostNovaSkinVariant skin,
            bool force = false)
        {
            RefreshSelectedAssets(skin, force);
        }

        internal static void RefreshSelectedAssets(
            FrostNovaSkinVariant skin,
            bool force)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            FrostNovaTuningAssetUtility.Ensure();

            // Audio comes from D:\\Ark and must refresh independently from the visual package.
            // Do this before checking D:\\Effect so a missing FX/Spine source cannot suppress sound.
            ImportAudioAssets(force);

            if (!Directory.Exists(SourceRoot))
            {
                Debug.LogWarning("[ArknightsACT/FrostNova] Local visual source is missing: " + SourceRoot);
                return;
            }

            var descriptor = GetDescriptor(skin);
            var sourceSpine = skin.UsesNewSpine() ? SourceNewSpine : SourceOriginalSpine;
            CopySpineSet(descriptor, sourceSpine, force);

            CopySprite(
                SourcePortrait,
                "Assets/_Game/Resources/UI/HUD/Operators/frostnova_default.png",
                force);

            BuildPresentationPrefab(skin);

            if (force || !FrostNovaExtractedFxSetup.HasImported(skin))
                ImportEffectFamily(skin.UsesWinterSkillSet());

            AssetDatabase.SaveAssets();
        }

        internal static void ImportCoreAssets(bool force)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!Directory.Exists(SourceRoot))
            {
                Debug.LogWarning("[ArknightsACT/FrostNova] Local source is missing: " + SourceRoot);
                return;
            }

            foreach (FrostNovaSkinVariant skin in Enum.GetValues(typeof(FrostNovaSkinVariant)))
            {
                var descriptor = GetDescriptor(skin);
                var sourceSpine = skin.UsesNewSpine() ? SourceNewSpine : SourceOriginalSpine;
                CopySpineSet(descriptor, sourceSpine, force);
            }

            // No dedicated skin portraits are present in the package. All four presentations use
            // the same FrostNova portrait until explicit portrait assets are provided.
            CopySprite(
                SourcePortrait,
                "Assets/_Game/Resources/UI/HUD/Operators/frostnova_default.png",
                force);

            EnsureWinterSkill3FrameSequence(force);
            ImportAudioAssets(force);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        internal static void ConfigureAudioProfile(
            GameObject owner,
            FrostNovaSkinVariant skin)
        {
            if (owner == null)
                return;

            var winter = skin.UsesWinterSkillSet();
            var profile = owner.GetComponent<PlayableOperatorAudioProfile>() ??
                          owner.AddComponent<PlayableOperatorAudioProfile>();

            profile.Configure(
                slot1Sfx: winter
                    ? AssetDatabase.LoadAssetAtPath<AudioClip>(Skill2SfxTarget)
                    : null,
                slot2Sfx: winter
                    ? AssetDatabase.LoadAssetAtPath<AudioClip>(Skill3StartSfxTarget)
                    : null,
                slot1Voices: Array.Empty<AudioClip>(),
                slot2Voices: Array.Empty<AudioClip>(),
                attackSwings: Array.Empty<AudioClip>(),
                attackImpact: null,
                optionalSkillSfx: !winter);

            var timedCue = owner.GetComponent<FrostNovaSkillAudioCue>() ??
                           owner.AddComponent<FrostNovaSkillAudioCue>();
            timedCue.Configure(
                AssetDatabase.LoadAssetAtPath<AudioClip>(Skill3ImpactSfxTarget),
                winter);

            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(timedCue);
        }

        private static void ImportAudioAssets(bool force)
        {
            if (!Directory.Exists(SourceSoundRoot))
            {
                Debug.LogWarning(
                    "[ArknightsACT/FrostNova] Local sound source is missing: " +
                    SourceSoundRoot);
                return;
            }

            CopyRaw(Skill2SfxSource, Skill2SfxTarget, force);
            CopyRaw(Skill3StartSfxSource, Skill3StartSfxTarget, force);
            CopyRaw(Skill3ImpactSfxSource, Skill3ImpactSfxTarget, force);
        }

        private static void ImportEffectFamily(bool winter)
        {
            if (!Directory.Exists(SourceFrames))
            {
                Debug.LogWarning("[ArknightsACT/FrostNova] FX frames are missing: " + SourceFrames);
                return;
            }

            var names = winter
                ? new[]
                {
                    "frstar2_attack_01_start",
                    "frstar2_attack_01_trail",
                    "frstar2_attack_01_hit",
                    "frstar2_skill_02_range",
                    "frstar2_skill_03_start",
                    "frstar2_skill_03_range",
                    "frstar2_skill_03_range_02",
                    "frstar2_buff_03_start",
                    "frstar2_buff_04_start",
                    "frstar2_buff_05_start"
                }
                : new[]
                {
                    "firstar_attack_01_start",
                    "firstar_attack_01_trail",
                    "firstar_attack_01_hit",
                    "firstar_skill_01_range",
                    "firstar_skill_01_buff",
                    "firstar_skill_02_start",
                    "firstar_skill_02_trail",
                    "firstar_skill_02_range",
                    "firstar_skill_02_range_02"
                };

            var folders = new string[names.Length];
            for (var i = 0; i < names.Length; i++)
                folders[i] = Path.Combine(SourceFrames, names[i]);

            var result = ExtractedFrameFxImporter.ImportSelected(
                folders,
                ExtractedFrameFxImporter.DefaultOutputRoot,
                "FrostNova");

            Debug.Log(
                $"[ArknightsACT/FrostNova] Imported {(winter ? "Winter" : "Default")} FX: " +
                $"{result.ImportedEffects} sequences / {result.ImportedFrames} frames.");
        }

        private static void BuildPresentationPrefab(FrostNovaSkinVariant skin)
        {
            var skeletonType = FindType("Spine.Unity.SkeletonAnimation");
            if (skeletonType == null)
            {
                Debug.LogWarning(
                    "[ArknightsACT/FrostNova] Spine.Unity.SkeletonAnimation is not available yet.");
                return;
            }

            var descriptor = GetDescriptor(skin);
            if (!PrtsSpinePrefabBuilder.TryBuild(descriptor, skeletonType, out var error))
                Debug.LogWarning(
                    $"[ArknightsACT/FrostNova] Failed to build {descriptor.DisplayName}: {error}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CopySpineSet(
            PrtsAssetDescriptor descriptor,
            string sourceDirectory,
            bool force)
        {
            var baseName = descriptor.BaseName;
            var atlasSource = Path.Combine(sourceDirectory, baseName + ".atlas");
            var skeletonSource = Path.Combine(sourceDirectory, baseName + ".skel");
            if (!File.Exists(atlasSource) || !File.Exists(skeletonSource))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/FrostNova] Missing Spine source for {descriptor.DisplayName}: " +
                    sourceDirectory);
                return;
            }

            Directory.CreateDirectory(ToAbsolutePath(descriptor.TargetDirectory));

            var pages = File.ReadAllLines(atlasSource);
            for (var i = 0; i < pages.Length; i++)
            {
                var line = pages[i].Trim();
                if (!line.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    line.IndexOf(':') >= 0)
                    continue;

                var pageName = Path.GetFileName(line);
                CopyRaw(
                    Path.Combine(sourceDirectory, pageName),
                    descriptor.TargetDirectory + "/" + pageName,
                    force);
            }

            CopyRaw(
                atlasSource,
                descriptor.TargetDirectory + "/" + baseName + ".atlas.txt",
                force);
            CopyRaw(
                skeletonSource,
                descriptor.TargetDirectory + "/" + baseName + ".skel.bytes",
                force);
        }
        internal static void ForceReimportWinterSkill3Frames()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[ArknightsACT/FrostNova] 请先退出 Play Mode，再强制重导 Winter Skill_3。");
                return;
            }

            AssetDatabase.DeleteAsset(WinterSkill3SequenceAssetPath);
            AssetDatabase.DeleteAsset(WinterSkill3FramesAssetDirectory);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            EnsureWinterSkill3FrameSequence(true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var drivers = UnityEngine.Object.FindObjectsByType<FrostNovaPresentationDriver25D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < drivers.Length; i++)
                drivers[i]?.RefreshTuning();

            var sequence =
                AssetDatabase.LoadAssetAtPath<FrostNovaFrameSequenceAsset>(
                    WinterSkill3SequenceAssetPath);
            var count = sequence != null ? sequence.FrameCount : 0;
            Debug.Log(
                $"[ArknightsACT/FrostNova] Winter Skill_3 强制重导完成: {count} frames. " +
                $"Source={SourceWinterSkill3Frames}");
        }

        private static void EnsureWinterSkill3FrameSequence(bool force)
        {
            if (!Directory.Exists(SourceWinterSkill3Frames))
            {
                Debug.LogWarning(
                    "[ArknightsACT/FrostNova] Winter Skill_3 frame source is missing: " +
                    SourceWinterSkill3Frames);
                return;
            }

            Directory.CreateDirectory(ToAbsolutePath(WinterSkill3FramesAssetDirectory));
            Directory.CreateDirectory(ToAbsolutePath("Assets/_Game/Resources/Config"));

            var sourceFiles = Directory.GetFiles(SourceWinterSkill3Frames, "*.png")
                .Where(IsNumberedFrameFile)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (sourceFiles.Length == 0)
            {
                Debug.LogWarning(
                    "[ArknightsACT/FrostNova] Winter Skill_3 frame source is empty: " +
                    SourceWinterSkill3Frames);
                return;
            }

            var sourceNames = new HashSet<string>(
                sourceFiles.Select(Path.GetFileName),
                StringComparer.OrdinalIgnoreCase);
            var absoluteTargetDirectory = ToAbsolutePath(WinterSkill3FramesAssetDirectory);
            foreach (var stale in Directory.GetFiles(absoluteTargetDirectory, "*.png"))
            {
                if (sourceNames.Contains(Path.GetFileName(stale)))
                    continue;

                var staleAssetPath =
                    WinterSkill3FramesAssetDirectory + "/" + Path.GetFileName(stale);
                AssetDatabase.DeleteAsset(staleAssetPath);
            }

            var assetPaths = new string[sourceFiles.Length];
            for (var i = 0; i < sourceFiles.Length; i++)
            {
                var source = sourceFiles[i];
                var assetPath =
                    WinterSkill3FramesAssetDirectory + "/" + Path.GetFileName(source);
                assetPaths[i] = assetPath;
                var absoluteTarget = ToAbsolutePath(assetPath);

                if (force ||
                    !File.Exists(absoluteTarget) ||
                    File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(absoluteTarget) ||
                    new FileInfo(source).Length != new FileInfo(absoluteTarget).Length)
                {
                    File.Copy(source, absoluteTarget, true);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                var assetPath = assetPaths[i];
                if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                    continue;

                var dirty =
                    importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single ||
                    importer.mipmapEnabled ||
                    importer.filterMode != FilterMode.Bilinear ||
                    Mathf.Abs(importer.spritePixelsPerUnit - 128f) > 0.01f;

                if (!dirty)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 512;
                importer.spritePixelsPerUnit = 128f;
                importer.SaveAndReimport();
            }

            var sprites = assetPaths
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .ToArray();
            if (sprites.Length == 0)
                return;

            var sequence =
                AssetDatabase.LoadAssetAtPath<FrostNovaFrameSequenceAsset>(
                    WinterSkill3SequenceAssetPath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<FrostNovaFrameSequenceAsset>();
                AssetDatabase.CreateAsset(sequence, WinterSkill3SequenceAssetPath);
            }

            sequence.Configure(sprites, 30f);
            EditorUtility.SetDirty(sequence);
            Debug.Log(
                $"[ArknightsACT/FrostNova] Bound Winter Skill_3 to original spine frames: " +
                $"{sprites.Length} frames from {SourceWinterSkill3Frames}");
        }

        private static bool IsNumberedFrameFile(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name) ||
                name.Length < 2 ||
                (name[0] != 'f' && name[0] != 'F'))
                return false;

            for (var i = 1; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i]))
                    return false;
            }

            return true;
        }

        private static void CopySprite(
            string source,
            string targetAssetPath,
            bool force)
        {
            if (!CopyRaw(source, targetAssetPath, force))
                return;

            AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(targetAssetPath) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static bool CopyRaw(
            string source,
            string targetAssetPath,
            bool force)
        {
            if (!File.Exists(source))
            {
                Debug.LogWarning("[ArknightsACT/FrostNova] Missing local asset: " + source);
                return false;
            }

            var target = ToAbsolutePath(targetAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? Application.dataPath);

            var needsCopy =
                force ||
                !File.Exists(target) ||
                File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(target) ||
                new FileInfo(source).Length != new FileInfo(target).Length;

            if (needsCopy)
                File.Copy(source, target, true);

            AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        internal static PrtsAssetDescriptor GetDescriptor(FrostNovaSkinVariant skin) =>
            skin switch
            {
                FrostNovaSkinVariant.Winter => PrtsPrototypeAssetCatalog.FrostNovaWinter,
                FrostNovaSkinVariant.DefaultNew => PrtsPrototypeAssetCatalog.FrostNovaDefaultNew,
                FrostNovaSkinVariant.WinterNew => PrtsPrototypeAssetCatalog.FrostNovaWinterNew,
                _ => PrtsPrototypeAssetCatalog.FrostNovaDefault
            };
    }

    internal static class FrostNovaExtractedFxSetup
    {
        private const string Root =
            "Assets/_Game/Art/FX/Extracted/FrostNova/Prefabs";

        internal static bool HasImported(FrostNovaSkinVariant skin) =>
            skin.UsesWinterSkillSet() ? HasWinterImported() : HasDefaultImported();

        internal static bool HasDefaultImported() =>
            Load("firstar_attack_01_start") != null &&
            Load("firstar_attack_01_trail") != null &&
            Load("firstar_attack_01_hit") != null &&
            Load("firstar_skill_01_range") != null &&
            Load("firstar_skill_01_buff") != null &&
            Load("firstar_skill_02_start") != null &&
            Load("firstar_skill_02_trail") != null &&
            Load("firstar_skill_02_range") != null &&
            Load("firstar_skill_02_range_02") != null;

        internal static bool HasWinterImported() =>
            Load("frstar2_attack_01_start") != null &&
            Load("frstar2_attack_01_trail") != null &&
            Load("frstar2_attack_01_hit") != null &&
            Load("frstar2_skill_02_range") != null &&
            Load("frstar2_skill_03_start") != null &&
            Load("frstar2_skill_03_range") != null &&
            Load("frstar2_skill_03_range_02") != null &&
            Load("frstar2_buff_03_start") != null &&
            Load("frstar2_buff_04_start") != null &&
            Load("frstar2_buff_05_start") != null;

        internal static void Configure(
            GameObject owner,
            FrostNovaSkinVariant skin)
        {
            if (owner == null)
                return;

            var controller = owner.GetComponent<FrostNovaExtractedFxController>() ??
                             owner.AddComponent<FrostNovaExtractedFxController>();

            if (skin.UsesWinterSkillSet())
            {
                controller.Configure(
                    skin,
                    Load("frstar2_attack_01_start"),
                    Load("frstar2_attack_01_trail"),
                    Load("frstar2_attack_01_hit"),
                    Load("frstar2_skill_02_range"),
                    null,
                    Load("frstar2_skill_03_start"),
                    null,
                    Load("frstar2_skill_03_range"),
                    Load("frstar2_skill_03_range_02"),
                    Load("frstar2_buff_03_start"),
                    Load("frstar2_buff_04_start"),
                    Load("frstar2_buff_05_start"));
            }
            else
            {
                controller.Configure(
                    skin,
                    Load("firstar_attack_01_start"),
                    Load("firstar_attack_01_trail"),
                    Load("firstar_attack_01_hit"),
                    Load("firstar_skill_01_range"),
                    Load("firstar_skill_01_buff"),
                    Load("firstar_skill_02_start"),
                    Load("firstar_skill_02_trail"),
                    Load("firstar_skill_02_range"),
                    Load("firstar_skill_02_range_02"));
            }

            EditorUtility.SetDirty(controller);
        }

        internal static bool TryApplyToOpenScene()
        {
            var identities = UnityEngine.Object.FindObjectsByType<PlayableOperatorIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            var applied = false;
            for (var i = 0; i < identities.Length; i++)
            {
                var identity = identities[i];
                if (identity == null ||
                    !string.Equals(identity.OperatorId, "FrostNova", StringComparison.OrdinalIgnoreCase))
                    continue;

                var skin = FrostNovaSkinVariantExtensions.FromSkinId(identity.SkinId);
                if (!HasImported(skin))
                    continue;

                Configure(identity.gameObject, skin);
                EditorUtility.SetDirty(identity.gameObject);
                applied = true;
            }

            if (applied && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            return applied;
        }

        private static GameObject Load(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + name + ".prefab");
    }
}
#endif
