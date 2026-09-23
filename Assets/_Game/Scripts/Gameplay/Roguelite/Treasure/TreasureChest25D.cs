using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.SkillUpgrades;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    public enum TreasureChestKind
    {
        Normal,
        Spike,
        Monster
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class TreasureChest25D : MonoBehaviour
    {
        [SerializeField] private TreasureChestKind kind;
        [SerializeField, Range(0f, 1f)] private float spikeReflectFraction = 0.10f;
        [SerializeField, Min(0f)] private float destroyDelay = 0.75f;

        private CombatEntity _entity;
        private CombatEntity _player;
        private TreasureMonsterBrain25D _monsterBrain;
        private bool _activated;
        private bool _rewardHandled;
        private bool _destroyScheduled;

        public TreasureChestKind Kind => kind;
        public bool IsActivated => _activated;

        public void Configure(TreasureChestKind value, CombatEntity player)
        {
            kind = value;
            _player = player;
        }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _monsterBrain = GetComponent<TreasureMonsterBrain25D>();
        }

        private void OnEnable()
        {
            if (_entity != null)
            {
                _entity.Damaged += OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_entity != null)
            {
                _entity.Damaged -= OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died -= OnDied;
            }
        }

        private void OnDamaged(DamageContext context, DamageResult result)
        {
            if (!result.Applied)
                return;

            if (kind == TreasureChestKind.Monster && !_activated)
            {
                _activated = true;
                _player ??= ResolvePlayer(context);
                _monsterBrain?.Activate(_player);
                Debug.Log("[ArknightsACT/Treasure] 怪物箱已激活，并永久锁定玩家。", this);
            }

            if (kind != TreasureChestKind.Spike || result.Killed || context.ProcGeneration > 0)
                return;

            var attacker = ResolvePlayer(context);
            if (attacker?.Health == null || attacker.Health.IsDead)
                return;

            var reflected = Mathf.Max(0f, result.Damage * spikeReflectFraction);
            var safeMaximum = Mathf.Max(0f, attacker.Health.CurrentHealth - 1f);
            reflected = Mathf.Min(reflected, safeMaximum);
            if (reflected <= 0f)
                return;

            DamageSystem.Apply(new DamageContext(
                _entity,
                _entity,
                attacker,
                reflected,
                DamageType.True,
                Vector2.zero,
                procGeneration: context.ProcGeneration + 1,
                sourceId: "SpikeTreasureReflect", tags: DamageTags.SecondaryProc));
        }

        private void OnDied()
        {
            if (_rewardHandled)
                return;
            _rewardHandled = true;
            _player ??= FindPlayer();

            DisableCollision();

            switch (kind)
            {
                case TreasureChestKind.Spike:
                    OpenCollectibleReward(
                        "尖刺宝箱 · 藏品二选一",
                        CollectibleRarity.Common,
                        2,
                        ScheduleDestroy);
                    break;
                case TreasureChestKind.Monster:
                    OpenMonsterRewardChain(ScheduleDestroy);
                    break;
                default:
                    GrantNormalReward();
                    ScheduleDestroy();
                    break;
            }
        }

        private void DisableCollision()
        {
            var controller = GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            var colliders = GetComponents<Collider>();
            for (var i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = false;
        }

        private void ScheduleDestroy()
        {
            if (_destroyScheduled)
                return;
            _destroyScheduled = true;
            Destroy(gameObject, Mathf.Max(0.05f, destroyDelay));
        }

        private void GrantNormalReward()
        {
            var run = RogueliteRunState.Instance ?? FindFirstObjectByType<RogueliteRunState>();
            var roll = Random.value;
            if (roll < 0.45f)
            {
                var amount = Random.Range(2, 5);
                run?.AddIngots(amount);
                Debug.Log($"[ArknightsACT/Treasure] 普通箱：获得 {amount} 源石锭。", this);
                return;
            }

            if (roll < 0.75f)
            {
                if (_player?.Health != null && !_player.Health.IsDead)
                {
                    var healed = _player.Health.Heal(_player.Health.MaxHealth * 0.22f);
                    Debug.Log($"[ArknightsACT/Treasure] 普通箱：恢复 {healed:0} 生命。", this);
                }
                return;
            }

            var buffs = _player != null ? _player.GetComponent<TemporaryCombatBuffs>() : null;
            buffs?.AddAllDamagePercent(0.15f, 45f);
            Debug.Log("[ArknightsACT/Treasure] 普通箱：45秒内所有伤害 +15%。", this);
        }

        private void OpenMonsterRewardChain(System.Action completed)
        {
            var skillRewards = FindFirstObjectByType<CharacterSkillUpgradeRewardController>();
            if (skillRewards != null && skillRewards.OpenReward(
                    "怪物箱 · 角色技能特化",
                    2,
                    () => OpenCollectibleReward(
                        "怪物箱 · 额外藏品",
                        CollectibleRarity.Common,
                        2,
                        completed)))
                return;

            OpenCollectibleReward("怪物箱 · 藏品二选一", CollectibleRarity.Common, 2, completed);
        }

        private void OpenCollectibleReward(string title, CollectibleRarity rarity, int count, System.Action completed)
        {
            var reward = FindFirstObjectByType<RogueliteRewardController>();
            if (reward != null && reward.OpenReward(title, rarity, count, completed))
                return;
            completed?.Invoke();
        }

        private CombatEntity ResolvePlayer(in DamageContext context)
        {
            if (context.Source != null && context.Source.Team == Team.Player)
                return context.Source;
            if (context.Owner != null && context.Owner.Team == Team.Player)
                return context.Owner;
            return _player ?? FindPlayer();
        }

        private static CombatEntity FindPlayer()
        {
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (candidate != null && candidate.Team == Team.Player && candidate.Health != null && !candidate.Health.IsDead)
                    return candidate;
            }
            return null;
        }
    }
}
