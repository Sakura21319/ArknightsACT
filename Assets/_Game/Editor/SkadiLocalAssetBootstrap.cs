#if UNITY_EDITOR
using System;
using ArknightsACT.Editor.PRTS;
using UnityEditor;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Asset-only Skadi integration. Gameplay mapping and manual FX offsets intentionally remain
    /// outside the automatic import pass.
    /// </summary>
    internal static class SkadiLocalAssetBootstrap
    {
        private static readonly PrtsAssetDescriptor Default = new(
            "斯卡蒂·原版",
            "local",
            string.Empty,
            "char_263_skadi",
            "Assets/_Game/Art/Characters/Skadi/Local/Default/Spine",
            "Player",
            1.64f,
            -0.72f,
            0.38f,
            "skadi_default");

        private static readonly PrtsAssetDescriptor DefaultMotion = new(
            "斯卡蒂·原版动作源",
            "local",
            string.Empty,
            "build_char_263_skadi",
            "Assets/_Game/Art/Characters/Skadi/Local/Default/BaseMotion",
            "MotionSource",
            1.64f,
            -0.72f,
            0.38f,
            "skadi_default_motion");

        private static readonly PrtsAssetDescriptor Marthe5 = new(
            "斯卡蒂·marthe#5",
            "local",
            string.Empty,
            "char_263_skadi_marthe#5",
            "Assets/_Game/Art/Characters/Skadi/Local/Marthe5/Spine",
            "Player",
            1.64f,
            -0.72f,
            0.38f,
            "skadi_marthe_5");

        private static readonly PrtsAssetDescriptor Marthe5Motion = new(
            "斯卡蒂·marthe#5 动作源",
            "local",
            string.Empty,
            "build_char_263_skadi_marthe#5",
            "Assets/_Game/Art/Characters/Skadi/Local/Marthe5/BaseMotion",
            "MotionSource",
            1.64f,
            -0.72f,
            0.38f,
            "skadi_marthe_5_motion");

        private static readonly PrtsAssetDescriptor Summer3 = new(
            "斯卡蒂·summer#3",
            "local",
            string.Empty,
            "char_263_skadi_summer#3",
            "Assets/_Game/Art/Characters/Skadi/Local/Summer3/Spine",
            "Player",
            1.64f,
            -0.72f,
            0.38f,
            "skadi_summer_3");

        private static readonly PrtsAssetDescriptor Summer3Motion = new(
            "斯卡蒂·summer#3 动作源",
            "local",
            string.Empty,
            "build_char_263_skadi_summer#3",
            "Assets/_Game/Art/Characters/Skadi/Local/Summer3/BaseMotion",
            "MotionSource",
            1.64f,
            -0.72f,
            0.38f,
            "skadi_summer_3_motion");

        private static readonly LocalOperatorImportPlan Plan = new(
            "Skadi",
            "skadi",
            "Skadi",
            new[]
            {
                new LocalOperatorSpriteImport(
                    "icons/skill_icon_skchr_skadi_2.png",
                    "Assets/_Game/Resources/UI/Skills/Skadi/s2.png"),
                new LocalOperatorSpriteImport(
                    "icons/skill_icon_skchr_skadi_3.png",
                    "Assets/_Game/Resources/UI/Skills/Skadi/s3.png")
            },
            new[]
            {
                new LocalOperatorSkinImportPlan(
                    "default",
                    Default,
                    DefaultMotion,
                    new[]
                    {
                        new LocalOperatorSpriteImport(
                            "ui_assets/avatars/char_263_skadi.png",
                            "Assets/_Game/Resources/UI/HUD/Operators/skadi_default.png")
                    },
                    new[]
                    {
                        "skadi_attack_01_hit",
                        "skadi_attack_01_start",
                        "skadi_attack_01_start_02",
                        "skadi_attack_02_hit",
                        "skadi_attack_02_start",
                        "skadi_attack_02_start_02",
                        "skill_02_start",
                        "skill_02_start_02",
                        "skill_03_buff"
                    }),
                new LocalOperatorSkinImportPlan(
                    "marthe#5",
                    Marthe5,
                    Marthe5Motion,
                    new[]
                    {
                        new LocalOperatorSpriteImport(
                            "ui_assets/avatars/char_263_skadi_marthe#5.png",
                            "Assets/_Game/Resources/UI/HUD/Operators/skadi_marthe_5.png")
                    },
                    new[]
                    {
                        "skadi_attack_01_hit_marthe#5",
                        "skadi_attack_01_start_marthe#5",
                        "skadi_attack_01_start_02_marthe#5",
                        "skadi_attack_02_hit_marthe#5",
                        "skadi_attack_02_start_marthe#5",
                        "skadi_attack_02_start_02_marthe#5",
                        "skill_01_buff_01_marthe#5",
                        "skill_02_start_marthe#5",
                        "skill_02_start_02_marthe#5",
                        "skill_03_buff_marthe#5"
                    }),
                new LocalOperatorSkinImportPlan(
                    "summer#3",
                    Summer3,
                    Summer3Motion,
                    new[]
                    {
                        new LocalOperatorSpriteImport(
                            "ui_assets/avatars/char_263_skadi_summer#3.png",
                            "Assets/_Game/Resources/UI/HUD/Operators/skadi_summer_3.png")
                    },
                    new[]
                    {
                        "skadi_attack_01_hit_summer#3",
                        "skadi_attack_01_start_summer#3",
                        "skadi_attack_01_start_02_summer#3",
                        "skadi_attack_02_hit_summer#3",
                        "skadi_attack_02_start_summer#3",
                        "skadi_attack_02_start_02_summer#3",
                        "skill_02_start_summer#3",
                        "skill_02_start_02_summer#3",
                        "skill_03_buff_summer#3"
                    })
            },
            "voice/voice",
            "Assets/_Game/Art/Audio/Skadi/Voices");

        internal static void ImportAll(bool force, bool showDialog = true)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEngine.Debug.LogWarning(
                    "[ArknightsACT/Skadi] Asset import is unavailable while Unity is compiling or entering Play Mode.");
                return;
            }

            var summary = LocalOperatorPresentationImporter.Import(
                Plan,
                skinId: null,
                force: force,
                importAllSkins: true);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "斯卡蒂素材同步完成：原版、marthe#5、summer#3 的战斗/Move Spine、头像、技能图标、语音和 FX 已处理。\n\n" +
                    $"本次重建 FX：{summary.FxRebuilt}/{summary.FxRequested}。偏移和 Gameplay 仍由后续人工确认。",
                    "确定");
            }
        }

        internal static void RefreshSkin(string skinId, bool force)
        {
            LocalOperatorPresentationImporter.Import(
                Plan,
                skinId,
                force,
                importAllSkins: false);
        }

        internal static PrtsAssetDescriptor GetCombatDescriptor(string skinId)
        {
            if (string.Equals(skinId, "marthe#5", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skinId, "marthe_5", StringComparison.OrdinalIgnoreCase))
                return Marthe5;

            if (string.Equals(skinId, "summer#3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skinId, "summer_3", StringComparison.OrdinalIgnoreCase))
                return Summer3;

            return Default;
        }

        internal static PrtsAssetDescriptor GetMotionDescriptor(string skinId)
        {
            if (string.Equals(skinId, "marthe#5", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skinId, "marthe_5", StringComparison.OrdinalIgnoreCase))
                return Marthe5Motion;

            if (string.Equals(skinId, "summer#3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skinId, "summer_3", StringComparison.OrdinalIgnoreCase))
                return Summer3Motion;

            return DefaultMotion;
        }
    }
}
#endif
