#if UNITY_EDITOR
using System;
using System.IO;
using ArknightsACT.Editor.Effects;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Schwarz;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Local-only bridge from D:\Ark\_Unpacked\shwaz into Unity. No network access is used.
    /// Core sprites/Spine sources are refreshed cheaply on script reload; the large extracted FX
    /// package is imported only from the explicit menu or when a Schwarz scene is built.
    /// </summary>
    internal static class SchwarzLocalAssetBootstrap
    {
        private const string SourceRoot = @"D:\Ark\_Unpacked\shwaz";
        private const string SourceSpine = SourceRoot + @"\spine";
        private const string SourceIcons = SourceRoot + @"\icons";
        private const string SourceAvatars = SourceRoot + @"\ui_assets\avatars";
        private const string SourceEffects = SourceRoot + @"\effects";
        private const string SourceS2CommonFrames = SourceRoot + @"\effects\candidates\black_s2\common_buff\frames";
        private const string SourceSoundBanks = SourceRoot + @"\sound\_banks";
        private const string SourceVoice = SourceRoot + @"\voice\voice_jp";
        private static void ImportAllFromMenu()
        {
            ImportCoreAssets(true);
            BuildPresentationPrefabs(SchwarzSkinVariant.Default);
            BuildPresentationPrefabs(SchwarzSkinVariant.Snow);
            BuildPresentationPrefabs(SchwarzSkinVariant.Striker);
            ImportEffects(force: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "ArknightsACT",
                "黑的原版/皮肤 Spine、头像、技能图标和皮肤特效已从 D:\\Ark\\_Unpacked\\shwaz 导入。",
                "OK");
        }

        internal static void PrepareForBuild(SchwarzSkinVariant skin)
        {
            RefreshSelectedAssets(skin, false);
        }

        internal static void RefreshSelectedAssets(
            SchwarzSkinVariant skin,
            bool force)
        {
            ImportCoreAssets(force);
            BuildPresentationPrefabs(skin);
            if (force ||
                !SchwarzExtractedFxSetup.HasVariantImported(skin) ||
                !SchwarzExtractedFxSetup.HasS2CompositeImported())
                ImportEffects(force);
        }

        internal static void EnsureS2CompositeFx()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!SchwarzExtractedFxSetup.HasS2CompositeImported())
                ImportEffects(force: false);
            else
                SchwarzExtractedFxSetup.TryApplyToOpenScene();
        }

        internal static void ImportCoreAssets(bool force)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!Directory.Exists(SourceRoot))
            {
                Debug.LogWarning("[ArknightsACT/Schwarz] Local source is missing: " + SourceRoot);
                return;
            }

            CopySpineSet(PrtsPrototypeAssetCatalog.SchwarzDefault, force);
            CopySpineSet(PrtsPrototypeAssetCatalog.SchwarzDefaultMotion, force);
            CopySpineSet(PrtsPrototypeAssetCatalog.SchwarzSnow, force);
            CopySpineSet(PrtsPrototypeAssetCatalog.SchwarzSnowMotion, force);
            CopySpineSet(PrtsPrototypeAssetCatalog.SchwarzStriker, force);
            CopySpineSet(PrtsPrototypeAssetCatalog.SchwarzStrikerMotion, force);

            CopySprite(
                Path.Combine(SourceAvatars, "char_340_shwaz.png"),
                "Assets/_Game/Resources/UI/HUD/Operators/schwarz_default.png",
                force);
            CopySprite(
                Path.Combine(SourceAvatars, "char_340_shwaz_snow#1.png"),
                "Assets/_Game/Resources/UI/HUD/Operators/schwarz_snow.png",
                force);
            CopySprite(
                Path.Combine(SourceAvatars, "char_340_shwaz_striker#1.png"),
                "Assets/_Game/Resources/UI/HUD/Operators/schwarz_striker.png",
                force);

            CopySprite(
                Path.Combine(SourceIcons, "skill_icon_skchr_shwaz_1.png"),
                "Assets/_Game/Resources/UI/Skills/Schwarz/s1.png",
                force);
            CopySprite(
                Path.Combine(SourceIcons, "skill_icon_skchr_shwaz_2.png"),
                "Assets/_Game/Resources/UI/Skills/Schwarz/s2.png",
                force);
            CopySprite(
                Path.Combine(SourceIcons, "skill_icon_skchr_shwaz_3.png"),
                "Assets/_Game/Resources/UI/Skills/Schwarz/s3.png",
                force);

            // Schwarz battle audio. Keep it local and character-specific so selecting Schwarz
            // never falls through to Ch'en's sword swings/skill voices.
            CopyRaw(
                Path.Combine(SourceSoundBanks, "p_atk_3", "p_atk_ttcrossbow_n.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Attack_01.wav",
                force);
            CopyRaw(
                Path.Combine(SourceSoundBanks, "p_atk_3", "p_atk_ttcrossbow_h.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Attack_02.wav",
                force);
            CopyRaw(
                Path.Combine(SourceSoundBanks, "p_atk_3", "p_atk_ttcrossbow_d.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Attack_03.wav",
                force);
            CopyRaw(
                Path.Combine(SourceSoundBanks, "p_imp_3", "p_imp_ttcrossbow_n.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Impact.wav",
                force);
            CopyRaw(
                Path.Combine(SourceSoundBanks, "btl_snd_0", "b_char_atkboost.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Skill2_Activate.wav",
                force);
            CopyRaw(
                Path.Combine(SourceSoundBanks, "btl_snd_1", "b_char_tactboost.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Skill3_Activate.wav",
                force);
            CopyRaw(
                Path.Combine(SourceSoundBanks, "p_skill_10", "p_skill_militaryxbowchange.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Skill3_WeaponChange.wav",
                force);
            CopyRaw(
                Path.Combine(SourceSoundBanks, "p_atk_2", "p_atk_militaryxbow_s.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_SpecialAttack_Shot.wav",
                force);
            CopyRaw(
                Path.Combine(SourceSoundBanks, "p_imp_3", "p_imp_militaryxbow_s.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Skill3_Impact.wav",
                force);
            CopyRaw(
                Path.Combine(SourceVoice, "CN_025.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Voice_JP_CN_025.wav",
                force);
            CopyRaw(
                Path.Combine(SourceVoice, "CN_026.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Voice_JP_CN_026.wav",
                force);
            CopyRaw(
                Path.Combine(SourceVoice, "CN_027.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Voice_JP_CN_027.wav",
                force);
            CopyRaw(
                Path.Combine(SourceVoice, "CN_028.wav"),
                "Assets/_Game/Art/Audio/Schwarz/Schwarz_Voice_JP_CN_028.wav",
                force);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        internal static void ConfigureAudioProfile(GameObject owner)
        {
            if (owner == null)
                return;

            var profile = owner.GetComponent<PlayableOperatorAudioProfile>() ??
                          owner.AddComponent<PlayableOperatorAudioProfile>();

            // CN_025..028 are battle skill lines. They are a shared combat pool rather than a
            // hard split where S2 can only say 025/026 and S3 can only say 027/028.
            var battleVoices = new[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Voice_JP_CN_025.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Voice_JP_CN_026.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Voice_JP_CN_027.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Voice_JP_CN_028.wav")
            };

            profile.Configure(
                slot1Sfx: AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Skill2_Activate.wav"),
                slot2Sfx: AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Skill3_Activate.wav"),
                slot1Voices: battleVoices,
                slot2Voices: battleVoices,
                attackSwings: new[]
                {
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Game/Art/Audio/Schwarz/Schwarz_Attack_01.wav"),
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Game/Art/Audio/Schwarz/Schwarz_Attack_02.wav"),
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/_Game/Art/Audio/Schwarz/Schwarz_Attack_03.wav")
                },
                attackImpact: AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Impact.wav"),
                optionalSkillSfx: false,
                specialManualAttackSourceId: "Schwarz_S3_AimedShot",
                specialManualAttackSfx: AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_SpecialAttack_Shot.wav"),
                specialManualAttackImpact: AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Skill3_Impact.wav"),
                slot2LayerSfx: AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Game/Art/Audio/Schwarz/Schwarz_Skill3_WeaponChange.wav"));
            EditorUtility.SetDirty(profile);
        }

        internal static bool TryApplyAudioToOpenScene()
        {
            if (EditorApplication.isCompiling)
                return false;

            var identities = UnityEngine.Object.FindObjectsByType<PlayableOperatorIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var applied = false;
            for (var i = 0; i < identities.Length; i++)
            {
                var identity = identities[i];
                if (identity == null ||
                    !string.Equals(identity.OperatorId, "Schwarz", StringComparison.OrdinalIgnoreCase))
                    continue;

                ConfigureAudioProfile(identity.gameObject);
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

        private static void BuildPresentationPrefabs(SchwarzSkinVariant skin)
        {
            var skeletonType = FindType("Spine.Unity.SkeletonAnimation");
            if (skeletonType == null)
            {
                Debug.LogWarning("[ArknightsACT/Schwarz] Spine.Unity.SkeletonAnimation is not available yet.");
                return;
            }

            var generatedRoot = ToAbsolutePath(PrtsSpinePrefabBuilder.GeneratedPrefabDirectory);
            Directory.CreateDirectory(generatedRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var combat = GetCombatDescriptor(skin);
            var motion = GetMotionDescriptor(skin);
            if (!PrtsSpinePrefabBuilder.TryBuild(combat, skeletonType, out var combatError))
                Debug.LogWarning($"[ArknightsACT/Schwarz] Failed to build {combat.DisplayName}: {combatError}");
            if (!PrtsSpinePrefabBuilder.TryBuild(motion, skeletonType, out var motionError))
                Debug.LogWarning($"[ArknightsACT/Schwarz] Failed to build {motion.DisplayName}: {motionError}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ImportEffects(bool force)
        {
            if (!Directory.Exists(SourceEffects))
            {
                Debug.LogWarning("[ArknightsACT/Schwarz] Extracted FX source is missing: " + SourceEffects);
                return;
            }

            if (force ||
                !SchwarzExtractedFxSetup.HasVariantImported(SchwarzSkinVariant.Default) ||
                !SchwarzExtractedFxSetup.HasVariantImported(SchwarzSkinVariant.Snow) ||
                !SchwarzExtractedFxSetup.HasVariantImported(SchwarzSkinVariant.Striker))
            {
                var result = ExtractedFrameFxImporter.Import(
                    SourceEffects,
                    ExtractedFrameFxImporter.DefaultOutputRoot,
                    "Schwarz");
                Debug.Log(
                    $"[ArknightsACT/Schwarz] Imported {result.ImportedEffects} FX sequences / " +
                    $"{result.ImportedFrames} frames from local extraction.");
            }

            if (force || !SchwarzExtractedFxSetup.HasS2CompositeImported())
            {
                var ignite = Path.Combine(SourceS2CommonFrames, "common_064_ignite_attack_red");
                var combustion = Path.Combine(SourceS2CommonFrames, "common_combustion_buff_02");
                var result = ExtractedFrameFxImporter.ImportSelected(
                    new[] { ignite, combustion },
                    ExtractedFrameFxImporter.DefaultOutputRoot,
                    "Schwarz");
                Debug.Log(
                    $"[ArknightsACT/Schwarz] Imported S2 composite common FX: " +
                    $"{result.ImportedEffects} sequences / {result.ImportedFrames} frames.");
            }

            SchwarzExtractedFxSetup.TryApplyToOpenScene();
        }

        private static void CopySpineSet(PrtsAssetDescriptor descriptor, bool force)
        {
            if (descriptor == null)
                return;

            var baseName = descriptor.BaseName;
            var atlasSource = Path.Combine(SourceSpine, baseName + ".atlas");
            var skeletonSource = Path.Combine(SourceSpine, baseName + ".skel");

            // Match the naming convention used by the existing PRTS downloader so the Spine
            // Unity importer recognizes the raw client files.
            DeleteLegacyRawAsset(descriptor.TargetDirectory + "/" + baseName + ".atlas");
            DeleteLegacyRawAsset(descriptor.TargetDirectory + "/" + baseName + ".skel");
            if (!File.Exists(atlasSource))
                return;

            // Import texture pages first. Spine's atlas postprocessor resolves/creates the
            // material immediately when the .atlas.txt arrives, so importing atlas before PNG
            // produces "Material is missing texture" and leaves a broken material asset.
            var pages = File.ReadAllLines(atlasSource);
            for (var i = 0; i < pages.Length; i++)
            {
                var line = pages[i].Trim();
                if (!line.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    line.IndexOf(':') >= 0)
                    continue;

                var pageName = Path.GetFileName(line);
                CopyRaw(
                    Path.Combine(SourceSpine, pageName),
                    descriptor.TargetDirectory + "/" + pageName,
                    force);
            }

            DeleteBrokenSpineMaterial(descriptor.TargetDirectory, baseName);
            CopyRaw(
                atlasSource,
                descriptor.TargetDirectory + "/" + baseName + ".atlas.txt",
                force);
            CopyRaw(
                skeletonSource,
                descriptor.TargetDirectory + "/" + baseName + ".skel.bytes",
                force);
        }

        private static void DeleteBrokenSpineMaterial(string targetDirectory, string baseName)
        {
            var materialPath = targetDirectory + "/" + baseName + "_Material.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null && material.mainTexture == null)
            {
                Debug.LogWarning(
                    "[ArknightsACT/Schwarz] Removing stale Spine material with missing texture: " +
                    materialPath);
                AssetDatabase.DeleteAsset(materialPath);
            }
        }

        private static void DeleteLegacyRawAsset(string assetPath)
        {
            var absolute = ToAbsolutePath(assetPath);
            if (!File.Exists(absolute))
                return;
            AssetDatabase.DeleteAsset(assetPath);
        }

        private static void CopySprite(string source, string targetAssetPath, bool force)
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
            importer.SaveAndReimport();
        }

        private static bool CopyRaw(string source, string targetAssetPath, bool force)
        {
            if (!File.Exists(source))
            {
                Debug.LogWarning("[ArknightsACT/Schwarz] Missing local asset: " + source);
                return false;
            }

            var target = ToAbsolutePath(targetAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? Application.dataPath);
            var needsCopy = force || !File.Exists(target) ||
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

        private static PrtsAssetDescriptor GetCombatDescriptor(SchwarzSkinVariant skin) =>
            skin switch
            {
                SchwarzSkinVariant.Snow => PrtsPrototypeAssetCatalog.SchwarzSnow,
                SchwarzSkinVariant.Striker => PrtsPrototypeAssetCatalog.SchwarzStriker,
                _ => PrtsPrototypeAssetCatalog.SchwarzDefault
            };

        private static PrtsAssetDescriptor GetMotionDescriptor(SchwarzSkinVariant skin) =>
            skin switch
            {
                SchwarzSkinVariant.Snow => PrtsPrototypeAssetCatalog.SchwarzSnowMotion,
                SchwarzSkinVariant.Striker => PrtsPrototypeAssetCatalog.SchwarzStrikerMotion,
                _ => PrtsPrototypeAssetCatalog.SchwarzDefaultMotion
            };
    }

    internal static class SchwarzExtractedFxSetup
    {
        private const string Root = "Assets/_Game/Art/FX/Extracted/Schwarz/Prefabs";

        internal static bool HasVariantImported(SchwarzSkinVariant skin) =>
            Load("skill_03_start", skin) != null &&
            Load("skill_03_trail", skin) != null &&
            Load("skill_01_hit", skin) != null &&
            Load("shwaz_attack_01_start", skin) != null &&
            Load("shwaz_attack_01_trail", skin) != null;

        internal static bool HasS2CompositeImported() =>
            LoadExact("common_064_ignite_attack_red") != null &&
            LoadExact("common_combustion_buff_02") != null;

        internal static void Configure(GameObject owner, SchwarzSkinVariant skin)
        {
            if (owner == null)
                return;

            var controller = owner.GetComponent<SchwarzExtractedFxController>() ??
                             owner.AddComponent<SchwarzExtractedFxController>();
            controller.Configure(
                Load("shwaz_attack_01_start", skin),
                Load("shwaz_attack_01_trail", skin),
                Load("shwaz_attack_01_hit", skin),
                LoadExact("common_064_ignite_attack_red"),
                LoadExact("common_combustion_buff_02"),
                Load("skill_03_start", skin),
                Load("skill_03_trail", skin),
                Load("skill_01_hit", skin),
                Load("skill_03_buff_02", skin),
                Load("skill_03_buff_03", skin));
            EditorUtility.SetDirty(controller);
        }

        internal static bool TryApplyToOpenScene()
        {
            var identities = UnityEngine.Object.FindObjectsByType<
                ArknightsACT.Gameplay.Characters.PlayableOperatorIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            var applied = false;
            for (var i = 0; i < identities.Length; i++)
            {
                var identity = identities[i];
                if (identity == null || identity.OperatorId != "Schwarz")
                    continue;

                var skin = identity.SkinId switch
                {
                    "snow#1" => SchwarzSkinVariant.Snow,
                    "striker#1" => SchwarzSkinVariant.Striker,
                    _ => SchwarzSkinVariant.Default
                };

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

        private static GameObject LoadExact(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + name + ".prefab");

        private static GameObject Load(string baseName, SchwarzSkinVariant skin)
        {
            var suffix = skin switch
            {
                SchwarzSkinVariant.Snow => "_snow#1",
                SchwarzSkinVariant.Striker => "_striker#1",
                _ => string.Empty
            };
            return AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + baseName + suffix + ".prefab");
        }
    }
}
#endif
