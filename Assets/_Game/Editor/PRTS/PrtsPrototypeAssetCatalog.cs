#if UNITY_EDITOR
namespace ArknightsACT.Editor.PRTS
{
    internal static class PrtsPrototypeAssetCatalog
    {
        public static readonly PrtsAssetDescriptor Chen = new(
            "陈·战斗",
            "https://prts.wiki/w/%E9%99%88/spine",
            "https://static.prts.wiki/spine/char/char_010_chen/char_010_chen/",
            "char_010_chen",
            "Assets/_Game/Art/Characters/Chen/PRTS/Spine",
            "Player",
            1.64f,
            -0.72f,
            0.38f);

        public static readonly PrtsAssetDescriptor ChenBaseMotion = new(
            "陈·基建动作源",
            "https://prts.wiki/w/%E9%99%88/spine",
            "https://static.prts.wiki/spine/char/char_010_chen/build_char_010_chen/",
            "build_char_010_chen",
            "Assets/_Game/Art/Characters/Chen/PRTS/BaseMotion",
            "MotionSource",
            1.64f,
            -0.72f,
            0.38f);

        public static readonly PrtsAssetDescriptor NormalTreasureChest = new(
            "宝箱·水月",
            "https://prts.wiki/w/%E5%AE%9D%E7%AE%B1%28%E6%B0%B4%E6%9C%88%29",
            "https://torappu.prts.wiki/assets/char_spine/trap_065_normbox/defaultskin/spine/",
            "trap_065_normbox",
            "Assets/_Game/Art/Props/PRTS/NormalTreasureChest/Spine",
            "Treasure",
            0.90f,
            -0.42f,
            0.42f);

        public static readonly PrtsAssetDescriptor SpikeTreasureChest = new(
            "尖刺宝箱·水月",
            "https://prts.wiki/w/%E5%B0%96%E5%88%BA%E5%AE%9D%E7%AE%B1%28%E6%B0%B4%E6%9C%88%29",
            "https://torappu.prts.wiki/assets/char_spine/trap_066_rarebox/defaultskin/spine/",
            "trap_066_rarebox",
            "Assets/_Game/Art/Props/PRTS/SpikeTreasureChest/Spine",
            "SpikeTreasure",
            0.95f,
            -0.44f,
            0.42f);

        public static readonly PrtsAssetDescriptor ChestSeaborn = new(
            "箱形恐鱼",
            "https://prts.wiki/w/%E7%AE%B1%E5%BD%A2%E6%81%90%E9%B1%BC",
            "https://torappu.prts.wiki/assets/enemy_spine/enemy_2035_sybox/",
            "enemy_2035_sybox",
            "Assets/_Game/Art/Enemies/PRTS/ChestSeaborn/Spine",
            "TreasureMonster",
            1.15f,
            -0.55f,
            0.38f);

        public static readonly PrtsAssetDescriptor[] RogueliteTreasureAssets =
        {
            NormalTreasureChest,
            SpikeTreasureChest,
            ChestSeaborn
        };

        public static readonly PrtsAssetDescriptor[] PrototypeEnemies =
        {
            new(
                "源石虫",
                "https://prts.wiki/w/%E6%BA%90%E7%9F%B3%E8%99%AB",
                "https://torappu.prts.wiki/assets/enemy_spine/enemy_1007_slime/",
                "enemy_1007_slime",
                "Assets/_Game/Art/Enemies/PRTS/OriginiumSlug/Spine",
                "Fodder",
                0.72f,
                -0.68f,
                0.40f),
            new(
                "士兵",
                "https://prts.wiki/w/%E5%A3%AB%E5%85%B5",
                "https://torappu.prts.wiki/assets/enemy_spine/enemy_1002_nsabr/",
                "enemy_1002_nsabr",
                "Assets/_Game/Art/Enemies/PRTS/Soldier/Spine",
                "Melee",
                1.50f,
                -0.70f,
                0.38f),
            new(
                "弩手",
                "https://prts.wiki/w/%E5%BC%A9%E6%89%8B",
                "https://torappu.prts.wiki/assets/enemy_spine/enemy_1003_ncbow/",
                "enemy_1003_ncbow",
                "Assets/_Game/Art/Enemies/PRTS/Crossbowman/Spine",
                "Ranged",
                1.48f,
                -0.70f,
                0.38f),
            new(
                "猎狗",
                "https://prts.wiki/w/%E7%8C%8E%E7%8B%97",
                "https://torappu.prts.wiki/assets/enemy_spine/enemy_1000_gopro/",
                "enemy_1000_gopro",
                "Assets/_Game/Art/Enemies/PRTS/Hound/Spine",
                "FastMelee",
                0.92f,
                -0.66f,
                0.36f),
            new(
                "妖怪",
                "https://prts.wiki/w/%E5%A6%96%E6%80%AA",
                "https://torappu.prts.wiki/assets/enemy_spine/enemy_1005_yokai/",
                "enemy_1005_yokai",
                "Assets/_Game/Art/Enemies/PRTS/YokaiDrone/Spine",
                "Flying",
                1.05f,
                -0.05f,
                0.36f),
            new(
                "重装防御者",
                "https://prts.wiki/w/%E9%87%8D%E8%A3%85%E9%98%B2%E5%BE%A1%E8%80%85",
                "https://torappu.prts.wiki/assets/enemy_spine/enemy_1006_shield/",
                "enemy_1006_shield",
                "Assets/_Game/Art/Enemies/PRTS/HeavyDefender/Spine",
                "EliteMelee",
                1.72f,
                -0.71f,
                0.38f)
        };

        public static PrtsAssetDescriptor[] GetFullPrototypePack()
        {
            var result = new PrtsAssetDescriptor[PrototypeEnemies.Length + RogueliteTreasureAssets.Length + 2];
            var index = 0;
            result[index++] = Chen;
            result[index++] = ChenBaseMotion;
            for (var i = 0; i < PrototypeEnemies.Length; i++)
                result[index++] = PrototypeEnemies[i];
            for (var i = 0; i < RogueliteTreasureAssets.Length; i++)
                result[index++] = RogueliteTreasureAssets[i];
            return result;
        }
    }
}
#endif
