using System.Text;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Routing;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;
using UnityEngine.UI;

namespace ArknightsACT.Gameplay.Abilities
{
    /// <summary>
    /// Production gameplay HUD for HP + skill/SP state.
    /// Values intentionally refresh at integer boundaries so the HUD is stable and readable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSkillController))]
    public sealed class GameplayHUDController : MonoBehaviour
    {
        private sealed class SkillSlotView
        {
            public Image Icon;
            public Text FallbackIcon;
            public Text Name;
            public Text Value;
            public Image Fill;
            public float FillWidth;
            public Text KeyText;
            public int LastCurrent = int.MinValue;
            public int LastCost = int.MinValue;
            public bool LastReady;
            public bool LastCasting;
            public bool LastActive;
            public string LastActiveValue = string.Empty;
        }

        private static readonly Color Panel = new(0.025f, 0.035f, 0.043f, 0.48f);
        private static readonly Color PanelAlt = new(0.035f, 0.048f, 0.056f, 0.56f);
        private static readonly Color White = new(0.96f, 0.98f, 0.99f, 1f);
        private static readonly Color Muted = new(0.56f, 0.62f, 0.65f, 1f);
        private static readonly Color Cyan = new(0.18f, 0.82f, 0.88f, 1f);
        private static readonly Color SpGreen = new(0.30f, 0.82f, 0.36f, 1f);
        private static readonly Color SpReadyYellow = new(1.00f, 0.78f, 0.12f, 1f);
        private static readonly Color Warning = new(1.00f, 0.74f, 0.18f, 1f);
        private static readonly Color Danger = new(1.00f, 0.32f, 0.22f, 1f);
        private const float SkillBarWidth = 196f;
        private const float SkillBarHeight = 7f;

        private PlayerSkillController _skills;
        private Health _health;
        private StatusController _status;
        private CollectibleInventory _collectibles;
        private ScavengingInventory25D _scavenging;
        private RogueliteRunState _runState;
        private Canvas _canvas;
        private GameObject _root;
        private Font _font;
        private Image _hpFill;
        private Text _hpValue;
        private SkillSlotView _slot1;
        private SkillSlotView _slot2;
        private SkillReadyWorldIndicator _readyIndicator;
        private Text _spFeedback;
        private CanvasGroup _spFeedbackGroup;
        private Text _backpackValue;
        private Text _ingotValue;
        private Text _statusValue;
        private float _spFeedbackUntil;
        private string _lastStatusText = string.Empty;

        private int _lastHp = int.MinValue;
        private int _lastMaxHp = int.MinValue;
        private int _lastReadyCount = -1;
        private int _lastBackpackUsed = -1;
        private int _lastBackpackCapacity = -1;
        private int _lastIngots = int.MinValue;

        private void Awake()
        {
            _skills = GetComponent<PlayerSkillController>();
            _health = GetComponent<Health>();
            _status = GetComponent<StatusController>();
            _collectibles = GetComponent<CollectibleInventory>();
            _scavenging = GetComponent<ScavengingInventory25D>();
            Build();
            _readyIndicator = GetComponent<SkillReadyWorldIndicator>();
            if (_readyIndicator == null)
                _readyIndicator = gameObject.AddComponent<SkillReadyWorldIndicator>();
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.Changed += OnHealthChanged;
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCastSucceeded;
            if (_collectibles != null)
                _collectibles.SkillPointFeedback += OnCollectibleSkillPointFeedback;
            if (_scavenging != null)
                _scavenging.Changed += OnBackpackChanged;
            if (_runState != null)
                _runState.Changed += OnRunStateChanged;
            AttachRuntimeSources();
            RefreshHealth(force: true);
            RefreshSkills(force: true);
            RefreshStatuses(force: true);
            RefreshBackpack(force: true);
            RefreshIngots(force: true);
        }

        private void Start()
        {
            AttachRuntimeSources();
            RefreshHealth(force: true);
            RefreshSkills(force: true);
            RefreshStatuses(force: true);
            RefreshBackpack(force: true);
            RefreshIngots(force: true);
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.Changed -= OnHealthChanged;
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCastSucceeded;
            if (_collectibles != null)
                _collectibles.SkillPointFeedback -= OnCollectibleSkillPointFeedback;
            if (_scavenging != null)
                _scavenging.Changed -= OnBackpackChanged;
            if (_runState != null)
                _runState.Changed -= OnRunStateChanged;
            _readyIndicator?.SetReadyCount(0);
        }

