using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public sealed partial class RogueliteStageCityStreetsController
    {
        // Smooth solid underlay avoids tiny risers catching CharacterController feet.
        private void WalkFlight(Transform parent, Vector3 start, Vector3 end, float width, bool rails)
        {
            var delta = end - start;
            var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            CreateBox(parent, "WalkSurface_Ramp", (start + end) * .5f - Vector3.up * .08f,
                new Vector3(width, .16f, delta.magnitude + .08f), .005f, _roofMetal, true, rotation);
            var right = Vector3.Cross(Vector3.up, delta).normalized;
            if (rails)
                foreach (var side in new[] { -1f, 1f })
                    CreateBox(parent, "WalkRail_Flight", (start + end) * .5f + right * side * (width * .5f + .08f) + Vector3.up * .55f,
                        new Vector3(.12f, 1.1f, delta.magnitude), .015f, _paintTrim, true, rotation);
            var steps = Mathf.Max(1, Mathf.CeilToInt(new Vector2(delta.x, delta.z).magnitude / .4f));
            for (var step = 0; step < steps; step++)
                CreateBox(parent, "StairTreadMark", Vector3.Lerp(start, end, (step + .5f) / steps) + Vector3.up * .008f,
                    new Vector3(width - .12f, .012f, .045f), .002f, _lanePaint, false, rotation);
        }

        private void WalkDeck(Transform parent, Vector3 center, Vector2 size)
        {
            CreateBox(parent, "WalkSurface_Deck", center - Vector3.up * .1f, new Vector3(size.x, .2f, size.y), .005f, _roofMetal, true);
        }

        private void BuildTowerAscent(Transform tower)
        {
            var stairs = new GameObject("TowerPublicStair").transform; stairs.SetParent(tower, false);
            const float ground = .18f;
            const float summit = 34.7f;
            var corners = new[] { new Vector3(-9f, 0f, -9f), new Vector3(9f, 0f, -9f), new Vector3(9f, 0f, 9f), new Vector3(-9f, 0f, 9f) };
            var route = new List<Vector3> { new Vector3(-9f, ground, -11.5f), corners[0] + Vector3.up * ground };
            for (var i = 0; i <= 16; i++)
            {
                var corner = corners[i % 4] + Vector3.up * (i == 0 ? ground : Mathf.Lerp(6.2f, summit, (i - 1) / 15f));
                WalkDeck(stairs, corner, new Vector2(2.4f, 2.4f));
                if (i > 0)
                {
                    var anchor = new Vector3(Mathf.Sign(corner.x) * 3.2f, corner.y - .4f, Mathf.Sign(corner.z) * 3.2f);
                    var outer = corner - Vector3.up * .4f; var brace = outer - anchor;
                    CreateBox(stairs, "StairStructuralOutrigger", (anchor + outer) * .5f, new Vector3(.3f, .45f, brace.magnitude), .025f, _roofMetal, false, Quaternion.LookRotation(brace));
                }
                // Only the outward sides need corner guards; flight mouths remain open.
                CreateBox(stairs, "WalkRail_CornerX", corner + new Vector3(Mathf.Sign(corner.x) * 1.22f, .55f, 0f), new Vector3(.12f, 1.1f, 2.5f), .01f, _paintTrim, true);
                CreateBox(stairs, "WalkRail_CornerZ", corner + new Vector3(0f, .55f, Mathf.Sign(corner.z) * 1.22f), new Vector3(2.5f, 1.1f, .12f), .01f, _paintTrim, true);
                if (i == 0) stairs.Find("WalkRail_CornerZ").gameObject.SetActive(false); // Ground entrance from the south.
                if (i == 16) break;
                var next = corners[(i + 1) % 4] + Vector3.up * Mathf.Lerp(6.2f, summit, i / 15f);
                var flat = next - corner; flat.y = 0f; flat.Normalize();
                var start = corner + flat * 1f; var end = next - flat * 1f;
                WalkFlight(stairs, start, end, 1.9f, true);
                route.Add(start); route.Add(end); route.Add(next);
            }
            WalkDeck(stairs, new Vector3(-4.5f, summit, -9f), new Vector2(9f, 2.2f));
            WalkDeck(stairs, new Vector3(0f, summit, -6.2f), new Vector2(2.2f, 5.6f));
            WalkDeck(stairs, new Vector3(0f, summit, 0f), new Vector2(12.4f, 10.4f));
            foreach (var side in new[] { -1f, 1f })
            {
                CreateBox(stairs, "WalkRail_Bridge", new Vector3(-4.5f, summit + .55f, -9f + side * 1.15f), new Vector3(7f, 1.1f, .12f), .01f, _paintTrim, true);
                CreateBox(stairs, "WalkRail_Deck", new Vector3(side * 6.2f, summit + .55f, 0f), new Vector3(.12f, 1.1f, 10.4f), .01f, _paintTrim, true);
                CreateBox(stairs, "WalkRail_Deck", new Vector3(side * 3.7f, summit + .55f, -5.2f), new Vector3(5f, 1.1f, .12f), .01f, _paintTrim, true);
                CreateBox(stairs, "WalkRail_BridgeSide", new Vector3(side * 1.15f, summit + .55f, -6.5f), new Vector3(.12f, 1.1f, 2.4f), .01f, _paintTrim, true);
            }
            CreateBox(stairs, "WalkRail_Deck", new Vector3(0f, summit + .55f, 5.2f), new Vector3(12.4f, 1.1f, .12f), .01f, _paintTrim, true);
            CreateBox(stairs, "WalkRail_BridgeEnd", new Vector3(0f, summit + .55f, -10.15f), new Vector3(2.3f, 1.1f, .12f), .01f, _paintTrim, true);
            CreateBox(stairs, "WalkRail_BridgeEnd", new Vector3(1.15f, summit + .55f, -8.9f), new Vector3(.12f, 1.1f, 2.4f), .01f, _paintTrim, true);
            route.Add(new Vector3(0f, summit, -9f)); route.Add(new Vector3(0f, summit, -3f));
            for (var i = 0; i < route.Count; i++) route[i] = stairs.TransformPoint(route[i]);
            stairs.gameObject.AddComponent<CityVerticalRoute>().Configure(route.ToArray());
            SearchableContainer25D.Create(stairs, new Vector3(-3.8f, summit, 2f), _roofMetal, _windowDark, stageMap.GenerationSeed, SalvageContainerKind.ArchiveVault);
            SearchableContainer25D.Create(stairs, new Vector3(3.8f, summit, 2f), _roofMetal, _windowDark, stageMap.GenerationSeed, SalvageContainerKind.MilitaryCrate);
            SectorLabel(stairs, "SummitPlaque", "调度塔顶 / 城市观测台", new Vector3(0f, summit + .03f, -2f), true);
            SectorLabel(stairs, "StairEntrance", "检修楼梯 ↑ 塔顶", new Vector3(-9f, .2f, -11.6f), true);
        }

        private void BuildCivicUpperFloor(Transform hall, float height)
        {
            const float upper = 3.2f;
            foreach (Transform child in hall)
                if (child.name == "ShellDoorWing" || child.name == "ClerestoryFrame" || child.name == "ClerestoryGlass") child.gameObject.SetActive(false);
            foreach (var side in new[] { -1f, 1f })
            {
                foreach (var band in new[] { new Vector2(0f, 1.1f), new Vector2(2.5f, 4.3f), new Vector2(5.7f, height) })
                    CreateBox(hall, "ShellFrontWindowBand", new Vector3(side * 4.75f, (band.x + band.y) * .5f, -2.5f), new Vector3(6.5f, band.y - band.x, .22f), .02f, _facades[0], true);
                foreach (var y in new[] { 1.8f, 5f })
                    foreach (var x in new[] { 1.65f, 4.75f, 7.85f })
                        CreateBox(hall, "ShellFrontWindowPier", new Vector3(side * x, y, -2.5f), new Vector3(.3f, 1.4f, .22f), .02f, _facades[0], true);
            }
            WalkDeck(hall, new Vector3(0f, upper, -1.25f), new Vector2(15.5f, 2f));
            WalkDeck(hall, new Vector3(6f, upper, .9f), new Vector2(3.5f, 2.3f));
            var bottom = new Vector3(-6f, .18f, 1.25f); var top = new Vector3(4.7f, upper, 1.25f);
            WalkFlight(hall, bottom, top, 1.4f, false);
            CreateBox(hall, "WalkRail_Atrium", new Vector3(-1.85f, upper + .5f, -.18f), new Vector3(11.7f, 1f, .1f), .01f, _paintTrim, true);
            var route = new[] { new Vector3(0f, .18f, 0f), new Vector3(-6f, .18f, 0f), bottom, top, new Vector3(6f, upper, -.6f) };
            for (var i = 0; i < route.Length; i++) route[i] = hall.TransformPoint(route[i]);
            var link = new GameObject("CivicSecondFloorRoute").transform; link.SetParent(hall, false);
            link.gameObject.AddComponent<CityVerticalRoute>().Configure(route);
            SearchableContainer25D.Create(hall, new Vector3(6f, upper, 1.25f), _roofMetal, _windowDark, stageMap.GenerationSeed, SalvageContainerKind.ArchiveVault);
            SectorLabel(hall, "UpperFloorAddress", "02 / 指挥档案回廊", new Vector3(-3f, upper + .03f, -1.3f), true);
            // Replace the rear solid wall with a window grid; openings retain a waist-high sill.
            hall.Find("ShellBack").gameObject.SetActive(false);
            for (var row = 0; row < 2; row++)
            {
                var y = row * upper;
                CreateBox(hall, "ShellWindowSill", new Vector3(0f, y + .55f, 2.5f), new Vector3(16f, 1.1f, .22f), .02f, _facades[0], true);
                CreateBox(hall, "ShellWindowLintel", new Vector3(0f, y + 2.85f, 2.5f), new Vector3(16f, .7f, .22f), .02f, _facades[0], true);
                for (var column = 0; column <= 5; column++)
                {
                    var x = -8f + column * 3.2f;
                    CreateBox(hall, "ShellWindowPier", new Vector3(x, y + 1.8f, 2.5f), new Vector3(column == 0 || column == 5 ? .2f : .9f, 1.4f, .22f), .02f, _facades[0], true);
                }
                for (var column = 0; column < 5; column++)
                    CreateBox(hall, "WindowMullion", new Vector3(-6.4f + column * 3.2f, y + 1.8f, 2.5f), new Vector3(.06f, 1.4f, .12f), .005f, _roofMetal, false);
            }
            if (height > 6.4f)
                CreateBox(hall, "ShellWindowAttic", new Vector3(0f, (height + 6.4f) * .5f, 2.5f), new Vector3(16f, height - 6.4f, .22f), .02f, _facades[0], true);
        }
    }
}
