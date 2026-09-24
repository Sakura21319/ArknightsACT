using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>One interaction owner per generated sector. Survey is separate from visited/cleared state.</summary>
    public sealed class CityFacilityController : MonoBehaviour
    {
        private const float WheelchairMountSeconds = .6f;
        private readonly List<CityFacility25D> _facilities = new();
        private readonly List<Wheelchair25D> _wheelchairs = new();
        private readonly HashSet<int> _surveyed = new();
        private RogueliteStageMapController _map;
        private RogueliteStageRuntimeController _runtime;
        private CityFacility25D _candidate;
        private Wheelchair25D _candidateChair;
        private Wheelchair25D _mountedChair;
        private PlayerMotor25D _seatedMotor;
        private Transform _actor;
        private Vector3 _startPosition;
        private float _health;
        private float _progress;
        private string _notice;
        private float _noticeUntil;
        private bool _visible;
        private GUIStyle _style;
        private Font _font;
        public IReadOnlyList<CityFacility25D> Facilities => _facilities;
        public IReadOnlyList<Wheelchair25D> Wheelchairs => _wheelchairs;
        public bool IsSeated => _mountedChair != null;
        public float Progress => _progress;
        public Transform CurrentActor => _actor;
        public bool IsAuthority { get; set; } = true;
        public event System.Action<CityFacility25D> StateChanged;
        public event System.Action<CityFacilitySnapshot> SnapshotChanged;
        public void NotifyStateChanged(CityFacility25D facility)
        {
            StateChanged?.Invoke(facility);
            SnapshotChanged?.Invoke(facility.CaptureState(Time.time));
        }
        public void Configure(RogueliteStageMapController map, RogueliteStageRuntimeController runtime) { _map = map; _runtime = runtime; }
        public void RegisterWheelchair(Wheelchair25D chair)
        {
            if (chair != null && !_wheelchairs.Contains(chair)) _wheelchairs.Add(chair);
        }
        public void Register(CityFacility25D facility)
        {
            if (facility == null || _facilities.Contains(facility)) return;
            facility.StableId = $"{_map.GenerationSeed}:{facility.BlockIndex}:{(int)facility.Kind}:{_facilities.Count}";
            _facilities.Add(facility);
        }
        public bool IsSurveyed(int index) => _surveyed.Contains(index);
        public void SurveyAround(int index)
        {
            if (_map == null || index < 0 || index >= _map.Blocks.Count) return;
            var center = _map.Blocks[index].Coordinate;
            foreach (var block in _map.Blocks)
                if (Mathf.Abs(block.Coordinate.x - center.x) + Mathf.Abs(block.Coordinate.y - center.y) <= 1)
                    _surveyed.Add(block.Index);
        }
        private void Update()
        {
            var flow = RogueliteGameFlowController.Instance;
            var pause = ArknightsACT.Gameplay.Feedback.GameplayPauseService.Instance;
            _visible = _runtime != null && (flow == null || flow.IsRunning) && Time.timeScale > 0f &&
                (pause == null || !pause.IsPaused) && !GameplayInputBlocker.IsBlocked;
            if (IsAuthority && (flow == null || flow.IsRunning) && Time.timeScale > 0f && (pause == null || !pause.IsPaused))
                foreach (var facility in _facilities)
                {
                    if (facility == null) continue;
                    facility.Advance(Time.time); facility.Link?.Advance(Time.time);
                }
            if (_mountedChair != null)
            {
                var rider = _mountedChair.Rider;
                var riderHealth = rider != null ? rider.GetComponent<Health>() : null;
                if (rider == null || (riderHealth != null && riderHealth.IsDead) ||
                    (_runtime != null && _runtime.PlayerTransform != null && rider != _runtime.PlayerTransform))
                    Dismount(rider);
            }
            Tick(_visible ? _runtime.PlayerTransform : null, Keyboard.current != null && Keyboard.current.gKey.isPressed, Time.deltaTime);
        }
        public void Tick(Transform actor, bool held, float dt)
        {
            var entity = actor != null ? actor.GetComponent<CombatEntity>() : null;
            var health = entity != null ? entity.Health : actor != null ? actor.GetComponent<Health>() : null;
            if (entity != null && CombatActionUtility.IsBlocked(entity, CombatActionMask.Interaction))
                held = false;
            CityFacility25D nearest = null;
            var distance = 2.8f * 2.8f;
            Wheelchair25D nearestChair = null;
            var chairDistance = 2.8f * 2.8f;
            if (health != null && !health.IsDead)
            {
                foreach (var facility in _facilities)
                {
                    if (facility == null || !facility.isActiveAndEnabled || facility.Used) continue;
                    var delta = facility.transform.position - actor.position; delta.y = 0f;
                    if (delta.sqrMagnitude >= distance) continue;
                    if (Physics.Linecast(actor.position + Vector3.up * 1.4f, facility.transform.position + Vector3.up * 1.4f,
                        out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(facility.transform) && !hit.transform.IsChildOf(actor)) continue;
                    nearest = facility; distance = delta.sqrMagnitude;
                }
                foreach (var chair in _wheelchairs)
                {
                    if (chair == null || !chair.isActiveAndEnabled || (chair.Occupied && chair != _mountedChair)) continue;
                    var delta = chair.transform.position - actor.position; delta.y = 0f;
                    if (delta.sqrMagnitude >= chairDistance) continue;
                    nearestChair = chair; chairDistance = delta.sqrMagnitude;
                }
            }
            // Facilities take priority; the wheelchair is the candidate only when no facility is in range.
            var selectedChair = nearest == null ? (_mountedChair != null ? _mountedChair : nearestChair) : null;
            if (nearest != _candidate || selectedChair != _candidateChair || actor != _actor) _progress = 0f;
            _candidate = nearest; _candidateChair = selectedChair; _actor = actor;
            if (selectedChair != null)
            {
                TickWheelchair(actor, health, held, dt, selectedChair);
                return;
            }
            if (!IsAuthority || nearest == null || !held || !nearest.CanUse(health)) { _progress = 0f; return; }
            if (_progress > 0f && (health.CurrentHealth < _health - .01f || (actor.position - _startPosition).sqrMagnitude > .25f))
            {
                _progress = 0f; _notice = "操作中断：请在设施旁保持位置，避免受击"; _noticeUntil = Time.unscaledTime + 2f; return;
            }
            if (_progress == 0f) { _startPosition = actor.position; _health = health.CurrentHealth; }
            _health = health.CurrentHealth;
            _progress += Mathf.Max(0f, dt);
            if (_progress < nearest.Duration) return;
            if (nearest.Activate(health, this))
            {
                _notice = nearest.ResultText;
                _noticeUntil = Time.unscaledTime + 4f;
            }
            _progress = 0f;
        }
        private void TickWheelchair(Transform actor, Health health, bool held, float dt, Wheelchair25D chair)
        {
            if (!IsAuthority || !held || health == null) { _progress = 0f; return; }
            if (_progress > 0f && (health.CurrentHealth < _health - .01f || (actor.position - _startPosition).sqrMagnitude > .25f))
            {
                _progress = 0f; _notice = "操作中断：请停稳后再上下轮椅"; _noticeUntil = Time.unscaledTime + 2f; return;
            }
            if (_progress == 0f) { _startPosition = actor.position; _health = health.CurrentHealth; }
            _health = health.CurrentHealth;
            _progress += Mathf.Max(0f, dt);
            if (_progress < WheelchairMountSeconds) return;
            _progress = 0f;
            if (_mountedChair == chair) Dismount(actor); else Mount(actor, chair);
        }
        private void Mount(Transform actor, Wheelchair25D chair)
        {
            var motor = actor != null ? actor.GetComponent<PlayerMotor25D>() : null;
            if (motor == null || chair == null || chair.Occupied)
            {
                _notice = "这台轮椅现在无法使用"; _noticeUntil = Time.unscaledTime + 2f; return;
            }
            motor.ResetMotion();
            motor.enabled = false;
            var locomotion = actor.gameObject.AddComponent<WheelchairLocomotion25D>();
            locomotion.Configure(chair, motor);
            chair.SetRider(actor);
            _mountedChair = chair;
            _seatedMotor = motor;
            _notice = "已坐上轮椅 · 起步迟缓、松手后缓慢滑行，移动中按住 Shift 可漂移，不能跳跃和冲刺";
            _noticeUntil = Time.unscaledTime + 4f;
        }
        private void Dismount(Transform actor)
        {
            var chair = _mountedChair;
            if (chair == null) return;
            if (actor != null)
            {
                var locomotion = actor.GetComponent<WheelchairLocomotion25D>();
                if (locomotion != null) Destroy(locomotion);
                var stand = chair.DismountPosition;
                actor.position = new Vector3(stand.x, actor.position.y, stand.z);
            }
            chair.SetRider(null);
            if (_seatedMotor != null)
            {
                _seatedMotor.enabled = true;
                _seatedMotor.ResetMotion();
            }
            _mountedChair = null;
            _seatedMotor = null;
            _candidateChair = null;
            _notice = "已离开轮椅";
            _noticeUntil = Time.unscaledTime + 2f;
        }
        private void OnDisable()
        {
            if (_mountedChair != null) Dismount(_mountedChair.Rider);
            _progress = 0f; _candidate = null; _candidateChair = null; _actor = null; _visible = false;
        }
        private void OnDestroy() { if (_font != null) Destroy(_font); }
        private void OnGUI()
        {
            if (!_visible || (_candidate == null && _candidateChair == null && Time.unscaledTime >= _noticeUntil)) return;
            if (_style == null)
            {
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 16);
                _style = new GUIStyle(GUI.skin.box) { font = _font, fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _style.normal.textColor = new Color(.8f, .94f, .9f);
            }
            var text = Time.unscaledTime < _noticeUntil ? _notice : "";
            if (_candidateChair != null && Time.unscaledTime >= _noticeUntil)
            {
                text = _mountedChair == _candidateChair
                    ? $"按住 G 离开轮椅 {_progress / WheelchairMountSeconds:P0}\n轮椅中：松手缓慢滑行，按住 Shift 漂移，不能跳跃和冲刺"
                    : $"按住 G 坐上轮椅 {_progress / WheelchairMountSeconds:P0}\n起步迟缓、松手后缓慢滑行，移动中按住 Shift 可漂移";
            }
            else if (_candidate != null && Time.unscaledTime >= _noticeUntil)
            {
                var health = _actor != null ? _actor.GetComponent<Health>() : null;
                text = _candidate.Kind == CityFacilityKind.Medical && !_candidate.CanUse(health) ? "罗德岛应急补给 · 生命已满，补给保留" :
                    $"按住 G  {_candidate.Label}  {_progress / _candidate.Duration:P0}\n{_candidate.Benefit}";
                if (_candidate.Kind == CityFacilityKind.Overload && !_candidate.CanUse(health)) text = "生命不足，无法支付超载代价（需要高于 20% 最大生命）";
                if (_candidate.State == CityFacilityState.Armed) text = _candidate.Kind == CityFacilityKind.PressureVent ?
                    $"泄压倒计时 {Mathf.Max(0f, _candidate.Deadline - Time.time):0.0}s · 立即离开黄色区域！" :
                    $"接力剩余 {Mathf.Max(0f, _candidate.Link.Deadline - Time.time):0.0}s · 前往另一端";
            }
            var width = Mathf.Min(500f, Screen.width - 24f);
            GUI.Box(new Rect((Screen.width - width) * .5f, Screen.height - 192f, width, 86f), text, _style);
        }
    }
}
