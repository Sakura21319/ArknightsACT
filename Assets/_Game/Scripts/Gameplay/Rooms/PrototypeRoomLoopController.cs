using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Rooms
{
    /// <summary>
    /// Combat-room loop shared by the legacy side-view prototype and the migrated 2.5D/XZ scene.
    /// Room completion can be gated by rewards, route selection and events.
    /// </summary>
    public sealed class PrototypeRoomLoopController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject[] enemyTemplates;
        [SerializeField] private Vector2[] spawnPoints;
        [SerializeField] private Vector3[] spawnPoints25D;
        [SerializeField] private Vector3 playerRoomStartPosition;

        [Header("Progression")]
        [SerializeField, Min(3)] private int startingEnemyCount = 3;
        [SerializeField, Min(3)] private int maxEnemyCount = 6;
        [SerializeField, Min(0f)] private float healthGrowthPerRoom = 0.10f;
        [SerializeField, Min(1)] private int rangedUnlockRoom = 4;
        [SerializeField, Range(0f, 1f)] private float roomClearHealFraction = 0f;
        [SerializeField, Min(0f)] private float nextRoomDelay = 0.35f;
        [SerializeField] private bool waitForExternalContinue = true;

        private readonly List<CombatEntity> _activeEnemies = new();
        private Coroutine _transitionRoutine;
        private bool _roomClearHandled;
        private PlayerAttackController _playerAttack;
        private PlayerSkillController _playerSkills;
        private PlayerDashController _playerDash;
        private CombatRoomTuning _pendingTuning = CombatRoomTuning.Default;

        public int CurrentRoom { get; private set; }
        public int LivingEnemies => CountLivingEnemies();
        public bool IsWaitingForContinue { get; private set; }
        public bool Uses25D => spawnPoints25D != null && spawnPoints25D.Length > 0;

        public event Action<int> RoomStarted;
        public event Action<int> RoomCleared;

        public void Configure(Transform playerTransform, GameObject[] templates, Vector2[] points)
        {
            player = playerTransform;
            enemyTemplates = templates;
            spawnPoints = points;
            spawnPoints25D = null;
            CapturePlayerStart();
        }

        public void Configure25D(Transform playerTransform, GameObject[] templates, Vector3[] points)
        {
            player = playerTransform;
            enemyTemplates = templates;
            spawnPoints25D = points;
            spawnPoints = null;
            CapturePlayerStart();
        }

        public void SetExternalContinueGate(bool enabled) => waitForExternalContinue = enabled;
        public bool ContinueToNextRoom() => ContinueToNextRoom(CombatRoomTuning.Default);

        public bool ContinueToNextRoom(CombatRoomTuning tuning)
        {
            if (!_roomClearHandled || _transitionRoutine != null || CurrentRoom <= 0)
                return false;

            _pendingTuning = tuning;
            IsWaitingForContinue = false;
            _transitionRoutine = StartCoroutine(NextRoomRoutine());
            return true;
        }

        private void Start()
        {
            if (!CanSpawn())
            {
                Debug.LogWarning("[ArknightsACT/RoomLoop] Missing player, enemy templates or spawn points. Rebuild Prototype Scene.", this);
                return;
            }

            CachePlayerActionState();
            SpawnRoom(1, CombatRoomTuning.Default);
        }

        private void OnDisable()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }
            IsWaitingForContinue = false;
        }

        private void Update()
        {
            if (CurrentRoom <= 0 || _roomClearHandled || _transitionRoutine != null)
                return;
            if (_activeEnemies.Count == 0 || CountLivingEnemies() > 0)
                return;
            if (!IsPlayerActionSettled())
                return;

            _roomClearHandled = true;
            HealPlayerAfterRoomClear();
            RoomCleared?.Invoke(CurrentRoom);

            if (waitForExternalContinue)
            {
                IsWaitingForContinue = true;
                return;
            }

            _pendingTuning = CombatRoomTuning.Default;
            _transitionRoutine = StartCoroutine(NextRoomRoutine());
        }

        private IEnumerator NextRoomRoutine()
        {
            if (nextRoomDelay > 0f)
                yield return new WaitForSecondsRealtime(nextRoomDelay);

            var tuning = _pendingTuning;
            _pendingTuning = CombatRoomTuning.Default;
            _transitionRoutine = null;
            SpawnRoom(CurrentRoom + 1, tuning);
        }

        private void SpawnRoom(int roomIndex, CombatRoomTuning tuning)
        {
            if (!CanSpawn())
                return;

            CurrentRoom = Mathf.Max(1, roomIndex);
            _roomClearHandled = false;
            IsWaitingForContinue = false;
            _activeEnemies.Clear();
            ResetPlayerForRoom();

            var spawnCount = GetSpawnPointCount();
            var baseEnemyCount = Mathf.Clamp(startingEnemyCount + CurrentRoom - 1, startingEnemyCount, maxEnemyCount);
            var enemyCount = tuning.EnemyCountOverride > 0
                ? Mathf.Clamp(tuning.EnemyCountOverride, 1, spawnCount)
                : Mathf.Clamp(baseEnemyCount + tuning.EnemyCountBonus, 1, spawnCount);
            var progressionHealthMultiplier = 1f + Mathf.Max(0, CurrentRoom - 1) * healthGrowthPerRoom;
            var healthMultiplier = progressionHealthMultiplier * Mathf.Max(0.1f, tuning.HealthMultiplier);

            for (var i = 0; i < enemyCount; i++)
            {
                var template = SelectEnemyTemplate(i, tuning);
                if (template == null)
                    continue;

                var spawnPosition = GetSpawnPosition(i);
                var instance = Instantiate(template, spawnPosition, Quaternion.identity);
                instance.name = $"Room_{CurrentRoom:00}_{template.name}_{i + 1}";

                var health = instance.GetComponent<Health>();
                if (health != null)
                    health.SetMaxHealth(health.MaxHealth * healthMultiplier);

                instance.SetActive(true);
                IgnoreActorCollision(instance, player != null ? player.gameObject : null);
                for (var previous = 0; previous < _activeEnemies.Count; previous++)
                {
                    var previousEntity = _activeEnemies[previous];
                    if (previousEntity != null)
                        IgnoreActorCollision(instance, previousEntity.gameObject);
                }

                var entity = instance.GetComponent<CombatEntity>();
                if (entity != null)
                    _activeEnemies.Add(entity);
            }

            var rangedEnabled = tuning.EnableRangedEarly || CurrentRoom >= rangedUnlockRoom;
            Debug.Log(
                $"[ArknightsACT/RoomLoop] Room {CurrentRoom} started: mode={(Uses25D ? "25D" : "2D")}, node={tuning.Label}, " +
                $"enemies={_activeEnemies.Count}, healthMultiplier={healthMultiplier:0.00}x, ranged={(rangedEnabled ? "enabled" : "locked")}.",
                this);
            RoomStarted?.Invoke(CurrentRoom);
        }

        private GameObject SelectEnemyTemplate(int spawnIndex, CombatRoomTuning tuning)
        {
            if (enemyTemplates == null || enemyTemplates.Length == 0)
                return null;

            if (tuning.ForcedTemplateIndex >= 0 && tuning.ForcedTemplateIndex < enemyTemplates.Length)
                return enemyTemplates[tuning.ForcedTemplateIndex];

            var rangedEnabled = tuning.EnableRangedEarly || CurrentRoom >= rangedUnlockRoom;
            var availableTemplateCount = rangedEnabled ? Mathf.Min(3, enemyTemplates.Length) : Mathf.Min(2, enemyTemplates.Length);
            if (availableTemplateCount <= 0)
                return null;

            var index = (spawnIndex + CurrentRoom - 1) % availableTemplateCount;
            return enemyTemplates[index];
        }

        private void HealPlayerAfterRoomClear()
        {
            if (player == null || roomClearHealFraction <= 0f)
                return;

            var entity = player.GetComponent<CombatEntity>();
            var health = entity != null ? entity.Health : player.GetComponent<Health>();
            if (health == null || health.IsDead)
                return;
            health.Heal(health.MaxHealth * roomClearHealFraction);
        }

        private int CountLivingEnemies()
        {
            var living = 0;
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var entity = _activeEnemies[i];
                if (entity == null)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }
                var health = entity.Health;
                if (health != null && !health.IsDead)
                    living++;
            }
            return living;
        }

        private void CachePlayerActionState()
        {
            if (player == null)
                return;
            _playerAttack = player.GetComponent<PlayerAttackController>();
            _playerSkills = player.GetComponent<PlayerSkillController>();
            _playerDash = player.GetComponent<PlayerDashController>();
        }

        private bool IsPlayerActionSettled()
        {
            if (player == null)
                return true;
            if (_playerAttack == null && _playerSkills == null && _playerDash == null)
                CachePlayerActionState();

            return !(_playerAttack?.IsAttacking ?? false) &&
                   !(_playerSkills?.IsCasting ?? false) &&
                   !(_playerDash?.IsDashing ?? false);
        }

        private void ResetPlayerForRoom()
        {
            if (player == null)
                return;

            var entity = player.GetComponent<CombatEntity>();
            if (entity != null && entity.Health != null && entity.Health.IsDead)
                return;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.position = playerRoomStartPosition;
                controller.enabled = true;
                player.GetComponent<PlayerMotor25D>()?.ResetMotion();
                return;
            }

            player.position = playerRoomStartPosition;
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private static void IgnoreActorCollision(GameObject first, GameObject second)
        {
            if (first == null || second == null || first == second)
                return;

            var first2D = first.GetComponentsInChildren<Collider2D>(true);
            var second2D = second.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < first2D.Length; i++)
            {
                var a = first2D[i];
                if (a == null || a.isTrigger)
                    continue;
                for (var j = 0; j < second2D.Length; j++)
                {
                    var b = second2D[j];
                    if (b == null || b.isTrigger)
                        continue;
                    Physics2D.IgnoreCollision(a, b, true);
                }
            }

            var first3D = first.GetComponentsInChildren<Collider>(true);
            var second3D = second.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < first3D.Length; i++)
            {
                var a = first3D[i];
                if (a == null || a.isTrigger)
                    continue;
                for (var j = 0; j < second3D.Length; j++)
                {
                    var b = second3D[j];
                    if (b == null || b.isTrigger)
                        continue;
                    Physics.IgnoreCollision(a, b, true);
                }
            }
        }

        private void CapturePlayerStart()
        {
            if (player == null)
                return;
            playerRoomStartPosition = player.position;
            CachePlayerActionState();
        }

        private int GetSpawnPointCount() => Uses25D ? spawnPoints25D.Length : spawnPoints != null ? spawnPoints.Length : 0;

        private Vector3 GetSpawnPosition(int index)
        {
            if (Uses25D)
                return spawnPoints25D[index % spawnPoints25D.Length];
            var point = spawnPoints[index % spawnPoints.Length];
            return new Vector3(point.x, point.y, 0f);
        }

        private bool CanSpawn()
        {
            return player != null && enemyTemplates != null && enemyTemplates.Length > 0 && GetSpawnPointCount() > 0;
        }
    }
}
