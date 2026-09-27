using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    /// <summary>
    /// PRTS-backed enemy stats plus the official Integrated Strategies 3
    /// (Mizuki & Caerula Arbor) difficulty/floor scaling used by the current prototype.
    /// Exact stage-instance overrides can replace the source level later.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity), typeof(Health), typeof(PrototypeEnemyCombatBrain25D))]
    public sealed class EnemyOfficialStats25D : MonoBehaviour
    {
        // ACT is real-time free movement rather than tile movement. Keep official relative speeds,
        // then apply one explicit pacing conversion requested for the action prototype.
        private const float ActMovementPaceMultiplier = 1.20f;

        [SerializeField] private string sourceId;
        [SerializeField, Range(0, 1)] private int officialLevel;
        [SerializeField, Min(1)] private int roguelikeFloor = 1;
        [SerializeField, Range(0, 5)] private int roguelikeDifficulty;
        [SerializeField] private EnemyRank rank = EnemyRank.Normal;

        public string SourceId => sourceId;
        public int OfficialLevel => officialLevel;
        public int RoguelikeFloor => roguelikeFloor;
        public int RoguelikeDifficulty => roguelikeDifficulty;

        public void Configure(string enemySourceId, int level)
        {
            ConfigureRoguelike(enemySourceId, level, 1, 0, EnemyRank.Normal);
        }

        public void ConfigureRoguelike(
            string enemySourceId,
            int level,
            int floor,
            int difficulty,
            EnemyRank enemyRank)
        {
            sourceId = enemySourceId ?? string.Empty;
            officialLevel = Mathf.Clamp(level, 0, 1);
            roguelikeFloor = Mathf.Max(1, floor);
            roguelikeDifficulty = Mathf.Clamp(difficulty, 0, 5);
            rank = enemyRank;
            Apply();
        }

        private void OnEnable()
        {
            if (!string.IsNullOrWhiteSpace(sourceId))
                Apply();
        }

        private void Apply()
        {
            if (!TryResolve(sourceId, officialLevel, out var source))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/EnemyStats] No official baseline registered for '{sourceId}' L{officialLevel}.",
                    this);
                return;
            }

            var hp = source.MaxHealth;
            var attack = source.Attack;
            var defense = source.Defense;
            var resistance = source.Resistance;

            // IS3: entering every new floor (including floor 1) multiplies enemy HP and ATK by
            // (1 + enemyDifficulty%). At difficulties 1-15 that percentage equals the difficulty.
            // This compounds per floor.
            var enemyDifficultyPercent = ResolveIs3EnemyDifficultyPercent(roguelikeDifficulty);
            var floorScale = Mathf.Pow(1f + enemyDifficultyPercent * 0.01f, roguelikeFloor);
            hp *= floorScale;
            attack *= floorScale;

            // IS3 difficulty 3: all enemies RES +10.
            if (roguelikeDifficulty >= 3)
                resistance += 10f;

            // IS3 difficulty 5: leader/Boss ATK and DEF receive an additional +15%.
            if (roguelikeDifficulty >= 5 && rank == EnemyRank.Boss)
            {
                attack *= 1.15f;
                defense *= 1.15f;
            }

            var health = GetComponent<Health>();
            if (health != null)
                health.SetMaxHealth(hp);

            var entity = GetComponent<CombatEntity>();
            var combatStats = entity != null ? entity.Stats : GetComponent<CombatStats>();
            if (combatStats != null)
            {
                combatStats.SetBasePhysicalDefense(defense);
                combatStats.SetBaseArtsResistance(
                    Mathf.Min(DamageSystem.MaxArtsResistance, resistance));
            }

            var brain = GetComponent<PrototypeEnemyCombatBrain25D>();
            if (brain != null)
            {
                brain.ApplyOfficialCombatStats(
                    attack,
                    source.AttackInterval,
                    source.MoveSpeed * ActMovementPaceMultiplier,
                    source.AttackRadius);
            }
        }

        private static float ResolveIs3EnemyDifficultyPercent(int difficulty)
        {
            // The current operation UI exposes only IS3 difficulties 0-5.
            // In this range the official enemy-difficulty percentage equals the selected level.
            return Mathf.Clamp(difficulty, 0, 5);
        }

        private static bool TryResolve(string id, int level, out EnemyStatsSnapshot result)
        {
            var high = level >= 1;
            switch (id)
            {
                case "enemy_1002_nsabr": // 士兵
                    result = high
                        ? new EnemyStatsSnapshot(2750f, 300f, 130f, 0f, 1.1f, 2.0f, 0f)
                        : new EnemyStatsSnapshot(1650f, 200f, 100f, 0f, 1.1f, 2.0f, 0f);
                    return true;

                case "enemy_1000_gopro": // 猎狗
                    result = high
                        ? new EnemyStatsSnapshot(2350f, 400f, 190f, 30f, 1.9f, 1.4f, 0f)
                        : new EnemyStatsSnapshot(820f, 190f, 0f, 20f, 1.9f, 1.4f, 0f);
                    return true;

                case "enemy_1003_ncbow": // 弩手
                    result = high
                        ? new EnemyStatsSnapshot(1850f, 310f, 100f, 0f, 0.9f, 2.4f, 1.9f)
                        : new EnemyStatsSnapshot(1400f, 240f, 100f, 0f, 0.9f, 2.4f, 1.9f);
                    return true;

                case "enemy_1006_shield": // 重装防御者
                    result = high
                        ? new EnemyStatsSnapshot(8000f, 600f, 850f, 0f, 0.75f, 2.6f, 0f)
                        : new EnemyStatsSnapshot(6000f, 600f, 800f, 0f, 0.75f, 2.6f, 0f);
                    return true;

                default:
                    result = default;
                    return false;
            }
        }

        private readonly struct EnemyStatsSnapshot
        {
            public readonly float MaxHealth;
            public readonly float Attack;
            public readonly float Defense;
            public readonly float Resistance;
            public readonly float MoveSpeed;
            public readonly float AttackInterval;
            public readonly float AttackRadius;

            public EnemyStatsSnapshot(
                float maxHealth,
                float attack,
                float defense,
                float resistance,
                float moveSpeed,
                float attackInterval,
                float attackRadius)
            {
                MaxHealth = maxHealth;
                Attack = attack;
                Defense = defense;
                Resistance = resistance;
                MoveSpeed = moveSpeed;
                AttackInterval = attackInterval;
                AttackRadius = attackRadius;
            }
        }
    }
}
