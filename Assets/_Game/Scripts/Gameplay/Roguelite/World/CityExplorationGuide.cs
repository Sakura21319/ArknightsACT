using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>Read-only guidance: records room visits, never opens loot or advances the run.</summary>
    public sealed class CityExplorationGuide : MonoBehaviour
    {
        private readonly List<EnterableBuilding25D> _rooms = new();
        private RogueliteStageMapController _map;
        private RogueliteStageRuntimeController _runtime;
        private Transform _stage;
        private float _nextRefresh;
        private float _nextRoomScan;
        private int _mode;
        public EnterableBuilding25D CurrentRoom { get; private set; }
        public int VisitedRooms { get; private set; }
        public bool HasTarget { get; private set; }
        public Vector3 TargetPosition { get; private set; }
        public string TargetLabel { get; private set; } = "探索周边街区";
        public string LocationText { get; private set; } = "";
        public string Hint { get; private set; } = "";
        public int Mode => _mode;
        public void Configure(RogueliteStageMapController map, RogueliteStageRuntimeController runtime) { _map = map; _runtime = runtime; }
        public void CycleMode()
        {
            _mode = (_mode + 1) % 3;
            if (_mode == 2 && (_runtime.NextStageTransform == null || !_runtime.NextStageTransform.gameObject.activeInHierarchy)) _mode = 0;
            _nextRefresh = 0f;
        }
        private void Update()
        {
            if (_map == null || _runtime == null) return;
            var flow = RogueliteGameFlowController.Instance;
            if (flow != null && !flow.IsRunning) return;
            if (!GameplayInputBlocker.IsBlocked && Time.timeScale > 0f && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame) CycleMode();
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .25f;
            Refresh(_runtime.PlayerTransform);
        }
        public void Refresh(Transform actor)
        {
            if (_runtime == null || _map == null) return;
            var stage = _runtime.CurrentStageRoot;
            if (_stage != stage)
            {
                _stage = stage; _mode = 0; CurrentRoom = null; _rooms.Clear(); _nextRoomScan = 0f;
            }
            HasTarget = false; VisitedRooms = 0;
            if (stage == null || actor == null) { CurrentRoom = null; LocationText = Hint = ""; return; }
            if (_rooms.Count == 0 || Time.unscaledTime >= _nextRoomScan)
            {
                stage.GetComponentsInChildren(false, _rooms);
                _nextRoomScan = Time.unscaledTime + 1f;
            }
            EnterableBuilding25D inside = null;
            foreach (var room in _rooms)
            {
                if (room == null || !room.isActiveAndEnabled) continue;
                if (room.Contains(actor.position)) inside = room;
                if (room.Visited) VisitedRooms++;
            }
            if (CurrentRoom != null && CurrentRoom != inside) CurrentRoom.Visit();
            CurrentRoom = inside;
            if (inside != null)
            {
                if (!inside.Visited) VisitedRooms++;
                inside.Visit(); LocationText = $"{inside.DisplayName}\n{inside.SearchStatus}";
            }
            else LocationText = $"街区探索 · 已进入 {VisitedRooms} 间建筑\nF 搜索容器 · G 操作设施";
            var normalized = RogueliteMinimapGraphic.WorldToNormalized(actor.position, _map.Width, _map.Height);
            _map.TryGetBlock(new Vector2Int(Mathf.FloorToInt(normalized.x * _map.Width), Mathf.FloorToInt(normalized.y * _map.Height)), out var currentBlock);
            var inventory = actor.GetComponent<ScavengingInventory25D>();
            var health = actor.GetComponent<Health>();
            Hint = inventory != null && inventory.UsedBackpackCells >= inventory.BackpackCellCapacity * .85f ? "背包接近装满，可按 N 切换撤离指引" :
                health != null && health.CurrentHealth < health.MaxHealth * .35f ? "生命偏低，寻找医疗补给或切换撤离指引" :
                currentBlock != null && currentBlock.Zone == CityZone.Core ? "核心高危：守军更多更强，储备物资更密集" :
                inside != null && inside.RemainingContainers > 0 ? "检索后记得取走物资，撤离才能保住收获" : "自由探索；击败首领后可前往下一阶段";
            if (_mode == 1)
            {
                if (_runtime.ExtractionTransform != null) SetTarget(_runtime.ExtractionTransform.position, "返回撤离点");
                Hint = "到达青色撤离点按 E，确认撤离后结算物资";
            }
            else if (_mode == 2)
            {
                var exit = _runtime.NextStageTransform;
                if (exit != null && exit.gameObject.activeInHierarchy)
                {
                    SetTarget(exit.position, "前往下一阶段入口");
                    Hint = "到入口按 E 前进，携带的物资仍有丢失风险";
                }
                else _mode = 0;
            }
            if (_mode == 0)
            {
                var distance = float.PositiveInfinity;
                foreach (var room in _rooms)
                {
                    if (room == null || !room.isActiveAndEnabled) continue;
                    if (room.Visited || !IsVisitedBlock(room.transform.position)) continue;
                    var delta = (room.transform.position - actor.position).sqrMagnitude;
                    if (delta >= distance) continue;
                    distance = delta; SetTarget(room.transform.position, room.DisplayName);
                }
                if (!HasTarget)
                    foreach (var block in _map.Blocks)
                    {
                        if (block.Explored) continue;
                        var adjacent = false;
                        foreach (var known in _map.Blocks)
                            if (known.Explored && Mathf.Abs(known.Coordinate.x - block.Coordinate.x) + Mathf.Abs(known.Coordinate.y - block.Coordinate.y) == 1) { adjacent = true; break; }
                        if (!adjacent) continue;
                        var point = new Vector3((block.Coordinate.x - (_map.Width - 1) * .5f) * RogueliteStageWorldMetrics.ChunkWidth, 0f,
                            (block.Coordinate.y - (_map.Height - 1) * .5f) * RogueliteStageWorldMetrics.ChunkDepth);
                        var delta = (point - actor.position).sqrMagnitude;
                        if (delta >= distance) continue;
                        distance = delta; SetTarget(point, "探索相邻街区");
                    }
            }
            if (HasTarget)
            {
                var delta = TargetPosition - actor.position;
                TargetLabel += $" · {Direction(delta)} {new Vector2(delta.x, delta.z).magnitude:0}m";
            }
            else
            {
                var allVisited = true;
                foreach (var block in _map.Blocks) if (!block.Explored) { allVisited = false; break; }
                TargetLabel = allVisited ? "已走访全部街区，可搜刮余下物资或撤离" : "沿主路探索，进入建筑寻找物资";
            }
        }
        private void SetTarget(Vector3 position, string label) { HasTarget = true; TargetPosition = position; TargetLabel = label; }
        private bool IsVisitedBlock(Vector3 position)
        {
            var normalized = RogueliteMinimapGraphic.WorldToNormalized(position, _map.Width, _map.Height);
            return _map.TryGetBlock(new Vector2Int(Mathf.FloorToInt(normalized.x * _map.Width), Mathf.FloorToInt(normalized.y * _map.Height)), out var block) && block.Explored;
        }
        public static string Direction(Vector3 delta)
        {
            if (new Vector2(delta.x, delta.z).sqrMagnitude < 4f) return "附近";
            var angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            var labels = new[] { "北", "东北", "东", "东南", "南", "西南", "西", "西北" };
            return labels[(Mathf.RoundToInt(angle / 45f) + 8) % 8];
        }
    }
}
