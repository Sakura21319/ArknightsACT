using System;
using ArknightsACT.Gameplay.Roguelite.Treasure;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public enum ChernobogSearchBuildingKind { Apartment, Grocery, Canteen, Clinic, Pharmacy, Office, RepairShop, Warehouse, Checkpoint, PowerStation, ArchiveOffice, AbandonedHouse }

    public sealed class ChernobogSearchBuildingProfile
    {
        public readonly ChernobogSearchBuildingKind Kind;
        public readonly string Label;
        public readonly string Stencil;
        public readonly int MinContainers;
        public readonly int MaxContainers;
        public readonly double EmptyChance;
        public readonly SalvageContainerKind[] Common;
        public readonly SalvageContainerKind Rare;
        public readonly double RareChance;
        public ChernobogSearchBuildingProfile(ChernobogSearchBuildingKind kind, string label, string stencil, int min, int max,
            double empty, SalvageContainerKind[] common, SalvageContainerKind rare, double rareChance)
        { Kind = kind; Label = label; Stencil = stencil; MinContainers = min; MaxContainers = max; EmptyChance = empty; Common = common; Rare = rare; RareChance = rareChance; }
    }

    // Authored building/loot grammar, consumed by the existing CityStreets generator.
    public static class ChernobogSearchBuildingProfiles
    {
        private static readonly ChernobogSearchBuildingProfile[] Profiles =
        {
            new(ChernobogSearchBuildingKind.Apartment, "居民公寓", "RES", 1, 2, .25, new[] { SalvageContainerKind.Wardrobe, SalvageContainerKind.KitchenCabinet, SalvageContainerKind.BedsideDrawer }, SalvageContainerKind.PersonalSafe, .06),
            new(ChernobogSearchBuildingKind.Grocery, "杂货铺", "MARKET", 1, 3, .12, new[] { SalvageContainerKind.StoreShelf, SalvageContainerKind.CashRegister, SalvageContainerKind.StockCarton }, SalvageContainerKind.CommercialSafe, .04),
            new(ChernobogSearchBuildingKind.Canteen, "街区食堂", "CANTEEN", 1, 2, .20, new[] { SalvageContainerKind.KitchenCabinet, SalvageContainerKind.Refrigerator, SalvageContainerKind.CashRegister }, SalvageContainerKind.StockCarton, 0),
            new(ChernobogSearchBuildingKind.Clinic, "社区诊所", "CLINIC", 1, 3, .12, new[] { SalvageContainerKind.MedicineCabinet, SalvageContainerKind.MedicalCrate, SalvageContainerKind.OfficeDesk }, SalvageContainerKind.MedicalFridge, .12),
            new(ChernobogSearchBuildingKind.Pharmacy, "药房", "PHARMACY", 1, 2, .12, new[] { SalvageContainerKind.MedicineCabinet, SalvageContainerKind.StockCarton, SalvageContainerKind.CashRegister }, SalvageContainerKind.MedicalFridge, .08),
            new(ChernobogSearchBuildingKind.Office, "市政办公室", "OFFICE", 1, 2, .25, new[] { SalvageContainerKind.OfficeDesk, SalvageContainerKind.FilingCabinet }, SalvageContainerKind.CommercialSafe, .05),
            new(ChernobogSearchBuildingKind.RepairShop, "维修铺", "REPAIR", 1, 2, .15, new[] { SalvageContainerKind.ToolChest, SalvageContainerKind.PartsCabinet, SalvageContainerKind.EquipmentCrate }, SalvageContainerKind.SealedCargo, .08),
            new(ChernobogSearchBuildingKind.Warehouse, "货运仓库", "FREIGHT", 2, 4, .08, new[] { SalvageContainerKind.FreightCrate, SalvageContainerKind.StockCarton, SalvageContainerKind.SealedCargo }, SalvageContainerKind.ValuableCargo, .06),
            new(ChernobogSearchBuildingKind.Checkpoint, "乌萨斯执勤室", "URSUS SEC", 1, 3, .10, new[] { SalvageContainerKind.DutyLocker, SalvageContainerKind.FilingCabinet, SalvageContainerKind.ToolChest }, SalvageContainerKind.MilitaryCrate, .15),
            new(ChernobogSearchBuildingKind.PowerStation, "动力维护站", "POWER", 1, 2, .10, new[] { SalvageContainerKind.ToolChest, SalvageContainerKind.PartsCabinet, SalvageContainerKind.EquipmentCrate }, SalvageContainerKind.OriginiumCase, .06),
            new(ChernobogSearchBuildingKind.ArchiveOffice, "管制档案室", "ARCHIVE", 1, 2, .15, new[] { SalvageContainerKind.FilingCabinet, SalvageContainerKind.OfficeDesk }, SalvageContainerKind.ArchiveVault, .18),
            new(ChernobogSearchBuildingKind.AbandonedHouse, "空置民房", "VACANT", 1, 1, .65, new[] { SalvageContainerKind.BedsideDrawer, SalvageContainerKind.Suitcase }, SalvageContainerKind.PersonalSafe, .01)
        };
        public static ChernobogSearchBuildingProfile Select(ChernobogDistrictType district, Random random)
        {
            var choices = district switch
            {
                ChernobogDistrictType.Residential => new[] { 0, 0, 2, 3, 11 },
                ChernobogDistrictType.Commercial => new[] { 1, 1, 2, 4, 5 },
                ChernobogDistrictType.Industrial => new[] { 6, 7, 7, 9 },
                ChernobogDistrictType.Checkpoint => new[] { 8, 8, 10, 5 },
                ChernobogDistrictType.RuinedBlock => new[] { 11, 11, 11, 0, 6 },
                _ => new[] { 6, 7, 9, 3, 11 }
            };
            return Profiles[choices[random.Next(choices.Length)]];
        }
        public static SalvageContainerKind[] RollContainers(ChernobogSearchBuildingProfile profile, Random random, bool core = false)
        {
            if (random.NextDouble() < profile.EmptyChance * (core ? .35 : 1)) return Array.Empty<SalvageContainerKind>();
            var count = Math.Min(4, random.Next(profile.MinContainers, profile.MaxContainers + 1) + (core ? 1 : 0));
            var result = new SalvageContainerKind[count];
            var offset = random.Next(profile.Common.Length);
            for (var i = 0; i < count; i++) result[i] = profile.Common[(offset + i) % profile.Common.Length];
            // At most one special container per occupied building, replacing a common slot.
            if (random.NextDouble() < profile.RareChance + (core ? .12 : 0)) result[count - 1] = profile.Rare;
            return result;
        }
    }
}
