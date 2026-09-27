using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Navigation;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public sealed partial class RogueliteStageCityStreetsController
    {
        private CityDistrictVarietyPlan _varietyPlan;
        private CityFacilityController _activeFacilityController;
        private Dictionary<int, Transform> _builtLots;

        private void BuildPlannedDistrictSpaces(Transform root)
        {
            if (_varietyPlan == null) return;
            foreach (var block in stageMap.Blocks)
            {
                if (_varietyPlan.TryGetBattle(block.Index, out var battle) && battle != CityBattleLayout.OpenCross)
                {
                    var cell = FindStreetCell(root, block.Index);
                    if (cell != null) BuildBattleSpace(cell, battle, block.Index);
                }
                if (block.Index == _varietyPlan.QuietBlockIndex)
                {
                    var cell = FindStreetCell(root, block.Index);
                    if (cell != null) BuildQuietAlcove(cell, block);
                }
            }

            foreach (var venue in _varietyPlan.Venues)
            {
                if (venue.Purpose == CityBlockPairPurpose.RooftopRoute) BuildRoofBridge(root, venue);
                else if (venue.Purpose == CityBlockPairPurpose.FreightTransfer) BuildCrossBlockVenue(root, venue);
            }
        }

        private static Transform FindStreetCell(Transform root, int blockIndex)
        {
            foreach (Transform child in root)
                if (child.name.StartsWith($"Block_{blockIndex:00}_CityStreet_")) return child;
            return null;
        }

        private void BuildLotInteriorVariation(Transform building, ChernobogSearchBuildingKind kind,
            CityRoomLayout layout, float width, float depth, Material steel, Material inset)
        {
            if (layout == CityRoomLayout.Open) return;
            var wallMaterial = inset != null ? inset : steel;
            if (layout == CityRoomLayout.SideWings)
            {
                // Two true partitions make side rooms. Their mouths begin past the straight entry
                // corridor and rejoin at the rear, so the door remains visible and traversable.
                foreach (var side in new[] { -1f, 1f })
                    CreateBox(building, "InteriorWingPartition", new Vector3(side * .85f, 1.10f, .38f),
                        new Vector3(.16f, 2.20f, Mathf.Max(1.1f, depth * .42f)), .02f, wallMaterial, true);
                CreateBox(building, "WingRoomLintel", new Vector3(0f, 2.04f, depth * .5f - .55f),
                    new Vector3(Mathf.Min(2.6f, width * .55f), .12f, .12f), .012f, steel, false);
            }
            else if (layout == CityRoomLayout.RearGallery)
            {
                // A rear partition leaves a centered actor-width opening; it creates a second room
                // without ever crossing the doorway-to-center verification line.
                var gap = 1.85f;
                var segment = (width - gap) * .5f;
                foreach (var side in new[] { -1f, 1f })
                    CreateBox(building, "RearGalleryPartition", new Vector3(side * (gap * .5f + segment * .5f), .91f, -.35f),
                        new Vector3(segment, 1.82f, .16f), .02f, wallMaterial, true);
                CreateBox(building, "RearGalleryHeader", new Vector3(0f, 2.08f, -.35f),
                    new Vector3(gap, .18f, .18f), .015f, steel, true);
            }
            else
            {
                // Warehouse shelving gives a readable two-aisle route. The center lane and all
                // standard container approach positions remain clear.
                foreach (var side in new[] { -1f, 1f })
                {
                    CreateBox(building, "WarehouseAisleShelf", new Vector3(side * 1.85f, .82f, -.35f),
                        new Vector3(.72f, 1.64f, 1.1f), .045f, steel, true);
                    CreateBox(building, "WarehouseAisleTop", new Vector3(side * 1.85f, 1.66f, -.35f),
                        new Vector3(.82f, .12f, 1.18f), .025f, inset, false);
                }
            }
            if (kind == ChernobogSearchBuildingKind.AbandonedHouse)
                CreateBox(building, "QuietRoomScreen", new Vector3(-width * .42f, .58f, .20f),
                    new Vector3(.10f, 1.16f, .72f), .02f, steel, true);
        }

        private void BuildDistrictBeacon(Transform building, float width, float depth, float height, string sign)
        {
            // The mast is deliberately above the ordinary roof line, visible across intervening lots;
            // the matching facade sign and existing indoor search containers make it a real destination.
            CreateBox(building, "DistrictSignalMast", new Vector3(0f, height + 2.15f, .18f),
                new Vector3(.16f, 4.3f, .16f), .02f, _roofMetal, false);
            CreateBox(building, "DistrictSignalCrossbar", new Vector3(0f, height + 3.65f, .18f),
                new Vector3(1.35f, .12f, .12f), .02f, _roofMetal, false);
            CreateBox(building, "DistrictSignalBeacon", new Vector3(0f, height + 4.35f, .18f),
                new Vector3(.62f, .58f, .62f), .10f, _windowWarm, false);
            for (var side = -1; side <= 1; side++)
                CreateBox(building, "SignalApproachMark", new Vector3(0f, .132f, -depth * .5f - 1.8f - side * 1.15f),
                    new Vector3(.52f, .018f, .38f), .01f, _windowWarm, false);
            SectorLabel(building, "LandmarkAddress", string.IsNullOrEmpty(sign) ? "档案信号点" : sign,
                new Vector3(0f, height - .50f, -depth * .5f - .2f), false);
        }

        private void BuildBattleSpace(Transform cell, CityBattleLayout layout, int blockIndex)
        {
            var seed = unchecked(stageMap.GenerationSeed ^ blockIndex * 104729);
            var angle = (seed & 1) == 0 ? 16f : -16f;
            switch (layout)
            {
                case CityBattleLayout.BrokenCover:
                    CreateBox(cell, "FightWreck_A", new Vector3(-5.4f, .58f, -4.4f), new Vector3(2.2f, 1.16f, 1.05f), .08f, _roofMetal, true, Quaternion.Euler(0f, angle, 0f));
                    CreateBox(cell, "FightWreck_B", new Vector3(5.4f, .58f, 4.4f), new Vector3(2.2f, 1.16f, 1.05f), .08f, _roofMetal, true, Quaternion.Euler(0f, -angle, 0f));
                    CreateBox(cell, "FightLowCover_C", new Vector3(-4.3f, .42f, 5.5f), new Vector3(1.7f, .84f, .75f), .06f, _facades[2], true);
                    break;
                case CityBattleLayout.RubbleCorridor:
                    CreateBox(cell, "RuinRubble_A", new Vector3(-6.0f, .50f, -4.7f), new Vector3(2.3f, 1f, 1.35f), .10f, _facades[2], true, Quaternion.Euler(0f, 21f, 0f));
                    CreateBox(cell, "RuinRubble_B", new Vector3(5.9f, .39f, 4.9f), new Vector3(1.8f, .78f, 1.15f), .10f, _roofMetal, true, Quaternion.Euler(0f, -27f, 0f));
                    CreateBox(cell, "RuinBrokenWall_A", new Vector3(-7.3f, .53f, .4f), new Vector3(2.4f, 1.06f, .28f), .06f, _facades[1], true, Quaternion.Euler(0f, 11f, 0f));
                    CreateBox(cell, "RuinBrokenWall_B", new Vector3(7.3f, .39f, -.1f), new Vector3(2.2f, .78f, .28f), .06f, _facades[1], true, Quaternion.Euler(0f, -13f, 0f));
                    break;
                case CityBattleLayout.DepotFunnel:
                    CreateBox(cell, "FightCargo_A", new Vector3(-5.6f, .65f, -4.6f), new Vector3(2.1f, 1.3f, 1.4f), .08f, _roofMetal, true);
                    CreateBox(cell, "FightCargo_B", new Vector3(5.6f, .65f, 4.6f), new Vector3(2.1f, 1.3f, 1.4f), .08f, _facades[2], true);
                    CreateBox(cell, "FightFunnelWall_A", new Vector3(-7.4f, .52f, 1.2f), new Vector3(2.8f, 1.04f, .32f), .035f, _facades[4], true, Quaternion.Euler(0f, -18f, 0f));
                    CreateBox(cell, "FightFunnelWall_B", new Vector3(7.4f, .52f, -1.2f), new Vector3(2.8f, 1.04f, .32f), .035f, _facades[4], true, Quaternion.Euler(0f, -18f, 0f));
                    break;
                case CityBattleLayout.OffsetLanes:
                    CreateBox(cell, "FightLaneBarrier_A", new Vector3(-5.8f, .48f, 4.6f), new Vector3(3.1f, .96f, .35f), .03f, _facades[3], true, Quaternion.Euler(0f, angle, 0f));
                    CreateBox(cell, "FightLaneBarrier_B", new Vector3(5.8f, .48f, -4.6f), new Vector3(3.1f, .96f, .35f), .03f, _facades[3], true, Quaternion.Euler(0f, angle, 0f));
                    CreateBox(cell, "FightLaneCover_C", new Vector3(0f, .46f, 5.9f), new Vector3(1.6f, .92f, .8f), .06f, _roofMetal, true);
                    break;
            }
            SectorLabel(cell, "CombatSpaceStencil", layout switch
            {
                CityBattleLayout.BrokenCover => "掩体交叉口",
                CityBattleLayout.RubbleCorridor => "废墟破口 / 碎墙侧翼",
                CityBattleLayout.DepotFunnel => "货运狭道",
                _ => "错列侧巷"
            }, new Vector3(-8.6f, .14f, 5.1f), true);
        }

        private void BuildQuietAlcove(Transform cell, RogueliteBlockState block)
        {
            // Start block is encounter-safe. The reserved south-east lot hosts a small, reachable
            // refuge with seating and a roof, giving the player a real low-pressure pause point.
            var nook = new GameObject("QuietRecoveryAlcove").transform;
            nook.SetParent(cell, false);
            var occupiedCourtX = IsTowerReservation(block, -10.5f, -9.4f) ? 10.5f : -10.5f;
            nook.localPosition = new Vector3(-occupiedCourtX, 0f, -9.4f);
            CreateBox(nook, "QuietPaving", new Vector3(0f, .12f, 0f), new Vector3(6.2f, .08f, 5.2f), .02f, _paving, true);
            CreateBox(nook, "QuietBackWall", new Vector3(0f, 1.1f, 2.45f), new Vector3(6.1f, 2.2f, .18f), .025f, _facades[0], true);
            foreach (var side in new[] { -1f, 1f })
                CreateBox(nook, "QuietSideWall", new Vector3(side * 3.0f, .65f, .35f), new Vector3(.16f, 1.3f, 4.1f), .02f, _facades[0], true);
            CreateBox(nook, "QuietCanopy", new Vector3(0f, 2.45f, .05f), new Vector3(6.1f, .16f, 4.9f), .025f, _roofMetal, false);
            CreateBox(nook, "QuietBenchSeat", new Vector3(0f, .48f, 1.40f), new Vector3(2.1f, .20f, .58f), .035f, _paintTrim, true);
            CreateBox(nook, "QuietBenchBack", new Vector3(0f, .88f, 1.67f), new Vector3(2.1f, .62f, .10f), .025f, _paintTrim, true);
            CreateBox(nook, "QuietWallLamp", new Vector3(0f, 1.85f, 2.32f), new Vector3(.48f, .24f, .08f), .02f, _windowWarm, false);
            SectorLabel(nook, "QuietAddress", "休整角 · 无敌情", new Vector3(0f, .17f, -.75f), true);
        }

        private void BuildCrossBlockVenue(Transform root, CityPlannedBlockPair pair)
        {
            var a = FindBlockTransform(root.parent, pair.A);
            var b = FindBlockTransform(root.parent, pair.B);
            if (a == null || b == null) return;
            var center = (a.position + b.position) * .5f;
            var horizontal = pair.Horizontal;
            var perpendicular = horizontal ? Vector3.forward : Vector3.right;
            var seam = center;
            var venue = new GameObject($"CrossBlockVenue_{pair.A}_{pair.B}_Seed{stageMap.GenerationSeed}").transform;
            venue.SetParent(root, false);
            venue.position = seam;
            venue.rotation = Quaternion.identity;
            // A loading arch marks the transition while keeping the full cardinal route underneath.
            CreateBox(venue, "VenueGantrypost_A", -perpendicular * 4.85f + Vector3.up * 2.55f,
                new Vector3(.72f, 5.1f, .72f), .06f, _roofMetal, true);
            CreateBox(venue, "VenueGantrypost_B", perpendicular * 4.85f + Vector3.up * 2.55f,
                new Vector3(.72f, 5.1f, .72f), .06f, _roofMetal, true);
            CreateBox(venue, "VenueTransferBeam", Vector3.up * 5.15f,
                horizontal ? new Vector3(.95f, .34f, 10.4f) : new Vector3(10.4f, .34f, .95f), .05f, _roofMetal, true);
            foreach (var side in new[] { -1f, 1f })
            {
                CreateBox(venue, "VenueFreightPallet", perpendicular * side * 5.9f + Vector3.up * .55f,
                    new Vector3(1.45f, 1.1f, 1.25f), .05f, _facades[2], false);
                SearchableContainer25D.Create(venue, perpendicular * side * 8.0f + Vector3.up * .18f, _roofMetal, _windowDark,
                    stageMap.GenerationSeed, side < 0f ? SalvageContainerKind.SealedCargo : SalvageContainerKind.StoreShelf);
            }
            SectorLabel(venue, "VenueAddress", pair.Label, new Vector3(0f, .14f, -2.8f), true);
        }

        private void BuildRoofBridge(Transform root, CityPlannedBlockPair pair)
        {
            if (!_builtLots.TryGetValue(CityDistrictVarietyPlan.Key(pair.A, pair.LotA), out var buildingA) ||
                !_builtLots.TryGetValue(CityDistrictVarietyPlan.Key(pair.B, pair.LotB), out var buildingB)) return;
            var roomA = buildingA.GetComponent<EnterableBuilding25D>();
            var roomB = buildingB.GetComponent<EnterableBuilding25D>();
            var widthA = roomA.Interior.size.x - .4f;
            var widthB = roomB.Interior.size.x - .4f;
            var roofA = buildingA.Find("Roof");
            var roofB = buildingB.Find("Roof");
            if (roofA == null || roofB == null) return;
            var roofTop = roofA.localPosition.y + .09f;
            var sideA = Mathf.Sign(buildingA.InverseTransformPoint(buildingB.position).x);
            var sideB = Mathf.Sign(buildingB.InverseTransformPoint(buildingA.position).x);
            if (sideA == 0f) sideA = pair.LotA < 2 ? -1f : 1f;
            if (sideB == 0f) sideB = pair.LotB < 2 ? 1f : -1f;
            var start = buildingA.TransformPoint(new Vector3(sideA * (widthA * .5f + .08f), roofTop, 0f));
            var end = buildingB.TransformPoint(new Vector3(sideB * (widthB * .5f + .08f), roofTop, 0f));
            var delta = end - start;
            var length = new Vector2(delta.x, delta.z).magnitude;
            if (length < 2f || Mathf.Abs(delta.y) > .05f) return;
            var direction = delta.normalized;
            var bridge = new GameObject($"RooftopServiceBridge_{pair.A}_{pair.B}").transform;
            bridge.SetParent(root, false);
            // A small step up also gives the exterior ramp below head clearance.
            bridge.position = (start + end) * .5f + Vector3.up * .25f;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            bridge.rotation = rotation;
            CreateBox(bridge, "RooftopBridgeDeck", Vector3.down * .10f,
                new Vector3(1.7f, .20f, length + .2f), .025f, _roofMetal, true);
            // Leave the first three metres at each end open for the side stair landings.
            foreach (var side in new[] { -1f, 1f })
                CreateBox(bridge, "RooftopBridgeGuardRail", new Vector3(side * .92f, .55f, 0f),
                    new Vector3(.12f, 1.1f, Mathf.Max(.5f, length - 6f)), .015f, _paintTrim, true);
            var points = new[] { start, (start + end) * .5f + Vector3.up * .25f, end };
            bridge.gameObject.AddComponent<CityVerticalRoute>().Configure(points, 2);
            SectorLabel(bridge, "RooftopBridgeAddress", "屋顶检修通道 · 注意边缘", Vector3.up * .03f, true);
        }

        private void AddRooftopAccess(Transform building, float width, float depth, float height, float connectionSide)
        {
            var roofTop = height + .19f;
            // A single shallow exterior flight passes beneath the bridge while still low,
            // then a separate parallel landing returns to the roof at full height.
            var innerX = connectionSide * (width * .5f + .9f);
            var outerX = connectionSide * (width * .5f + 2.3f);
            var frontZ = -depth * .5f + .8f;
            var backZ = depth * .5f + 1.4f;
            var bottom = new Vector3(innerX, .18f, frontZ);
            var top = new Vector3(innerX, roofTop, backZ);
            var landingA = new Vector3(innerX, roofTop, backZ + .35f);
            var landingB = new Vector3(outerX, roofTop, backZ + .35f);
            var outerTop = new Vector3(outerX, roofTop, backZ);
            WalkFlight(building, bottom, top, 1.1f, false);
            WalkDeck(building, (landingA + landingB) * .5f,
                new Vector2(Mathf.Abs(outerX - innerX) + 1.1f, .7f));
            // Roof guardrails stop falls. The bridge-facing side remains open at its midpoint.
            foreach (var zSide in new[] { -1f, 1f })
                CreateBox(building, "RooftopSafetyRail", new Vector3(0f, roofTop + .55f, zSide * (depth * .5f + .11f)),
                    new Vector3(width + .15f, 1.1f, .12f), .015f, _paintTrim, true);
            var seamSide = connectionSide;
            var railLength = Mathf.Max(.5f, depth * .5f - .8f);
            foreach (var zSide in new[] { -1f, 1f })
                CreateBox(building, "RooftopBridgeGateRail", new Vector3(seamSide * (width * .5f + .08f), roofTop + .55f, zSide * (depth * .25f + .4f)),
                    new Vector3(.12f, 1.1f, railLength), .015f, _paintTrim, true);
            CreateBox(building, "RooftopOppositeRail", new Vector3(-seamSide * (width * .5f + .08f), roofTop + .55f, 0f),
                new Vector3(.12f, 1.1f, depth + .1f), .015f, _paintTrim, true);
            var roofWalk = new Vector3(seamSide * (width * .5f - .10f), roofTop, 0f);
            var outerWalk = new Vector3(outerX, roofTop, 0f);
            WalkDeck(building, (outerTop + outerWalk) * .5f,
                new Vector2(1.1f, backZ + .2f));
            WalkDeck(building, (outerWalk + roofWalk) * .5f,
                new Vector2(Mathf.Abs(outerX - roofWalk.x) + .15f, 1.2f));

            var route = new List<Vector3>
            {
                bottom,
                top,
                landingA,
                landingB,
                outerTop,
                outerWalk,
                roofWalk
            };
            var vertical = new GameObject("RooftopAccessRoute").transform;
            vertical.SetParent(building, false);
            for (var i = 0; i < route.Count; i++) route[i] = building.TransformPoint(route[i]);
            vertical.gameObject.AddComponent<CityVerticalRoute>().Configure(route.ToArray());
        }
    }

    /// <summary>One-use alley door. Its reset is the generated street root's lifetime.</summary>
    public sealed class CityReturnShortcut25D : MonoBehaviour
    {
        private Collider _panel;
        private bool _opened;
        public string Label { get; private set; } = "返程捷径";
        public bool Opened => _opened;
        public bool IsActorOnInteriorSide(Vector3 actorPosition)
        {
            var room = GetComponentInParent<EnterableBuilding25D>();
            if (room == null) return false;
            var localActor = room.transform.InverseTransformPoint(actorPosition);
            var localDoor = room.transform.InverseTransformPoint(transform.position);
            return localActor.z < localDoor.z - .35f &&
                   Mathf.Abs(localActor.x) < room.Interior.size.x * .5f &&
                   localActor.y > -.2f && localActor.y < room.Interior.size.y + .5f;
        }
        public void Configure(string label)
        {
            Label = string.IsNullOrEmpty(label) ? "返程捷径" : label;
            _panel = GetComponent<Collider>();
        }
        public bool Open()
        {
            if (_opened) return false;
            _opened = true;
            if (_panel == null) _panel = GetComponent<Collider>();
            if (_panel != null) _panel.enabled = false;
            transform.localRotation = Quaternion.Euler(0f, 104f, 0f);
            var room = GetComponentInParent<EnterableBuilding25D>();
            if (room != null)
            {
                Physics.SyncTransforms();
                var points = new[]
                {
                    room.transform.TransformPoint(new Vector3(.35f, .18f, 0f)),
                    room.transform.TransformPoint(new Vector3(0f, .18f, room.Interior.size.z * .5f)),
                    room.transform.TransformPoint(new Vector3(0f, .18f, room.Interior.size.z * .5f + 1.7f))
                };
                PrototypeNavigationGraph25D.Instance?.AppendRoomRoute(points, 2);
            }
            return true;
        }
    }
}
