using System.Collections.Generic;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite.Rewards;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Progression
{
    [DisallowMultipleComponent]
    public sealed class LevelUpRewardController : MonoBehaviour
    {
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private LevelUpgradeInventory inventory;
        [SerializeField] private PlayerCombatProfile combatProfile;
        [SerializeField] private LevelUpgradeDefinition[] upgradePool;
        [SerializeField, Range(1, 3)] private int choiceCount = 3;
        [SerializeField] private Transform player;

        private readonly Queue<int> _pendingLevels = new();
        private readonly List<LevelUpgradeDefinition> _choices = new();
        private readonly List<LevelUpgradeDefinition> _runtimeFallbacks = new();
        private GameplayPauseService _pause;
        private RewardSelectionCoordinator _coordinator;
        private bool _isOpen;
        private int _displayLevel;
        private int _inputUnlockFrame;
        private PlayerSkillController _playerSkills;
        private IGameplayPresentationLock _presentationLock;

        public bool IsOpen => _isOpen;

        public void Configure(
            RogueliteRunState state,
            LevelUpgradeInventory targetInventory,
            PlayerCombatProfile profile,
            LevelUpgradeDefinition[] pool,
            Transform playerTransform = null)
        {
            runState = state;
            inventory = targetInventory;
            combatProfile = profile;
            upgradePool = pool;
            player = playerTransform;
            EnsureCoreUpgradePool();
            ResolvePresentationDependencies();
        }

        private void OnEnable()
        {
            EnsureCoreUpgradePool();
            _pause = GameplayPauseService.Instance;
            _coordinator = RewardSelectionCoordinator.Instance ?? FindFirstObjectByType<RewardSelectionCoordinator>();
            ResolvePresentationDependencies();
            if (runState != null)
                runState.LevelIncreased += OnLevelIncreased;
        }

        private void OnDisable()
        {
            if (runState != null)
                runState.LevelIncreased -= OnLevelIncreased;
            Close();
            _coordinator?.Release(this);
            _pendingLevels.Clear();
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _runtimeFallbacks.Count; i++)
            {
                if (_runtimeFallbacks[i] != null)
                    Destroy(_runtimeFallbacks[i]);
            }
            _runtimeFallbacks.Clear();
        }

        private void EnsureCoreUpgradePool()
        {
            var combined = new List<LevelUpgradeDefinition>();
            if (upgradePool != null)
            {
                for (var i = 0; i < upgradePool.Length; i++)
                {
                    var existing = upgradePool[i];
                    if (existing != null && !ContainsUpgrade(combined, existing.Id))
                        combined.Add(existing);
                }
            }

            AddRuntimeFallback(
                combined,
                "level_armor_training",
                "装甲强化",
                "物理防御 +12%。最多强化 3 次。",
                CombatFeature.None,
                LevelUpgradeEffectType.PhysicalDefensePercent,
                0.12f,
                3,
                1.00f);
            AddRuntimeFallback(
                combined,
                "level_arts_guard",
                "术式防护",
                "法术抗性 +5。最多强化 3 次。",
                CombatFeature.None,
                LevelUpgradeEffectType.ArtsResistanceFlat,
                5f,
                3,
                0.95f);
            AddRuntimeFallback(
                combined,
                "level_mobility_training",
                "机动训练",
                "移动速度 +6%。最多强化 3 次。",
                CombatFeature.None,
                LevelUpgradeEffectType.MoveSpeedPercent,
                0.06f,
                3,
                0.90f);
            AddRuntimeFallback(
                combined,
                "level_attack_speed_training",
                "快速整备",
                "攻击速度 +8%。最多强化 3 次。",
                CombatFeature.BasicAttack,
                LevelUpgradeEffectType.AttackSpeedPercent,
                0.08f,
                3,
                0.95f);

            AddRuntimeFallback(
                combined,
                "level_overload_reaction",
                "过载反应",
                "【联动】需要“灼烧 + 连锁”。电弧命中后引爆 2.8m 范围，造成相当于本次基础伤害 40% 的 Arts 伤害。",
                CombatFeature.None,
                LevelUpgradeEffectType.OverloadExplosion,
                0.40f,
                1,
                1.10f,
                LevelUpgradeArchetype.Synergy,
                RunBuildTag.Burn | RunBuildTag.Chain,
                RunBuildTag.Overload);

            AddRuntimeFallback(
                combined,
                "level_originium_overclock",
                "源石超频协议",
                "【危险协议】所有伤害 +25%，技力恢复 +35%；每次成功释放技能损失 4% 最大生命值（不会因此直接死亡）。",
                CombatFeature.ActiveSkills,
                LevelUpgradeEffectType.OriginiumOverclock,
                0.25f,
                1,
                0.34f,
                LevelUpgradeArchetype.DangerousProtocol,
                RunBuildTag.None,
                RunBuildTag.Risk | RunBuildTag.Skill);

            AddRuntimeFallback(
                combined,
                "level_blood_debt",
                "血债协议",
                "【危险协议】生命低于 35% 时伤害与攻击速度 +45%；但本局受到的伤害 +20%。",
                CombatFeature.BasicAttack,
                LevelUpgradeEffectType.BloodDebt,
                0.45f,
                1,
                0.30f,
                LevelUpgradeArchetype.DangerousProtocol,
                RunBuildTag.None,
                RunBuildTag.Risk | RunBuildTag.Hunt);

            upgradePool = combined.ToArray();
        }

        private void AddRuntimeFallback(
            List<LevelUpgradeDefinition> pool,
            string id,
            string displayName,
            string description,
            CombatFeature requiredFeatures,
            LevelUpgradeEffectType effectType,
            float value,
            int maxStacks,
            float weight,
            LevelUpgradeArchetype archetype = LevelUpgradeArchetype.Foundation,
            RunBuildTag requiredTags = RunBuildTag.None,
            RunBuildTag grantedTags = RunBuildTag.None)
        {
            if (ContainsUpgrade(pool, id))
                return;

            LevelUpgradeDefinition definition = null;
            for (var i = 0; i < _runtimeFallbacks.Count; i++)
            {
                var candidate = _runtimeFallbacks[i];
                if (candidate != null && candidate.Id == id)
                {
                    definition = candidate;
                    break;
                }
            }

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<LevelUpgradeDefinition>();
                definition.name = id + "_RuntimeFallback";
                definition.hideFlags = HideFlags.HideAndDontSave;
                definition.Configure(
                    id,
                    displayName,
                    description,
                    requiredFeatures,
                    effectType,
                    value,
                    maxStacks,
                    weight,
                    archetype,
                    requiredTags,
                    grantedTags);
                _runtimeFallbacks.Add(definition);
            }

            pool.Add(definition);
        }

        private static bool ContainsUpgrade(List<LevelUpgradeDefinition> pool, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            for (var i = 0; i < pool.Count; i++)
            {
                var candidate = pool[i];
                if (candidate != null && candidate.Id == id)
                    return true;
            }
            return false;
        }

        private void OnLevelIncreased(int level)
        {
            _pendingLevels.Enqueue(level);
            // Health.Died is raised from inside DamageSystem.Apply. Defer the actual UI attempt
            // to Update so the current hit can finish dispatching its presentation events first.
            // Otherwise the level-up overlay can open before the final skill-3 hit FX registers
            // its remaining playback time.
        }

        private void TryOpenNext()
        {
            if (_isOpen || _pendingLevels.Count == 0)
                return;

            ResolvePresentationDependencies();
            if (IsPresentationBusy())
                return;

            _coordinator ??= RewardSelectionCoordinator.Instance ?? FindFirstObjectByType<RewardSelectionCoordinator>();
            if (_coordinator != null && !_coordinator.TryAcquire(this))
                return;

            _displayLevel = _pendingLevels.Dequeue();
            BuildChoices();
            if (_choices.Count == 0)
            {
                // Never consume a level-up without a reward. Once the finite upgrade pool is
                // exhausted, convert the level into a small amount of in-run currency.
                runState?.AddIngots(2);
                Debug.LogWarning(
                    "[ArknightsACT/Progression] No valid level-up choices remain; granted 2 ingots instead.",
                    this);
                _coordinator?.Release(this);
                TryOpenNext();
                return;
            }

            _pause ??= GameplayPauseService.Instance;
            if (_pause != null)
                _pause.Pause(this);
            else
                Time.timeScale = 0f;

            _isOpen = true;
            _inputUnlockFrame = Time.frameCount + 1;
        }

        private void Update()
        {
            if (ArknightsACT.Gameplay.Input.GameplayInputBlocker.IsBlocked) return;
            if (!_isOpen)
            {
                TryOpenNext();
                return;
            }
            if (Time.frameCount < _inputUnlockFrame)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                Choose(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                Choose(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                Choose(2);
        }

        private void BuildChoices()
        {
            _choices.Clear();
            if (upgradePool == null || inventory == null)
                return;

            var candidates = new List<LevelUpgradeDefinition>();
            for (var i = 0; i < upgradePool.Length; i++)
            {
                var definition = upgradePool[i];
                if (definition == null || !inventory.CanAcquire(definition))
                    continue;
                if (combatProfile != null && !combatProfile.Supports(definition.RequiredFeatures))
                    continue;
                candidates.Add(definition);
            }

            var wanted = Mathf.Min(Mathf.Clamp(choiceCount, 1, 3), candidates.Count);
            var usedCategories = new HashSet<LevelUpgradeCategory>();
            for (var i = 0; i < wanted; i++)
            {
                // Prefer different reward families in the same three-choice screen. This avoids
                // rolls such as "three damage cards" while still falling back to the full pool
                // when only one category remains.
                var diversified = candidates.FindAll(
                    definition => definition != null && !usedCategories.Contains(definition.Category));
                var source = diversified.Count > 0 ? diversified : candidates;
                var picked = WeightedPick(source);
                if (picked == null)
                    break;

                _choices.Add(picked);
                usedCategories.Add(picked.Category);
                candidates.Remove(picked);
            }
        }

        private static LevelUpgradeDefinition WeightedPick(List<LevelUpgradeDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            var totalWeight = 0f;
            for (var i = 0; i < candidates.Count; i++)
                totalWeight += candidates[i].RewardWeight;

            var roll = UnityEngine.Random.value * totalWeight;
            for (var i = 0; i < candidates.Count; i++)
            {
                roll -= candidates[i].RewardWeight;
                if (roll <= 0f)
                    return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private void Choose(int index)
        {
            if (!_isOpen || inventory == null || index < 0 || index >= _choices.Count)
                return;

            if (!inventory.Acquire(_choices[index]))
                return;

            Close();
            TryOpenNext();
        }

        private void ResolvePresentationDependencies()
        {
            var activePlayer = PlayerRuntimeContext.Resolve(player);
            if (activePlayer != null && activePlayer != player)
            {
                player = activePlayer;
                inventory = player.GetComponent<LevelUpgradeInventory>();
                combatProfile = player.GetComponent<PlayerCombatProfile>();
                _playerSkills = null;
                _presentationLock = null;
            }

            if (_playerSkills == null && player != null)
                _playerSkills = player.GetComponent<PlayerSkillController>();
            if (inventory == null && player != null)
                inventory = player.GetComponent<LevelUpgradeInventory>();
            if (combatProfile == null && player != null)
                combatProfile = player.GetComponent<PlayerCombatProfile>();

            if (_presentationLock == null && _playerSkills != null)
                _presentationLock = _playerSkills.GetComponent<IGameplayPresentationLock>();
        }

        private bool IsPresentationBusy()
        {
            return (_playerSkills != null && _playerSkills.IsCasting) ||
                   (_presentationLock != null && _presentationLock.IsPresentationBusy);
        }

        private void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            _choices.Clear();
            if (_pause != null)
                _pause.Resume(this);
            else
                Time.timeScale = 1f;
            _coordinator?.Release(this);
        }

        private void OnGUI()
        {
            if (!_isOpen)
                return;

            var width = Screen.width;
            var height = Screen.height;
            GUI.Box(new Rect(0f, 0f, width, height), GUIContent.none);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 44, 24, 40),
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(0f, height * 0.09f, width, 62f), $"等级提升 · Lv.{_displayLevel}", titleStyle);

            var subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 90, 14, 20)
            };
            GUI.Label(new Rect(0f, height * 0.16f, width, 36f), "选择一项本局战斗强化", subtitleStyle);

            var cardWidth = Mathf.Min(310f, width * 0.27f);
            var cardHeight = Mathf.Min(320f, height * 0.50f);
            var gap = Mathf.Min(30f, width * 0.024f);
            var totalWidth = _choices.Count * cardWidth + Mathf.Max(0, _choices.Count - 1) * gap;
            var startX = (width - totalWidth) * 0.5f;
            var startY = height * 0.25f;

            var cardStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(width / 88, 14, 21),
                wordWrap = true,
                padding = new RectOffset(18, 18, 18, 18)
            };

            for (var i = 0; i < _choices.Count; i++)
            {
                var definition = _choices[i];
                var current = inventory.GetStackCount(definition);
                var family = definition.Archetype switch
                {
                    LevelUpgradeArchetype.Synergy => "联动",
                    LevelUpgradeArchetype.DangerousProtocol => "危险协议",
                    _ => "战术强化"
                };
                var text = $"[{i + 1}]  {definition.DisplayName}\n<{family}>\n\n{definition.Description}\n\n等级：{current + 1}/{definition.MaxStacks}";
                var rect = new Rect(startX + i * (cardWidth + gap), startY, cardWidth, cardHeight);
                if (GUI.Button(rect, text, cardStyle))
                    Choose(i);
            }

            GUI.Label(
                new Rect(0f, startY + cardHeight + 24f, width, 32f),
                "点击卡片，或按 1 / 2 / 3 选择",
                subtitleStyle);
        }
    }
}
