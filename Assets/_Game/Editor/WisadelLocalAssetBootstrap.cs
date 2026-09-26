#if UNITY_EDITOR
using System;
using System.IO;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Wisadel;
using ArknightsACT.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Wisadel integration on top of the shared local-operator presentation importer.
    /// Runtime FX binding/tuning stays character-specific; file import does not.
    /// </summary>
    internal static class WisadelLocalAssetBootstrap
    {
        private const string TuningProfilePath =
            "Assets/_Game/Resources/Config/WisadelFxTuningProfile.asset";

        private const string AudioTargetRoot = "Assets/_Game/Art/Audio/Wisadel/Battle";
        private const string BasicShotTarget = AudioTargetRoot + "/Wisadel_BasicShot.wav";
        private const string Skill2ActivateTarget = AudioTargetRoot + "/Wisadel_S2_Activate.wav";
        private const string Skill3ActivateTarget = AudioTargetRoot + "/Wisadel_S3_Activate.wav";
        private const string Skill3ShotTarget = AudioTargetRoot + "/Wisadel_S3_Shot.wav";
        private const string Skill2ImpactTarget = AudioTargetRoot + "/Wisadel_S2_Impact.wav";
        private const string Skill3ImpactTarget = AudioTargetRoot + "/Wisadel_S3_Impact.wav";

        // Explicit current-gameplay FX set after rescanning all 79 exported Wisadel groups.
        // sale#14 stays excluded. S1 FX are exported but intentionally not part of the current
        // playable loadout (the prototype exposes original S2 + S3).
        private static readonly string[] CommonFx =
        {
            "wisdel_attack_a_start",
            "wisdel_attack_b_start",
            "wisdel_attack_c_start",
            "wisdel_attack_down_a_start",
            "wisdel_attack_down_b_start",
            "wisdel_attack_down_c_start",
            "wisdel_attack_01_trail",
            "wisdel_attack_01_hit",
            "wisdel_attack_01_hit_02",

            "skill_02_start",
            "skill_02_buff",
            "skill_02_buff_02",
            "skill_02_hit",
            "skill_02_hit_02",
            "skill_02_overload_start",

            "skill_03_up_start",
            "skill_03_down_start",
            "skill_03_buff_b",
            "skill_03_buff_f",
            "skill_03_buff_02_b"
        };

        private static readonly string[] DefaultSkill3Fx =
        {
            "skill_03_start",
            "skill_03_trail",
            "skill_03_hit",
            "skill_03_hit_02",
            "skill_03_hit_03",
            "skill_03_buff_02_f"
        };

        private static readonly string[] Game9Skill3Fx =
        {
            "skill_03_start_game#9",
            "skill_03_trail_game#9",
            "skill_03_hit_game#9",
            "skill_03_hit_02_game#9",
            "skill_03_hit_03_game#9",
            "skill_03_buff_02_f_game#9"
        };

        private static readonly string[] AllFx = CombineFx(
            CombineFx(CommonFx, DefaultSkill3Fx),
            Game9Skill3Fx);

        private static readonly LocalOperatorImportPlan Plan = new(
            "Wisadel",
            "wisdel",
            "Wisadel",
            new[]
            {
                new LocalOperatorSpriteImport(
                    "icons/skill_icon_skchr_wisdel_2.png",
                    "Assets/_Game/Resources/UI/Skills/Wisadel/s2.png"),
                new LocalOperatorSpriteImport(
                    "icons/skill_icon_skchr_wisdel_3.png",
                    "Assets/_Game/Resources/UI/Skills/Wisadel/s3.png")
            },
            new[]
            {
                new LocalOperatorSkinImportPlan(
                    "default",
                    PrtsPrototypeAssetCatalog.WisadelDefault,
                    PrtsPrototypeAssetCatalog.WisadelDefaultMotion,
                    new[]
                    {
                        new LocalOperatorSpriteImport(
                            "ui_assets/avatars/char_1035_wisdel.png",
                            "Assets/_Game/Resources/UI/HUD/Operators/wisadel_default.png")
                    },
                    CombineFx(CommonFx, DefaultSkill3Fx)),
                new LocalOperatorSkinImportPlan(
                    "game#9",
                    PrtsPrototypeAssetCatalog.WisadelGame9,
                    PrtsPrototypeAssetCatalog.WisadelGame9Motion,
                    new[]
                    {
                        new LocalOperatorSpriteImport(
                            "ui_assets/avatars/char_1035_wisdel_game#9.png",
                            "Assets/_Game/Resources/UI/HUD/Operators/wisadel_game_9.png")
                    },
                    CombineFx(CommonFx, Game9Skill3Fx))
            },
            "voice/voice",
            "Assets/_Game/Art/Audio/Wisadel/Voices");

        internal static void ImportOriginalAndGame9(bool force, bool showDialog = true)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[ArknightsACT/Wisadel] Asset import is unavailable while Unity is compiling or entering Play Mode.");
                return;
            }

            var summary = LocalOperatorPresentationImporter.Import(
                Plan,
                skinId: null,
                force: force,
                importAllSkins: true);
            LocalOperatorAssetImportUtility.PruneGeneratedFx("Wisadel", AllFx);
            ImportBattleAudio(force);
            EnsureTuningProfile();
            ApplyToOpenScene();

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "维什戴尔素材同步完成：只处理原版与 game#9，sale#14 及未绑定 FX 不导入。\n\n" +
                    $"本次重建 FX：{summary.FxRebuilt}/{summary.FxRequested}。",
                    "确定");
            }
        }

        internal static void PrepareForBuild(string skinId, bool force = false)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            LocalOperatorPresentationImporter.Import(
                Plan,
                NormalizeSkinId(skinId),
                force,
                importAllSkins: false);
            ImportBattleAudio(force);
            EnsureTuningProfile();
        }

        internal static void RefreshSelectedAssets(string skinId, bool force)
        {
            PrepareForBuild(skinId, force);
        }

        internal static void ConfigurePlayer(GameObject player, string skinId)
        {
            if (player == null)
                return;

            var game9 = IsGame9(skinId);
            var retarget = player.GetComponent<SpineBoneMotionRetarget2D>();
            if (retarget != null)
            {
                retarget.EnableFullSourceVisuals(true);
                EditorUtility.SetDirty(retarget);
            }
            var baseMotionShortcuts = retarget != null
                ? player.GetComponent<BaseMotionActionShortcutController>() ??
                  player.AddComponent<BaseMotionActionShortcutController>()
                : null;
            var fx = player.GetComponent<WisadelExtractedFxController>() ??
                     player.AddComponent<WisadelExtractedFxController>();

            fx.Configure(
                new[]
                {
                    LoadFx("wisdel_attack_a_start"),
                    LoadFx("wisdel_attack_b_start"),
                    LoadFx("wisdel_attack_c_start")
                },
                new[]
                {
                    LoadFx("wisdel_attack_down_a_start"),
                    LoadFx("wisdel_attack_down_b_start"),
                    LoadFx("wisdel_attack_down_c_start")
                },
                LoadFx("wisdel_attack_01_trail"),
                LoadFx("wisdel_attack_01_hit"),
                LoadFx("wisdel_attack_01_hit_02"),

                LoadFx("skill_02_start"),
                LoadFx("skill_02_buff"),
                LoadFx("skill_02_buff_02"),
                LoadFx("skill_02_hit"),
                LoadFx("skill_02_hit_02"),
                LoadFx("skill_02_overload_start"),

                LoadFx(game9 ? "skill_03_start_game#9" : "skill_03_start"),
                LoadFx("skill_03_up_start"),
                LoadFx("skill_03_down_start"),
                LoadFx(game9 ? "skill_03_trail_game#9" : "skill_03_trail"),
                LoadFx(game9 ? "skill_03_hit_game#9" : "skill_03_hit"),
                LoadFx(game9 ? "skill_03_hit_02_game#9" : "skill_03_hit_02"),
                LoadFx(game9 ? "skill_03_hit_03_game#9" : "skill_03_hit_03"),
                LoadFx("skill_03_buff_b"),
                LoadFx("skill_03_buff_f"),
                LoadFx("skill_03_buff_02_b"),
                LoadFx(game9 ? "skill_03_buff_02_f_game#9" : "skill_03_buff_02_f"));
            fx.RefreshTuning();

            // vfx_binding/audio_mapping are authoritative for semantics. The prototype exposes
            // original S2 as gameplay slot 1 and original S3 as gameplay slot 2.
            ImportBattleAudio(force: false);
            var audio = player.GetComponent<PlayableOperatorAudioProfile>() ??
                        player.AddComponent<PlayableOperatorAudioProfile>();
            var battleVoices = LoadBattleVoices();
            audio.Configure(
                slot1Sfx: AssetDatabase.LoadAssetAtPath<AudioClip>(Skill2ActivateTarget),
                slot2Sfx: AssetDatabase.LoadAssetAtPath<AudioClip>(Skill3ActivateTarget),
                slot1Voices: battleVoices,
                slot2Voices: battleVoices,
                attackSwings: new[]
                {
                    AssetDatabase.LoadAssetAtPath<AudioClip>(BasicShotTarget)
                },
                attackImpact: null,
                optionalSkillSfx: false,
                specialManualAttackSourceId: null,
                specialManualAttackSfx: AssetDatabase.LoadAssetAtPath<AudioClip>(Skill3ShotTarget),
                specialManualAttackImpact: null);

            var audioCue = player.GetComponent<WisadelCombatAudioCue>() ??
                           player.AddComponent<WisadelCombatAudioCue>();
            audioCue.Configure(
                AssetDatabase.LoadAssetAtPath<AudioClip>(Skill2ImpactTarget),
                AssetDatabase.LoadAssetAtPath<AudioClip>(Skill3ImpactTarget));

            if (baseMotionShortcuts != null)
                EditorUtility.SetDirty(baseMotionShortcuts);
            EditorUtility.SetDirty(fx);
            EditorUtility.SetDirty(audio);
            EditorUtility.SetDirty(audioCue);
        }

        private static void ImportBattleAudio(bool force)
        {
            var sourceRoot = LocalOperatorAssetImportUtility.GetCharacterSourceRoot("wisdel");
            var bankRoot = Path.Combine(sourceRoot, "sound", "_banks");
            if (!Directory.Exists(bankRoot))
            {
                Debug.LogWarning(
                    "[ArknightsACT/Wisadel] Battle audio bank is missing: " + bankRoot);
                return;
            }

            // audio_mapping.json:
            // basic ON_ABILITY_START -> p_atk_dkmrcaygn_n
            // S2/S3 ON_SKILL_START -> b_char_atkboost
            // attack ON_ABILITY_ON special variant -> p_atk_dkmrcaygnfr_s
            // S2 hit -> p_imp_dkmrcaygn_h; S3 hit -> p_imp_dkmrcaygn_s.
            LocalOperatorAssetImportUtility.CopyRaw(
                Path.Combine(bankRoot, "p_atk_0", "p_atk_dkmrcaygn_n.wav"),
                BasicShotTarget,
                force);
            LocalOperatorAssetImportUtility.CopyRaw(
                Path.Combine(bankRoot, "btl_snd_0", "b_char_atkboost.wav"),
                Skill2ActivateTarget,
                force);
            LocalOperatorAssetImportUtility.CopyRaw(
                Path.Combine(bankRoot, "btl_snd_0", "b_char_atkboost.wav"),
                Skill3ActivateTarget,
                force);
            LocalOperatorAssetImportUtility.CopyRaw(
                Path.Combine(bankRoot, "p_atk_0", "p_atk_dkmrcaygnfr_s.wav"),
                Skill3ShotTarget,
                force);
            LocalOperatorAssetImportUtility.CopyRaw(
                Path.Combine(bankRoot, "p_imp_2", "p_imp_dkmrcaygn_h.wav"),
                Skill2ImpactTarget,
                force);
            LocalOperatorAssetImportUtility.CopyRaw(
                Path.Combine(bankRoot, "p_imp_1", "p_imp_dkmrcaygn_s.wav"),
                Skill3ImpactTarget,
                force);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static AudioClip[] LoadBattleVoices()
        {
            var result = new System.Collections.Generic.List<AudioClip>();
            for (var index = 25; index <= 28; index++)
            {
                var path =
                    $"Assets/_Game/Art/Audio/Wisadel/Voices/CN_{index:000}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                    result.Add(clip);
            }

            return result.ToArray();
        }

        private static string[] CombineFx(string[] common, params string[] extra)
        {
            var result = new string[common.Length + extra.Length];
            Array.Copy(common, result, common.Length);
            Array.Copy(extra, 0, result, common.Length, extra.Length);
            return result;
        }

        private static void ApplyToOpenScene()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<WisadelExtractedFxController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var controller in controllers)
            {
                if (controller == null)
                    continue;

                var identity = controller.GetComponent<PlayableOperatorIdentity>();
                ConfigurePlayer(
                    controller.gameObject,
                    identity != null ? identity.SkinId : "default");
                controller.ApplySavedTuning();
            }

            if (controllers.Length <= 0)
                return;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject LoadFx(string name) =>
            LocalOperatorAssetImportUtility.LoadFx("Wisadel", name);

        private static string NormalizeSkinId(string skinId) =>
            IsGame9(skinId) ? "game#9" : "default";

        private static bool IsGame9(string skinId) =>
            string.Equals(skinId, "game#9", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skinId, "game_9", StringComparison.OrdinalIgnoreCase);

        private static void EnsureTuningProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WisadelFxTuningProfile>(TuningProfilePath);
            if (profile != null)
                return;

            EnsureFolder("Assets/_Game/Resources/Config");
            profile = ScriptableObject.CreateInstance<WisadelFxTuningProfile>();
            profile.ResetDefaults();
            AssetDatabase.CreateAsset(profile, TuningProfilePath);
            EditorUtility.SetDirty(profile);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;

            var parent = path.Substring(0, slash);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
#endif
