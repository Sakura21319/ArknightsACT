using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters.Texas;
using UnityEngine;

namespace ArknightsACT.Gameplay.Rooms
{
    /// <summary>
    /// Small prototype Roguelite loop:
    /// spawn room -> clear enemies -> choose upgrade -> spawn next room.
    /// Enemy presentation templates are scene-local and remain inactive outside spawning.
    /// </summary>
    public sealed class PrototypeRoomLoopController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private TexasUpgradeChoicePanel upgradePanel;
        [SerializeField] private GameObject[] enemyTemplates;
        [SerializeField] private Vector2[] spawnPoints;
        [SerializeField] private Vector3 playerRoomStartPosition;

        [Header("Progression")]
        [SerializeField, Min(3)] private int startingEnemyCount = 3;
        [SerializeField, Min(3)] private int maxEnemyCount = 6;
        [SerializeField, Min(0f)] private float healthGrowthPerRoom = 0.10f;
        [SerializeField, Min(1)] private int rangedUnlockRoom = 4;
        [SerializeField, Range(0f, 1f)] private float roomClearHealFraction = 0.20f;
        [SerializeField, Min(0f)] private float nextRoomDelay = 1.0f;

        private readonly List<CombatEntity> _activeEnemies = new();
        private Coroutine _transitionRoutine;
        private bool _roomClearHandled;

        public int CurrentRoom { get; private set; }
        public int LivingEnemies => CountLivingEnemies();

        public event Action<int> RoomStarted;
        public event Action<int> RoomCleared;

        public void Configure(
            Transform playerTransform,
            TexasUpgradeChoicePanel rewardPanel,
            GameObject[] templates,
            Vector2[] points)
        {
            player = playerTransform;
            upgradePanel = rewardPanel;
            enemyTemplates = templates;
            spawnPoints = points;
            if (player != null)
                playerRoomStartPosition = player.position;
        }

        private void OnEnable()
        {
            if (upgradePanel != null)
                upgradePanel.UpgradeChosen += OnUpgradeChosen;
        }

        private void Start()
        {
            if (upgradePanel != null)
            {
                upgradePanel.UpgradeChosen -= OnUpgradeChosen;
                upgradePanel.UpgradeChosen += OnUpgradeChosen;
            }

            if (!CanSpawn())
            {
                Debug.LogWarning(
                    "[ArknightsACT/RoomLoop] Missing player, reward panel, enemy templates or spawn points. " +
                    "Rebuild Prototype Scene.",
                    this);
                return;
            }

            SpawnRoom(1);
        }

