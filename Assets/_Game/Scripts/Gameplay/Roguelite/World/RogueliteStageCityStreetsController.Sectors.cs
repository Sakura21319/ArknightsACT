using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public sealed partial class RogueliteStageCityStreetsController
    {
        private Font _sectorFont;
        private Material _sectorTextMaterial;
        private static bool HasFacilityCourt(RogueliteBlockState block) => block.Type == RogueliteBlockType.Start || block.Index % 3 == 0;

        private void BuildSectorFeatures(Transform cell, RogueliteBlockState block, CityFacilityController controller)
        {
            var maintenance = stageMap.StageIndex == 2;
            if (maintenance && (block.Zone == CityZone.Industrial || block.Zone == CityZone.Core))
            {
                // High service gantries leave the ground-level cardinal routes open.
                for (var line = 0; line < 2; line++)
                    CreateBox(cell, "DistrictHeatMain", new Vector3(0f, 7.6f + line * .45f, 14.4f), new Vector3(36f, .32f, .32f), .1f, _roofMetal, false);
                foreach (var side in new[] { -1f, 1f })
                {
                    CreateBox(cell, "PipeGantry", new Vector3(side * 17.2f, 3.8f, 14.4f), new Vector3(.22f, 7.6f, .3f), .02f, _roofMetal, false);
                    CreateBox(cell, "FreightGuideRail", new Vector3(0f, .12f, side * 1.6f), new Vector3(35.8f, .025f, .1f), .004f, _roofMetal, false);
                }
                SectorLabel(cell, "MaintenanceStencil", "02 / 动力维护层\nORIGINIUM SERVICE", new Vector3(-15f, .14f, -2f), true);
            }
            if (!HasFacilityCourt(block)) return;
            var court = new GameObject("UtilityCourt").transform;
            court.SetParent(cell, false);
            court.localPosition = new Vector3(IsTowerReservation(block, -10.5f, -9.4f) ? 10.5f : -10.5f, 0f, -9.4f);
            CreateBox(court, "CourtApron", new Vector3(0f, .11f, 0f), new Vector3(7f, .05f, 6.6f), .015f, maintenance ? _roofMetal : kit.deckMaterial, false);
            var kind = block.Type == RogueliteBlockType.Start ? CityFacilityKind.Medical : (CityFacilityKind)((block.Index / 3) % 3);
            var device = new GameObject(kind.ToString()).transform;
            device.SetParent(court, false);
            device.localPosition = new Vector3(-1.6f, .14f, 0f);
            CreateBox(device, "DeviceBase", new Vector3(0f, .55f, 0f), new Vector3(1.25f, 1.1f, .85f), .07f, kind == CityFacilityKind.Medical ? _facades[3] : _roofMetal, true);
            var lamp = CreateBox(device, "StatusLamp", new Vector3(0f, 1.35f, .02f), new Vector3(.85f, .22f, .12f), .02f, _windowWarm, false);
            var screen = CreateBox(device, "TerminalScreen", new Vector3(0f, .9f, .46f), new Vector3(.7f, .35f, .035f), .005f, _windowDark, false);
            if (kind == CityFacilityKind.Relay)
            {
                CreateBox(device, "RelayMast", new Vector3(0f, 2.4f, -.2f), new Vector3(.13f, 3.2f, .13f), .015f, _roofMetal, false);
                for (var arm = 0; arm < 3; arm++)
                    CreateBox(device, "SignalAntenna", new Vector3(0f, 3.1f + arm * .35f, -.2f), new Vector3(1.5f - arm * .3f, .08f, .08f), .015f, _paintTrim, false);
            }
            else if (kind == CityFacilityKind.Medical)
            {
                CreateBox(device, "AidCrossV", new Vector3(0f, 1.9f, 0f), new Vector3(.15f, .65f, .12f), .01f, _windowWarm, false);
                CreateBox(device, "AidCrossH", new Vector3(0f, 1.9f, 0f), new Vector3(.65f, .15f, .12f), .01f, _windowWarm, false);
                CreateBox(court, "ReliefAwning", new Vector3(0f, 3.3f, 0f), new Vector3(5.6f, .12f, 3.1f), .02f, _paintTrim, false);
                foreach (var x in new[] { -2.6f, 2.6f })
                    CreateBox(court, "ReliefPost", new Vector3(x, 1.65f, -1.2f), new Vector3(.1f, 3.3f, .1f), .012f, _roofMetal, false);
            }
            else
            {
                for (var rib = 0; rib < 4; rib++)
                    CreateBox(device, "TransformerFin", new Vector3(-.48f + rib * .32f, 1.1f, -.4f), new Vector3(.1f, 1.25f, .55f), .015f, _roofMetal, false);
            }
            var title = kind == CityFacilityKind.Medical ? "罗德岛应急补给" : kind == CityFacilityKind.Relay ? "天灾观测中继" : "源石配电 / 应急储备";
            SectorLabel(court, "FacilitySign", title, kind == CityFacilityKind.Medical ? new Vector3(0f, 3.15f, -1.65f) : new Vector3(0f, 2.55f, -.6f), false);
            SearchableContainer25D cache = null;
            if (kind == CityFacilityKind.Power)
            {
                SearchableContainer25D.Create(court, new Vector3(1.7f, .18f, 0f), _roofMetal, _windowDark,
                    stageMap.GenerationSeed, maintenance ? SalvageContainerKind.OriginiumCase : SalvageContainerKind.SealedCargo);
                cache = court.GetComponentInChildren<SearchableContainer25D>();
                cache.gameObject.SetActive(false);
                CreateBox(court, "LockedReserveHousing", new Vector3(1.7f, .65f, 0f), new Vector3(1.65f, 1f, 1.3f), .06f, _roofMetal, true);
            }
            var facility = device.gameObject.AddComponent<CityFacility25D>();
            facility.Configure(kind, block.Index, cache, court.Find("LockedReserveHousing"), lamp.GetComponent<Renderer>(), screen.GetComponent<Renderer>());
            controller.Register(facility);
            BuildSecondaryFacility(court, block, controller);
            if (block.Type == RogueliteBlockType.Start || block.Index % 6 == 0)
            {
                var chair = Wheelchair25D.Create(court, new Vector3(-2.9f, .1f, 2.1f), _roofMetal, _paintTrim, _windowWarm);
                controller.RegisterWheelchair(chair);
                if (block.Type == RogueliteBlockType.Start)
                    SectorLabel(court, "WheelchairSign", "应急轮椅\n按住 G 乘坐", new Vector3(-2.9f, 1.5f, 2.1f), false);
            }
        }

        private void BuildSecondaryFacility(Transform court, RogueliteBlockState block, CityFacilityController controller)
        {
            var kind = block.Type == RogueliteBlockType.Start
                ? CityFacilityKind.WaterStation
                : (block.Index / 3 + stageMap.StageIndex) % 3 == 0 ? CityFacilityKind.ChargeStation
                : (block.Index / 3 + stageMap.StageIndex) % 3 == 1 ? CityFacilityKind.ScrapCache
                : CityFacilityKind.WaterStation;
            var device = new GameObject(kind.ToString()).transform;
            device.SetParent(court, false);
            device.localPosition = new Vector3(1.7f, .14f, 2.2f);
            if (kind == CityFacilityKind.ScrapCache)
            {
                CreateBox(device, "ScrapBase", new Vector3(0f, .22f, 0f), new Vector3(1.35f, .42f, 1.05f), .06f, _roofMetal, true);
                CreateBox(device, "ScrapChunkA", new Vector3(-.3f, .52f, .1f), new Vector3(.6f, .28f, .5f), .04f, _paintTrim, false, Quaternion.Euler(0f, 18f, 0f));
                CreateBox(device, "ScrapChunkB", new Vector3(.28f, .5f, -.16f), new Vector3(.5f, .24f, .42f), .035f, _facades[3], false, Quaternion.Euler(0f, -24f, 0f));
                CreateBox(device, "ScrapChunkC", new Vector3(.05f, .74f, .05f), new Vector3(.38f, .2f, .34f), .03f, _roofMetal, false, Quaternion.Euler(0f, 41f, 0f));
            }
            else
            {
                CreateBox(device, "DeviceBase", new Vector3(0f, .45f, 0f), new Vector3(.95f, .9f, .7f), .06f, kind == CityFacilityKind.WaterStation ? _facades[3] : _roofMetal, true);
                if (kind == CityFacilityKind.ChargeStation)
                {
                    for (var ring = 0; ring < 3; ring++)
                        CreateBox(device, "ChargeCoil", new Vector3(0f, .38f + ring * .26f, 0f), new Vector3(1.1f - ring * .12f, .07f, .82f - ring * .1f), .015f, _windowWarm, false);
                    CreateBox(device, "ChargeTip", new Vector3(0f, 1.12f, 0f), new Vector3(.18f, .3f, .18f), .02f, _windowWarm, false);
                }
                else
                {
                    CreateBox(device, "WaterTank", new Vector3(0f, 1.05f, -.08f), new Vector3(.7f, .55f, .55f), .08f, _roofMetal, false);
                    CreateBox(device, "WaterTap", new Vector3(0f, .62f, .4f), new Vector3(.12f, .2f, .12f), .015f, _windowWarm, false);
                }
            }
            var lamp = CreateBox(device, "StatusLamp", new Vector3(0f, kind == CityFacilityKind.ScrapCache ? .95f : 1.32f, .02f), new Vector3(.6f, .16f, .1f), .015f, _windowWarm, false);
            var screen = CreateBox(device, "TerminalScreen", new Vector3(0f, kind == CityFacilityKind.ScrapCache ? .18f : .72f, kind == CityFacilityKind.ScrapCache ? .55f : .4f), new Vector3(.5f, .24f, .03f), .005f, _windowDark, false);
            SectorLabel(court, "SecondaryFacilitySign", kind == CityFacilityKind.ChargeStation ? "源石充能桩" : kind == CityFacilityKind.WaterStation ? "应急净水点" : "废料回收堆",
                new Vector3(1.7f, 1.85f, 2.2f), false);
            var facility = device.gameObject.AddComponent<CityFacility25D>();
            facility.Configure(kind, block.Index, null, null, lamp.GetComponent<Renderer>(), screen.GetComponent<Renderer>());
            controller.Register(facility);
        }

        private void SectorLabel(Transform parent, string name, string text, Vector3 position, bool ground)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(ground ? 90f : 0f, 0f, 0f);
            var label = go.AddComponent<TextMesh>(); label.text = text; label.characterSize = ground ? .19f : .16f;
            if (_sectorFont == null)
            {
                _sectorFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 40);
                var shader = Resources.Load<Shader>("CityWorldText");
                _sectorTextMaterial = shader != null ? new Material(shader) : new Material(_sectorFont.material);
                _buildingMaterials.Add(_sectorTextMaterial);
                Font.textureRebuilt += RefreshSectorFont;
            }
            _sectorFont.RequestCharactersInTexture(text, 40);
            RefreshSectorFont(_sectorFont);
            label.font = _sectorFont; go.GetComponent<Renderer>().sharedMaterial = _sectorTextMaterial;
            label.fontSize = 40; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = new Color(.74f, .78f, .65f);
        }
        private void RefreshSectorFont(Font font)
        {
            if (font == _sectorFont && _sectorTextMaterial != null) _sectorTextMaterial.mainTexture = font.material.mainTexture;
        }
    }
}
