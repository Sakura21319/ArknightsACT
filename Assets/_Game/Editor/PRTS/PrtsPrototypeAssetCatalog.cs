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

        // Ch'en's base/dorm model is kept as a hidden locomotion source. The visible model remains
        // the combat skeleton so the weapon and combat attachments never disappear during movement.
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

        // Texas is retained only as an optional local reference while the prototype player moves to Ch'en.
        public static readonly PrtsAssetDescriptor Texas = new(
            "德克萨斯·战斗",
            "https://prts.wiki/w/%E5%BE%B7%E5%85%8B%E8%90%A8%E6%96%AF/spine",
            "https://static.prts.wiki/spine/char/char_102_texas/char_102_texas/",
            "char_102_texas",
            "Assets/_Game/Art/Characters/Texas/PRTS/Spine",
            "PlayerReference",
            1.62f,
            -0.72f,
            0.38f);

        public static readonly PrtsAssetDescriptor TexasBaseMotion = new(
            "德克萨斯·基建动作源",
            "https://prts.wiki/w/%E5%BE%B7%E5%85%8B%E8%90%A8%E6%96%AF/spine",
            "https://static.prts.wiki/spine/char/char_102_texas/build_char_102_texas/",
            "build_char_102_texas",
            "Assets/_Game/Art/Characters/Texas/PRTS/BaseMotion",
            "MotionSourceReference",
            1.62f,
            -0.72f,
            0.38f);

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
            var result = new PrtsAssetDescriptor[PrototypeEnemies.Length + 2];
            result[0] = Chen;
            result[1] = ChenBaseMotion;
            for (var i = 0; i < PrototypeEnemies.Length; i++)
                result[i + 2] = PrototypeEnemies[i];
            return result;
        }
    }
}
#endif