        private void OnDisable()
        {
            if (upgradePanel != null)
                upgradePanel.UpgradeChosen -= OnUpgradeChosen;

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }
        }

        private void Update()
        {
            if (CurrentRoom <= 0 || _roomClearHandled || _transitionRoutine != null)
                return;

            if (_activeEnemies.Count == 0 || CountLivingEnemies() > 0)
                return;

            _roomClearHandled = true;
            HealPlayerAfterRoomClear();
            RoomCleared?.Invoke(CurrentRoom);

            if (upgradePanel != null && upgradePanel.HasAvailableUpgrade && upgradePanel.OpenForRoom(CurrentRoom))
                return;

            // Once the three prototype upgrades have all been collected, keep the room loop
            // running without trapping the player in an empty reward panel.
            BeginNextRoomTransition();
        }

        private void OnUpgradeChosen(int _)
        {
            BeginNextRoomTransition();
        }

        private void BeginNextRoomTransition()
        {
            if (_transitionRoutine == null)
                _transitionRoutine = StartCoroutine(NextRoomRoutine());
        }

        private IEnumerator NextRoomRoutine()
        {
            if (nextRoomDelay > 0f)
                yield return new WaitForSecondsRealtime(nextRoomDelay);

            _transitionRoutine = null;
            SpawnRoom(CurrentRoom + 1);
        }

        private void SpawnRoom(int roomIndex)
        {
            if (!CanSpawn())
                return;

            CurrentRoom = Mathf.Max(1, roomIndex);
            _roomClearHandled = false;
            _activeEnemies.Clear();
            ResetPlayerForRoom();

            var enemyCount = Mathf.Clamp(startingEnemyCount + CurrentRoom - 1, startingEnemyCount, maxEnemyCount);
            var healthMultiplier = 1f + Mathf.Max(0, CurrentRoom - 1) * healthGrowthPerRoom;

            for (var i = 0; i < enemyCount; i++)
            {
                var template = SelectEnemyTemplate(i);
                if (template == null)
                    continue;

                var spawnPoint = spawnPoints[i % spawnPoints.Length];
                var instance = Instantiate(template, new Vector3(spawnPoint.x, spawnPoint.y, 0f), Quaternion.identity);
                instance.name = $"Room_{CurrentRoom:00}_{template.name}_{i + 1}";

                var health = instance.GetComponent<Health>();
                if (health != null)
                    health.SetMaxHealth(health.MaxHealth * healthMultiplier);

                instance.SetActive(true);

                // Combat actors overlap instead of physically shoving one another. Their colliders
                // still collide with floors, platforms and world bounds, and hit detection still
                // uses overlap queries against the same colliders.
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

            var rangedState = CurrentRoom < rangedUnlockRoom ? "melee-only" : "ranged-enabled";
            Debug.Log(
                $"[ArknightsACT/RoomLoop] Room {CurrentRoom} started: enemies={_activeEnemies.Count}, " +
                $"healthMultiplier={healthMultiplier:0.00}x, {rangedState}.",
                this);
            RoomStarted?.Invoke(CurrentRoom);
        }

        private GameObject SelectEnemyTemplate(int spawnIndex)
        {
            if (enemyTemplates == null || enemyTemplates.Length == 0)
                return null;

            // Template order is Soldier, Hound, Crossbowman. Keep the ranged Crossbowman out of
            // the onboarding rooms so the player can learn movement / attack / dash first.
            var availableTemplateCount = CurrentRoom < rangedUnlockRoom
                ? Mathf.Min(2, enemyTemplates.Length)
                : enemyTemplates.Length;

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

            var healed = health.Heal(health.MaxHealth * roomClearHealFraction);
            if (healed > 0.01f)
            {
                Debug.Log(
                    $"[ArknightsACT/RoomLoop] Room {CurrentRoom} clear heal: +{healed:0.#} HP " +
                    $"({health.CurrentHealth:0.#}/{health.MaxHealth:0.#}).",
                    this);
            }
        }

        private int CountLivingEnemies()
        {
            var living = 0;
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var entity = _activeEnemies[i];
                if (entity == null)
                    continue;
                if (entity.Health != null && !entity.Health.IsDead)
                    living++;
            }
            return living;
        }

        private void ResetPlayerForRoom()
        {
            if (player == null)
                return;

            var entity = player.GetComponent<CombatEntity>();
            if (entity != null && entity.Health != null && entity.Health.IsDead)
                return;

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

            var firstColliders = first.GetComponentsInChildren<Collider2D>(true);
            var secondColliders = second.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < firstColliders.Length; i++)
            {
                var firstCollider = firstColliders[i];
                if (firstCollider == null || firstCollider.isTrigger)
                    continue;

                for (var j = 0; j < secondColliders.Length; j++)
                {
                    var secondCollider = secondColliders[j];
                    if (secondCollider == null || secondCollider.isTrigger)
                        continue;
                    Physics2D.IgnoreCollision(firstCollider, secondCollider, true);
                }
            }
        }

        private bool CanSpawn()
        {
            return player != null &&
                   upgradePanel != null &&
                   enemyTemplates != null && enemyTemplates.Length > 0 &&
                   spawnPoints != null && spawnPoints.Length > 0;
        }
    }
}
