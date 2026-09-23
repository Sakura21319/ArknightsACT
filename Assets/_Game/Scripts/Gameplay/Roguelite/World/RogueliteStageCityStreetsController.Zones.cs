using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public sealed partial class RogueliteStageCityStreetsController
    {
        private readonly Material[] _zoneSurfaces = new Material[4];
        private void BuildZoneDetails(Transform cell, RogueliteBlockState block)
        {
            var index = (int)block.Zone;
            if (_zoneSurfaces[index] == null)
                _zoneSurfaces[index] = BuildingMaterial(_paving, "Zone_" + block.Zone, Color.Lerp(CityZoneRules.Color(block.Zone), new Color(.28f, .29f, .28f), .5f), .12f, true);
            CreateBox(cell, "DistrictSurface", new Vector3(0f, .045f, 0f), new Vector3(35.6f, .024f, 29.6f), .008f, _zoneSurfaces[index], false);
            CreateBox(cell, "DistrictStripe", new Vector3(0f, .127f, -3.05f), new Vector3(34f, .012f, .13f), .003f, _zoneSurfaces[index], false);
            SectorLabel(cell, "DistrictAddress", CityZoneRules.Label(block.Zone) + (block.Zone == CityZone.Core ? " / 高危 · 高物资" : ""), new Vector3(-9f, .16f, -3.8f), true);
            if (block.Zone == CityZone.Core)
            {
                var points = new[] { new Vector3(-14.8f, .12f, -4.8f), new Vector3(14.8f, .12f, -4.8f), new Vector3(-14.8f, .12f, 4.8f), new Vector3(14.8f, .12f, 4.8f), new Vector3(-4.8f, .12f, -10f), new Vector3(4.8f, .12f, -10f) };
                var kinds = new[] { SalvageContainerKind.MilitaryCrate, SalvageContainerKind.ArchiveVault, SalvageContainerKind.EquipmentCrate };
                var count = 0;
                foreach (var point in points)
                {
                    if (IsTowerReservation(block, point.x, point.z)) continue;
                    SearchableContainer25D.Create(cell, point, _roofMetal, _windowDark, stageMap.GenerationSeed, kinds[count]);
                    if (++count == kinds.Length) break;
                }
                return;
            }
            if (block.Theme == RogueliteChunkTheme.Facility) return;
            if (block.Zone == CityZone.Outskirts)
            {
                foreach (var x in new[] { -12f, -6f, 6f, 12f })
                {
                    CreateBox(cell, "PerimeterDefensePost", new Vector3(x, .9f, 14.2f), new Vector3(.18f, 1.8f, .18f), .02f, _roofMetal, false);
                    CreateBox(cell, "PerimeterDefensePanel", new Vector3(x, .7f, 14.2f), new Vector3(4.3f, 1.2f, .15f), .03f, _facades[4], true);
                }
            }
            else if (block.Zone == CityZone.Ruins)
            {
                for (var piece = 0; piece < 6; piece++)
                {
                    var side = piece % 2 == 0 ? -1f : 1f;
                    CreateBox(cell, "RuinedPavingSlab", new Vector3(side * 4.8f, .16f, -11f + piece * 3.8f), new Vector3(1.5f, .12f, .75f), .035f, _facades[5], false, Quaternion.Euler(0f, piece * 31f, 0f));
                }
                CreateBox(cell, "BrokenServicePier", new Vector3(-16.5f, .9f, -10f), new Vector3(.65f, 1.8f, .7f), .09f, _facades[2], true);
            }
            else
            {
                for (var tank = 0; tank < 2; tank++)
                {
                    CreateBox(cell, "IndustrialCompressor", new Vector3(-4.6f, 1f, 8f + tank * 2.4f), new Vector3(1.2f, 2f, 1.5f), .12f, _roofMetal, true);
                    CreateBox(cell, "CompressorBand", new Vector3(-4.6f, 1.1f, 7.22f + tank * 2.4f), new Vector3(1.15f, .2f, .04f), .004f, _lanePaint, false);
                }
            }
        }

        private void BuildCivicCore(Transform parent)
        {
            var first = FindBlockTransform(parent.parent, 0); var last = FindBlockTransform(parent.parent, stageMap.Blocks.Count - 1);
            if (first == null || last == null) return;
            var core = new GameObject("[Chernobog_CivicCommandSquare]").transform;
            core.SetParent(parent, false); core.position = (first.position + last.position) * .5f;
            CreateBox(core, "CivicPlaza", new Vector3(0f, .13f, 0f), new Vector3(30f, .02f, 24f), .02f, _paving, false);
            foreach (var side in new[] { -1f, 1f })
            {
                var hall = new GameObject(side > 0 ? "CivicCommandHall" : "CivicArchiveHall").transform;
                hall.SetParent(core, false); hall.localPosition = new Vector3(0f, 0f, side * 7f);
                hall.localRotation = Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                var height = side > 0 ? 8.5f : 6.2f;
                RogueliteStagePlayableArchitectureController.BuildInteriorShell(hall, 16f, 5f, height, _facades[0], _roofMetal, _windowDark, 3f, false);
                var room = hall.GetComponent<EnterableBuilding25D>(); room.SetIdentity(side > 0 ? "市政应急指挥所" : "市政封存档案馆");
                hall.Find("BuildingStencil").gameObject.SetActive(false);
                room.SetNavigationRoute(new[] { core.TransformPoint(new Vector3(-18f, .18f, 0f)), core.TransformPoint(new Vector3(0f, .18f, 0f)), hall.TransformPoint(new Vector3(0f, .18f, -4f)), hall.TransformPoint(new Vector3(0f, .18f, 0f)) });
                CreateBox(hall, "Roof", new Vector3(0f, height, 0f), new Vector3(16.5f, .28f, 5.5f), .06f, _roofMetal, false);
                BuildLotDetail(hall, ChernobogSearchBuildingKind.ArchiveOffice, 16f, 5f, height, _facades[0], _roofMetal, new System.Random(stageMap.GenerationSeed + (int)side));
                foreach (var x in new[] { -7f, -4.5f, 4.5f, 7f })
                    CreateBox(hall, "CivicFacadePier", new Vector3(x, height * .5f, -2.75f), new Vector3(.4f, height, .45f), .04f, _facades[3], false);
                foreach (var face in new[] { -1f, 1f })
                {
                    CreateBox(hall, "MunicipalCornice", new Vector3(0f, height - .6f, face * 2.64f), new Vector3(16.2f, .22f, .22f), .025f, _facades[3], false);
                    foreach (var x in new[] { -6f, -3f, 0f, 3f, 6f })
                    {
                        CreateBox(hall, "ClerestoryFrame", new Vector3(x, height - 1.6f, face * 2.55f), new Vector3(2.1f, .85f, .14f), .025f, _roofMetal, false);
                        CreateBox(hall, "ClerestoryGlass", new Vector3(x, height - 1.6f, face * 2.64f), new Vector3(1.85f, .58f, .025f), .003f, _windowDark, false);
                    }
                }
                var kinds = new[] { SalvageContainerKind.ArchiveVault, SalvageContainerKind.FilingCabinet, SalvageContainerKind.MilitaryCrate, SalvageContainerKind.OfficeDesk };
                for (var i = 0; i < kinds.Length; i++)
                    SearchableContainer25D.Create(hall, new Vector3(i % 2 == 0 ? -1f : 1f, 0f, 0f) * (i < 2 ? 5.5f : 2.8f) + new Vector3(0f, .18f, -.8f), _roofMetal, _windowDark, stageMap.GenerationSeed, kinds[i]);
                BuildCivicUpperFloor(hall, height);
                if (side > 0)
                {
                    CreateBox(hall, "CommandRoofHouse", new Vector3(0f, 10f, .2f), new Vector3(4f, 3f, 3f), .08f, _roofMetal, false);
                    CreateBox(hall, "MunicipalSignalMast", new Vector3(0f, 13f, .2f), new Vector3(.15f, 4f, .15f), .02f, _roofMetal, false);
                }
            }
            SectorLabel(core, "CommandSquareStencil", "市政应急指挥所 / 核心管制区", new Vector3(0f, .17f, -1.5f), true);
        }
    }
}
