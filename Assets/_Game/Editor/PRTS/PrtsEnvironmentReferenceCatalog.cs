#if UNITY_EDITOR
namespace ArknightsACT.Editor.PRTS
{
    internal sealed class PrtsEnvironmentReference
    {
        public PrtsEnvironmentReference(
            string displayName,
            string sourcePage,
            string remoteUrl,
            string localPath,
            bool runtimeBackdrop)
        {
            DisplayName = displayName;
            SourcePage = sourcePage;
            RemoteUrl = remoteUrl;
            LocalPath = localPath;
            RuntimeBackdrop = runtimeBackdrop;
        }

        public string DisplayName { get; }
        public string SourcePage { get; }
        public string RemoteUrl { get; }
        public string LocalPath { get; }
        public bool RuntimeBackdrop { get; }
    }

    /// <summary>
    /// PRTS environment references used only by the local prototype workflow. Binary files are
    /// downloaded into Assets/_Game/Art/Environment/PRTS, which is already excluded by .gitignore.
    /// </summary>
    internal static class PrtsEnvironmentReferenceCatalog
    {
        private const string Root = "Assets/_Game/Art/Environment/PRTS/Chernobog";
        private const string BackgroundSourcePage = "https://prts.wiki/w/剧情资源概览/背景";

        public static readonly PrtsEnvironmentReference ChernobogStreet0 = new(
            "切尔诺伯格剧情背景 0",
            BackgroundSourcePage,
            "https://media.prts.wiki/b/ba/Avg_bg_bg_cher_0.png",
            Root + "/Avg_bg_bg_cher_0.png",
            true);

        public static readonly PrtsEnvironmentReference ChernobogStreet2 = new(
            "切尔诺伯格剧情背景 2",
            BackgroundSourcePage,
            "https://media.prts.wiki/4/42/Avg_bg_bg_cher_2.png",
            Root + "/Avg_bg_bg_cher_2.png",
            false);

        public static readonly PrtsEnvironmentReference ChernobogCore2 = new(
            "切尔诺伯格核心城区背景 2",
            "https://prts.wiki/w/文件:Avg_bg_bg_chercen_2.png",
            "https://media.prts.wiki/d/df/Avg_bg_bg_chercen_2.png",
            Root + "/Avg_bg_bg_chercen_2.png",
            true);

        public static readonly PrtsEnvironmentReference ChernobogStreet5 = new(
            "切尔诺伯格剧情背景 5",
            BackgroundSourcePage,
            "https://media.prts.wiki/1/12/Avg_bg_bg_cher_5.png",
            Root + "/Avg_bg_bg_cher_5.png",
            true);

        public static readonly PrtsEnvironmentReference ChernobogSixDistrictMap = new(
            "切尔诺伯格 6 区废墟关卡预览",
            "https://prts.wiki/w/切尔诺伯格_6区废墟",
            "https://torappu.prts.wiki/assets/map_preview/level_rune_05-02.png",
            Root + "/Reference_level_rune_05-02.png",
            false);

        public static readonly PrtsEnvironmentReference[] All =
        {
            ChernobogStreet0,
            ChernobogStreet2,
            ChernobogCore2,
            ChernobogStreet5,
            ChernobogSixDistrictMap
        };

        public static readonly PrtsEnvironmentReference[] RuntimeBackdrops =
        {
            ChernobogStreet0,
            ChernobogCore2,
            ChernobogStreet5
        };
    }
}
#endif
