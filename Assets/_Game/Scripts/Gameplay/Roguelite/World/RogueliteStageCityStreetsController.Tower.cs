using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public sealed partial class RogueliteStageCityStreetsController
    {
        private bool IsTowerReservation(RogueliteBlockState block, float localX, float localZ)
        {
            if (stageMap.StageIndex > 2) return false;
            var x = (block.Coordinate.x - (stageMap.Width - 1) * .5f) * ChunkWidth + localX;
            var z = (block.Coordinate.y - (stageMap.Height - 1) * .5f) * ChunkDepth + localZ;
            return Mathf.Abs(x) < 16f && Mathf.Abs(z) < 12f;
        }

        private void BuildCentralTower(Transform parent, CityFacilityController facilities)
        {
            var first = FindBlockTransform(parent.parent, 0);
            var last = FindBlockTransform(parent.parent, stageMap.Blocks.Count - 1);
            if (first == null || last == null) return;
            var landmark = new GameObject("[Chernobog_CentralDispatchTower]").transform;
            landmark.SetParent(parent, false); landmark.position = (first.position + last.position) * .5f;
            var structure = new GameObject("TowerStructure").transform; structure.SetParent(landmark, false);
            landmark.gameObject.AddComponent<CityTowerLandmark>().Configure(structure);
            var armor = _facades[2];
            var red = BuildingMaterial(armor, "TowerOxideRed", new Color(.37f, .095f, .065f), .18f, true);
            var beacon = BuildingMaterial(armor, "TowerAmberBeacon", new Color(.95f, .33f, .06f), .2f, false);
            beacon.EnableKeyword("_EMISSION"); beacon.SetColor("_EmissionColor", new Color(1.2f, .22f, .025f));
            CreateBox(landmark, "TowerPlaza", new Vector3(0f, .13f, 0f), new Vector3(29f, .02f, 24f), .02f, _paving, false);
            // Four buttresses and a raised shaft preserve both cardinal lanes underneath the tower.
            foreach (var x in new[] { -5.5f, 5.5f })
            foreach (var z in new[] { -4.8f, 4.8f })
            {
                CreateBox(structure, "ArmoredButtress", new Vector3(x, 3f, z), new Vector3(3.1f, 6f, 3.1f), .18f, armor, true);
                CreateBox(structure, "ButtressBoot", new Vector3(x, .5f, z), new Vector3(3.6f, 1f, 3.6f), .12f, _roofMetal, true);
                CreateBox(structure, "WarningInset", new Vector3(x, 2f, z - 1.6f), new Vector3(1.8f, .35f, .04f), .01f, _lanePaint, false);
            }
            CreateBox(structure, "TransferTruss", new Vector3(0f, 6.8f, 0f), new Vector3(15f, 1.6f, 13f), .18f, _roofMetal, true);
            CreateBox(structure, "DispatchShaft", new Vector3(0f, 19f, 0f), new Vector3(6.8f, 24f, 6.8f), .14f, armor, true);
            for (var floor = 0; floor < 4; floor++)
            {
                var y = 10f + floor * 5.2f;
                CreateBox(structure, "MaintenanceRing", new Vector3(0f, y, 0f), new Vector3(9.2f, .4f, 9.2f), .09f, _roofMetal, false);
                foreach (var side in new[] { -1f, 1f })
                {
                    CreateBox(structure, "RingRail", new Vector3(0f, y + .65f, side * 4.5f), new Vector3(9.2f, .12f, .10f), .015f, _paintTrim, false);
                    CreateBox(structure, "RingRail", new Vector3(side * 4.5f, y + .65f, 0f), new Vector3(.10f, .12f, 9.2f), .015f, _paintTrim, false);
                    CreateBox(structure, "ShaftWindowStrip", new Vector3(0f, y + 1.5f, side * 3.45f), new Vector3(4.5f, .45f, .08f), .01f, _windowWarm, false);
                }
            }
            foreach (var side in new[] { -1f, 1f })
            {
                CreateBox(structure, "ExposedThermalConduit", new Vector3(side * 3.7f, 19f, 2.4f), new Vector3(.5f, 24f, .5f), .1f, _roofMetal, false);
                CreateBox(structure, "RedArmorBlade", new Vector3(side * 3.48f, 21f, -.9f), new Vector3(.16f, 16f, 2.4f), .02f, red, false);
                CreateBox(structure, "ControlWing", new Vector3(side * 4.5f, 32.5f, 0f), new Vector3(3f, 4f, 7.5f), .18f, armor, false);
            }
            CreateBox(structure, "CommandCrown", new Vector3(0f, 32.5f, 0f), new Vector3(10.8f, 4f, 9f), .25f, _roofMetal, true);
            CreateBox(structure, "CommandGlazing", new Vector3(0f, 32.4f, -4.55f), new Vector3(8.8f, 1.2f, .1f), .015f, _windowWarm, false);
            CreateBox(structure, "AerialSpine", new Vector3(0f, 38.5f, 0f), new Vector3(.45f, 9f, .45f), .05f, _roofMetal, false);
            CreateBox(structure, "Beacon", new Vector3(0f, 43.2f, 0f), new Vector3(.65f, .75f, .65f), .06f, beacon, false);
            for (var aerial = 0; aerial < 3; aerial++)
                CreateBox(structure, "AerialCrossbar", new Vector3(0f, 36.5f + aerial * 1.4f, 0f), new Vector3(5f - aerial, .15f, .15f), .02f, _roofMetal, false);
            SectorLabel(structure, "TowerNumber", "URSUS\n02", new Vector3(0f, 28.5f, -3.55f), false);
            structure.Find("TowerNumber").GetComponent<TextMesh>().characterSize = .3f;
            SectorLabel(landmark, "TowerPlaque", "切尔诺伯格 · 城区动力调度塔", new Vector3(0f, .16f, -11f), true);
            for (var stripe = -5; stripe <= 5; stripe++)
                CreateBox(landmark, "PlazaHazardStripe", new Vector3(stripe * 1.8f, .16f, -10f), new Vector3(.35f, .015f, 1f), .005f, _lanePaint, false, Quaternion.Euler(0f, 25f, 0f));
            var blockIndex = Mathf.Min(stageMap.Blocks.Count - 1, stageMap.Height / 2 * stageMap.Width + stageMap.Width / 2);
            BuildTowerConsole(landmark, facilities, CityFacilityKind.Overload, new Vector3(-11f, .15f, -7.5f), blockIndex, SalvageContainerKind.ValuableCargo);
            BuildTowerConsole(landmark, facilities, CityFacilityKind.PressureVent, new Vector3(11f, .15f, -7.5f), blockIndex, SalvageContainerKind.EquipmentCrate);
            var link = landmark.gameObject.AddComponent<CityRelayLink>();
            var a = BuildTowerConsole(landmark, facilities, CityFacilityKind.RelaySwitch, new Vector3(-10f, .15f, 7.5f), blockIndex, null);
            var b = BuildTowerConsole(landmark, facilities, CityFacilityKind.RelaySwitch, new Vector3(10f, .15f, 7.5f), blockIndex, SalvageContainerKind.ArchiveVault);
            link.Configure(a, b); a.Link = link; b.Link = link;
            BuildTowerAscent(landmark);
        }

        private CityFacility25D BuildTowerConsole(Transform parent, CityFacilityController controller, CityFacilityKind kind, Vector3 position, int block, SalvageContainerKind? reward)
        {
            var console = new GameObject($"TowerConsole_{kind}_{position.x}").transform; console.SetParent(parent, false); console.localPosition = position;
            CreateBox(console, "ControlPlinth", new Vector3(0f, .5f, 0f), new Vector3(1.1f, 1f, .8f), .05f, _roofMetal, true);
            var screen = CreateBox(console, "SwitchScreen", new Vector3(0f, 1.15f, 0f), new Vector3(.85f, .35f, .1f), .015f, _windowWarm, false);
            SearchableContainer25D cache = null;
            Transform housing = null;
            if (reward.HasValue)
            {
                SearchableContainer25D.Create(console, new Vector3(2.5f, .03f, 0f), _roofMetal, _windowDark, stageMap.GenerationSeed, reward);
                cache = console.GetComponentInChildren<SearchableContainer25D>(); cache.gameObject.SetActive(false);
                housing = CreateBox(console, "LockedRewardCabinet", new Vector3(2.5f, .65f, 0f), new Vector3(1.5f, 1.3f, 1f), .05f, _roofMetal, true).transform;
            }
            var facility = console.gameObject.AddComponent<CityFacility25D>();
            facility.Configure(kind, block, cache, housing, screen.GetComponent<Renderer>(), null); controller.Register(facility);
            SectorLabel(console, "ConsoleLabel", facility.Label, new Vector3(0f, 1.85f, -.2f), false);
            if (kind == CityFacilityKind.PressureVent)
            {
                var warning = new GameObject("SteamWarningZone").transform; warning.SetParent(console, false);
                for (var segment = 0; segment < 24; segment++)
                {
                    var angle = segment * Mathf.PI / 12f;
                    CreateBox(warning, "WarningArc", new Vector3(Mathf.Sin(angle) * 4f, .04f, Mathf.Cos(angle) * 4f), new Vector3(.65f, .015f, .16f), .005f, _lanePaint, false, Quaternion.Euler(0f, segment * 15f, 0f));
                }
                warning.gameObject.SetActive(false); facility.WarningZone = warning.gameObject;
            }
            return facility;
        }
    }
}
