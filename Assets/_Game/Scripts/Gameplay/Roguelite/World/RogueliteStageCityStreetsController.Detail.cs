using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public sealed partial class RogueliteStageCityStreetsController
    {
        private readonly List<Material> _buildingMaterials = new();
        private Material[] _facades;
        private Material _windowDark;
        private Material _windowWarm;
        private Material _roofMetal;
        private Material _paintTrim;
        private Texture2D _concreteGrain;
        private Material _paving;
        private Material _roadPatch;

        private void PrepareBuildingMaterials()
        {
            if (_facades != null) return;
            var source = kit.wallMaterial != null ? kit.wallMaterial : kit.deckMaterial;
            _concreteGrain = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = "City_QuietConcrete", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[4096];
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var shade = .87f + Hash01(x * 97 + y * 271) * .11f;
                if (y % 32 == 0) shade *= .88f;
                pixels[y * 64 + x] = new Color(shade, shade, shade);
            }
            _concreteGrain.SetPixels(pixels); _concreteGrain.Apply(true, true);
            _facades = new[]
            {
                BuildingMaterial(source, "ResidentialLimestone", new Color(.48f, .46f, .40f), .18f, true),
                BuildingMaterial(source, "MunicipalBlueGrey", new Color(.35f, .42f, .46f), .18f, true),
                BuildingMaterial(source, "IndustrialConcrete", new Color(.32f, .34f, .33f), .12f, true),
                BuildingMaterial(source, "ClinicPlaster", new Color(.62f, .65f, .60f), .24f, true),
                BuildingMaterial(source, "SecurityOlive", new Color(.36f, .40f, .34f), .16f, true),
                BuildingMaterial(source, "WeatheredBrick", new Color(.43f, .34f, .29f), .14f, true)
            };
            _windowDark = BuildingMaterial(source, "SmokedGlass", new Color(.11f, .20f, .24f), .72f, false);
            _windowWarm = BuildingMaterial(source, "ShelteredWindow", new Color(.58f, .45f, .25f), .48f, false);
            _windowWarm.EnableKeyword("_EMISSION");
            if (_windowWarm.HasProperty("_EmissionColor")) _windowWarm.SetColor("_EmissionColor", new Color(.24f, .15f, .055f));
            _roofMetal = BuildingMaterial(source, "StandingSeamRoof", new Color(.21f, .26f, .28f), .3f, false);
            _paintTrim = BuildingMaterial(source, "FadedMunicipalPaint", new Color(.26f, .43f, .44f), .2f, false);
            _paving = BuildingMaterial(source, "CourtyardPaving", new Color(.38f, .40f, .37f), .12f, true);
            _roadPatch = BuildingMaterial(source, "RoadRepair", new Color(.16f, .18f, .18f), .06f, true);
        }
        private Material BuildingMaterial(Material source, string label, Color color, float smoothness, bool textured)
        {
            var material = new Material(source) { name = "City_" + label };
            foreach (var property in new[] { "_BaseColor", "_Color" }) if (material.HasProperty(property)) material.SetColor(property, color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", textured ? .02f : .22f);
            if (textured)
                foreach (var property in new[] { "_BaseMap", "_MainTex" })
                    if (material.HasProperty(property)) { material.SetTexture(property, _concreteGrain); material.SetTextureScale(property, new Vector2(3f, 2f)); }
            _buildingMaterials.Add(material); return material;
        }
        private Material LotFacade(ChernobogSearchBuildingKind kind, System.Random random) => kind switch
        {
            ChernobogSearchBuildingKind.Clinic or ChernobogSearchBuildingKind.Pharmacy => _facades[3],
            ChernobogSearchBuildingKind.Checkpoint or ChernobogSearchBuildingKind.ArchiveOffice => _facades[4],
            ChernobogSearchBuildingKind.Warehouse or ChernobogSearchBuildingKind.PowerStation or ChernobogSearchBuildingKind.RepairShop => _facades[2],
            ChernobogSearchBuildingKind.AbandonedHouse => _facades[5],
            ChernobogSearchBuildingKind.Apartment => _facades[random.Next(2) == 0 ? 0 : 5],
            _ => _facades[random.Next(2)]
        };

        private void BuildStreetAmbience(Transform cell, ChernobogDistrictType district, int seed)
        {
            var random = new System.Random(seed);
            var steel = kit.steelMaterial != null ? kit.steelMaterial : _roofMetal;
            // Low-profile repairs and slab joints provide human scale without obstructing travel.
            for (var patch = 0; patch < 2; patch++)
                CreateBox(cell, "AsphaltRepair", new Vector3(-12f + (float)random.NextDouble() * 24f, .111f, patch == 0 ? -1.8f : 1.1f),
                    new Vector3(1.4f + (float)random.NextDouble() * 2f, .012f, .5f), .015f, _roadPatch, false);
            for (var joint = -3; joint <= 3; joint++)
                foreach (var side in new[] { -1f, 1f })
                    CreateBox(cell, "PavementExpansionJoint", new Vector3(joint * 4.8f, .118f, side * 3.62f), new Vector3(.035f, .006f, 1.3f), .002f, _roofMetal, false);
            // Street furniture occupies sidewalk pockets; doorway approaches remain on the outer lots.
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * (6.8f + (float)random.NextDouble());
                var z = side * 4.4f;
                CreateBox(cell, "StreetLightPole", new Vector3(x, 1.8f, z), new Vector3(.12f, 3.6f, .12f), .018f, _roofMetal, false);
                CreateBox(cell, "StreetLightArm", new Vector3(x, 3.6f, z - side * .38f), new Vector3(.1f, .1f, .9f), .012f, _roofMetal, false);
                CreateBox(cell, "StreetLightPanel", new Vector3(x, 3.55f, z - side * .8f), new Vector3(.4f, .07f, .65f), .01f, _windowWarm, false);
                // Recessed grilles and painted repairs add scale to otherwise empty pavement.
                CreateBox(cell, "SidewalkDrain", new Vector3(x - side * 1.2f, .11f, side * 3.0f), new Vector3(1.1f, .018f, .35f), .003f, _roofMetal, false);
                for (var bar = 0; bar < 5; bar++)
                    CreateBox(cell, "DrainSlat", new Vector3(x - side * 1.6f + bar * .19f, .122f, side * 3f), new Vector3(.045f, .014f, .3f), .002f, steel, false);
            }
            if (district == ChernobogDistrictType.Residential || district == ChernobogDistrictType.Commercial)
            {
                CreateBox(cell, "StreetBenchSeat", new Vector3(-4.8f, .46f, 4.2f), new Vector3(1.5f, .14f, .5f), .03f, _paintTrim, true);
                CreateBox(cell, "StreetBenchBack", new Vector3(-4.8f, .82f, 4.43f), new Vector3(1.5f, .55f, .08f), .025f, _paintTrim, false);
                foreach (var side in new[] { -1f, 1f })
                    CreateBox(cell, "BenchLeg", new Vector3(-4.8f + side * .55f, .2f, 4.2f), new Vector3(.09f, .4f, .42f), .015f, steel, false);
            }
        }

        private void BuildLotDetail(Transform building, ChernobogSearchBuildingKind kind, float width, float depth, float height,
            Material wall, Material steel, System.Random random)
        {
            var front = -depth * .5f - .17f;
            var residential = kind == ChernobogSearchBuildingKind.Apartment;
            var warehouse = kind == ChernobogSearchBuildingKind.Warehouse;
            CreateBox(building, "LotPaving", new Vector3(0f, .12f, 0f), new Vector3(width + 1.1f, .022f, depth + .75f), .025f, _paving, false);
            CreateBox(building, "EntranceDoormat", new Vector3(0f, .151f, -depth * .5f - .48f), new Vector3(1.65f, .016f, .7f), .015f, _roofMetal, false);
            SectorLabel(building, "EntranceAddress", building.GetComponent<EnterableBuilding25D>().DisplayName,
                new Vector3(0f, 2.98f, front - .2f), false);
            if (kind == ChernobogSearchBuildingKind.RepairShop || warehouse)
            {
                for (var stripe = 0; stripe < 4; stripe++)
                    CreateBox(building, "LoadingSafetyStripe", new Vector3(-width * .4f + stripe * .35f, .145f, front - .35f), new Vector3(.16f, .008f, .55f), .003f, _lanePaint, false, Quaternion.Euler(0f, 25f, 0f));
            }
            else if (random.Next(3) == 0)
            {
                // Shallow wall-mounted letterboxes stay outside the entrance clearance.
                CreateBox(building, "ResidentLetterbox", new Vector3(-width * .36f, 1.05f, front - .12f), new Vector3(.65f, .45f, .2f), .025f, _paintTrim, false);
                CreateBox(building, "LetterSlot", new Vector3(-width * .36f, 1.14f, front - .23f), new Vector3(.43f, .035f, .015f), .002f, _windowDark, false);
            }
            // Separate plinth, panel seams and roof coping give the facade depth under oblique lighting.
            foreach (var side in new[] { -1f, 1f })
            {
                CreateBox(building, "FoundationCourse", new Vector3(side * (width * .25f + .6f), .3f, front), new Vector3(width * .5f - 1.25f, .34f, .2f), .025f, _roofMetal, false);
                CreateBox(building, "CornerPier", new Vector3(side * (width * .5f - .06f), height * .5f, front), new Vector3(.17f, height, .23f), .02f, steel, false);
                CreateBox(building, "RainDownpipe", new Vector3(side * (width * .5f + .2f), height * .5f, depth * .32f), new Vector3(.1f, height, .1f), .025f, _roofMetal, false);
                for (var level = 0; level < (residential ? 2 : 1); level++)
                {
                    var y = 1.45f + level * 2.15f;
                    var x = side * width * .31f;
                    CreateBox(building, "RecessedWindowFrame", new Vector3(x, y, front), new Vector3(1.18f, 1.06f, .14f), .02f, steel, false);
                    CreateBox(building, "WindowGlass", new Vector3(x, y, front - .08f), new Vector3(1f, .87f, .025f), .003f, random.Next(5) == 0 ? _windowWarm : _windowDark, false);
                    CreateBox(building, "WindowMullion", new Vector3(x, y, front - .1f), new Vector3(.045f, .87f, .025f), .003f, steel, false);
                    CreateBox(building, "WindowSill", new Vector3(x, y - .55f, front - .11f), new Vector3(1.3f, .09f, .35f), .015f, wall, false);
                }
            }
            CreateBox(building, "Cornice", new Vector3(0f, height - .14f, front), new Vector3(width + .25f, .22f, .28f), .03f, steel, false);
            CreateBox(building, "EntranceLightHousing", new Vector3(0f, 2.55f, front - .13f), new Vector3(.65f, .13f, .22f), .015f, _roofMetal, false);
            CreateBox(building, "EntranceLight", new Vector3(0f, 2.48f, front - .16f), new Vector3(.48f, .035f, .15f), .005f, _windowWarm, false);
            // Industrial roofs and stepped apartment roof houses break the repeated box silhouette.
            if (warehouse)
            {
                foreach (var side in new[] { -1f, 1f })
                    CreateBox(building, "PitchedRoof", new Vector3(side * width * .25f, height + .55f, 0f), new Vector3(width * .53f, .16f, depth + .5f), .025f, _roofMetal, false, Quaternion.Euler(0f, 0f, -side * 17f));
            }
            else if (residential || kind == ChernobogSearchBuildingKind.Office)
            {
                var offset = random.Next(2) == 0 ? -1f : 1f;
                CreateBox(building, "RoofServiceHouse", new Vector3(offset * width * .2f, height + .5f, depth * .18f), new Vector3(width * .43f, 1f, depth * .5f), .05f, wall, false);
                CreateBox(building, "RoofHouseCap", new Vector3(offset * width * .2f, height + 1.04f, depth * .18f), new Vector3(width * .45f, .12f, depth * .54f), .02f, _roofMetal, false);
            }
            else
                CreateBox(building, "RearParapet", new Vector3(0f, height + .3f, depth * .5f), new Vector3(width + .15f, .5f, .16f), .02f, steel, false);
            if (kind == ChernobogSearchBuildingKind.Grocery || kind == ChernobogSearchBuildingKind.Canteen || kind == ChernobogSearchBuildingKind.Pharmacy)
            {
                CreateBox(building, "PaintedShopFascia", new Vector3(0f, 3.02f, front - .04f), new Vector3(width * .88f, .45f, .14f), .015f, _paintTrim, false);
                for (var stripe = -2; stripe <= 2; stripe++)
                    CreateBox(building, "AwningStripe", new Vector3(stripe * width * .16f, 2.80f, front - .3f), new Vector3(width * .075f, .02f, .82f), .002f, wall, false);
            }
            // Rear-wall objects tell a small story without occupying search stances or the central aisle.
            var rear = depth * .5f - .16f;
            CreateBox(building, "InteriorNoticeBoard", new Vector3(0f, 1.8f, rear), new Vector3(.85f, .65f, .035f), .005f, _roofMetal, false);
            for (var note = 0; note < 3; note++)
                CreateBox(building, "PinnedPaper", new Vector3(-.25f + note * .23f, 1.83f - note % 2 * .16f, rear - .025f), new Vector3(.16f, .25f, .012f), .002f, wall, false, Quaternion.Euler(0f, 0f, -8f + note * 6f));
            if (random.Next(3) == 0)
                CreateBox(building, "FacadeRepairPlate", new Vector3(width * .32f, .52f, front - .035f), new Vector3(.8f, .42f, .025f), .006f, _paintTrim, false);
        }
    }
}
