using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters.Texas;
using UnityEngine;

namespace ArknightsACT.Gameplay.Survivor
{
    /// <summary>
    /// Small, isolated survivor-mode experiment: continuous enemy pressure, kill XP and
    /// repeated Texas build choices. It intentionally reuses the existing combat entities,
    /// enemy brains and upgrade panel so this mode can be deleted or promoted independently.
    /// </summary>
    public sealed class SurvivorPrototypeController2D : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Transform player;
        [SerializeField] private TexasUpgradeChoicePanel upgradePanel;
        [SerializeField] private GameObject[] enemyTemplates;
        [SerializeField] private float worldMinX = -8f;
        [SerializeField] private float worldMaxX = 112f;
        [SerializeField] private float enemyGroundY = -0.37f;

        [Header("Spawning")]
        [SerializeField, Min(0.1f)] private float initialSpawnInterval = 0.85f;
        [SerializeField, Min(0.05f)] private float minimumSpawnInterval = 0.22f;
        [SerializeField, Min(1f)] private float spawnDistanceMin = 7.5f;
        [SerializeField, Min(1f)] private float spawnDistanceMax = 11.5f;
        [SerializeField, Min(1)] private int initialAliveCap = 10;
        [SerializeField, Min(1)] private int maximumAliveCap = 36;
        [SerializeField, Min(0f)] private float rangedUnlockSeconds = 35f;
        [SerializeField, Min(1f)] private float fullRampSeconds = 180f;

        [Header("Progression")]
        [SerializeField, Min(1)] private int startingXpRequirement = 12;
        [SerializeField, Min(1.05f)] private float xpRequirementGrowth = 1.28f;

        private CombatEntity _playerEntity;
        private Transform _runtimeEnemyRoot;
        private float _startedAt;
        private float _nextSpawnAt;
        private int _livingEnemies;
        private int _kills;
        private int _level = 1;
        private int _xp;
        private int _xpRequired;

        public float ElapsedSeconds => Mathf.Max(0f, Time.time - _startedAt);
        public int LivingEnemies => _livingEnemies;
        public int Level => _level;
        public int Xp => _xp;
        public int XpRequired => _xpRequired;

        public void Configure(
            Transform playerTransform,
            TexasUpgradeChoicePanel panel,
            GameObject[] templates,
            float minX,
            float maxX,
            float groundY)
        {
            player = playerTransform;
            upgradePanel = panel;
            enemyTemplates = templates;
            worldMinX = Mathf.Min(minX, maxX);
            worldMaxX = Mathf.Max(minX, maxX);
            enemyGroundY = groundY;
            _playerEntity = player != null ? player.GetComponent<CombatEntity>() : null;
        }

        private void Awake()
        {
            _playerEntity = player != null ? player.GetComponent<CombatEntity>() : null;
            _xpRequired = Mathf.Max(1, startingXpRequirement);
            _startedAt = Time.time;
            _nextSpawnAt = Time.time + 0.6f;

            var root = new GameObject("[SurvivorEnemies]");
            root.transform.SetParent(transform, false);
            _runtimeEnemyRoot = root.transform;
        }

        private void Update()
        {
            if (player == null || _playerEntity == null || _playerEntity.Health == null || _playerEntity.Health.IsDead)
                return;

            TryConsumeLevelUps();

            if (upgradePanel != null && upgradePanel.IsOpen)
                return;

            if (Time.time < _nextSpawnAt)
                return;

            var ramp = Mathf.Clamp01(ElapsedSeconds / Mathf.Max(1f, fullRampSeconds));
            var aliveCap = Mathf.RoundToInt(Mathf.Lerp(initialAliveCap, maximumAliveCap, ramp));
            var interval = Mathf.Lerp(initialSpawnInterval, minimumSpawnInterval, ramp);
            _nextSpawnAt = Time.time + Mathf.Max(minimumSpawnInterval, interval);

            if (_livingEnemies >= aliveCap)
                return;

            SpawnOne();
            if (ElapsedSeconds >= 85f && _livingEnemies < aliveCap - 1 && Random.value < 0.55f)
                SpawnOne();
        }

