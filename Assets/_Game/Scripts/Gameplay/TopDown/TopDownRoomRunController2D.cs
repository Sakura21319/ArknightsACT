using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Fixed four-room run used only to validate the top-down combat loop before investing in
    /// procedural generation. Rooms lock, spawn a hand-authored encounter, then unlock on clear.
    /// </summary>
    public sealed class TopDownRoomRunController2D : MonoBehaviour
    {
        private Transform _player;
        private CombatEntity _playerEntity;
        private TopDownTexasUpgradePanel _upgradePanel;
        private TopDownTexasBuildLab _build;
        private GameObject[] _templates;
        private Vector2[] _roomCenters;
        private Vector2 _roomHalfSize;
        private GameObject[] _doors;
        private Transform _enemyRoot;

        private int _currentRoom = -1;
        private int _livingEnemies;
        private bool _battleActive;
        private bool _completed;
        private float _roomStartedAt;

        public void Configure(
            Transform player,
            TopDownTexasUpgradePanel upgradePanel,
            GameObject[] templates,
            Vector2[] roomCenters,
            Vector2 roomHalfSize,
            GameObject[] doors)
        {
            _player = player;
            _playerEntity = player != null ? player.GetComponent<CombatEntity>() : null;
            _upgradePanel = upgradePanel;
            _build = player != null ? player.GetComponent<TopDownTexasBuildLab>() : null;
            _templates = templates;
            _roomCenters = roomCenters;
            _roomHalfSize = roomHalfSize;
            _doors = doors;
        }

        private void Awake()
        {
            _enemyRoot = new GameObject("[TopDownRuntimeEnemies]").transform;
        }

        private void Start()
        {
            if (_doors != null)
            {
                for (var i = 0; i < _doors.Length; i++)
                    if (_doors[i] != null) _doors[i].SetActive(true);
            }
            BeginRoom(0);
        }

        private void Update()
        {
            if (_completed || _battleActive || _player == null || _roomCenters == null)
                return;
            if (_upgradePanel != null && _upgradePanel.IsOpen)
                return;

            var next = _currentRoom + 1;
            if (next < 0 || next >= _roomCenters.Length)
                return;

            if (InsideRoom(_player.position, _roomCenters[next]))
                BeginRoom(next);
        }

        private void BeginRoom(int index)
        {
            if (_roomCenters == null || index < 0 || index >= _roomCenters.Length)
                return;

            _currentRoom = index;
            _battleActive = true;
            _livingEnemies = 0;
            _roomStartedAt = Time.time;

            // Lock entrance and exit while enemies are alive.
            SetDoor(index - 1, true);
            SetDoor(index, true);

            switch (index)
            {
                case 0:
                    Spawn(0, new Vector2(-3.0f, 2.0f));
                    Spawn(0, new Vector2(3.0f, 2.1f));
                    Spawn(0, new Vector2(-3.2f, -2.1f));
                    Spawn(1, new Vector2(3.2f, -2.0f));
                    Spawn(1, new Vector2(0.0f, 3.0f));
                    break;
                case 1:
                    Spawn(0, new Vector2(-3.4f, 2.4f), 1.10f);
                    Spawn(0, new Vector2(2.8f, -2.4f), 1.10f);
                    Spawn(1, new Vector2(-2.5f, -2.7f));
                    Spawn(1, new Vector2(0.0f, 3.1f));
                    Spawn(2, new Vector2(3.5f, 2.6f));
                    Spawn(2, new Vector2(-3.6f, 0.0f));
                    break;
                case 2:
                    Spawn(0, new Vector2(-3.2f, 2.6f), 1.45f);
                    Spawn(0, new Vector2(3.0f, -2.5f), 1.25f);
                    Spawn(0, new Vector2(0.2f, 3.1f), 1.25f);
                    Spawn(1, new Vector2(-2.8f, -2.7f), 1.20f);
                    Spawn(1, new Vector2(2.7f, 2.5f), 1.20f);
                    Spawn(2, new Vector2(-3.7f, 0.2f), 1.15f);
                    Spawn(2, new Vector2(3.7f, -0.2f), 1.15f);
                    break;
                default:
                    Spawn(3, new Vector2(1.6f, 0f), 1.35f);
                    Spawn(1, new Vector2(-3.3f, 2.6f), 1.15f);
                    Spawn(1, new Vector2(-3.3f, -2.6f), 1.15f);
                    Spawn(2, new Vector2(3.5f, 2.6f), 1.15f);
                    Spawn(2, new Vector2(3.5f, -2.6f), 1.15f);
                    break;
            }

            if (_livingEnemies <= 0)
                CompleteRoom();
        }

        private void Spawn(int templateIndex, Vector2 localOffset, float healthScale = 1f)
        {
            if (_templates == null || templateIndex < 0 || templateIndex >= _templates.Length ||
                _templates[templateIndex] == null)
                return;

            var position = _roomCenters[_currentRoom] + localOffset;
            var clone = Instantiate(_templates[templateIndex], position, Quaternion.identity, _enemyRoot);
            clone.name = $"TopDown_R{_currentRoom + 1}_{_templates[templateIndex].name}";
            clone.SetActive(true);

            var entity = clone.GetComponent<CombatEntity>();
            if (entity == null || entity.Health == null)
            {
                Destroy(clone);
                return;
            }

            entity.Health.SetMaxHealth(entity.Health.MaxHealth * Mathf.Max(0.1f, healthScale));
            _livingEnemies++;
            entity.Health.Died += OnEnemyDied;
        }

        private void OnEnemyDied()
        {
            _livingEnemies = Mathf.Max(0, _livingEnemies - 1);
            if (_battleActive && _livingEnemies == 0)
                CompleteRoom();
        }

        private void CompleteRoom()
        {
            if (!_battleActive)
                return;

            _battleActive = false;
            SetDoor(_currentRoom - 1, false);
            SetDoor(_currentRoom, false);

            if (_playerEntity != null && _playerEntity.Health != null && !_playerEntity.Health.IsDead)
                _playerEntity.Health.Heal(_playerEntity.Health.MaxHealth * 0.20f);

            if (_currentRoom >= 3)
            {
                _completed = true;
                return;
            }

            if (_upgradePanel != null && _upgradePanel.HasAvailableUpgrade)
                _upgradePanel.OpenForRoom(_currentRoom + 1);
        }

        private bool InsideRoom(Vector2 position, Vector2 center)
        {
            var delta = position - center;
            return Mathf.Abs(delta.x) <= _roomHalfSize.x * 0.90f &&
                   Mathf.Abs(delta.y) <= _roomHalfSize.y * 0.90f;
        }

        private void SetDoor(int index, bool active)
        {
            if (_doors == null || index < 0 || index >= _doors.Length || _doors[index] == null)
                return;
            _doors[index].SetActive(active);
        }

        private void OnGUI()
        {
            if (_playerEntity == null || _playerEntity.Health == null)
                return;

            var hp = _playerEntity.Health;
            GUI.Box(new Rect(12f, 12f, 500f, 190f), "Top-Down Prototype · Texas");
            GUI.Label(new Rect(24f, 38f, 460f, 22f), $"Room {Mathf.Max(1, _currentRoom + 1)}/4   Enemies {_livingEnemies}   HP {hp.CurrentHealth:0}/{hp.MaxHealth:0}");
            GUI.Label(new Rect(24f, 62f, 460f, 22f), "WASD move · Mouse aim · LMB/J melee · Space/Shift/K dash · RMB/L Sword Rain");
            GUI.Label(new Rect(24f, 86f, 460f, 22f), "No auto attack · no auto movement · aim assist only snaps slightly toward nearby targets");
            if (_build != null)
            {
                GUI.Label(new Rect(24f, 110f, 460f, 22f), $"迅刃 Lv.{_build.SwiftBladeLevel}   余雷 Lv.{_build.ResidualThunderLevel}   导电 Lv.{_build.ConductiveLevel}");
            }
            GUI.Label(new Rect(24f, 134f, 460f, 22f), _battleActive ? "Doors locked: clear the room." : "Room clear: move through the open door.");
            if (_completed)
                GUI.Label(new Rect(24f, 158f, 460f, 24f), "BOSS CLEAR · top-down prototype complete");
        }
    }
}
