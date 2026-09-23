#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.World;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEditor;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEngine;

namespace ArknightsACT.Editor
{
    // Isolated batch validation: uses the production map, street, room and container implementations.
    // Does not load or save PrototypeRun, change PlayerPrefs or start a gameplay run.
    public static class MobileCityGenerationValidation
    {
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static void ValidateZoneEncounters(RogueliteStageRuntimeController runtime, RogueliteStageMapController map, List<string> report)
        {
            var template = new GameObject("ZoneTestEnemy"); template.SetActive(false);
            template.AddComponent<ArknightsACT.Combat.Health>().SetMaxHealth(100f);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var templates = typeof(RogueliteStageRuntimeController).GetField("enemyTemplates", flags);
            var previous = templates.GetValue(runtime);
            templates.SetValue(runtime, new[] { template, template, template, template });
            var blockType = typeof(RogueliteStageRuntimeController).GetNestedType("BlockRuntime", BindingFlags.NonPublic);
            try
            {
                map.GenerateStageWithSeed(1, 21319);
                foreach (var zone in new[] { CityZone.Outskirts, CityZone.Core, CityZone.Industrial })
                {
                    var root = new GameObject("ZoneEncounterTest");
                    try
                    {
                        var data = new RogueliteBlockState(1, Vector2Int.one, RogueliteBlockType.Combat, RogueliteChunkTheme.Street, false, false, false);
                        data.SetZone(zone);
                        var block = Activator.CreateInstance(blockType, true);
                        blockType.GetField("Data").SetValue(block, data);
                        blockType.GetField("ContentRoot").SetValue(block, root.transform);
                        Call(runtime, "SpawnEncounter", block);
                        var enemies = root.GetComponentsInChildren<ArknightsACT.Combat.Health>();
                        Require(enemies.Length == (zone == CityZone.Core ? 5 : 3), "Zone encounter population mismatch");
                        var expected = zone == CityZone.Core ? 155f : zone == CityZone.Industrial ? 115f : 100f;
                        Require(enemies.All(x => Mathf.Abs(x.MaxHealth - expected) < .01f), "Zone encounter health mismatch");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                var ordinaryCount = 0; var coreCount = 0;
                for (var seed = 0; seed < 2000; seed++)
                {
                    var profile = ChernobogSearchBuildingProfiles.Select(ChernobogDistrictType.Checkpoint, new System.Random(seed));
                    ordinaryCount += ChernobogSearchBuildingProfiles.RollContainers(profile, new System.Random(seed)).Length;
                    coreCount += ChernobogSearchBuildingProfiles.RollContainers(profile, new System.Random(seed), true).Length;
                }
                Require(coreCount > ordinaryCount * 1.3f, "Core loot density did not increase");
                report.Add("PASS: production encounter spawning: outskirts 3x100 HP, industrial 3x115 HP, core 5x155 HP; core container density >1.3x across 2000 paired samples.");
            }
            finally { templates.SetValue(runtime, previous); UnityEngine.Object.DestroyImmediate(template); }
        }

        private static void ValidateVerticalRoutes(GameObject root, ArknightsACT.Gameplay.Navigation.PrototypeNavigationGraph25D graph, List<string> report)
        {
            foreach (var route in root.GetComponentsInChildren<CityVerticalRoute>())
            {
                graph.AppendRoomRoute(route.Points, 1);
                var path = new List<Vector3>();
                Require(graph.TryBuildPath(route.Points[0], route.Points.Last(), path), "Vertical graph disconnected");
                Require(graph.TryBuildPath(Vector3.zero, route.Points.Last(), new List<Vector3>()), "Vertical route has no street connection");
                var actor = new GameObject("StairTraversalProbe");
                var controller = actor.AddComponent<CharacterController>();
                controller.height = 1.8f; controller.radius = .36f; controller.center = Vector3.up * .9f;
                controller.stepOffset = .3f; controller.slopeLimit = 45f; controller.skinWidth = .025f;
                try
                {
                    foreach (var reverse in new[] { false, true })
                    {
                        var points = reverse ? route.Points.Reverse().ToArray() : route.Points;
                        controller.enabled = false; actor.transform.position = points[0] + Vector3.up * .04f; controller.enabled = true;
                        var fall = 0f;
                        for (var index = 1; index < points.Length; index++)
                        {
                            var target = points[index]; var ticks = 0;
                            while (ticks++ < 1200)
                            {
                                var delta = target - actor.transform.position; delta.y = 0f;
                                if (delta.magnitude < .09f && Mathf.Abs(target.y - actor.transform.position.y) < .3f) break;
                                fall = controller.isGrounded ? -2f : Mathf.Max(-30f, fall - 24f / 60f);
                                controller.Move(Vector3.ClampMagnitude(delta, 4.5f / 60f) + Vector3.up * (fall / 60f));
                            }
                            Require(ticks < 1200, $"Stair walk blocked {route.name}, reverse={reverse}, node={index}, actor={actor.transform.position}, target={target}");
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(actor); }
            }
            if (root.GetComponentsInChildren<CityVerticalRoute>().Length > 0)
                report.Add("PASS: CharacterController walked every vertical route up and down without jumping; upper-floor / summit navigation connected.");
        }

        public static void Run()
        {
            var report = new List<string>();
            var owner = new GameObject("MapValidation");
            owner.SetActive(false);
            var kit = ScriptableObject.CreateInstance<ChernobogEnvironmentKit>();
            var material = new Material(Shader.Find("Standard"));
            kit.wallMaterial = kit.deckMaterial = kit.deckHeavyMaterial = kit.deckSecondaryMaterial =
                kit.steelMaterial = kit.insetMaterial = kit.grateMaterial = material;
            var success = false;
            try
            {
                ValidateLootProfiles(owner, report);
                ValidateMinimapProjection(report);
                ValidateExplorationGuide(material, report);
                var map = owner.AddComponent<RogueliteStageMapController>();
                var signatures = new HashSet<string>();
                var neighbors = new List<int>();
                for (var seed = 0; seed < 128; seed++)
                for (var stage = 1; stage <= 3; stage++)
                {
                    map.GenerateStageWithSeed(stage, seed);
                    var signature = Signature(map);
                    signatures.Add(signature);
                    map.GenerateStageWithSeed(stage, seed);
                    Require(Signature(map) == signature, "Seed failed to reproduce layout");
                    Require(map.Blocks.Count == (stage == 1 ? 12 : stage == 2 ? 16 : 20), "Wrong dimensions");
                    Require(map.Blocks.Count(x => x.Type == RogueliteBlockType.Start) == 1, "Start count");
                    Require(map.Blocks.Count(x => x.Type == RogueliteBlockType.Boss) == 1, "Boss count");
                    Require(map.Blocks.Count(x => x.Theme == RogueliteChunkTheme.Facility) == 1, "Facility count");
                    Require(map.Blocks.Select(x => x.Zone).Distinct().Count() == 4, "Missing city zone");
                    Require(map.Blocks.Where(x => x.Zone == CityZone.Core).All(x => x.Type != RogueliteBlockType.Shop), "Safe shop in high-risk core");
                    if (stage == 2)
                    {
                        Require(map.Blocks.All(x => RogueliteStageDistrictTemplateController.ResolveDistrict(x, x.Index, stage) != ChernobogDistrictType.Residential), "Maintenance layer uses residential grammar");
                        Require(map.Blocks.Count(x => RogueliteStageDistrictTemplateController.ResolveDistrict(x, x.Index, stage) == ChernobogDistrictType.Industrial) >= 4, "Missing industrial district majority");
                    }
                    var visited = new HashSet<int> { map.StartIndex };
                    var pending = new Queue<int>(); pending.Enqueue(map.StartIndex);
                    while (pending.Count > 0)
                    {
                        map.GetNeighborIndices(pending.Dequeue(), neighbors);
                        foreach (var next in neighbors) if (visited.Add(next)) pending.Enqueue(next);
                    }
                    Require(visited.Count == map.Blocks.Count, "Disconnected block graph");
                }
                Require(signatures.Count > 350, "Insufficient layout variety");
                report.Add($"PASS: 384 seeded stage plans; deterministic replay, dimensions, roles, connectivity; {signatures.Count} distinct layouts.");
                var streets = owner.AddComponent<RogueliteStageCityStreetsController>();
                streets.Configure(map, kit);
                var runtime = owner.AddComponent<RogueliteStageRuntimeController>();
                runtime.Configure(null, null, map, null, null, null, material, material, material, material, material, material, material, material);
                ValidateZoneEncounters(runtime, map, report);
                foreach (var geometrySeed in new[] { 21319, 42, 931 })
                for (var stage = 1; stage <= 3; stage++)
                {
                    map.GenerateStageWithSeed(stage, geometrySeed);
                    var root = new GameObject("GeometryValidation");
                    try
                    {
                        foreach (var block in map.Blocks)
                        {
                            var center = new Vector3(block.Coordinate.x * 36f, 0f, block.Coordinate.y * 30f);
                            Call(runtime, "BuildChunk", block, center, root.transform);
                        }
                        Call(streets, "Build", root.transform);
                        typeof(RogueliteStageRuntimeController).GetField("_stageRoot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, root);
                        Call(runtime, "BuildNavigationGraph");
                        var graph = root.GetComponentInChildren<ArknightsACT.Gameplay.Navigation.PrototypeNavigationGraph25D>();
                        Physics.SyncTransforms();
                        var rooms = root.GetComponentsInChildren<EnterableBuilding25D>();
                        var containers = root.GetComponentsInChildren<SearchableContainer25D>();
                        Require(rooms.Length >= map.Blocks.Count * 2, "Too few rooms");
                        Require(containers.Length >= map.Blocks.Count * 2, "Too few search containers");
                        var emptyRooms = rooms.Count(x => x.transform.Find("ShellBack") != null && x.GetComponentsInChildren<SearchableContainer25D>().Length == 0);
                        Require(emptyRooms > 0, "No empty buildings in sample");
                        Require(containers.Select(x => x.Kind).Distinct().Count() >= 10, "Insufficient container variety");
                        var allContainers = root.GetComponentsInChildren<SearchableContainer25D>(true);
                        Require(allContainers.Select(x => x.RewardSeed).Distinct().Count() == allContainers.Length, "Duplicate container seeds including locked rewards");
                        ValidateFacilities(root, map, report);
                        ValidateTower(root, map, report);
                        foreach (var room in rooms)
                        {
                            var route = (Vector3[])typeof(EnterableBuilding25D).GetField("_route", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(room);
                            if (route != null) graph.AppendRoomRoute(route);
                        }
                        ValidateVerticalRoutes(root, graph, report);
                        foreach (var room in rooms)
                        {
                            if (room.transform.Find("ShellBack") == null) continue;
                            foreach (var wall in room.GetComponentsInChildren<BoxCollider>())
                            {
                                if (!wall.name.StartsWith("Shell")) continue;
                                var half = Vector3.Scale(wall.size * .49f, wall.transform.lossyScale);
                                foreach (var other in Physics.OverlapBox(wall.transform.TransformPoint(wall.center), half, wall.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                                {
                                    if (other.transform.IsChildOf(room.transform) || other.bounds.max.y < .25f) continue;
                                    Require(!Physics.ComputePenetration(wall, wall.transform.position, wall.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out var penetration) || penetration < .02f,
                                        $"Building overlap seed={geometrySeed}: {room.name}/{wall.name} vs {other.name} at {other.transform.position} parent={other.transform.parent.name}");
                                }
                            }
                            var path = new List<Vector3>();
                            Require(graph.TryBuildPath(Vector3.zero, room.transform.position + Vector3.up * 0.18f, path), $"Disconnected room: {room.name}");
                            var from = room.transform.TransformPoint(new Vector3(0f, 0.9f, -10f));
                            var to = room.transform.TransformPoint(new Vector3(0f, 0.9f, 0f));
                            Require(!Physics.SphereCast(from, 0.28f, (to - from).normalized, out var hit,
                                (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore), $"Blocked road/door: {room.name}, {hit.collider?.name}");
                            foreach (var container in room.GetComponentsInChildren<SearchableContainer25D>())
                            {
                                var local = room.transform.InverseTransformPoint(container.transform.position);
                                var stand = room.transform.TransformPoint(local + new Vector3(0f, 0.7f, -0.8f));
                                Require(!Physics.CheckSphere(stand, 0.25f, ~0, QueryTriggerInteraction.Ignore), $"Blocked search stance: {room.name}");
                                Require(room.Contains(container.transform.position), "Container outside room");
                            }
                        }
                        report.Add($"PASS: Seed {geometrySeed}, Stage {stage}, {map.Width * 36}x{map.Height * 30} units, {rooms.Length} rooms, {containers.Length} containers, {emptyRooms} empty rooms, {containers.Select(x => x.Kind).Distinct().Count()} container kinds; no external wall overlaps; door corridors/search stances clear; connected room navigation; unique loot seeds.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                success = true;
            }
            catch (Exception e) { report.Add("FAIL: " + e); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(kit);
                UnityEngine.Object.DestroyImmediate(material);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/MobileCityGenerationValidation.txt", report);
                foreach (var line in report) Debug.Log(line);
                EditorApplication.Exit(success ? 0 : 1);
            }
        }

        private static void ValidateExplorationGuide(Material material, List<string> report)
        {
            var owner = new GameObject("GuideValidation"); owner.SetActive(false);
            var root = new GameObject("GuideStage");
            var actor = new GameObject("GuideActor");
            var item = ScriptableObject.CreateInstance<CollectibleDefinition>();
            GameObject nextStage = null;
            try
            {
                var map = owner.AddComponent<RogueliteStageMapController>(); map.GenerateStageWithSeed(1, 17); map.Blocks[0].MarkExplored();
                var runtime = owner.AddComponent<RogueliteStageRuntimeController>();
                runtime.Configure(actor.transform, null, map, null, null, null, material, material, material, material, material, material, material, material);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(RogueliteStageRuntimeController).GetField("_stageRoot", flags).SetValue(runtime, root);
                var extraction = new GameObject("GuideExtraction"); extraction.transform.SetParent(root.transform);
                extraction.transform.position = new Vector3(-54f, 0f, -24f);
                typeof(RogueliteStageRuntimeController).GetField("_extractionMarker", flags).SetValue(runtime, extraction);
                EnterableBuilding25D Room(string name, Vector3 position)
                {
                    var roomObject = new GameObject(name); roomObject.transform.SetParent(root.transform); roomObject.transform.position = position;
                    var room = roomObject.AddComponent<EnterableBuilding25D>(); room.Configure(Vector3.up, new Vector3(6f, 4f, 5f)); room.SetIdentity(name); return room;
                }
                var first = Room("TestApartment", new Vector3(-54f, 0f, -30f));
                var next = Room("KnownClinic", new Vector3(-45f, 0f, -30f));
                Room("HiddenWarehouse", new Vector3(54f, 0f, 30f));
                SearchableContainer25D.Create(first.transform, Vector3.zero, material, material, 17, SalvageContainerKind.BedsideDrawer);
                var container = first.GetComponentInChildren<SearchableContainer25D>();
                var health = actor.AddComponent<ArknightsACT.Combat.Health>(); health.SetMaxHealth(100f);
                var guide = owner.AddComponent<CityExplorationGuide>(); guide.Configure(map, runtime);
                actor.transform.position = first.transform.position; guide.Refresh(actor.transform);
                Require(first.Visited && guide.CurrentRoom == first && first.UnsearchedContainers == 1, "Room entry/search state not recorded");
                Require(!container.Initialized, "Guide rolled loot before search");
                Require(guide.TargetLabel.StartsWith("KnownClinic"), "Guide revealed unknown building or ignored known one");
                container.EnsureSlots((_, _) => item); foreach (var slot in container.Slots) container.Reveal(slot);
                guide.Refresh(actor.transform);
                Require(first.UnsearchedContainers == 0 && first.RemainingContainers == 1, "Revealed loot mistaken for collected loot");
                foreach (var slot in container.Slots) container.Take(slot);
                guide.Refresh(actor.transform); Require(first.RemainingContainers == 0, "Room completion not refreshed");
                actor.transform.position = next.transform.position; guide.Refresh(actor.transform);
                Require(next.Visited && next.ContainerCount == 0 && guide.VisitedRooms == 2, "Empty room / visit count incorrect");
                Require(guide.TargetLabel.StartsWith("探索相邻街区"), "Unknown room name leaked into guidance");
                guide.CycleMode(); guide.Refresh(actor.transform);
                Require(guide.Mode == 1 && guide.TargetPosition == extraction.transform.position, "Return guidance wrong");
                Require(guide.Hint.Contains("按 E"), "Missing extraction action hint");
                guide.CycleMode(); health.SetCurrentHealth(10f); guide.Refresh(actor.transform); Require(guide.Hint.Contains("生命偏低"), "Missing low health guidance");
                nextStage = new GameObject("NextGuideStage");
                typeof(RogueliteStageRuntimeController).GetField("_stageRoot", flags).SetValue(runtime, nextStage);
                guide.Refresh(actor.transform); Require(guide.Mode == 0 && guide.VisitedRooms == 0 && guide.CurrentRoom == null, "Stale guide state after stage switch");
                Require(CityExplorationGuide.Direction(Vector3.forward * 10f) == "北" && CityExplorationGuide.Direction(Vector3.left * 10f) == "西", "Wrong guide compass");
                report.Add("PASS: exploration guide: room visits, unknown/revealed/taken/empty states, no automatic loot rolls, no unknown-building leak, return target, low-health hint, stage reset and compass.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(item); if (nextStage != null) UnityEngine.Object.DestroyImmediate(nextStage);
            }
        }

        private static void ValidateFacilities(GameObject root, RogueliteStageMapController map, List<string> report)
        {
            var controller = root.GetComponentInChildren<CityFacilityController>();
            Require(controller != null && controller.Facilities.Count >= 3, "Missing interactive courts");
            Require(controller.Facilities.Select(x => x.Kind).Distinct().Count() >= 3, "Missing facility kind");
            Require(controller.Facilities.Select(x => x.StableId).Distinct().Count() == controller.Facilities.Count, "Duplicate facility IDs");
            var actor = new GameObject("FacilityTestActor");
            var health = actor.AddComponent<ArknightsACT.Combat.Health>(); health.SetMaxHealth(100f);
            try
            {
                var visited = map.Blocks.Count(x => x.Explored);
                foreach (var facility in controller.Facilities)
                {
                    if ((int)facility.Kind > (int)CityFacilityKind.Power) continue;
                    actor.transform.position = facility.transform.position + Vector3.forward * 2f;
                    Require(!Physics.CheckSphere(actor.transform.position + Vector3.up * .9f, .28f, ~0, QueryTriggerInteraction.Ignore), "Facility approach blocked");
                    if (facility.Kind == CityFacilityKind.Medical) Require(!facility.CanUse(health), "Full-health aid consumed");
                    health.SetCurrentHealth(50f);
                    controller.Tick(actor.transform, true, .5f);
                    Require(controller.Progress > 0f, "Facility inaccessible / no progress");
                    controller.Tick(actor.transform, false, .1f); Require(controller.Progress == 0f, "Release did not cancel");
                    controller.Tick(actor.transform, true, .5f);
                    health.SetCurrentHealth(40f); controller.Tick(actor.transform, true, .1f);
                    Require(controller.Progress == 0f && !facility.Used, "Damage did not interrupt");
                    controller.Tick(actor.transform, true, .5f);
                    actor.transform.position += Vector3.forward; controller.Tick(actor.transform, true, .1f);
                    Require(controller.Progress == 0f && !facility.Used, "Movement/range did not interrupt");
                    actor.transform.position = facility.transform.position + Vector3.forward * 2f;
                    controller.Tick(actor.transform, true, facility.Duration);
                    Require(facility.Used, "Facility did not activate");
                    Require(!facility.Activate(health, controller), "Facility rewards repeated");
                    if (facility.Kind == CityFacilityKind.Medical) Require(Mathf.Abs(health.CurrentHealth - 70f) < .01f, "Wrong heal fraction");
                    if (facility.Kind == CityFacilityKind.Relay) Require(controller.IsSurveyed(facility.BlockIndex), "Relay failed to survey");
                    if (facility.Kind == CityFacilityKind.Power)
                        Require(facility.transform.parent.GetComponentInChildren<SearchableContainer25D>() != null, "Powered cache not searchable");
                    health.SetCurrentHealth(100f);
                }
                Require(map.Blocks.Count(x => x.Explored) == visited, "Survey incorrectly records exploration / skips encounter");
                report.Add($"PASS: Stage {map.StageIndex} facilities: all three kinds, clear approaches, hold/release/movement/damage cancellation, one-use effects, survey separated from visits.");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        private static void ValidateTower(GameObject root, RogueliteStageMapController map, List<string> report)
        {
            var tower = root.GetComponentInChildren<CityTowerLandmark>();
            Require((tower != null) == (map.StageIndex == 2), "Tower appears on wrong stage");
            foreach (var room in root.GetComponentsInChildren<EnterableBuilding25D>())
            {
                if (room.transform.Find("ShellBack") == null || !room.name.Contains("_Seed")) continue;
                var p = room.transform.localPosition;
                Require(Mathf.Abs(Mathf.Abs(p.x) - 11.2f) < .01f && Mathf.Abs(Mathf.Abs(p.z) - 10.2f) < .01f, "Housing alignment changed");
                Require(Mathf.Abs(Mathf.DeltaAngle(room.transform.localEulerAngles.y, 0f)) < .01f || Mathf.Abs(Mathf.DeltaAngle(room.transform.localEulerAngles.y, 180f)) < .01f, "Housing rotated off grid");
            }
            if (tower == null)
            {
                if (map.StageIndex == 1)
                {
                    var civic = root.GetComponentsInChildren<EnterableBuilding25D>().Where(x => x.name.StartsWith("Civic")).ToArray();
                    Require(civic.Length == 2 && civic.All(x => x.GetComponentsInChildren<SearchableContainer25D>().Length == 5), "Civic landmark / archives missing");
                }
                return;
            }
            var center = tower.transform.position;
            Require((center - new Vector3((map.Width - 1) * 18f, 0f, (map.Height - 1) * 15f)).sqrMagnitude < .01f, "Tower is not central");
            foreach (var direction in new[] { Vector3.forward, Vector3.right })
                Require(!Physics.SphereCast(center - direction * 12f + Vector3.up * .9f, .3f, direction, out var hit, 24f, ~0, QueryTriggerInteraction.Ignore), $"Tower ground lane blocked: {hit.collider?.name}");
            Require(tower.Structure.GetComponentsInChildren<Renderer>().Max(x => x.bounds.max.y) > 43f, "Landmark too short");
            var controller = root.GetComponentInChildren<CityFacilityController>();
            Require(controller.Facilities.Select(x => x.Kind).Distinct().Count() == 6, "Missing tower interactions");
            foreach (var facility in controller.Facilities.Where(x => (int)x.Kind > 2))
                Require(!Physics.CheckSphere(facility.transform.position + Vector3.forward * 2f + Vector3.up * .9f, .28f, ~0, QueryTriggerInteraction.Ignore), "Tower console approach blocked");
            var actor = new GameObject("RiskActor"); var health = actor.AddComponent<ArknightsACT.Combat.Health>(); health.SetMaxHealth(100f);
            var bystander = new GameObject("RiskBystander"); var otherHealth = bystander.AddComponent<ArknightsACT.Combat.Health>(); otherHealth.SetMaxHealth(100f);
            var outside = new GameObject("RiskOutside"); var outsideHealth = outside.AddComponent<ArknightsACT.Combat.Health>(); outsideHealth.SetMaxHealth(100f);
            try
            {
                var overload = controller.Facilities.First(x => x.Kind == CityFacilityKind.Overload);
                actor.transform.position = overload.transform.position + Vector3.forward * 2f;
                controller.IsAuthority = false; Require(!overload.Activate(health, controller), "Replica applied authority effect"); controller.IsAuthority = true;
                health.SetCurrentHealth(20f); Require(!overload.Activate(health, controller), "Overload accepted lethal payment"); health.SetCurrentHealth(100f);
                Require(overload.Activate(health, controller) && Mathf.Abs(health.CurrentHealth - 80f) < .01f, "Overload payment wrong");
                Require(overload.GetComponentInChildren<SearchableContainer25D>() != null && !overload.Activate(health, controller), "Overload reward repeated / missing");
                var vent = controller.Facilities.First(x => x.Kind == CityFacilityKind.PressureVent);
                actor.transform.position = vent.transform.position + Vector3.forward * 2f; health.SetCurrentHealth(100f);
                bystander.transform.position = vent.transform.position + Vector3.left * 2f;
                outside.transform.position = vent.transform.position + Vector3.forward * 5f;
                Require(vent.Activate(health, controller) && vent.State == CityFacilityState.Armed && vent.WarningZone.activeSelf, "Vent warning not armed");
                vent.Advance(vent.Deadline - .01f); Require(health.CurrentHealth == 100f, "Vent hit before warning elapsed");
                vent.Advance(vent.Deadline); vent.Advance(vent.Deadline + 1f);
                Require(health.CurrentHealth == 85f && otherHealth.CurrentHealth == 85f && outsideHealth.CurrentHealth == 100f, "Vent range/damage/deduplication wrong");
                Require(vent.Used && !vent.WarningZone.activeSelf && vent.GetComponentInChildren<SearchableContainer25D>() != null, "Vent reward/warning completion wrong");
                var ends = controller.Facilities.Where(x => x.Kind == CityFacilityKind.RelaySwitch).ToArray(); var link = ends[0].Link;
                Require(link.Activate(ends[0], controller, 100f), "Relay start failed");
                Require(!link.Activate(ends[0], controller, 101f), "Same endpoint finished relay");
                link.Advance(112f); Require(ends[0].State == CityFacilityState.Ready && !ends[1].Used, "Relay timeout did not reset");
                Require(link.Activate(ends[1], controller, 200f) && link.Activate(ends[0], controller, 211f), "Relay reverse-order completion failed");
                Require(ends.All(x => x.Used) && ends.Any(x => x.GetComponentInChildren<SearchableContainer25D>() != null), "Relay reward missing");
                Require(!link.Activate(ends[0], controller, 212f), "Relay reward repeated");
                Require(ends.All(x => x.Revision > 0), "Missing state revisions");
                var snapshot = ends[0].CaptureState(212f);
                Require(snapshot.Id == ends[0].StableId && snapshot.State == CityFacilityState.Spent && snapshot.RemainingSeconds == 0f, "Invalid network-ready snapshot");
                report.Add("PASS: central 43m tower, clear ground cross, aligned housing; authority guard, nonlethal overload payment, warned area damage to all actors, safe outside radius, one-use caches, relay timeout/retry/reverse-order completion.");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(bystander); UnityEngine.Object.DestroyImmediate(outside); }
        }

        private static void ValidateMinimapProjection(List<string> report)
        {
            foreach (var size in new[] { new Vector2Int(4, 3), new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(2, 6) })
            {
                var corner = new Vector3(size.x * 18f, 0f, size.y * 15f);
                Require((RogueliteMinimapGraphic.WorldToNormalized(-corner, size.x, size.y) - Vector2.zero).sqrMagnitude < .00001f, "Minimap southwest mapping");
                Require((RogueliteMinimapGraphic.WorldToNormalized(corner, size.x, size.y) - Vector2.one).sqrMagnitude < .00001f, "Minimap northeast mapping");
                Require((RogueliteMinimapGraphic.WorldToNormalized(Vector3.zero, size.x, size.y) - Vector2.one * .5f).sqrMagnitude < .00001f, "Minimap center mapping");
            }
            report.Add("PASS: minimap projection, cardinal corners and center across four map aspect ratios.");
        }

        // Optional graphics-enabled batch entry point. Produces diagnostic views of production geometry.
        public static void RenderPreview()
        {
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
                var owner = new GameObject("PreviewControllers"); owner.SetActive(false);
                var map = owner.AddComponent<RogueliteStageMapController>(); map.GenerateStageWithSeed(1, 21319);
                var kit = ScriptableObject.CreateInstance<ChernobogEnvironmentKit>();
                var material = new Material(Shader.Find("Standard")); material.color = new Color(.42f, .46f, .48f);
                kit.wallMaterial = kit.deckMaterial = kit.deckHeavyMaterial = kit.deckSecondaryMaterial = kit.steelMaterial = kit.insetMaterial = kit.grateMaterial = material;
                var actor = new GameObject("PreviewPlayer"); actor.transform.position = new Vector3(-54f, .2f, -30f);
                var runtime = owner.AddComponent<RogueliteStageRuntimeController>();
                runtime.Configure(actor.transform, null, map, null, null, null, material, material, material, material, material, material, material, material);
                var root = new GameObject("CityPreview");
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(RogueliteStageRuntimeController).GetField("_stageRoot", flags).SetValue(runtime, root);
                typeof(RogueliteStageRuntimeController).GetField("_gridOrigin", flags).SetValue(runtime, new Vector3(-54f, 0f, -30f));
                foreach (var block in map.Blocks)
                {
                    Call(runtime, "BuildChunk", block, new Vector3(block.Coordinate.x * 36f - 54f, 0f, block.Coordinate.y * 30f - 30f), root.transform);
                    if (block.Index < 6) block.MarkExplored();
                }
                Call(runtime, "BuildExtractionPoint");
                var streets = owner.AddComponent<RogueliteStageCityStreetsController>(); streets.Configure(map, kit); Call(streets, "Build", root.transform);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.48f, .53f, .58f);
                var sun = new GameObject("PreviewSun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 61f;
                camera.transform.position = new Vector3(0f, 115f, -95f); camera.transform.LookAt(Vector3.zero);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .10f, .12f); camera.farClipPlane = 500f;
                var minimap = new GameObject("MinimapPreview").AddComponent<RogueliteMinimapController>(); minimap.Configure(map, runtime); Call(minimap, "Update");
                var canvas = minimap.GetComponentInChildren<Canvas>(true); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
                Directory.CreateDirectory("Logs/CityVisuals");
                CapturePreview(camera, "Logs/CityVisuals/city-overview.png");
                typeof(RogueliteMinimapController).GetField("_expanded", flags).SetValue(minimap, true); Call(minimap, "ApplySize");
                CapturePreview(camera, "Logs/CityVisuals/exploration-map-expanded.png");
                typeof(RogueliteMinimapController).GetField("_expanded", flags).SetValue(minimap, false); Call(minimap, "ApplySize");
                canvas.gameObject.SetActive(false);
                camera.orthographicSize = 17f; camera.transform.position = new Vector3(-54f, 34f, -73f); camera.transform.LookAt(new Vector3(-54f, 1f, -30f));
                CapturePreview(camera, "Logs/CityVisuals/street-detail.png");
                camera.orthographicSize = 24f; camera.transform.position = new Vector3(26f, 35f, -42f); camera.transform.LookAt(new Vector3(0f, 3f, 0f));
                CapturePreview(camera, "Logs/CityVisuals/civic-core.png");
                var civicHall = root.GetComponentsInChildren<EnterableBuilding25D>().First(x => x.name == "CivicCommandHall").transform;
                var hidden = new List<Renderer>();
                foreach (var renderer in civicHall.GetComponentsInChildren<Renderer>())
                {
                    var local = civicHall.InverseTransformPoint(renderer.transform.position);
                    if (local.y > 5.8f || local.z < -2.2f)
                    {
                        if (renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }
                    }
                }
                camera.orthographicSize = 9.3f; camera.transform.position = civicHall.TransformPoint(new Vector3(1f, 13f, -15f)); camera.transform.LookAt(civicHall.TransformPoint(new Vector3(0f, 1.7f, 0f)));
                CapturePreview(camera, "Logs/CityVisuals/civic-second-floor-cutaway.png");
                foreach (var renderer in hidden) renderer.enabled = true;
                root.SetActive(false);
                map.GenerateStageWithSeed(2, 21319);
                var maintenanceRoot = new GameObject("MaintenancePreview");
                typeof(RogueliteStageRuntimeController).GetField("_stageRoot", flags).SetValue(runtime, maintenanceRoot);
                typeof(RogueliteStageRuntimeController).GetField("_gridOrigin", flags).SetValue(runtime, new Vector3(-54f, 0f, -45f));
                actor.transform.position = new Vector3(-54f, .2f, -45f);
                foreach (var block in map.Blocks)
                {
                    Call(runtime, "BuildChunk", block, new Vector3(block.Coordinate.x * 36f - 54f, 0f, block.Coordinate.y * 30f - 45f), maintenanceRoot.transform);
                    if (block.Index < 6) block.MarkExplored();
                }
                Call(runtime, "BuildExtractionPoint"); Call(streets, "Build", maintenanceRoot.transform);
                camera.orthographicSize = 70f; camera.transform.position = new Vector3(0f, 125f, -105f); camera.transform.LookAt(Vector3.zero);
                canvas.gameObject.SetActive(true);
                typeof(RogueliteMinimapController).GetField("_nextRefresh", flags).SetValue(minimap, 0f); Call(minimap, "Update");
                CapturePreview(camera, "Logs/CityVisuals/maintenance-overview.png");
                canvas.gameObject.SetActive(false);
                camera.orthographicSize = 19f; camera.transform.position = new Vector3(-45f, 32f, -85f); camera.transform.LookAt(new Vector3(-45f, 1f, -45f));
                CapturePreview(camera, "Logs/CityVisuals/maintenance-detail.png");
                camera.orthographicSize = 30f; camera.transform.position = new Vector3(40f, 45f, -65f); camera.transform.LookAt(new Vector3(0f, 18f, 0f));
                CapturePreview(camera, "Logs/CityVisuals/dispatch-tower.png");
                maintenanceRoot.SetActive(false);
                var display = new GameObject("ContainerDisplay");
                var index = 0;
                foreach (SalvageContainerKind kind in Enum.GetValues(typeof(SalvageContainerKind)))
                {
                    if ((int)kind < 5) continue;
                    var position = new Vector3((index % 7 - 3) * 2.7f, 0f, (index / 7 - 1.5f) * 3f);
                    SearchableContainer25D.Create(display.transform, position, material, material, 21319, kind); index++;
                }
                camera.orthographicSize = 9f; camera.transform.position = new Vector3(0f, 16f, -22f); camera.transform.LookAt(Vector3.up * .4f);
                CapturePreview(camera, "Logs/CityVisuals/container-families.png");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void CapturePreview(Camera camera, string path)
        {
            var target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target; UnityEngine.UI.CanvasScaler scaler = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UI.CanvasScaler>();
            if (scaler != null) scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in UnityEngine.Object.FindObjectsByType<RogueliteMinimapGraphic>(FindObjectsSortMode.None))
            {
                graphic.Refresh(); graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
                using var vertices = new UnityEngine.UI.VertexHelper(); typeof(RogueliteMinimapGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null).Invoke(graphic, new object[] { vertices });
                Debug.Log($"MINIMAP_RENDER vertices={vertices.currentVertCount}, renderer={graphic.GetComponent<CanvasRenderer>() != null}, color={graphic.color}, rect={graphic.rectTransform.rect}");
                Require(vertices.currentVertCount > 48, "Minimap geometry is empty");
            }
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }

        private static void ValidateLootProfiles(GameObject owner, List<string> report)
        {
            var inventory = owner.AddComponent<ScavengingInventory25D>();
            var collectibles = owner.AddComponent<CollectibleInventory>();
            typeof(ScavengingInventory25D).GetField("_inventory", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, collectibles);
            var catalog = (List<CollectibleDefinition>)typeof(ScavengingInventory25D).GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
            // Deliberately uneven pool: rarity probability must not depend on entries per rarity.
            for (var rarity = 0; rarity < 4; rarity++)
            for (var i = 0; i < (rarity == 0 ? 17 : rarity + 1); i++)
            {
                var item = ScriptableObject.CreateInstance<CollectibleDefinition>();
                item.Configure($"validation_{rarity}_{i}", "test", "", CollectibleRarity.Common,
                    (ArknightsACT.Gameplay.Roguelite.CombatFeature)0, CollectibleEffectType.AllDamagePercent, 0f);
                item.ConfigureScavengingMetadata(true, (SalvageRarity)rarity, Vector2Int.one, "", 1);
                catalog.Add(item);
            }
            try
            {
                for (var tier = 1; tier <= 5; tier++)
                {
                    var counts = new int[4];
                    for (var seed = 0; seed < 50000; seed++)
                        counts[(int)inventory.ResolveContainerReward(seed, tier).SalvageRarity]++;
                    for (var rarity = 0; rarity < 4; rarity++)
                        Require(Mathf.Abs(counts[rarity] / 50000f - SalvageContainerProfiles.RarityWeight(tier, (SalvageRarity)rarity) / 100f) < .003f,
                            $"Tier {tier} rarity {rarity} distribution drift");
                    Require(inventory.ResolveContainerReward(21319, tier) == inventory.ResolveContainerReward(21319, tier), "Loot replay changed");
                }
                var kinds = new HashSet<SalvageContainerKind>();
                var buildings = new HashSet<ChernobogSearchBuildingKind>();
                var empty = 0;
                for (var seed = 0; seed < 3000; seed++)
                foreach (var district in new[] { ChernobogDistrictType.Residential, ChernobogDistrictType.Commercial, ChernobogDistrictType.Industrial, ChernobogDistrictType.Checkpoint, ChernobogDistrictType.ServiceYard })
                {
                    var random = new System.Random(seed);
                    var profile = ChernobogSearchBuildingProfiles.Select(district, random);
                    buildings.Add(profile.Kind);
                    var loot = ChernobogSearchBuildingProfiles.RollContainers(profile, random);
                    if (loot.Length == 0) empty++;
                    Require(loot.Length <= profile.MaxContainers, "Building container limit");
                    Require(loot.Count(x => x == profile.Rare && !profile.Common.Contains(x)) <= 1, "Multiple rare containers in building");
                    foreach (var kind in loot)
                    {
                        Require(profile.Common.Contains(kind) || kind == profile.Rare, "Container outside building pool");
                        kinds.Add(kind);
                    }
                }
                Require(buildings.Count == 12 && kinds.Count >= 24 && empty > 0, "Missing building/container variants");
                // A no-result container must not invoke its factory again on a later opening.
                var testRoot = new GameObject("LootReplayTest");
                try
                {
                    foreach (SalvageContainerKind kind in Enum.GetValues(typeof(SalvageContainerKind)))
                    {
                        if ((int)kind < 5) continue;
                        var position = new Vector3((int)kind * 3f, 0f, 0f);
                        SearchableContainer25D.Create(testRoot.transform, position, null, null, 123, kind);
                        var created = testRoot.transform.GetChild(testRoot.transform.childCount - 1).GetComponent<SearchableContainer25D>();
                        created.EnsureSlots((i, seed) => inventory.ResolveContainerReward(seed, created.Tier));
                        Require(created.SlotCount >= created.Profile.MinItems && created.SlotCount <= created.Profile.MaxItems, "Container item count");
                        var before = string.Join(";", created.Slots.Select(x => x.Item.Id));
                        created.EnsureSlots((i, seed) => throw new InvalidOperationException("Initialized container rerolled"));
                        Require(string.Join(";", created.Slots.Select(x => x.Item.Id)) == before, "Container contents changed");
                        Physics.SyncTransforms();
                        Require(!Physics.CheckSphere(position + new Vector3(0f, .7f, -.8f), .25f, ~0, QueryTriggerInteraction.Ignore), "Container search stance blocked");
                    }
                    SearchableContainer25D.Create(testRoot.transform, Vector3.zero, null, null, 123, SalvageContainerKind.TrashBin);
                    var container = testRoot.transform.GetChild(testRoot.transform.childCount - 1).GetComponent<SearchableContainer25D>();
                    var calls = 0;
                    container.EnsureSlots((i, seed) => { calls++; return null; });
                    var firstCalls = calls;
                    container.EnsureSlots((i, seed) => { calls++; return catalog[0]; });
                    Require(container.Barren && calls == firstCalls && container.SlotCount == 0, "Empty container rerolled");
                }
                finally { UnityEngine.Object.DestroyImmediate(testRoot); }
                report.Add($"PASS: 250000 rarity rolls across five tiers (uneven catalog); 15000 building plans, all 12 building types and {kinds.Count} indoor container types; bounded placement; all 28 container meshes/counts/replay and empty-container replay.");
            }
            finally
            {
                foreach (var item in catalog) UnityEngine.Object.DestroyImmediate(item);
                catalog.Clear();
            }
        }

        private static string Signature(RogueliteStageMapController map) => string.Join(";",
            map.Blocks.Select(x => $"{x.Type},{x.Theme},{x.HasNormalChest},{x.HasSpikeChest},{x.HasMonsterChest}"));
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif

