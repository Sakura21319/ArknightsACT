using System;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Progression;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.SkillUpgrades;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Shop
{
    [DisallowMultipleComponent]
    public sealed class RogueliteShopController : MonoBehaviour
    {
        private enum OfferKind
        {
            Heal,
            Collectible,
            LevelUpgrade,
            SkillUpgrade,
            TemporaryDamage
        }

        [Serializable]
        private sealed class ShopOffer
        {
            public OfferKind Kind;
            public string Name;
            public string Description;
            public int Cost;
            public UnityEngine.Object Payload;
            public bool Sold;
        }

        [SerializeField] private Transform player;
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private CollectibleInventory collectibleInventory;
        [SerializeField] private LevelUpgradeInventory levelInventory;
        [SerializeField] private CharacterSkillUpgradeInventory skillInventory;
        [SerializeField] private PlayerCombatProfile combatProfile;
        [SerializeField] private CollectibleDefinition[] collectiblePool;
        [SerializeField] private LevelUpgradeDefinition[] levelPool;
        [SerializeField] private CharacterSkillUpgradeDefinition[] skillPool;

        private readonly List<ShopOffer> _offers = new(3);
        private RewardSelectionCoordinator _coordinator;
        private GameplayPauseService _pause;
        private CombatEntity _playerEntity;
        private TemporaryCombatBuffs _temporaryBuffs;
        private ScavengingInventory25D _scavenging;
        private bool _isOpen;
        private int _stockStage;
        private int _rerolls;
        private int _inputUnlockFrame;

        public bool IsOpen => _isOpen;

        public void Configure(
            Transform playerTransform,
            RogueliteRunState state,
            RogueliteStageMapController map,
            CollectibleInventory collectibles,
            LevelUpgradeInventory levels,
            CharacterSkillUpgradeInventory skills,
            PlayerCombatProfile profile,
            CollectibleDefinition[] collectiblesPool,
            LevelUpgradeDefinition[] upgradesPool,
            CharacterSkillUpgradeDefinition[] skillsPool)
        {
            player = playerTransform;
            runState = state;
            stageMap = map;
            collectibleInventory = collectibles;
            levelInventory = levels;
            skillInventory = skills;
            combatProfile = profile;
            collectiblePool = collectiblesPool;
            levelPool = upgradesPool;
            skillPool = skillsPool;
            CachePlayer();
        }

        private void Awake() => CachePlayer();

        private void OnEnable()
        {
            _coordinator = RewardSelectionCoordinator.Instance ?? FindFirstObjectByType<RewardSelectionCoordinator>();
            _pause = GameplayPauseService.Instance;
            if (PlayerRuntimeContext.Instance != null)
                PlayerRuntimeContext.Instance.ActivePlayerChanged += OnActivePlayerChanged;
            CachePlayer();
        }

        private void OnDisable()
        {
            if (PlayerRuntimeContext.Instance != null)
                PlayerRuntimeContext.Instance.ActivePlayerChanged -= OnActivePlayerChanged;
            Close();
            _coordinator?.Release(this);
        }

        private void OnActivePlayerChanged(Transform previousPlayer, Transform nextPlayer)
        {
            CachePlayer();
            if (_isOpen)
                BuildStock();
        }

        private void CachePlayer()
        {
            var activePlayer = PlayerRuntimeContext.Resolve(player);
            if (activePlayer == null)
                return;

            player = activePlayer;
            _playerEntity = player.GetComponent<CombatEntity>();
            _temporaryBuffs = player.GetComponent<TemporaryCombatBuffs>();
            _scavenging = player.GetComponent<ScavengingInventory25D>();
            collectibleInventory = player.GetComponent<CollectibleInventory>();
            levelInventory = player.GetComponent<LevelUpgradeInventory>();
            skillInventory = player.GetComponent<CharacterSkillUpgradeInventory>();
            combatProfile = player.GetComponent<PlayerCombatProfile>();
        }

        private void Update()
        {
            CachePlayer();
            if (ArknightsACT.Gameplay.Input.GameplayInputBlocker.IsBlocked) return;
            if (_isOpen)
            {
                HandleOpenInput();
                return;
            }

            if (GameplayPauseService.Instance != null && GameplayPauseService.Instance.IsPaused)
                return;
            if (!IsPlayerInsideShopBlock())
                return;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                Open();
        }

        private bool IsPlayerInsideShopBlock()
        {
            if (player == null || stageMap == null || stageMap.Blocks == null || stageMap.Blocks.Count == 0)
                return false;

            var width = stageMap.Width;
            var height = stageMap.Height;
            const float chunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
            const float chunkDepth = RogueliteStageWorldMetrics.ChunkDepth;
            var origin = new Vector3(-(width - 1) * chunkWidth * 0.5f, 0f, -(height - 1) * chunkDepth * 0.5f);
            var x = Mathf.FloorToInt((player.position.x - (origin.x - chunkWidth * 0.5f)) / chunkWidth);
            var y = Mathf.FloorToInt((player.position.z - (origin.z - chunkDepth * 0.5f)) / chunkDepth);
            if (x < 0 || y < 0 || x >= width || y >= height)
                return false;
            var index = y * width + x;
            return index >= 0 && index < stageMap.Blocks.Count && stageMap.Blocks[index].Type == RogueliteBlockType.Shop;
        }

        private void Open()
        {
            if (_isOpen || runState == null || stageMap == null)
                return;

            _coordinator ??= RewardSelectionCoordinator.Instance ?? FindFirstObjectByType<RewardSelectionCoordinator>();
            if (_coordinator != null && !_coordinator.TryAcquire(this))
                return;

            if (_stockStage != stageMap.StageIndex || _offers.Count == 0)
            {
                _stockStage = stageMap.StageIndex;
                _rerolls = 0;
                BuildStock();
            }

            _pause ??= GameplayPauseService.Instance;
            if (_pause != null)
                _pause.Pause(this);
            else
                Time.timeScale = 0f;
            _isOpen = true;
            _inputUnlockFrame = Time.frameCount + 1;
        }

        private void HandleOpenInput()
        {
            if (Time.frameCount < _inputUnlockFrame || Keyboard.current == null)
                return;
            var keyboard = Keyboard.current;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) TryBuy(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) TryBuy(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) TryBuy(2);
            else if (keyboard.rKey.wasPressedThisFrame) TryReroll();
        }

        private void BuildStock()
        {
            _offers.Clear();
            AddOffer(BuildSustainOffer());
            AddOffer(BuildCollectibleOffer());

            var roll = UnityEngine.Random.value;
            if (roll < 0.42f)
                AddOffer(BuildLevelOffer());
            else if (roll < 0.68f)
                AddOffer(BuildSkillOffer());
            else if (roll < 0.84f)
                AddOffer(BuildCollectibleOffer());
            else
                AddOffer(BuildTemporaryOffer());

            while (_offers.Count < 3)
                AddOffer(BuildSustainOffer());
        }

        private void AddOffer(ShopOffer offer)
        {
            if (offer != null)
                _offers.Add(offer);
        }

        private ShopOffer BuildSustainOffer()
        {
            if (_playerEntity?.Health != null && _playerEntity.Health.CurrentHealth < _playerEntity.Health.MaxHealth * 0.82f)
            {
                return new ShopOffer
                {
                    Kind = OfferKind.Heal,
                    Name = "医疗补给",
                    Description = "恢复 35% 最大生命值。",
                    Cost = 3
                };
            }
            return BuildTemporaryOffer();
        }

        private ShopOffer BuildTemporaryOffer() => new()
        {
            Kind = OfferKind.TemporaryDamage,
            Name = "临时战术增幅",
            Description = "接下来 90 秒所有伤害 +15%。",
            Cost = 4
        };

        private ShopOffer BuildCollectibleOffer()
        {
            var candidates = new List<CollectibleDefinition>();
            _scavenging ??= player != null ? player.GetComponent<ScavengingInventory25D>() : null;
            var catalog = _scavenging?.Catalog;
            if (catalog != null && collectibleInventory != null)
            {
                for (var i = 0; i < catalog.Count; i++)
                {
                    var definition = catalog[i];
                    if (definition == null || definition.IsSalvageCommodity || !collectibleInventory.CanAcquire(definition))
                        continue;
                    if (combatProfile != null && !combatProfile.Supports(definition.RequiredFeatures))
                        continue;
                    if (stageMap != null && stageMap.StageIndex == 1 && definition.Rarity == CollectibleRarity.Epic)
                        continue;
                    candidates.Add(definition);
                }
            }
            if (candidates.Count == 0)
                return BuildTemporaryOffer();

            var definitionPick = WeightedCollectiblePick(candidates, stageMap != null ? stageMap.StageIndex : 1);
            var cost = definitionPick.Rarity switch
            {
                CollectibleRarity.Epic => 15,
                CollectibleRarity.Rare => 10,
                _ => 6
            };
            return new ShopOffer
            {
                Kind = OfferKind.Collectible,
                Name = definitionPick.DisplayName,
                Description = definitionPick.Description,
                Cost = cost,
                Payload = definitionPick
            };
        }

        private ShopOffer BuildLevelOffer()
        {
            var candidates = new List<LevelUpgradeDefinition>();
            if (levelPool != null && levelInventory != null)
            {
                for (var i = 0; i < levelPool.Length; i++)
                {
                    var definition = levelPool[i];
                    if (definition == null || !levelInventory.CanAcquire(definition))
                        continue;
                    if (combatProfile != null && !combatProfile.Supports(definition.RequiredFeatures))
                        continue;
                    candidates.Add(definition);
                }
            }
            if (candidates.Count == 0)
                return BuildCollectibleOffer();
            var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return new ShopOffer
            {
                Kind = OfferKind.LevelUpgrade,
                Name = "训练：" + pick.DisplayName,
                Description = pick.Description,
                Cost = 5,
                Payload = pick
            };
        }

        private ShopOffer BuildSkillOffer()
        {
            var candidates = new List<CharacterSkillUpgradeDefinition>();
            if (skillPool != null && skillInventory != null)
            {
                for (var i = 0; i < skillPool.Length; i++)
                {
                    var definition = skillPool[i];
                    if (definition != null && skillInventory.CanAcquire(definition))
                        candidates.Add(definition);
                }
            }
            if (candidates.Count == 0)
                return BuildLevelOffer();
            var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return new ShopOffer
            {
                Kind = OfferKind.SkillUpgrade,
                Name = "特化：" + pick.DisplayName,
                Description = pick.Description,
                Cost = 12,
                Payload = pick
            };
        }

        private static CollectibleDefinition WeightedCollectiblePick(List<CollectibleDefinition> candidates, int stage)
        {
            var total = 0f;
            for (var i = 0; i < candidates.Count; i++)
                total += CollectibleStageWeight(candidates[i].Rarity, stage);
            var roll = UnityEngine.Random.value * Mathf.Max(0.01f, total);
            for (var i = 0; i < candidates.Count; i++)
            {
                roll -= CollectibleStageWeight(candidates[i].Rarity, stage);
                if (roll <= 0f)
                    return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private static float CollectibleStageWeight(CollectibleRarity rarity, int stage)
        {
            stage = Mathf.Clamp(stage, 1, 3);
            return rarity switch
            {
                CollectibleRarity.Epic => stage == 1 ? 0f : (stage == 2 ? 0.12f : 0.30f),
                CollectibleRarity.Rare => stage == 1 ? 0.32f : (stage == 2 ? 0.62f : 0.82f),
                _ => stage == 1 ? 1.0f : (stage == 2 ? 0.82f : 0.62f)
            };
        }

        private void TryBuy(int index)
        {
            if (!_isOpen || index < 0 || index >= _offers.Count || runState == null)
                return;
            var offer = _offers[index];
            if (offer == null || offer.Sold || runState.Ingots < offer.Cost)
                return;

            if (!ApplyOffer(offer))
                return;
            if (!runState.TrySpendIngots(offer.Cost))
                return;
            offer.Sold = true;
        }

        private bool ApplyOffer(ShopOffer offer)
        {
            switch (offer.Kind)
            {
                case OfferKind.Heal:
                    if (_playerEntity?.Health == null || _playerEntity.Health.IsDead || _playerEntity.Health.CurrentHealth >= _playerEntity.Health.MaxHealth)
                        return false;
                    return _playerEntity.Health.Heal(_playerEntity.Health.MaxHealth * 0.35f) > 0f;
                case OfferKind.Collectible:
                    _scavenging ??= player != null ? player.GetComponent<ScavengingInventory25D>() : null;
                    return offer.Payload is CollectibleDefinition collectible &&
                           _scavenging != null &&
                           _scavenging.TryAcceptReward(collectible, "商店购买");
                case OfferKind.LevelUpgrade:
                    return offer.Payload is LevelUpgradeDefinition level && levelInventory != null && levelInventory.Acquire(level);
                case OfferKind.SkillUpgrade:
                    return offer.Payload is CharacterSkillUpgradeDefinition skill && skillInventory != null && skillInventory.Acquire(skill);
                case OfferKind.TemporaryDamage:
                    _temporaryBuffs ??= player != null ? player.GetComponent<TemporaryCombatBuffs>() : null;
                    if (_temporaryBuffs == null)
                        return false;
                    _temporaryBuffs.AddAllDamagePercent(0.15f, 90f);
                    return true;
                default:
                    return false;
            }
        }

        private void TryReroll()
        {
            if (!_isOpen || runState == null)
                return;
            var cost = RerollCost;
            if (!runState.TrySpendIngots(cost))
                return;
            _rerolls++;
            BuildStock();
            _inputUnlockFrame = Time.frameCount + 1;
        }

        private int RerollCost => Mathf.Min(16, 2 << Mathf.Min(_rerolls, 3));

        private void Close()
        {
            if (!_isOpen)
                return;
            _isOpen = false;
            if (_pause != null)
                _pause.Resume(this);
            else
                Time.timeScale = 1f;
            _coordinator?.Release(this);
        }

        private void OnGUI()
        {
            if (!_isOpen)
            {
                if (IsPlayerInsideShopBlock() && (GameplayPauseService.Instance == null || !GameplayPauseService.Instance.IsPaused))
                    GUI.Box(new Rect(Screen.width * 0.5f - 145f, Screen.height - 78f, 290f, 42f), "商店 · 按 E 查看商品");
                return;
            }

            var width = Screen.width;
            var height = Screen.height;
            GUI.Box(new Rect(0f, 0f, width, height), GUIContent.none);
            var title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Clamp(width / 44, 24, 40)
            };
            var subtitle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 90, 14, 20)
            };
            GUI.Label(new Rect(0f, height * 0.08f, width, 56f), $"移动城市补给站 · 第 {stageMap?.StageIndex ?? 1} 关", title);
            GUI.Label(new Rect(0f, height * 0.15f, width, 34f), $"源石锭：{runState?.Ingots ?? 0}    刷新：{RerollCost}锭（R）", subtitle);

            var cardWidth = Mathf.Min(320f, width * 0.28f);
            var cardHeight = Mathf.Min(330f, height * 0.50f);
            var gap = Mathf.Min(28f, width * 0.025f);
            var totalWidth = _offers.Count * cardWidth + Mathf.Max(0, _offers.Count - 1) * gap;
            var startX = (width - totalWidth) * 0.5f;
            var startY = height * 0.25f;
            var cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = Mathf.Clamp(width / 88, 14, 21),
                padding = new RectOffset(18, 18, 18, 18)
            };

            for (var i = 0; i < _offers.Count; i++)
            {
                var offer = _offers[i];
                var text = offer.Sold
                    ? $"[{i + 1}]  {offer.Name}\n\n已售出"
                    : $"[{i + 1}]  {offer.Name}\n\n{offer.Description}\n\n价格：{offer.Cost} 源石锭";
                GUI.enabled = !offer.Sold && runState != null && runState.Ingots >= offer.Cost;
                if (GUI.Button(new Rect(startX + i * (cardWidth + gap), startY, cardWidth, cardHeight), text, cardStyle))
                    TryBuy(i);
                GUI.enabled = true;
            }

            if (GUI.Button(new Rect(width * 0.5f - 170f, startY + cardHeight + 24f, 160f, 42f), $"刷新  {RerollCost}锭"))
                TryReroll();
            if (GUI.Button(new Rect(width * 0.5f + 10f, startY + cardHeight + 24f, 160f, 42f), "离开商店"))
                Close();
            GUI.Label(new Rect(0f, startY + cardHeight + 76f, width, 30f), "1 / 2 / 3 购买 · R 刷新 · E / Esc 离开", subtitle);
        }
    }
}
