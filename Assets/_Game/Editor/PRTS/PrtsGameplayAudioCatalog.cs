#if UNITY_EDITOR
using System;

namespace ArknightsACT.Editor.PRTS
{
    internal enum PrtsGameplayAudioKind
    {
        Music,
        SkillSfx,
        CombatSfx,
        Voice
    }

    internal sealed class PrtsGameplayAudioAsset
    {
        public string DisplayName { get; }
        public string LocalPath { get; }
        public string SourcePage { get; }
        public PrtsGameplayAudioKind Kind { get; }
        public string[] RemoteUrls { get; }

        public PrtsGameplayAudioAsset(
            string displayName,
            string localPath,
            string sourcePage,
            PrtsGameplayAudioKind kind,
            params string[] remoteUrls)
        {
            DisplayName = displayName;
            LocalPath = localPath;
            SourcePage = sourcePage;
            Kind = kind;
            RemoteUrls = remoteUrls ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Local-only PRTS audio references used by the ACT prototype.
    /// PRTS Data_Audio logical paths often start with Sound_Beta_2/, while the public media endpoint
    /// serves the normalized path below /assets/audio/ directly. Keep that normalization here so the
    /// downloader never has to guess the CDN layout.
    /// </summary>
    internal static class PrtsGameplayAudioCatalog
    {
        public const string Root = "Assets/_Game/Art/Audio/PRTS";

        private const string AudioBase = "https://torappu.prts.wiki/assets/audio/";
        private const string MusicPage = "https://prts.wiki/w/%E9%9F%B3%E4%B9%90%E9%89%B4%E8%B5%8F/%E6%B8%B8%E6%88%8F%E5%86%85%E9%9F%B3%E4%B9%90%E4%B8%80%E8%A7%88";
        private const string ChenVoicePage = "https://prts.wiki/w/%E9%99%88/%E8%AF%AD%E9%9F%B3%E8%AE%B0%E5%BD%95";
        private const string AudioDataPage = "https://prts.wiki/w/%E5%BE%AE%E4%BB%B6:Data_Audio";

        public static readonly PrtsGameplayAudioAsset ChernobogIntro = new(
            "BGM · 切尔诺伯格 Intro",
            Root + "/BGM_Chernobog_Intro.mp3",
            MusicPage,
            PrtsGameplayAudioKind.Music,
            AudioBase + "music/act9d2d0/m_bat_chernobog_intro.mp3");

        public static readonly PrtsGameplayAudioAsset ChernobogLoop = new(
            "BGM · 切尔诺伯格 Loop",
            Root + "/BGM_Chernobog_Loop.mp3",
            MusicPage,
            PrtsGameplayAudioKind.Music,
            AudioBase + "music/act9d2d0/m_bat_chernobog_loop.mp3");

        // PRTS does not expose the previously guessed p_skill_* media paths. These two verified
        // Chixiao/story SFX are used as prototype presentation for the two ACT skill slots; they are
        // not claimed to be the exact dedicated in-battle skill files from the original client.
        public static readonly PrtsGameplayAudioAsset ChenSkill1Sfx = new(
            "陈 · 赤霄·拔刀 技能音效",
            Root + "/Chen_Skill1_ChixiaoBadao.mp3",
            AudioDataPage,
            PrtsGameplayAudioKind.SkillSfx,
            AudioBase + "avg/d_avg_chixiaosword.mp3");

        public static readonly PrtsGameplayAudioAsset ChenSkill2Sfx = new(
            "陈 · 赤霄·绝影 技能音效",
            Root + "/Chen_Skill2_Jueying.mp3",
            AudioDataPage,
            PrtsGameplayAudioKind.SkillSfx,
            AudioBase + "avg/d_avg_chixiaotiancheng.mp3");

        public static readonly PrtsGameplayAudioAsset ChenAttackSwing1 = Combat(
            "陈 · 普攻挥刀 1", "Chen_AttackSwing_01.mp3", "AVG/d_avg_swordtsing1");
        public static readonly PrtsGameplayAudioAsset ChenAttackSwing2 = Combat(
            "陈 · 普攻挥刀 2", "Chen_AttackSwing_02.mp3", "AVG/d_avg_swordtsing2");
        public static readonly PrtsGameplayAudioAsset ChenAttackSwing3 = Combat(
            "陈 · 普攻挥刀 3", "Chen_AttackSwing_03.mp3", "AVG/d_avg_swordtsing3");
        public static readonly PrtsGameplayAudioAsset ChenSwordImpact = Combat(
            "陈 · 刀剑命中", "Chen_SwordImpact.mp3", "Player/p_imp/p_imp_sword_n");
        public static readonly PrtsGameplayAudioAsset PlayerHurt = Combat(
            "角色受击", "Player_Hurt.mp3", "AVG/d_avg_shockbody");
        public static readonly PrtsGameplayAudioAsset PlayerDeath = Combat(
            "角色倒地", "Player_Death.mp3", "AVG/d_avg_bodyfalldown2");
        public static readonly PrtsGameplayAudioAsset EnemyMeleeAttack = Combat(
            "敌人近战攻击", "Enemy_MeleeAttack.mp3", "Enemy/e_atk/e_atk_blunt_n");
        public static readonly PrtsGameplayAudioAsset EnemyRangedAttack = Combat(
            "敌人远程攻击", "Enemy_RangedAttack.mp3", "Enemy/e_atk/e_atk_arrow_h");
        public static readonly PrtsGameplayAudioAsset EnemyDeath = Combat(
            "敌人死亡", "Enemy_Death.mp3", "AVG/d_avg_bodyfalldown3");

        public static readonly PrtsGameplayAudioAsset ChenVoice025 = Voice("CN_025", "战斗语音 1");
        public static readonly PrtsGameplayAudioAsset ChenVoice026 = Voice("CN_026", "战斗语音 2");
        public static readonly PrtsGameplayAudioAsset ChenVoice027 = Voice("CN_027", "战斗语音 3");
        public static readonly PrtsGameplayAudioAsset ChenVoice028 = Voice("CN_028", "战斗语音 4");

        public static readonly PrtsGameplayAudioAsset[] All =
        {
            ChernobogIntro,
            ChernobogLoop,
            ChenSkill1Sfx,
            ChenSkill2Sfx,
            ChenAttackSwing1,
            ChenAttackSwing2,
            ChenAttackSwing3,
            ChenSwordImpact,
            PlayerHurt,
            PlayerDeath,
            EnemyMeleeAttack,
            EnemyRangedAttack,
            EnemyDeath,
            ChenVoice025,
            ChenVoice026,
            ChenVoice027,
            ChenVoice028
        };

        public static readonly PrtsGameplayAudioAsset[] Bgm =
        {
            ChernobogIntro,
            ChernobogLoop
        };

        public static readonly PrtsGameplayAudioAsset[] CombatAndSkills =
        {
            ChenSkill1Sfx,
            ChenSkill2Sfx,
            ChenAttackSwing1,
            ChenAttackSwing2,
            ChenAttackSwing3,
            ChenSwordImpact,
            PlayerHurt,
            PlayerDeath,
            EnemyMeleeAttack,
            EnemyRangedAttack,
            EnemyDeath
        };

        public static readonly PrtsGameplayAudioAsset[] ChenVoices =
        {
            ChenVoice025,
            ChenVoice026,
            ChenVoice027,
            ChenVoice028
        };

        public static readonly PrtsGameplayAudioAsset[] ChenSkill1Voices =
        {
            ChenVoice025,
            ChenVoice026
        };

        public static readonly PrtsGameplayAudioAsset[] ChenSkill2Voices =
        {
            ChenVoice027,
            ChenVoice028
        };

        public static readonly PrtsGameplayAudioAsset[] ChenAttackSwings =
        {
            ChenAttackSwing1,
            ChenAttackSwing2,
            ChenAttackSwing3
        };

        private static PrtsGameplayAudioAsset Combat(string displayName, string fileName, string canonicalPath)
        {
            var normalized = NormalizeDataAudioPath(canonicalPath);
            return new PrtsGameplayAudioAsset(
                displayName,
                Root + "/" + fileName,
                AudioDataPage,
                PrtsGameplayAudioKind.CombatSfx,
                AudioBase + normalized + ".mp3");
        }

        private static PrtsGameplayAudioAsset Voice(string id, string label)
        {
            var fileId = id.ToLowerInvariant();
            return new PrtsGameplayAudioAsset(
                $"陈 · {label} ({id})",
                $"{Root}/Chen_Voice_{id}.mp3",
                ChenVoicePage,
                PrtsGameplayAudioKind.Voice,
                AudioBase + $"voice_cn/char_010_chen/{fileId}.mp3",
                AudioBase + $"voice_cn/char_010_chen/{id}.mp3");
        }

        private static string NormalizeDataAudioPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var normalized = path.Replace('\\', '/').TrimStart('/');
            const string root = "Sound_Beta_2/";
            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(root.Length);
            return normalized.ToLowerInvariant();
        }
    }
}
#endif
