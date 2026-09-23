using UnityEngine;
using ArknightsACT.Gameplay.Roguelite.Collectibles;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    public enum SalvageContainerShape { Case, Bin, Pile, Cabinet, Drawer, Shelf, Register, Carton, Safe, Cargo }

    public sealed class SalvageContainerProfile
    {
        public readonly string Label;
        public readonly int Tier;
        public readonly SalvageContainerShape Shape;
        public readonly int MinItems;
        public readonly int MaxItems;
        public SalvageContainerProfile(string label, int tier, SalvageContainerShape shape, int minItems, int maxItems)
        { Label = label; Tier = tier; Shape = shape; MinItems = minItems; MaxItems = maxItems; }
        public Vector3 Size => Shape switch
        {
            SalvageContainerShape.Cabinet => new Vector3(0.85f, 1.55f, 0.55f),
            SalvageContainerShape.Shelf => new Vector3(1.15f, 1.4f, 0.48f),
            SalvageContainerShape.Bin => new Vector3(0.75f, 0.85f, 0.65f),
            SalvageContainerShape.Pile => new Vector3(1.1f, 0.35f, 0.6f),
            SalvageContainerShape.Drawer => new Vector3(0.85f, 0.8f, 0.55f),
            SalvageContainerShape.Register => new Vector3(0.65f, 0.65f, 0.5f),
            SalvageContainerShape.Safe => new Vector3(0.85f, 1.0f, 0.65f),
            SalvageContainerShape.Cargo => new Vector3(1.15f, 0.85f, 0.65f),
            SalvageContainerShape.Carton => new Vector3(0.7f, 0.55f, 0.55f),
            _ => new Vector3(0.85f, 0.6f, 0.58f)
        };
    }

    // One table for labels, grade, mesh silhouettes and item counts. No item categories required.
    public static class SalvageContainerProfiles
    {
        private static readonly SalvageContainerProfile[] Profiles =
        {
            new("垃圾箱", 1, SalvageContainerShape.Bin, 1, 2),
            new("废料堆", 1, SalvageContainerShape.Pile, 1, 2),
            new("遗弃行李", 2, SalvageContainerShape.Case, 1, 2),
            new("车辆后备箱", 2, SalvageContainerShape.Cargo, 1, 3),
            new("衣柜", 2, SalvageContainerShape.Cabinet, 1, 3),
            new("厨房橱柜", 1, SalvageContainerShape.Drawer, 1, 2),
            new("床头柜", 1, SalvageContainerShape.Drawer, 1, 2),
            new("商店货架", 2, SalvageContainerShape.Shelf, 1, 3),
            new("收银机", 2, SalvageContainerShape.Register, 1, 2),
            new("库存纸箱", 1, SalvageContainerShape.Carton, 1, 3),
            new("冷藏柜", 2, SalvageContainerShape.Cabinet, 1, 3),
            new("药品柜", 3, SalvageContainerShape.Cabinet, 1, 3),
            new("医疗补给箱", 3, SalvageContainerShape.Case, 2, 3),
            new("医用冷藏箱", 4, SalvageContainerShape.Cabinet, 1, 2),
            new("办公抽屉", 2, SalvageContainerShape.Drawer, 1, 2),
            new("文件柜", 2, SalvageContainerShape.Cabinet, 1, 3),
            new("私人保险箱", 4, SalvageContainerShape.Safe, 1, 2),
            new("商用保险柜", 5, SalvageContainerShape.Safe, 1, 2),
            new("维修工具箱", 2, SalvageContainerShape.Case, 1, 3),
            new("零件柜", 3, SalvageContainerShape.Drawer, 2, 3),
            new("工业设备箱", 3, SalvageContainerShape.Cargo, 1, 3),
            new("普通运输箱", 2, SalvageContainerShape.Cargo, 2, 4),
            new("封装货箱", 3, SalvageContainerShape.Cargo, 2, 4),
            new("贵重货物箱", 5, SalvageContainerShape.Cargo, 1, 2),
            new("执勤储物柜", 3, SalvageContainerShape.Cabinet, 1, 3),
            new("军用补给箱", 4, SalvageContainerShape.Case, 2, 3),
            new("封存档案柜", 4, SalvageContainerShape.Safe, 1, 2),
            new("源石设备封存箱", 5, SalvageContainerShape.Safe, 1, 2),
        };
        private static readonly SalvageContainerProfile Legacy = new("物资箱", 2, SalvageContainerShape.Case, 2, 4);
        public static SalvageContainerProfile Get(SalvageContainerKind kind)
        {
            var index = (int)kind - (int)SalvageContainerKind.TrashBin;
            return index >= 0 && index < Profiles.Length ? Profiles[index] : Legacy;
        }
        public static Vector3 Size(SalvageContainerKind kind) => kind switch
        {
            SalvageContainerKind.Wardrobe => new Vector3(1.05f, 1.8f, .58f),
            SalvageContainerKind.Refrigerator or SalvageContainerKind.MedicalFridge => new Vector3(.85f, 1.7f, .62f),
            SalvageContainerKind.MedicineCabinet => new Vector3(.8f, 1.3f, .45f),
            SalvageContainerKind.FilingCabinet => new Vector3(.7f, 1.45f, .55f),
            SalvageContainerKind.DutyLocker => new Vector3(.65f, 1.85f, .55f),
            SalvageContainerKind.PersonalSafe => new Vector3(.65f, .65f, .55f),
            SalvageContainerKind.BedsideDrawer => new Vector3(.62f, .55f, .48f),
            SalvageContainerKind.Suitcase => new Vector3(.7f, .45f, .4f),
            _ => Get(kind).Size
        };
        // Per-rarity masses, normalized over available rarities by the inventory roller.
        private static readonly float[,] Weights =
        {
            {97f, 2.8f, 0.19f, 0.01f},
            {84f, 14f, 1.8f, 0.2f},
            {62f, 30f, 7f, 1f},
            {35f, 43f, 19f, 3f},
            {15f, 40f, 37f, 8f}
        };
        public static float RarityWeight(int tier, SalvageRarity rarity) => Weights[Mathf.Clamp(tier, 1, 5) - 1, Mathf.Clamp((int)rarity, 0, 3)];
        public static string TierName(int tier) => tier switch { 1 => "杂物", 2 => "日常", 3 => "专业", 4 => "贵重", _ => "封存" };
    }
}
