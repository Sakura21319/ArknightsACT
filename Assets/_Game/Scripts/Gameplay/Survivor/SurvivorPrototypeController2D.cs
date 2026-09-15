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

        private Transform _player;
        private CombatEntity _playerEntity;
        private TexasUpgradeChoicePanel _upgradePanel;
        private GameObject[] _enemyTemplates;
        private Transform _runtimeEnemyRoot;
        private float _worldMinX;
        private float _worldMaxX;
        private float _enemyGroundY;

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
            Transform player,
            TexasUpgradeChoicePanel upgradePanel,
            GameObject[] enemyTemplates,
            float worldMinX,
            float worldMaxX,
            float enemyGroundY)
        {
            _player = player;
            _playerEntity = player != null ? player.GetComponent<CombatEntity>() : null;
            _upgradePanel = upgradePanel;
            _enemyTemplates = enemyTemplates;
            _worldMinX = Mathf.Min(worldMinX, worldMaxX);
            _worldMaxX = Mathf.Max(worldMinX, worldMaxX);
            _enemyGroundY = enemyGroundY;
        }

        private void Awake()
        {
            _xpRequired = Mathf.Max(1, startingXpRequirement);
            _startedAt = Time.time;
            _nextSpawnAt = Time.time + 0.6f;

            var root = new GameObject("[SurvivorEnemies]");
            _runtimeEnemyRoot = root.transform;
        }

        private void Update()
        {
            if (_player == null || _playerEntity == null || _playerEntity.Health == null || _playerEntity.Health.IsDead)
                return;

            TryConsumeLevelUps();

            if (_upgradePanel != null && _upgradePanel.IsOpen)
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
            if (_enemyTemplates == null || _enemyTemplates.Length == 0 || _player == null)
                return;

            var templateIndex = ChooseTemplateIndex();
            templateIndex = Mathf.Clamp(templateIndex, 0, _enemyTemplates.Length - 1);
            var template = _enemyTemplates[templateIndex];
            if (template == null)
                return;

            var side = Random.value < 0.5f ? -1f : 1f;
            var distance = Random.Range(spawnDistanceMin, Mathf.Max(spawnDistanceMin, spawnDistanceMax));
            var x = Mathf.Clamp(_player.position.x + side * distance, _worldMinX + 0.8f, _worldMaxX - 0.8f);

            // If clamping placed the spawn too close, try the opposite side once.
            if (Mathf.Abs(x - _player.position.x) < spawnDistanceMin * 0.65f)
                x = Mathf.Clamp(_player.position.x - side * distance, _worldMinX + 0.8f, _worldMaxX - 0.8f);

            var clone = Instantiate(template, new Vector3(x, _enemyGroundY, 0f), Quaternion.identity, _runtimeEnemyRoot);
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
            if (_enemyTemplates == null || _enemyTemplates.Length <= 1)
                return 0;

            var roll = Random.value;
            if (ElapsedSeconds >= rangedUnlockSeconds && _enemyTemplates.Length >= 3 && roll < 0.24f)
                return 2;
            if (roll < 0.50f)
                return Mathf.Min(1, _enemyTemplates.Length - 1);
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
            if (_xp < _xpRequired || (_upgradePanel != null && _upgradePanel.IsOpen))
                return;

            _xp -= _xpRequired;
            _level++;
            _xpRequired = Mathf.Max(_xpRequired + 1, Mathf.RoundToInt(_xpRequired * xpRequirementGrowth));

            if (_upgradePanel != null && _upgradePanel.HasAvailableUpgrade)
                _upgradePanel.OpenForRoom(_level);
        }

        private void OnGUI()
        {
            if (_player == null)
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