        private void OnDestroy()
        {
            if (_font != null)
                Destroy(_font);
        }

        private void Update()
        {
            var flow = RogueliteGameFlowController.Instance;
            var visible = flow == null || flow.IsRunning;
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);

            if (!visible)
            {
                if (_lastReadyCount != 0)
                {
                    _lastReadyCount = 0;
                    _readyIndicator?.SetReadyCount(0);
                }
                return;
            }

            AttachRuntimeSources();
            // HP is event-driven, but this also catches a component that changed max HP without
            // emitting an event in older content.
            RefreshHealth(force: false);
            RefreshSkills(force: false);
            RefreshStatuses(force: false);
            RefreshBackpack(force: false);
            RefreshIngots(force: false);
            UpdateSkillPointFeedback();
        }

        private void OnHealthChanged(float _, float __) => RefreshHealth(force: false);
        private void OnSkillCastSucceeded(int _) => RefreshSkills(force: true);
        private void OnBackpackChanged() => RefreshBackpack(force: false);
        private void OnRunStateChanged() => RefreshIngots(force: false);

        private void OnCollectibleSkillPointFeedback(int amount, string source)
        {
            if (_spFeedback == null || _spFeedbackGroup == null || amount <= 0)
                return;
            _spFeedback.text = $"+{amount} SP";
            _spFeedbackGroup.alpha = 1f;
            _spFeedbackUntil = Time.unscaledTime + 1.05f;
        }

        private void AttachRuntimeSources()
        {
            var scavenging = GetComponent<ScavengingInventory25D>();
            if (_scavenging != scavenging)
            {
                if (_scavenging != null)
                    _scavenging.Changed -= OnBackpackChanged;
                _scavenging = scavenging;
                if (_scavenging != null && isActiveAndEnabled)
                    _scavenging.Changed += OnBackpackChanged;
                _lastBackpackUsed = -1;
                _lastBackpackCapacity = -1;
            }

            var runState = RogueliteRunState.Instance ?? FindFirstObjectByType<RogueliteRunState>();
            if (_runState != runState)
            {
                if (_runState != null)
                    _runState.Changed -= OnRunStateChanged;
                _runState = runState;
                if (_runState != null && isActiveAndEnabled)
                    _runState.Changed += OnRunStateChanged;
                _lastIngots = int.MinValue;
            }
        }

        private void RefreshBackpack(bool force)
        {
            if (_backpackValue == null)
                return;
            var used = _scavenging != null ? _scavenging.UsedBackpackCells : 0;
            var capacity = _scavenging != null ? _scavenging.BackpackCellCapacity : 0;
            if (!force && used == _lastBackpackUsed && capacity == _lastBackpackCapacity)
                return;

            _lastBackpackUsed = used;
            _lastBackpackCapacity = capacity;
            _backpackValue.text = $"B  背包  {used}/{capacity}";
            var ratio = capacity > 0 ? used / (float)capacity : 0f;
            _backpackValue.color = ratio >= 0.999f ? Danger : ratio >= 0.80f ? Warning : White;
        }

        private void RefreshIngots(bool force)
        {
            if (_ingotValue == null)
                return;
            var ingots = _runState != null ? _runState.Ingots : 0;
            if (!force && ingots == _lastIngots)
                return;
            _lastIngots = ingots;
            _ingotValue.text = $"源石锭  {ingots}";
        }

        private void RefreshStatuses(bool force)
        {
            if (_statusValue == null)
                return;

            var builder = new StringBuilder(96);
            if (_status != null)
            {
                var shown = 0;
                foreach (var instance in _status.ActiveStatuses)
                {
                    if (instance?.Definition == null)
                        continue;
                    if (shown >= 4)
                    {
                        builder.Append("  …");
                        break;
                    }

                    if (builder.Length > 0)
                        builder.Append("  ");
                    builder.Append(StatusDisplayName(instance.Id));
                    if (instance.Stacks > 1)
                        builder.Append('×').Append(instance.Stacks);
                    var remaining = Mathf.Max(0f, instance.ExpiresAt - Time.time);
                    if (remaining > 0.05f)
                        builder.Append(' ').Append(remaining.ToString("0.0")).Append('s');
                    shown++;
                }
            }

            var value = builder.ToString();
            if (!force && string.Equals(value, _lastStatusText, System.StringComparison.Ordinal))
                return;

            _lastStatusText = value;
            _statusValue.text = value;
            if (_statusValue.transform.parent != null)
                _statusValue.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        private static string StatusDisplayName(string id)
        {
            return id switch
            {
                CombatStatusIds.Cold => "寒冷",
                CombatStatusIds.Freeze => "冻结",
                CombatStatusIds.Burn => "灼烧",
                CombatStatusIds.Disarm => "缴械",
                CombatStatusIds.Silence => "沉默",
                CombatStatusIds.Stun => "晕眩",
                CombatStatusIds.Root => "束缚",
                CombatStatusIds.Slow => "减速",
                CombatStatusIds.AttackSlow => "攻速↓",
                CombatStatusIds.DefenseDown => "防御↓",
                CombatStatusIds.ResistanceDown => "法抗↓",
                CombatStatusIds.Fragile => "脆弱",
                CombatStatusIds.Weaken => "虚弱",
                CombatStatusIds.Shock => "震荡",
                CombatStatusIds.Ink => "墨染",
                _ => id
            };
        }

        private void UpdateSkillPointFeedback()
        {
            if (_spFeedbackGroup == null || _spFeedbackGroup.alpha <= 0f)
                return;
            var remaining = _spFeedbackUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                _spFeedbackGroup.alpha = 0f;
                return;
            }
            if (remaining < 0.25f)
                _spFeedbackGroup.alpha = Mathf.Clamp01(remaining / 0.25f);
        }

        private void RefreshHealth(bool force)
        {
            if (_health == null || _hpFill == null || _hpValue == null)
                return;

            var hp = Mathf.Max(0, Mathf.RoundToInt(_health.CurrentHealth));
            var maxHp = Mathf.Max(1, Mathf.RoundToInt(_health.MaxHealth));
            if (!force && hp == _lastHp && maxHp == _lastMaxHp)
                return;

            _lastHp = hp;
            _lastMaxHp = maxHp;
            _hpValue.text = $"{hp} / {maxHp}";
            SetHorizontalFill(_hpFill.rectTransform, 330f, Mathf.Clamp01(hp / (float)maxHp));
        }

        private void RefreshSkills(bool force)
        {
            var skill1 = _skills?.Skill1;
            var skill2 = _skills?.Skill2;
            var ready1 = IsReady(skill1);
            var ready2 = IsReady(skill2);
            var readyCount = (ready1 ? 1 : 0) + (ready2 ? 1 : 0);

            var readyPaletteChanged = readyCount != _lastReadyCount;
            RefreshSkill(_slot1, skill1, "L", force || readyPaletteChanged);
            RefreshSkill(_slot2, skill2, "I / RMB", force || readyPaletteChanged);

            if (force || readyPaletteChanged)
            {
                _lastReadyCount = readyCount;
                _readyIndicator?.SetReadyCount(readyCount);
            }
        }

        private static bool IsReady(IPlayerSkill skill) =>
            PlayerSkillLifecycleUtility.IsReady(skill);

        private void RefreshSkill(SkillSlotView view, IPlayerSkill skill, string key, bool force)
        {
            if (view == null)
                return;

            if (skill == null)
            {
                view.Name.text = "未配置技能";
                view.Value.text = "-- / --";
                SetHorizontalFill(view.Fill.rectTransform, view.FillWidth, 0f);
                return;
            }

            var active = PlayerSkillLifecycleUtility.IsActive(skill);
            var activeChanged = view.LastActive != active;
            var activeValue = active ? PlayerSkillLifecycleUtility.GetActiveHudValue(skill) : string.Empty;
            var activeValueChanged = !string.Equals(view.LastActiveValue, activeValue, System.StringComparison.Ordinal);
            var ready = IsReady(skill);
            var cost = Mathf.Max(1, Mathf.RoundToInt(skill.SkillPointCost));
            var current = ready
                ? cost
                : Mathf.Clamp(Mathf.FloorToInt(skill.SkillPoints + 0.0001f), 0, Mathf.Max(0, cost - 1));
            var casting = skill.IsCasting;

            if (force || view.LastCurrent != current || view.LastCost != cost || activeChanged || activeValueChanged)
            {
                view.LastCurrent = current;
                view.LastCost = cost;
                view.LastActiveValue = activeValue;
                view.Value.text = active ? activeValue : $"{current} / {cost}";
                var ratio = active ? 1f : Mathf.Clamp01(current / (float)cost);
                SetHorizontalFill(view.Fill.rectTransform, view.FillWidth, ratio);
            }

            if (force || view.LastReady != ready || view.LastCasting != casting || activeChanged)
            {
                view.LastReady = ready;
                view.LastCasting = casting;
                var lifecycleLabel = active ? PlayerSkillLifecycleUtility.GetLifecycleLabel(skill) : string.Empty;
                view.Name.text = active && !string.IsNullOrEmpty(lifecycleLabel)
                    ? $"{skill.DisplayName}  {lifecycleLabel}"
                    : skill.DisplayName;
                view.KeyText.text = key;
                view.Fill.color = active || ready ? SpReadyYellow : SpGreen;
            }

            view.LastActive = active;
        }

        private void Build()
        {
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 20);
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("GameplayHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 360;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _root = CreateRect("GameplayHUD", canvasGo.transform, 18f, 18f, 438f, 218f);
            var flow = RogueliteGameFlowController.Instance;
            _root.SetActive(flow == null || flow.IsRunning);
            var rootImage = _root.AddComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = false;

            var identity = GetComponent<PlayableOperatorIdentity>();
            var avatarKey = identity != null && !string.IsNullOrWhiteSpace(identity.AvatarResourceKey)
                ? identity.AvatarResourceKey
                : "UI/HUD/chen_avatar";
            var skill1Key = identity != null && !string.IsNullOrWhiteSpace(identity.Skill1IconResourceKey)
                ? identity.Skill1IconResourceKey
                : "UI/Skills/chen_badao";
            var skill2Key = identity != null && !string.IsNullOrWhiteSpace(identity.Skill2IconResourceKey)
                ? identity.Skill2IconResourceKey
                : "UI/Skills/chen_jueying";

            BuildHealth(_root.transform, Resources.Load<Sprite>(avatarKey));
            _slot1 = BuildSkill(_root.transform, 58f, 386f, "S1", Resources.Load<Sprite>(skill1Key));
            _slot2 = BuildSkill(_root.transform, 112f, 386f, "S2", Resources.Load<Sprite>(skill2Key));
            BuildStatusRow(_root.transform);
            BuildRunStatus(_root.transform);
            BuildSkillPointFeedback(_root.transform);
        }

        private void BuildHealth(Transform parent, Sprite avatarSprite)
        {
            var panel = CreatePanel(parent, "HP", 0f, 0f, 420f, 50f, Panel);
            var avatarBg = CreatePanel(panel.transform, "AvatarBG", 0f, 0f, 50f, 50f, new Color(0.025f, 0.035f, 0.043f, 0.82f));
            var avatarGo = CreateRect("Avatar", avatarBg.transform, 2f, 2f, 46f, 46f);
            var avatar = avatarGo.AddComponent<Image>();
            avatar.sprite = avatarSprite;
            avatar.color = avatar.sprite != null ? White : new Color(1f, 1f, 1f, 0f);
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;

            CreateText(panel.transform, "HP", 11, FontStyle.Bold, White, 58f, 3f, 34f, 18f);
            _hpValue = CreateText(panel.transform, "0 / 0", 12, FontStyle.Bold, White, 306f, 2f, 104f, 20f, TextAnchor.MiddleRight);

            var bar = CreatePanel(panel.transform, "HPBar", 58f, 29f, 330f, 8f, new Color(0.08f, 0.11f, 0.12f, 0.72f));
            var fillGo = CreateRect("Fill", bar.transform, 0f, 0f, 330f, 8f);
            _hpFill = fillGo.AddComponent<Image>();
            _hpFill.color = Cyan;
            _hpFill.raycastTarget = false;
            _hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        private SkillSlotView BuildSkill(Transform parent, float y, float rowWidth, string fallbackLabel, Sprite sprite)
        {
            var panel = CreatePanel(parent, "Skill", 0f, y, rowWidth, 48f, PanelAlt);
            var iconBg = CreatePanel(panel.transform, "IconBG", 3f, 3f, 42f, 42f, new Color(0.06f, 0.08f, 0.09f, 0.72f));

            var iconGo = CreateRect("Icon", iconBg.transform, 2f, 2f, 38f, 38f);
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = sprite;
            icon.color = sprite != null ? White : new Color(1f, 1f, 1f, 0f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            Text fallback = null;
            if (sprite == null)
                fallback = CreateText(iconBg.transform, fallbackLabel, 15, FontStyle.Bold, White, 0f, 0f, 42f, 42f, TextAnchor.MiddleCenter);

            var name = CreateText(panel.transform, "技能", 13, FontStyle.Bold, White, 53f, 2f, rowWidth - 160f, 20f);
            var keyText = CreateText(panel.transform, string.Empty, 9, FontStyle.Bold, Muted, rowWidth - 92f, 2f, 84f, 19f, TextAnchor.MiddleRight);
            CreateText(panel.transform, "SP", 8, FontStyle.Bold, Muted, 53f, 25f, 20f, 16f);

            var fillWidth = SkillBarWidth;
            var bar = CreatePanel(panel.transform, "SPBar", 77f, 31f, fillWidth, SkillBarHeight, new Color(0.07f, 0.095f, 0.105f, 0.68f));
            var fillGo = CreateRect("Fill", bar.transform, 0f, 0f, fillWidth, SkillBarHeight);
            var fill = fillGo.AddComponent<Image>();
            fill.color = SpGreen;
            fill.raycastTarget = false;
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);

            var value = CreateText(panel.transform, "0 / 0", 11, FontStyle.Bold, White, 279f, 21f, 78f, 22f, TextAnchor.MiddleRight);

            return new SkillSlotView
            {
                Icon = icon,
                FallbackIcon = fallback,
                Name = name,
                Value = value,
                Fill = fill,
                FillWidth = fillWidth,
                KeyText = keyText
            };
        }

        private void BuildStatusRow(Transform parent)
        {
            var panel = CreatePanel(parent, "CombatStatus", 0f, 164f, 420f, 20f, new Color(0.025f, 0.035f, 0.043f, 0.28f));
            _statusValue = CreateText(panel.transform, string.Empty, 9, FontStyle.Bold, Warning, 7f, 0f, 406f, 20f);
            panel.SetActive(false);
        }

        private void BuildRunStatus(Transform parent)
        {
            var panel = CreatePanel(parent, "RunStatus", 0f, 190f, 346f, 22f, new Color(0.025f, 0.035f, 0.043f, 0.34f));
            _backpackValue = CreateText(panel.transform, "B  背包  0/0", 10, FontStyle.Bold, White, 7f, 0f, 158f, 22f);
            _ingotValue = CreateText(panel.transform, "源石锭  0", 10, FontStyle.Bold, White, 174f, 0f, 164f, 22f, TextAnchor.MiddleRight);
        }

        private void BuildSkillPointFeedback(Transform parent)
        {
            var go = CreateRect("SkillPointFeedback", parent, 190f, 24f, 220f, 18f);
            _spFeedbackGroup = go.AddComponent<CanvasGroup>();
            _spFeedbackGroup.alpha = 0f;
            _spFeedbackGroup.blocksRaycasts = false;
            _spFeedbackGroup.interactable = false;
            _spFeedback = go.AddComponent<Text>();
            _spFeedback.font = _font;
            _spFeedback.fontSize = 10;
            _spFeedback.fontStyle = FontStyle.Bold;
            _spFeedback.color = Cyan;
            _spFeedback.alignment = TextAnchor.MiddleRight;
            _spFeedback.horizontalOverflow = HorizontalWrapMode.Overflow;
            _spFeedback.verticalOverflow = VerticalWrapMode.Truncate;
            _spFeedback.raycastTarget = false;
        }

        private static void SetHorizontalFill(RectTransform rt, float fullWidth, float ratio)
        {
            if (rt == null)
                return;
            rt.sizeDelta = new Vector2(Mathf.Max(0f, fullWidth * Mathf.Clamp01(ratio)), rt.sizeDelta.y);
        }

        private GameObject CreatePanel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = CreateRect(name, parent, x, y, w, h);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        private Text CreateText(
            Transform parent, string value, int size, FontStyle style, Color color,
            float x, float y, float w, float h, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = CreateRect("Text", parent, x, y, w, h);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }
    }
}
