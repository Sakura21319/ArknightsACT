using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.World;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;
using UnityEngine.UI;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RogueliteMinimapGraphic : MaskableGraphic
    {
        private RogueliteStageMapController _map;
        private RogueliteStageRuntimeController _runtime;
        private readonly List<EnterableBuilding25D> _rooms = new();
        private readonly List<SearchableContainer25D> _containers = new();
        private Transform _stage;
        private CityFacilityController _facilities;
        private CityExplorationGuide _guide;
        private CityTowerLandmark _tower;
        private float _nextGeometry;
        private Vector3 _lastPosition;
        private Transform _lastActor;
        private Vector2 _heading = Vector2.up;
        public void Configure(RogueliteStageMapController map, RogueliteStageRuntimeController runtime) { _map = map; _runtime = runtime; _guide = runtime.GetComponent<CityExplorationGuide>(); }
        public void Refresh()
        {
            var stage = _runtime.CurrentStageRoot;
            if (_stage != stage || Time.unscaledTime >= _nextGeometry)
            {
                _stage = stage; _nextGeometry = Time.unscaledTime + .6f;
                _facilities = stage != null ? stage.GetComponentInChildren<CityFacilityController>() : null;
                _tower = stage != null ? stage.GetComponentInChildren<CityTowerLandmark>() : null;
                _rooms.Clear();
                _containers.Clear();
                if (stage != null)
                {
                    stage.GetComponentsInChildren(false, _rooms);
                    stage.GetComponentsInChildren(false, _containers);
                }
            }
            var actor = _runtime.PlayerTransform;
            if (actor != null)
            {
                var delta = new Vector2(actor.position.x - _lastPosition.x, actor.position.z - _lastPosition.z);
                if (_lastActor != actor) _heading = Vector2.up;
                else if (delta.sqrMagnitude > .0001f && delta.sqrMagnitude < 100f) _heading = delta.normalized;
                _lastActor = actor; _lastPosition = actor.position;
            }
            SetVerticesDirty();
        }
        public static Vector2 WorldToNormalized(Vector3 world, int width, int height) => new(
            world.x / (Mathf.Max(1, width) * RogueliteStageWorldMetrics.ChunkWidth) + .5f,
            world.z / (Mathf.Max(1, height) * RogueliteStageWorldMetrics.ChunkDepth) + .5f);
        private Vector2 Point(Vector3 world, Rect mapRect)
        {
            var p = WorldToNormalized(world, _map.Width, _map.Height);
            return new Vector2(mapRect.x + p.x * mapRect.width, mapRect.y + p.y * mapRect.height);
        }
        private bool Explored(Vector3 position)
        {
            var p = WorldToNormalized(position, _map.Width, _map.Height);
            var x = Mathf.FloorToInt(p.x * _map.Width); var y = Mathf.FloorToInt(p.y * _map.Height);
            return _map.TryGetBlock(new Vector2Int(x, y), out var block) && (block.Explored || (_facilities != null && _facilities.IsSurveyed(block.Index)));
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_map == null || _runtime == null) return;
            var rect = rectTransform.rect;
            var scale = Mathf.Min(rect.width / (_map.Width * 36f), rect.height / (_map.Height * 30f));
            var size = new Vector2(_map.Width * 36f * scale, _map.Height * 30f * scale);
            var mapRect = new Rect(rect.center - size * .5f, size);
            foreach (var block in _map.Blocks)
            {
                var origin = mapRect.min + new Vector2(block.Coordinate.x * 36f, block.Coordinate.y * 30f) * scale;
                var cellSize = new Vector2(36f, 30f) * scale;
                var surveyed = _facilities != null && _facilities.IsSurveyed(block.Index);
                Quad(vh, origin + Vector2.one, cellSize - Vector2.one * 2f,
                    block.Explored ? Color.Lerp(CityZoneRules.Color(block.Zone), new Color(.05f, .07f, .08f), .48f) : surveyed ? Color.Lerp(CityZoneRules.Color(block.Zone), new Color(.07f, .07f, .12f), .65f) : new Color(.07f, .09f, .11f));
                if (!block.Explored && !surveyed) continue;
                var road = new Color(.29f, .36f, .36f);
                Quad(vh, origin + new Vector2(0f, cellSize.y * .5f - 1f), new Vector2(cellSize.x, 2f), road);
                Quad(vh, origin + new Vector2(cellSize.x * .5f - 1f, 0f), new Vector2(2f, cellSize.y), road);
            }
            foreach (var room in _rooms)
            {
                if (room == null || !room.gameObject.activeInHierarchy || !Explored(room.transform.position)) continue;
                var center = Point(room.transform.position, mapRect);
                var footprint = new Vector2(room.Interior.size.x, room.Interior.size.z) * scale;
                var tint = room.Visited ? room.RemainingContainers == 0 ? new Color(.25f, .32f, .34f) : new Color(.30f, .65f, .61f) : new Color(.50f, .56f, .53f);
                Quad(vh, center - footprint * .5f, footprint, tint);
            }
            var actor = _runtime.PlayerTransform;
            if (actor == null) return;
            if (_tower != null)
            {
                var p = Point(_tower.transform.position, mapRect);
                Quad(vh, p - new Vector2(4f, 4f), new Vector2(8f, 8f), new Color(.8f, .35f, .16f));
                Triangle(vh, p + Vector2.up * 8f, p + new Vector2(-5f, 3f), p + new Vector2(5f, 3f), new Color(1f, .72f, .32f));
            }
            if (_facilities != null)
                foreach (var facility in _facilities.Facilities)
                {
                    if (facility == null || facility.Used || !Explored(facility.transform.position)) continue;
                    var point = Point(facility.transform.position, mapRect);
                    var tint = new Color(.6f, .65f, 1f);
                    Triangle(vh, point + Vector2.up * 3f, point + Vector2.right * 3f, point - Vector2.up * 3f, tint);
                    Triangle(vh, point + Vector2.up * 3f, point - Vector2.up * 3f, point - Vector2.right * 3f, tint);
                }
            foreach (var container in _containers)
            {
                if (container == null || container.Emptied || _stage == null || !container.transform.IsChildOf(_stage) ||
                    (container.transform.position - actor.position).sqrMagnitude > 225f || !Explored(container.transform.position)) continue;
                Quad(vh, Point(container.transform.position, mapRect) - Vector2.one * 1.5f, Vector2.one * 3f, new Color(.95f, .74f, .30f));
            }
            if (_runtime.ExtractionTransform != null)
            {
                var p = Point(_runtime.ExtractionTransform.position, mapRect);
                Quad(vh, p - new Vector2(4f, 1f), new Vector2(8f, 2f), new Color(.2f, .95f, .75f));
                Quad(vh, p - new Vector2(1f, 4f), new Vector2(2f, 8f), new Color(.2f, .95f, .75f));
            }
            if (_runtime.NextStageTransform != null && _runtime.NextStageTransform.gameObject.activeSelf)
                Quad(vh, Point(_runtime.NextStageTransform.position, mapRect) - Vector2.one * 3f, Vector2.one * 6f, new Color(1f, .45f, .24f));
            var playerPoint = Point(actor.position, mapRect);
            if (_guide != null && _guide.HasTarget)
            {
                var p = Point(_guide.TargetPosition, mapRect);
                var tint = new Color(1f, .9f, .65f);
                Quad(vh, p - new Vector2(5f, 5f), new Vector2(10f, 1f), tint);
                Quad(vh, p + new Vector2(-5f, 4f), new Vector2(10f, 1f), tint);
                Quad(vh, p - new Vector2(5f, 5f), new Vector2(1f, 10f), tint);
                Quad(vh, p + new Vector2(4f, -5f), new Vector2(1f, 10f), tint);
            }
            var forward = _heading;
            if (forward.sqrMagnitude < .1f) forward = Vector2.up;
            var right = new Vector2(forward.y, -forward.x);
            Triangle(vh, playerPoint + forward * 5f, playerPoint - forward * 3f + right * 3f, playerPoint - forward * 3f - right * 3f, Color.white);
        }
        private static void Quad(VertexHelper vh, Vector2 p, Vector2 size, Color tint)
        {
            var i = vh.currentVertCount;
            vh.AddVert(p, tint, Vector2.zero); vh.AddVert(p + new Vector2(0f, size.y), tint, Vector2.zero);
            vh.AddVert(p + size, tint, Vector2.zero); vh.AddVert(p + new Vector2(size.x, 0f), tint, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            var i = vh.currentVertCount; vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero); vh.AddTriangle(i, i + 1, i + 2);
        }
    }
}
