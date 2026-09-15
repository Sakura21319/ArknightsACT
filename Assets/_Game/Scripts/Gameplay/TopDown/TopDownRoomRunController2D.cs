using System.Collections;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Fixed four-room run used only to validate the top-down combat loop before investing in
    /// procedural generation. Scene-builder configuration is serialized so references survive
    /// the Editor -> Play Mode domain reload.
    /// </summary>
    public sealed class TopDownRoomRunController2D : MonoBehaviour
    {
        [Header("Generated scene references")]
        [SerializeField] private Transform player;
        [SerializeField] private TopDownTexasUpgradePanel upgradePanel;
        [SerializeField] private GameObject[] templates;
        [SerializeField] private Vector2[] roomCenters;
        [SerializeField] private Vector2 roomHalfSize = new(7f, 4.2f);
        [SerializeField] private GameObject[] doors;

        private CombatEntity _playerEntity;
        private TopDownTexasBuildLab _build;
        private Transform _enemyRoot;

        private int _currentRoom = -1;
        private int _livingEnemies;
        private bool _battleActive;
        private bool _completed;
        private bool _rewardPending;

        public int CurrentRoom => Mathf.Max(0, _currentRoom);
        public int LivingEnemies => _livingEnemies;

        public void Configure(
            Transform newPlayer,
            TopDownTexasUpgradePanel newUpgradePanel,
            GameObject[] newTemplates,
            Vector2[] newRoomCenters,
            Vector2 newRoomHalfSize,
            GameObject[] newDoors)
        {
            player = newPlayer;
            upgradePanel = newUpgradePanel;
            templates = newTemplates;
            roomCenters = newRoomCenters;
            roomHalfSize = newRoomHalfSize;
            doors = newDoors;
            ResolveRuntimeReferences();
        }

        private void Awake()
        {
            ResolveRuntimeReferences();
            var root = new GameObject("[TopDownRuntimeEnemies]");
            _enemyRoot = root.transform;
        }

        private void ResolveRuntimeReferences()
        {
            _playerEntity = player != null ? player.GetComponent<CombatEntity>() : null;
            _build = player != null ? player.GetComponent<TopDownTexasBuildLab>() : null;
            if (upgradePanel == null && player != null)
                upgradePanel = player.GetComponent<TopDownTexasUpgradePanel>();
        }

        private void Start()
        {
            ResolveRuntimeReferences();

            if (player == null || _playerEntity == null)
            {
                Debug.LogError("[ArknightsACT/TopDown] Run controller lost its player reference after scene load.", this);
                return;
            }
            if (templates == null || templates.Length == 0)
            {
                Debug.LogError("[ArknightsACT/TopDown] No serialized enemy templates are configured.", this);
                return;
            }
            if (roomCenters == null || roomCenters.Length == 0)
            {
                Debug.LogError("[ArknightsACT/TopDown] No serialized room centers are configured.", this);
                return;
            }

            if (doors != null)
            {
                for (var i = 0; i < doors.Length; i++)
                    if (doors[i] != null) doors[i].SetActive(true);
            }

            Debug.Log($"[ArknightsACT/TopDown] Run ready: templates={templates.Length}, rooms={roomCenters.Length}.", this);
            BeginRoom(0);
        }

        private void Update()
        {
            if (_completed || _battleActive || _rewardPending || player == null || roomCenters == null)
                return;
            if (upgradePanel != null && upgradePanel.IsOpen)
                return;

            var next = _currentRoom + 1;
            if (next < 0 || next >= roomCenters.Length)
                return;

            if (InsideRoom(player.position, roomCenters[next]))
                BeginRoom(next);
        }

        private void BeginRoom(int index)
        {
            if (roomCenters == null || index < 0 || index >= roomCenters.Length)
                return;

            _currentRoom = index;
            _battleActive = true;
            _livingEnemies = 0;

            SetDoor(index - 1, true);
            SetDoor(index, true);

            switch (index)
            {
                case 0:
                    Spawn(0, new Vector2(-3.0f, 1.7f));
                    Spawn(0, new Vector2(3.0f, 1.8f));
                    Spawn(0, new Vector2(-3.2f, -1.8f));
                    Spawn(1, new Vector2(3.2f, -1.7f));
                    Spawn(1, new Vector2(0.0f, 2.5f));
                    break;
                case 1:
                    Spawn(0, new Vector2(-3.4f, 2.0f), 1.10f);
                    Spawn(0, new Vector2(2.8f, -2.0f), 1.10f);
                    Spawn(1, new Vector2(-2.5f, -2.2f));
                    Spawn(1, new Vector2(0.0f, 2.6f));
                    Spawn(2, new Vector2(3.5f, 2.1f));
                    Spawn(2, new Vector2(-3.6f, 0.0f));
                    break;
                case 2:
                    Spawn(0, new Vector2(-3.2f, 2.1f), 1.45f);
                    Spawn(0, new Vector2(3.0f, -2.0f), 1.25f);
                    Spawn(0, new Vector2(0.2f, 2.5f), 1.25f);
                    Spawn(1, new Vector2(-2.8f, -2.2f), 1.20f);
                    Spawn(1, new Vector2(2.7f, 2.0f), 1.20f);
                    Spawn(2, new Vector2(-3.7f, 0.2f), 1.15f);
                    Spawn(2, new Vector2(3.7f, -0.2f), 1.15f);
                    break;
                default:
                    Spawn(3, new Vector2(1.6f, 0f), 1.35f);
                    Spawn(1, new Vector2(-3.3f, 2.0f), 1.15f);
                    Spawn(1, new Vector2(-3.3f, -2.0f), 1.15f);
                    Spawn(2, new Vector2(3.5f, 2.0f), 1.15f);
                    Spawn(2, new Vector2(3.5f, -2.0f), 1.15f);
                    break;
            }

            Debug.Log($"[ArknightsACT/TopDown] Room {index + 1} started with {_livingEnemies} enemies.", this);
            if (_livingEnemies <= 0)
                CompleteRoom();
        }

        private void Spawn(int templateIndex, Vector2 localOffset, float healthScale = 1f)
        {
            if (templates == null || templateIndex < 0 || templateIndex >= templates.Length || templates[templateIndex] == null)
            {
                Debug.LogWarning($"[ArknightsACT/TopDown] Missing enemy template index {templateIndex}.", this);
                return;
            }

            var position = roomCenters[_currentRoom] + localOffset;
            var clone = Instantiate(templates[templateIndex], position, Quaternion.identity, _enemyRoot);
            clone.name = $"TopDown_R{_currentRoom + 1}_{templates[templateIndex].name}";
            clone.SetActive(true);

            var entity = clone.GetComponent<CombatEntity>();
            if (entity == null || entity.Health == null)
            {
                Destroy(clone);
                Debug.LogWarning($"[ArknightsACT/TopDown] Spawned template {templateIndex} has no CombatEntity/Health.", this);
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

            if (upgradePanel != null && upgradePanel.HasAvailableUpgrade)
            {
                _rewardPending = true;
                StartCoroutine(OpenRewardAfterHitStop(_currentRoom + 1));
            }
        }

        private IEnumerator OpenRewardAfterHitStop(int clearedRoom)
        {
            yield return new WaitForSecondsRealtime(0.12f);
            _rewardPending = false;
            if (upgradePanel != null && !upgradePanel.IsOpen && upgradePanel.HasAvailableUpgrade)
                upgradePanel.OpenForRoom(clearedRoom);
        }

        private bool InsideRoom(Vector2 position, Vector2 center)
        {
            var delta = position - center;
            return Mathf.Abs(delta.x) <= roomHalfSize.x * 0.90f && Mathf.Abs(delta.y) <= roomHalfSize.y * 0.90f;
        }

        private void SetDoor(int index, bool active)
        {
            if (doors == null || index < 0 || index >= doors.Length || doors[index] == null)
                return;
            doors[index].SetActive(active);
        }

        private void OnGUI()
        {
            if (_playerEntity == null || _playerEntity.Health == null)
                return;

            var hp = _playerEntity.Health;
            GUI.Box(new Rect(12f, 12f, 500f, 190f), "Oblique Room Prototype · Texas");
            GUI.Label(new Rect(24f, 38f, 460f, 22f), $"Room {Mathf.Max(1, _currentRoom + 1)}/4   Enemies {_livingEnemies}   HP {hp.CurrentHealth:0}/{hp.MaxHealth:0}");
            GUI.Label(new Rect(24f, 62f, 460f, 22f), "WASD move · Mouse aim · LMB/J melee · Space/Shift/K dash · RMB/L Sword Rain");
            GUI.Label(new Rect(24f, 86f, 460f, 22f), "Pseudo-3/4 view · no auto attack · no auto movement");
            if (_build != null)
                GUI.Label(new Rect(24f, 110f, 460f, 22f), $"迅刃 Lv.{_build.SwiftBladeLevel}   余雷 Lv.{_build.ResidualThunderLevel}   导电 Lv.{_build.ConductiveLevel}");
            GUI.Label(new Rect(24f, 134f, 460f, 22f), _battleActive ? "Doors locked: clear the room." : "Room clear: move through the open door.");
            if (_completed)
                GUI.Label(new Rect(24f, 158f, 460f, 24f), "BOSS CLEAR · prototype complete");
        }
    }
}