        private void SpawnOne()
        {
            if (enemyTemplates == null || enemyTemplates.Length == 0 || player == null)
                return;

            var templateIndex = ChooseTemplateIndex();
            templateIndex = Mathf.Clamp(templateIndex, 0, enemyTemplates.Length - 1);
            var template = enemyTemplates[templateIndex];
            if (template == null)
                return;

            var side = Random.value < 0.5f ? -1f : 1f;
            var distance = Random.Range(spawnDistanceMin, Mathf.Max(spawnDistanceMin, spawnDistanceMax));
            var x = Mathf.Clamp(player.position.x + side * distance, worldMinX + 0.8f, worldMaxX - 0.8f);

            // If clamping placed the spawn too close, try the opposite side once.
            if (Mathf.Abs(x - player.position.x) < spawnDistanceMin * 0.65f)
                x = Mathf.Clamp(player.position.x - side * distance, worldMinX + 0.8f, worldMaxX - 0.8f);

            var clone = Instantiate(template, new Vector3(x, enemyGroundY, 0f), Quaternion.identity, _runtimeEnemyRoot);
            clone.name = "Survivor_" + template.name.Replace("EnemyTemplate_", string.Empty);
            clone.SetActive(true);

            var entity = clone.GetComponent<CombatEntity>();
            if (entity == null || entity.Health == null)
            {
                Destroy(clone);
                return;
            }

            var healthScale = 1f + Mathf.Clamp01(ElapsedSeconds / 210f) * 0.85f;
            entity.Health.SetMaxHealth(entity.Health.MaxHealth * healthScale);

            var xpReward = templateIndex == 2 ? 3 : 2;
            _livingEnemies++;
            entity.Health.Died += () => OnEnemyDied(xpReward);
        }

        private int ChooseTemplateIndex()
        {
            if (enemyTemplates == null || enemyTemplates.Length <= 1)
                return 0;

            var roll = Random.value;
            if (ElapsedSeconds >= rangedUnlockSeconds && enemyTemplates.Length >= 3 && roll < 0.24f)
                return 2;
            if (roll < 0.50f)
                return Mathf.Min(1, enemyTemplates.Length - 1);
            return 0;
        }

        private void OnEnemyDied(int xpReward)
        {
            _livingEnemies = Mathf.Max(0, _livingEnemies - 1);
            _kills++;
            _xp += Mathf.Max(1, xpReward);
        }

        private void TryConsumeLevelUps()
        {
            if (_xp < _xpRequired || (upgradePanel != null && upgradePanel.IsOpen))
                return;

            _xp -= _xpRequired;
            _level++;
            _xpRequired = Mathf.Max(_xpRequired + 1, Mathf.RoundToInt(_xpRequired * xpRequirementGrowth));

            if (upgradePanel != null && upgradePanel.HasAvailableUpgrade)
                upgradePanel.OpenForRoom(_level);
        }

        private void OnGUI()
        {
            if (player == null)
                return;

            var elapsed = ElapsedSeconds;
            var minutes = Mathf.FloorToInt(elapsed / 60f);
            var seconds = Mathf.FloorToInt(elapsed % 60f);
            var health = _playerEntity != null ? _playerEntity.Health : null;
            var hp = health != null ? health.CurrentHealth : 0f;
            var maxHp = health != null ? health.MaxHealth : 0f;

            GUI.Box(new Rect(12f, 12f, 430f, 170f), "Survivor Prototype · Texas");
            GUI.Label(new Rect(24f, 38f, 390f, 22f), $"Time {minutes:00}:{seconds:00}   Lv.{_level}   XP {_xp}/{_xpRequired}");
            GUI.Label(new Rect(24f, 61f, 390f, 22f), $"HP {hp:0}/{maxHp:0}   Enemies {_livingEnemies}   Kills {_kills}");
            GUI.Label(new Rect(24f, 84f, 390f, 22f), "Basic attack: AUTO MELEE · no magnet · no auto-step");
            GUI.Label(new Rect(24f, 107f, 390f, 22f), "A/D move   Space jump   K/Shift dash   L/RMB Sword Rain");
            GUI.Label(new Rect(24f, 130f, 390f, 30f), "Auto attack only checks the CURRENT facing melee range. You own positioning.");
        }
    }
}
