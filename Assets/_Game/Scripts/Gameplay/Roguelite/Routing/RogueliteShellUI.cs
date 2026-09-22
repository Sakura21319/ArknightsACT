using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>
    /// Production-facing runtime shell built with UGUI.
    /// Home uses a dedicated static Chernobog-style background so the gameplay scene never leaks through.
    /// Warehouse / Trade intentionally uses an opaque gray-white presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteShellUI : MonoBehaviour
    {
        private static readonly Color Ink = new(0.065f, 0.075f, 0.085f, 1f);
        private static readonly Color InkSoft = new(0.11f, 0.125f, 0.135f, 1f);
        private static readonly Color Paper = new(0.93f, 0.94f, 0.945f, 1f);
        private static readonly Color PaperAlt = new(0.86f, 0.875f, 0.885f, 1f);
        private static readonly Color Line = new(0.48f, 0.52f, 0.55f, 0.55f);
        private static readonly Color White = new(0.98f, 0.985f, 0.99f, 1f);
        private static readonly Color Orange = new(1f, 0.42f, 0.075f, 1f);
        private static readonly Color Cyan = new(0.18f, 0.72f, 0.95f, 1f);
        private static readonly Color Muted = new(0.50f, 0.54f, 0.57f, 1f);
        private const int LowValueSellThreshold = 2000;

        private RogueliteGameFlowController _flow;
        private Canvas _canvas;
        private Font _font;
        private Sprite _lmdIcon;
        private Sprite _homeBackground;
        private bool _built;

        private GameObject _home;
        private GameObject _operationPreparation;
        private GameObject _extraction;
        private GameObject _settlement;
        private GameObject _warehouse;

        private Text _homeLmd;
        private Text _homeIngots;
        private Text _homeCommander;

        private Text _prepAreaTitle;
        private Text _prepAreaDescription;
        private Text _prepRiskSummary;
        private readonly Button[] _prepAreaButtons = new Button[4];
        private readonly Button[] _prepRiskButtons = new Button[5];

        private Text _extractLmd;
        private Text _extractIngots;
        private Text _extractRegion;
        private Text _extractRisk;
        private Text _extractStats;
        private Text _extractBagUsage;
        private Text _extractBagValue;
        private Transform _extractItems;

        private Text _settlementLmd;
        private Text _settlementIngots;
        private Text _settlementTitle;
        private Text _settlementSubtitle;
        private Text _settlementSummary;
        private Transform _settlementItems;

        private Text _warehouseLmd;
        private Text _warehouseIngots;
        private Text _warehouseCount;
        private Text _warehouseTotalValue;
        private InputField _warehouseSearch;
        private Button _warehouseBulkSellButton;
        private Button _warehouseLowValueButton;
        private Text _warehouseDetailName;
        private Text _warehouseDetailMeta;
        private Text _warehouseDetailDescription;
        private Text _warehouseDetailOwned;
        private Text _warehouseDetailPrice;
        private Image _warehouseDetailIcon;
        private RawImage _warehouseDetailRawIcon;
        private Button _warehouseTransactionButton;
        private Text _warehouseTransactionLabel;
        private RectTransform _warehouseGridContent;
        private readonly List<GameObject> _warehouseDynamicCards = new();
        private readonly Button[] _warehouseCategoryButtons = new Button[2];
        private Button _warehouseBuyTab;
        private Button _warehouseSellTab;

        private int _warehouseCategory;
        private bool _warehouseSellMode = true;
        private CollectibleDefinition _selectedItem;

        public bool IsReady => _built && _canvas != null;

        public void Configure(RogueliteGameFlowController flow)
        {
            if (_flow == flow && _built)
            {
                RefreshState();
                return;
            }

            Unsubscribe();
            _flow = flow;
            if (_flow != null)
            {
                _flow.StateChanged += OnStateChanged;
                if (_flow.MetaState != null)
                    _flow.MetaState.Changed += RefreshState;
            }

            BuildIfNeeded();
            RefreshState();
        }

        private void Start()
        {
            if (_flow == null)
                Configure(RogueliteGameFlowController.Instance ?? GetComponent<RogueliteGameFlowController>());
            else
                BuildIfNeeded();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_font != null)
                Destroy(_font);
        }

        private void Unsubscribe()
        {
            if (_flow == null) return;
            _flow.StateChanged -= OnStateChanged;
            if (_flow.MetaState != null)
                _flow.MetaState.Changed -= RefreshState;
        }

        private void OnStateChanged(RogueliteShellState state)
        {
            if (state == RogueliteShellState.WarehouseTrade)
            {
                _warehouseSellMode = true;
                _selectedItem = null;
                _warehouseSearch?.SetTextWithoutNotify(string.Empty);
            }
            RefreshState();
        }

        private void BuildIfNeeded()
        {
            if (_built || _flow == null)
                return;

            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 22);
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _lmdIcon = Resources.Load<Sprite>("UI/Currency/lmd");
            _homeBackground = Resources.Load<Sprite>("UI/Shell/home_chernobog");
            EnsureEventSystem();

            var canvasGo = new GameObject("RogueliteShellCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 800;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Always keep the full 1920x1080 reference frame visible. Expand adds safe canvas
            // space on non-16:9 displays instead of clipping fixed-position shell controls.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _home = BuildHome(canvasGo.transform);
            _operationPreparation = BuildOperationPreparation(canvasGo.transform);
            _extraction = BuildExtraction(canvasGo.transform);
            _settlement = BuildSettlement(canvasGo.transform);
            _warehouse = BuildWarehouse(canvasGo.transform);

            _built = true;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var go = new GameObject("RogueliteShellEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetAsFirstSibling();
        }

        private GameObject BuildHome(Transform parent)
        {
            // Opaque fallback is intentional: even if the background asset failed to import, the live
            // gameplay camera must never become the main-menu background.
            var root = CreateRoot("Home", parent, new Color(0.13f, 0.145f, 0.155f, 1f));
            if (_homeBackground != null)
            {
                var background = CreateImage(root.transform, "HomeBackground", 0f, 0f, 1920f, 1080f, Color.white);
                background.sprite = _homeBackground;
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
                Stretch(background.rectTransform);
            }

            var shade = CreateImage(root.transform, "HomeShade", 0f, 0f, 1920f, 1080f, new Color(0.01f, 0.02f, 0.025f, 0.28f));
            Stretch(shade.rectTransform);

            AddTopBar(root.transform, "ARKNIGHTS ACT", "SCAVENGING", dark: true, showIngots: false, out _homeLmd, out _homeIngots);

            // Left side intentionally contains only commander level.
            var commander = CreatePanel(root.transform, "Commander", 42, 824, 340, 190, new Color(0.035f, 0.045f, 0.055f, 0.94f), Cyan);
            CreateText(commander.transform, "指挥官等级", 28, FontStyle.Bold, White, 26, 24, 260, 36);
            _homeCommander = CreateText(commander.transform, "Lv.1", 58, FontStyle.Bold, White, 26, 67, 220, 66);
            CreateText(commander.transform, "COMMANDER", 14, FontStyle.Normal, Muted, 26, 140, 220, 24);
            AddAccentBar(commander.transform, 0, 0, 7, 190, Cyan);

            CreateText(root.transform, "TACTICAL RECOVERY", 17, FontStyle.Bold, new Color(0.78f, 0.82f, 0.84f), 855, 140, 360, 28);
            CreateText(root.transform, "行动终端", 50, FontStyle.Bold, White, 855, 171, 420, 62);

            var start = CreateActionButton(root.transform, "开始行动", "OPERATION", 855, 255, 990, 230, Ink, Orange, 44);
            start.onClick.AddListener(_flow.OpenOperationPreparation);
            AddAccentBar(start.transform, 0, 0, 9, 230, Orange);
            CreateText(start.transform, "进入当前行动区域", 18, FontStyle.Normal, new Color(0.72f, 0.75f, 0.77f), 36, 148, 360, 28);
            CreateText(start.transform, "››", 54, FontStyle.Bold, Orange, 865, 82, 90, 70, TextAnchor.MiddleCenter);

            var warehouse = CreateActionButton(root.transform, "仓库 / 交易", "WAREHOUSE & TRADE", 855, 510, 990, 165, White, Cyan, 36, darkText: true);
            warehouse.onClick.AddListener(_flow.OpenWarehouseTrade);
            AddAccentBar(warehouse.transform, 0, 0, 8, 165, Cyan);
            CreateText(warehouse.transform, "物资管理与买卖", 16, FontStyle.Normal, new Color(0.32f, 0.35f, 0.37f), 760, 72, 190, 28, TextAnchor.MiddleRight);

            var squad = CreateActionButton(root.transform, "编队", "SQUAD", 855, 700, 475, 142, new Color(0.91f, 0.92f, 0.925f), Muted, 30, darkText: true);
            squad.interactable = false;
            CreateText(squad.transform, "预留", 15, FontStyle.Normal, Muted, 365, 55, 72, 28, TextAnchor.MiddleRight);

            var mission = CreateActionButton(root.transform, "任务", "MISSION", 1370, 700, 475, 142, new Color(0.91f, 0.92f, 0.925f), Muted, 30, darkText: true);
            mission.interactable = false;
            CreateText(mission.transform, "预留", 15, FontStyle.Normal, Muted, 365, 55, 72, 28, TextAnchor.MiddleRight);

            // No operator/module/store/intelligence strip. Deliberately left empty.
            return root;
        }

        private GameObject BuildOperationPreparation(Transform parent)
        {
            var root = CreateRoot("OperationPreparation", parent, new Color(0.035f, 0.045f, 0.052f, 1f));
            if (_homeBackground != null)
            {
                var background = CreateImage(root.transform, "Background", 0f, 0f, 1920f, 1080f, new Color(0.62f, 0.68f, 0.72f, 1f));
                background.sprite = _homeBackground;
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
                Stretch(background.rectTransform);
            }

            var shade = CreateImage(root.transform, "PrepShade", 0f, 0f, 1920f, 1080f, new Color(0.015f, 0.025f, 0.032f, 0.68f));
            Stretch(shade.rectTransform);

            var header = CreatePanel(root.transform, "Header", 0, 0, 1920, 92,
                new Color(0.025f, 0.035f, 0.043f, 0.96f), new Color(0.28f, 0.34f, 0.37f, 0.7f));
            CreateText(header.transform, "切城 / CHERNOB0G", 15, FontStyle.Bold, Muted, 42, 18, 330, 24);
            CreateText(header.transform, "正式行动准备", 38, FontStyle.Bold, White, 710, 11, 500, 48, TextAnchor.MiddleCenter);
            CreateText(header.transform, "OPERATION PREPARATION", 12, FontStyle.Bold, Muted, 710, 57, 500, 22, TextAnchor.MiddleCenter);
            CreateText(header.transform, "选择行动区域与风险等级", 14, FontStyle.Normal, new Color(0.72f, 0.76f, 0.78f), 1510, 29, 350, 28, TextAnchor.MiddleRight);

            var left = CreatePanel(root.transform, "Areas", 28, 112, 500, 810,
                new Color(0.035f, 0.05f, 0.06f, 0.96f), new Color(0.24f, 0.31f, 0.34f, 0.9f));
            CreateText(left.transform, "01", 34, FontStyle.Bold, Muted, 18, 14, 62, 44);
            CreateText(left.transform, "区域选择", 28, FontStyle.Bold, White, 82, 13, 220, 42);
            CreateText(left.transform, "AREA SELECTION", 11, FontStyle.Bold, Muted, 84, 49, 220, 20);
            CreateText(left.transform, "仅开放切城行动区域", 13, FontStyle.Normal, Muted, 286, 20, 188, 28, TextAnchor.MiddleRight);

            var areaNames = new[] { "切城外围", "切城核心区", "切城工业区", "切城南部废墟" };
            var areaDescriptions = new[]
            {
                "城市防线前沿 · 残破城墙与检查站",
                "行政中枢与地标建筑群 · 高强度交战区",
                "重工业设施与物流枢纽 · 地形复杂",
                "居民区残骸密集 · 巷道与废墟交错"
            };
            for (var i = 0; i < areaNames.Length; i++)
            {
                var y = 82f + i * 170f;
                var card = CreatePanel(left.transform, "Area_" + i, 16, y, 468, 150,
                    new Color(0.065f, 0.085f, 0.095f, 0.98f), new Color(0.25f, 0.32f, 0.35f, 0.85f));
                var cardImage = card.GetComponent<Image>();
                var button = card.AddComponent<Button>();
                button.targetGraphic = cardImage;
                button.colors = ButtonColors(cardImage.color, new Color(0.10f, 0.18f, 0.21f, 1f), new Color(0.12f, 0.25f, 0.28f, 1f));
                _prepAreaButtons[i] = button;

                if (_homeBackground != null && _homeBackground.texture != null)
                {
                    var previewGo = CreateRect("Preview", card.transform, 0, 0, 468, 150);
                    var preview = previewGo.AddComponent<RawImage>();
                    preview.texture = _homeBackground.texture;
                    preview.uvRect = new Rect(0.05f + i * 0.08f, 0.18f + i * 0.07f, 0.72f, 0.58f);
                    preview.color = new Color(0.62f, 0.68f, 0.70f, 0.52f);
                    preview.raycastTarget = false;
                }

                var cover = CreateImage(card.transform, "Cover", 0, 0, 468, 150, new Color(0.015f, 0.025f, 0.03f, 0.48f));
                cover.raycastTarget = false;
                AddAccentBar(card.transform, 0, 0, 5, 150, Cyan);
                CreateText(card.transform, areaNames[i], 25, FontStyle.Bold, White, 22, 72, 270, 36);
                CreateText(card.transform, areaDescriptions[i], 13, FontStyle.Normal, new Color(0.78f, 0.82f, 0.84f), 22, 110, 402, 24);
                CreateText(card.transform, $"0{i + 1}", 16, FontStyle.Bold, new Color(0.82f, 0.86f, 0.88f), 398, 18, 42, 24, TextAnchor.MiddleRight);

                var captured = i;
                button.onClick.AddListener(() => _flow.SetOperationArea(captured));
            }

            var center = CreatePanel(root.transform, "Map", 548, 112, 820, 810,
                new Color(0.025f, 0.04f, 0.048f, 0.97f), new Color(0.25f, 0.32f, 0.35f, 0.9f));
            CreateText(center.transform, "// 区域情报", 16, FontStyle.Bold, Muted, 20, 12, 220, 26);
            CreateText(center.transform, "AREA INFORMATION", 10, FontStyle.Bold, new Color(0.39f, 0.47f, 0.50f), 147, 17, 220, 18);
            CreateText(center.transform, "切城", 46, FontStyle.Bold, White, 24, 48, 220, 60);
            CreateText(center.transform, "CHERNOB0G", 15, FontStyle.Bold, Orange, 27, 102, 190, 22);
            _prepAreaTitle = CreateText(center.transform, "切城外围", 24, FontStyle.Bold, White, 552, 58, 230, 34, TextAnchor.MiddleRight);
            _prepAreaDescription = CreateText(center.transform, "城市防线前沿", 13, FontStyle.Normal, new Color(0.72f, 0.77f, 0.79f), 522, 96, 260, 44, TextAnchor.UpperRight);

            var mapFrame = CreatePanel(center.transform, "TacticalMap", 20, 150, 780, 570,
                new Color(0.045f, 0.065f, 0.075f, 1f), new Color(0.32f, 0.40f, 0.43f, 0.9f));
            if (_homeBackground != null && _homeBackground.texture != null)
            {
                var mapImageGo = CreateRect("MapImage", mapFrame.transform, 0, 0, 780, 570);
                var mapImage = mapImageGo.AddComponent<RawImage>();
                mapImage.texture = _homeBackground.texture;
                mapImage.uvRect = new Rect(0.12f, 0.12f, 0.76f, 0.76f);
                mapImage.color = new Color(0.60f, 0.66f, 0.69f, 0.56f);
                mapImage.raycastTarget = false;
            }
            var mapShade = CreateImage(mapFrame.transform, "MapShade", 0, 0, 780, 570, new Color(0.02f, 0.035f, 0.043f, 0.42f));
            mapShade.raycastTarget = false;

            // Simple live tactical overlay: all sectors belong to Chernobog.
            AddAccentBar(mapFrame.transform, 126, 92, 520, 2, new Color(0.74f, 0.82f, 0.84f, 0.75f));
            AddAccentBar(mapFrame.transform, 126, 92, 2, 340, new Color(0.74f, 0.82f, 0.84f, 0.75f));
            AddAccentBar(mapFrame.transform, 646, 92, 2, 340, new Color(0.74f, 0.82f, 0.84f, 0.75f));
            AddAccentBar(mapFrame.transform, 126, 430, 520, 2, new Color(0.74f, 0.82f, 0.84f, 0.75f));
            AddAccentBar(mapFrame.transform, 292, 92, 2, 338, new Color(0.42f, 0.63f, 0.68f, 0.58f));
            AddAccentBar(mapFrame.transform, 474, 92, 2, 338, new Color(0.42f, 0.63f, 0.68f, 0.58f));
            AddAccentBar(mapFrame.transform, 126, 248, 520, 2, new Color(0.42f, 0.63f, 0.68f, 0.58f));
            CreateText(mapFrame.transform, "北部防线", 17, FontStyle.Bold, White, 310, 112, 160, 28, TextAnchor.MiddleCenter);
            CreateText(mapFrame.transform, "西部城区", 16, FontStyle.Bold, White, 150, 275, 140, 28, TextAnchor.MiddleCenter);
            CreateText(mapFrame.transform, "核心区", 21, FontStyle.Bold, Orange, 318, 275, 150, 36, TextAnchor.MiddleCenter);
            CreateText(mapFrame.transform, "东部工业区", 16, FontStyle.Bold, White, 493, 275, 150, 28, TextAnchor.MiddleCenter);
            CreateText(mapFrame.transform, "南部废墟", 17, FontStyle.Bold, White, 312, 382, 160, 30, TextAnchor.MiddleCenter);
            CreateText(mapFrame.transform, "◇", 42, FontStyle.Bold, Cyan, 365, 245, 54, 54, TextAnchor.MiddleCenter);
            CreateText(mapFrame.transform, "N", 14, FontStyle.Bold, White, 706, 22, 26, 26, TextAnchor.MiddleCenter);
            AddAccentBar(mapFrame.transform, 718, 48, 2, 54, White);

            CreateText(center.transform, "区域特征", 15, FontStyle.Bold, White, 24, 738, 120, 24);
            CreateText(center.transform, "多层城市结构 · 建筑密集 · 巷战环境复杂 · 低能见度", 13, FontStyle.Normal,
                new Color(0.69f, 0.74f, 0.76f), 150, 738, 630, 24, TextAnchor.MiddleRight);

            var right = CreatePanel(root.transform, "Risk", 1388, 112, 504, 810,
                new Color(0.035f, 0.05f, 0.06f, 0.96f), new Color(0.24f, 0.31f, 0.34f, 0.9f));
            CreateText(right.transform, "02", 34, FontStyle.Bold, Muted, 18, 14, 62, 44);
            CreateText(right.transform, "风险等级", 28, FontStyle.Bold, White, 82, 13, 220, 42);
            CreateText(right.transform, "RISK LEVEL", 11, FontStyle.Bold, Muted, 84, 49, 180, 20);

            var riskNames = new[] { "低风险", "标准", "高风险", "危险", "极限" };
            var pressure = new[] { "低", "中", "高", "很高", "极高" };
            var multipliers = new[] { "×0.8", "×1.0", "×1.4", "×1.8", "×2.5" };
            for (var i = 0; i < 5; i++)
            {
                var y = 82f + i * 120f;
                var card = CreatePanel(right.transform, "Risk_" + (i + 1), 16, y, 472, 104,
                    new Color(0.065f, 0.082f, 0.092f, 0.99f), new Color(0.25f, 0.32f, 0.35f, 0.85f));
                var image = card.GetComponent<Image>();
                var button = card.AddComponent<Button>();
                button.targetGraphic = image;
                button.colors = ButtonColors(image.color, new Color(0.11f, 0.14f, 0.15f, 1f), new Color(0.15f, 0.17f, 0.18f, 1f));
                _prepRiskButtons[i] = button;

                CreateText(card.transform, ToRoman(i + 1), 34, FontStyle.Normal, White, 18, 16, 58, 60, TextAnchor.MiddleCenter);
                CreateText(card.transform, riskNames[i], 20, FontStyle.Bold, White, 88, 14, 160, 30);
                CreateText(card.transform, $"敌人压力  {pressure[i]}", 13, FontStyle.Normal, Muted, 88, 49, 150, 24);
                CreateText(card.transform, multipliers[i], 16, FontStyle.Bold, i >= 2 ? Orange : White, 374, 36, 72, 28, TextAnchor.MiddleRight);
                CreateText(card.transform, "战利品", 11, FontStyle.Normal, Muted, 300, 39, 66, 22, TextAnchor.MiddleRight);
                var captured = i + 1;
                button.onClick.AddListener(() => _flow.SetRiskLevel(captured));
            }

            _prepRiskSummary = CreateText(right.transform, "当前：标准", 15, FontStyle.Bold, White, 22, 694, 440, 30);
            CreateText(right.transform, "风险等级将在后续接入敌人强度与战利品倍率。", 12, FontStyle.Normal, Muted, 22, 726, 440, 40);

            var back = CreateFlatButton(root.transform, "‹  返回主页", 1250, 952, 260, 82, new Color(0.12f, 0.15f, 0.17f, 0.98f), White);
            back.onClick.AddListener(_flow.ReturnHome);
            var deploy = CreateFlatButton(root.transform, "开始行动  ›", 1530, 952, 362, 82, Orange, Ink);
            deploy.onClick.AddListener(_flow.BeginOperation);
            CreateText(root.transform, "DEPLOY", 11, FontStyle.Bold, new Color(0.32f, 0.15f, 0.04f), 1660, 1018, 110, 18, TextAnchor.MiddleCenter);

            return root;
        }

        private static string ToRoman(int value) => value switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => value.ToString()
        };

        private GameObject BuildExtraction(Transform parent)
        {
            var root = CreateRoot("Extraction", parent, new Color(0.015f, 0.02f, 0.025f, 0.78f));
            AddTopBar(root.transform, "撤离决策", "EXTRACTION", dark: true, showIngots: true, out _extractLmd, out _extractIngots);

            var status = CreatePanel(root.transform, "Status", 42, 128, 430, 760, new Color(0.045f, 0.055f, 0.065f, 0.96f), Orange);
            AddAccentBar(status.transform, 0, 0, 8, 760, Orange);
            _extractRegion = CreateText(status.transform, "A-1", 44, FontStyle.Bold, White, 32, 42, 340, 60);
            CreateText(status.transform, "风险评估", 18, FontStyle.Bold, Muted, 32, 142, 200, 28);
            _extractRisk = CreateText(status.transform, "34%", 64, FontStyle.Bold, Orange, 32, 177, 250, 82);
            _extractStats = CreateText(status.transform, string.Empty, 20, FontStyle.Normal, White, 32, 310, 340, 210);

            var bag = CreatePanel(root.transform, "Bag", 505, 128, 1370, 575, new Color(0.92f, 0.93f, 0.935f, 0.98f), Cyan);
            CreateText(bag.transform, "未撤离背包", 30, FontStyle.Bold, Ink, 32, 24, 330, 44);
            _extractBagUsage = CreateText(bag.transform, "0 / 20 格", 19, FontStyle.Bold, InkSoft, 1020, 28, 300, 32, TextAnchor.MiddleRight);
            _extractItems = CreateRect("Items", bag.transform, 32, 94, 1306, 330).transform;
            _extractBagValue = CreateText(bag.transform, "预计总价值  0", 28, FontStyle.Bold, Ink, 32, 474, 620, 42);

            var continueBtn = CreateActionButton(root.transform, "继续探索", "CONTINUE", 505, 742, 645, 130, White, Cyan, 30, darkText: true);
            continueBtn.onClick.AddListener(_flow.CancelExtraction);

            var extractBtn = CreateActionButton(root.transform, "立即撤离", "EXTRACT", 1190, 742, 685, 130, Orange, White, 32, darkText: true);
            extractBtn.onClick.AddListener(_flow.ConfirmExtraction);
            CreateText(extractBtn.transform, "››", 46, FontStyle.Bold, Ink, 575, 37, 70, 60, TextAnchor.MiddleCenter);

            CreateText(root.transform, "撤离后普通物资进入系统仓库；藏品与源石锭均仅限本局。", 16, FontStyle.Normal, new Color(0.74f, 0.77f, 0.79f), 505, 900, 1040, 30);
            return root;
        }

        private GameObject BuildSettlement(Transform parent)
        {
            var root = CreateRoot("Settlement", parent, new Color(0.01f, 0.015f, 0.02f, 0.82f));
            AddTopBar(root.transform, "行动结算", "OPERATION RESULT", dark: true, showIngots: false, out _settlementLmd, out _settlementIngots);

            AddAccentBar(root.transform, 42, 142, 9, 165, Orange);
            _settlementTitle = CreateText(root.transform, "成功撤离", 66, FontStyle.Bold, White, 72, 142, 620, 82);
            _settlementSubtitle = CreateText(root.transform, "普通物资已安全回收 · 藏品随本局结束", 20, FontStyle.Normal, new Color(0.72f, 0.75f, 0.77f), 74, 232, 720, 34);

            var summary = CreatePanel(root.transform, "Summary", 42, 340, 465, 555, new Color(0.045f, 0.055f, 0.065f, 0.96f), Orange);
            CreateText(summary.transform, "行动数据", 25, FontStyle.Bold, White, 28, 22, 300, 40);
            _settlementSummary = CreateText(summary.transform, string.Empty, 21, FontStyle.Normal, White, 28, 92, 385, 405);

            var itemsPanel = CreatePanel(root.transform, "Recovered", 545, 340, 1330, 555, new Color(0.92f, 0.93f, 0.935f, 0.98f), Orange);
            CreateText(itemsPanel.transform, "新获得的物资", 27, FontStyle.Bold, Ink, 30, 22, 420, 42);
            _settlementItems = CreateRect("Items", itemsPanel.transform, 30, 84, 1270, 380).transform;

            var homeBtn = CreateActionButton(root.transform, "返回主页", "HOME", 42, 930, 380, 100, White, Cyan, 26, darkText: true);
            homeBtn.onClick.AddListener(_flow.ReturnHome);

            var warehouseBtn = CreateActionButton(root.transform, "前往仓库 / 交易", "WAREHOUSE & TRADE", 1240, 930, 635, 100, Orange, White, 27, darkText: true);
            warehouseBtn.onClick.AddListener(_flow.OpenWarehouseTrade);

            return root;
        }

        private GameObject BuildWarehouse(Transform parent)
        {
            // Deliberately opaque: no scene background on warehouse/trade.
            var root = CreateRoot("Warehouse", parent, Paper);
            AddTopBar(root.transform, "仓库 / 交易", "WAREHOUSE & TRADE", dark: false, showIngots: false, out _warehouseLmd, out _warehouseIngots);

            var categories = CreatePanel(root.transform, "Categories", 28, 110, 250, 930, new Color(0.84f, 0.855f, 0.865f, 1f), Cyan);
            CreateText(categories.transform, "分类", 26, FontStyle.Bold, Ink, 24, 22, 180, 40);
            CreateCategoryButton(categories.transform, "全部", 0, 24, 90);
            CreateCategoryButton(categories.transform, $"低价值 ≤{LowValueSellThreshold:N0}", 1, 24, 156);
            var back = CreateFlatButton(categories.transform, "‹  返回主页", 24, 838, 202, 58, new Color(0.74f, 0.76f, 0.77f), Ink);
            back.onClick.AddListener(_flow.ReturnHome);

            var inventory = CreatePanel(root.transform, "Inventory", 298, 110, 975, 930, new Color(0.955f, 0.96f, 0.965f, 1f), Cyan);
            CreateText(inventory.transform, "系统仓库", 30, FontStyle.Bold, Ink, 28, 20, 250, 44);
            _warehouseSearch = CreateInputField(inventory.transform, "搜索名称 / ID", 305, 18, 370, 48);
            _warehouseSearch.onValueChanged.AddListener(_ => { _selectedItem = null; RefreshWarehouse(); });
            _warehouseCount = CreateText(inventory.transform, "0 件", 17, FontStyle.Bold, InkSoft, 700, 27, 220, 30, TextAnchor.MiddleRight);
            BuildWarehouseScroll(inventory.transform);

            _warehouseBulkSellButton = CreateFlatButton(inventory.transform, "批量出售当前结果", 26, 812, 270, 62, Ink, White);
            _warehouseBulkSellButton.onClick.AddListener(ExecuteWarehouseBulkSell);
            _warehouseLowValueButton = CreateFlatButton(inventory.transform, "一键出售低价值", 312, 812, 270, 62, new Color(0.78f, 0.80f, 0.81f), Ink);
            _warehouseLowValueButton.onClick.AddListener(ExecuteWarehouseLowValueSell);
            CreateText(inventory.transform, $"低价值 ≤ {LowValueSellThreshold:N0}", 12, FontStyle.Normal, Muted, 312, 875, 270, 22, TextAnchor.MiddleCenter);
            _warehouseTotalValue = CreateText(inventory.transform, "仓库总价值  0", 21, FontStyle.Bold, Ink, 610, 818, 310, 52, TextAnchor.MiddleRight);

            var detail = CreatePanel(root.transform, "Detail", 1292, 110, 600, 930, new Color(0.89f, 0.90f, 0.905f, 1f), Orange);
            _warehouseSellTab = CreateFlatButton(detail.transform, "我的物品", 24, 20, 260, 62, White, Ink);
            _warehouseSellTab.onClick.AddListener(() => { _warehouseSellMode = true; _selectedItem = null; RefreshWarehouse(); });
            _warehouseBuyTab = CreateFlatButton(detail.transform, "商店", 300, 20, 260, 62, Ink, White);
            _warehouseBuyTab.onClick.AddListener(() => { _warehouseSellMode = false; _selectedItem = null; RefreshWarehouse(); });

            var iconFrame = CreatePanel(detail.transform, "IconFrame", 110, 118, 380, 270, new Color(0.82f, 0.835f, 0.845f, 1f), Cyan);
            _warehouseDetailIcon = CreateImage(iconFrame.transform, "Icon", 65, 35, 250, 190, Color.white);
            _warehouseDetailIcon.preserveAspect = true;
            var rawIconGo = CreateRect("RawIcon", iconFrame.transform, 65, 35, 250, 190);
            _warehouseDetailRawIcon = rawIconGo.AddComponent<RawImage>();
            _warehouseDetailRawIcon.color = Color.white;
            _warehouseDetailRawIcon.raycastTarget = false;
            _warehouseDetailRawIcon.enabled = false;

            _warehouseDetailName = CreateText(detail.transform, "选择物品", 30, FontStyle.Bold, Ink, 34, 420, 520, 46);
            _warehouseDetailMeta = CreateText(detail.transform, string.Empty, 16, FontStyle.Bold, Muted, 34, 468, 520, 28);
            _warehouseDetailDescription = CreateText(detail.transform, string.Empty, 17, FontStyle.Normal, InkSoft, 34, 525, 520, 120);
            _warehouseDetailDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            _warehouseDetailDescription.verticalOverflow = VerticalWrapMode.Truncate;
            _warehouseDetailOwned = CreateText(detail.transform, string.Empty, 18, FontStyle.Bold, Ink, 34, 680, 520, 32);
            AddCurrencyIcon(detail.transform, 34, 734, 32, 32);
            _warehouseDetailPrice = CreateText(detail.transform, string.Empty, 26, FontStyle.Bold, Ink, 76, 728, 478, 42);

            _warehouseTransactionButton = CreateFlatButton(detail.transform, "选择物品", 34, 830, 520, 70, Orange, Ink);
            _warehouseTransactionLabel = _warehouseTransactionButton.GetComponentInChildren<Text>();
            _warehouseTransactionButton.onClick.AddListener(ExecuteWarehouseTransaction);

            return root;
        }

        private void BuildWarehouseScroll(Transform parent)
        {
            var scrollGo = CreateRect("Scroll", parent, 26, 88, 923, 700);
            var viewport = CreateRect("Viewport", scrollGo.transform, 0, 0, 923, 700);
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.90f, 0.91f, 0.915f, 1f);
            viewport.AddComponent<Mask>().showMaskGraphic = true;

            var content = CreateRect("Content", viewport.transform, 18, 18, 870, 664);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = new Vector2(0f, -18f);
            contentRt.sizeDelta = new Vector2(-36f, 664f);

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(196f, 190f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            _warehouseGridContent = contentRt;
        }

        private void CreateCategoryButton(Transform parent, string label, int category, float x, float y)
        {
            var btn = CreateFlatButton(parent, label, x, y, 202, 52,
                category == _warehouseCategory ? White : new Color(0.78f, 0.795f, 0.805f), Ink);
            _warehouseCategoryButtons[category] = btn;
            btn.onClick.AddListener(() => { _warehouseCategory = category; _selectedItem = null; RefreshWarehouse(); });
        }

        private void RefreshState()
        {
            if (!_built || _flow == null)
                return;

            _home.SetActive(_flow.State == RogueliteShellState.Home);
            _operationPreparation.SetActive(_flow.State == RogueliteShellState.OperationPreparation);
            _extraction.SetActive(_flow.State == RogueliteShellState.ExtractionDecision);
            _settlement.SetActive(_flow.State == RogueliteShellState.Settlement);
            _warehouse.SetActive(_flow.State == RogueliteShellState.WarehouseTrade);
            _canvas.gameObject.SetActive(_flow.State != RogueliteShellState.Running);

            var lmd = _flow.MetaState?.Lmd ?? 0;
            var ingots = _flow.RunState?.Ingots ?? 0;
            SetText(_homeLmd, $"{lmd:N0}");
            SetText(_homeIngots, $"{ingots:N0}");
            SetText(_extractLmd, $"{lmd:N0}");
            SetText(_extractIngots, $"{ingots:N0}");
            SetText(_settlementLmd, $"{lmd:N0}");
            SetText(_settlementIngots, $"{ingots:N0}");
            SetText(_warehouseLmd, $"{lmd:N0}");
            SetText(_warehouseIngots, $"{ingots:N0}");
            SetText(_homeCommander, $"Lv.{_flow.MetaState?.CommanderLevel ?? 1}");

            if (_flow.State == RogueliteShellState.OperationPreparation)
                RefreshOperationPreparation();
            else if (_flow.State == RogueliteShellState.ExtractionDecision)
                RefreshExtraction();
            else if (_flow.State == RogueliteShellState.Settlement)
                RefreshSettlement();
            else if (_flow.State == RogueliteShellState.WarehouseTrade)
                RefreshWarehouse();
        }

        private void RefreshOperationPreparation()
        {
            var area = Mathf.Clamp(_flow.SelectedOperationArea, 0, 3);
            var areaNames = new[] { "切城外围", "切城核心区", "切城工业区", "切城南部废墟" };
            var areaDescriptions = new[]
            {
                "城市防线前沿 · 残破城墙与检查站",
                "行政中枢与地标建筑群 · 高强度交战区",
                "重工业设施与物流枢纽 · 地形复杂",
                "居民区残骸密集 · 巷道与废墟交错"
            };
            SetText(_prepAreaTitle, areaNames[area]);
            SetText(_prepAreaDescription, areaDescriptions[area]);

            for (var i = 0; i < _prepAreaButtons.Length; i++)
            {
                var button = _prepAreaButtons[i];
                if (button == null || button.targetGraphic is not Image image) continue;
                image.color = i == area
                    ? new Color(0.075f, 0.18f, 0.21f, 0.99f)
                    : new Color(0.065f, 0.085f, 0.095f, 0.98f);
                var outline = button.GetComponent<Outline>();
                if (outline != null)
                    outline.effectColor = i == area ? Cyan : new Color(0.25f, 0.32f, 0.35f, 0.85f);
            }

            var risk = Mathf.Clamp(_flow.SelectedRiskLevel, 1, 5);
            var riskNames = new[] { "低风险", "标准", "高风险", "危险", "极限" };
            var multipliers = new[] { "×0.8", "×1.0", "×1.4", "×1.8", "×2.5" };
            for (var i = 0; i < _prepRiskButtons.Length; i++)
            {
                var button = _prepRiskButtons[i];
                if (button == null || button.targetGraphic is not Image image) continue;
                var selected = i + 1 == risk;
                image.color = selected
                    ? new Color(0.18f, 0.12f, 0.075f, 0.99f)
                    : new Color(0.065f, 0.082f, 0.092f, 0.99f);
                var outline = button.GetComponent<Outline>();
                if (outline != null)
                    outline.effectColor = selected ? Orange : new Color(0.25f, 0.32f, 0.35f, 0.85f);
            }

            SetText(_prepRiskSummary, $"当前：{riskNames[risk - 1]}  ·  战利品倍率 {multipliers[risk - 1]}");
        }

        private void RefreshExtraction()
        {
            var stage = _flow.RunState?.StageIndex ?? 1;
            var emergency = _flow.RunState?.EmergencyClears ?? 0;
            var risk = Mathf.Clamp(18 + (_flow.SelectedRiskLevel - 1) * 18 + (stage - 1) * 8 + emergency * 4, 0, 95);
            SetText(_extractRegion, $"{_flow.SelectedOperationAreaName} · Stage {stage}");
            SetText(_extractRisk, $"{risk}%");
            SetText(_extractStats,
                $"当前阶段        {stage}/3\n\n" +
                $"已探索区块      {_flow.RunState?.ExploredBlocks ?? 0}\n\n" +
                $"紧急作战        {emergency}");

            var bag = _flow.Scavenging;
            SetText(_extractBagUsage, bag != null ? $"{bag.UsedBackpackCells} / {bag.BackpackCellCapacity} 格" : "0 / 0 格");
            SetText(_extractBagValue, $"预计总价值  {(bag?.PendingCollectionValue ?? 0):N0}  龙门币");

            ClearChildren(_extractItems);
            if (bag == null) return;
            var shown = Mathf.Min(12, bag.PendingCount);
            for (var i = 0; i < shown; i++)
            {
                var item = bag.Pending[i];
                var x = (i % 6) * 207f;
                var y = (i / 6) * 160f;
                CreateCompactItemCard(_extractItems, item, 1, x, y, 190, 142, dark: false);
            }
        }

        private void RefreshSettlement()
        {
            SetText(_settlementTitle, _flow.SettlementSuccess ? "成功撤离" : "行动失败");
            SetText(_settlementSubtitle, _flow.SettlementSuccess ? "普通物资已安全回收 · 藏品随本局结束" : "未撤离物资已遗失");
            SetText(_settlementSummary,
                $"到达阶段        {_flow.SettlementStage}/3\n\n" +
                $"探索区块        {_flow.SettlementExplored}\n\n" +
                $"战斗记录        {_flow.SettlementCombatWins}\n\n" +
                $"行动时间        {FormatDuration(_flow.SettlementDuration)}\n\n" +
                $"回收件数        {_flow.SettlementItemCount}\n\n" +
                $"回收总价值      {_flow.SettlementValue:N0} 龙门币");

            ClearChildren(_settlementItems);
            var shown = Mathf.Min(10, _flow.SettlementEntryCount);
            for (var i = 0; i < shown; i++)
            {
                var item = _flow.GetSettlementItemDefinition(i);
                var count = _flow.GetSettlementItemStack(i);
                var x = (i % 5) * 245f;
                var y = (i / 5) * 190f;
                CreateCompactItemCard(_settlementItems, item, count, x, y, 224, 172, dark: false);
            }
        }

        private void RefreshWarehouse()
        {
            if (_flow == null || _warehouseGridContent == null)
                return;

            for (var i = 0; i < _warehouseDynamicCards.Count; i++)
                if (_warehouseDynamicCards[i] != null)
                    Destroy(_warehouseDynamicCards[i]);
            _warehouseDynamicCards.Clear();

            for (var i = 0; i < _warehouseCategoryButtons.Length; i++)
            {
                var button = _warehouseCategoryButtons[i];
                if (button != null && button.targetGraphic is Image image)
                    image.color = i == _warehouseCategory ? White : new Color(0.78f, 0.795f, 0.805f);
            }
            if (_warehouseBuyTab != null && _warehouseBuyTab.targetGraphic is Image buyImage)
                buyImage.color = !_warehouseSellMode ? Orange : new Color(0.76f, 0.775f, 0.785f);
            if (_warehouseSellTab != null && _warehouseSellTab.targetGraphic is Image sellImage)
                sellImage.color = _warehouseSellMode ? White : new Color(0.76f, 0.775f, 0.785f);

            var visible = BuildVisibleWarehouseItems();
            var catalog = _flow.MarketCatalog;
            var totalOwned = 0;
            var ownedKinds = 0;
            if (_flow.MetaState != null && catalog != null)
            {
                for (var i = 0; i < catalog.Count; i++)
                {
                    var item = catalog[i];
                    if (item == null || !item.IsSalvageCommodity) continue;
                    var count = _flow.MetaState.GetCount(item.Id);
                    if (count <= 0) continue;
                    totalOwned += count;
                    ownedKinds++;
                }
            }

            SetText(_warehouseCount, $"持有 {totalOwned} 件 · {ownedKinds} 种");
            SetText(_warehouseTotalValue,
                $"仓库总价值  {(_flow.MetaState?.GetWarehouseTotalValue(catalog) ?? 0):N0}");

            var canBulkSell = _warehouseSellMode && visible.Count > 0;
            if (_warehouseBulkSellButton != null)
                _warehouseBulkSellButton.interactable = canBulkSell;

            var hasLowValue = false;
            if (_flow.MetaState != null && catalog != null)
                for (var i = 0; i < catalog.Count && !hasLowValue; i++)
                {
                    var item = catalog[i];
                    hasLowValue = item != null && item.IsSalvageCommodity &&
                                  _flow.MetaState.GetCount(item.Id) > 0 &&
                                  _flow.MetaState.GetSellPrice(item) <= LowValueSellThreshold;
                }
            if (_warehouseLowValueButton != null)
                _warehouseLowValueButton.interactable = _warehouseSellMode && hasLowValue;

            for (var i = 0; i < visible.Count; i++)
            {
                var card = CreateWarehouseItemCard(_warehouseGridContent, visible[i]);
                _warehouseDynamicCards.Add(card);
            }

            RefreshWarehouseDetail();
        }

        private List<CollectibleDefinition> BuildVisibleWarehouseItems()
        {
            var visible = new List<CollectibleDefinition>();
            var catalog = _flow?.MarketCatalog;
            var query = _warehouseSearch != null ? _warehouseSearch.text?.Trim() : string.Empty;
            if (catalog == null)
                return visible;

            for (var i = 0; i < catalog.Count; i++)
            {
                var item = catalog[i];
                if (item == null || !item.IsSalvageCommodity) continue;
                if (_warehouseSellMode && (_flow.MetaState?.GetCount(item.Id) ?? 0) <= 0) continue;
                if (_warehouseCategory == 1 && (_flow.MetaState?.GetSellPrice(item) ?? item.CollectionValue) > LowValueSellThreshold) continue;

                if (!string.IsNullOrWhiteSpace(query))
                {
                    var nameMatch = !string.IsNullOrWhiteSpace(item.DisplayName) &&
                                    item.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    var idMatch = !string.IsNullOrWhiteSpace(item.Id) &&
                                  item.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    var descriptionMatch = !string.IsNullOrWhiteSpace(item.Description) &&
                                           item.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!nameMatch && !idMatch && !descriptionMatch)
                        continue;
                }

                visible.Add(item);
            }
            return visible;
        }

        private GameObject CreateWarehouseItemCard(Transform parent, CollectibleDefinition item)
        {
            var go = CreatePanel(parent, "Item_" + item.Id, 0, 0, 196, 190, White, SearchableContainer25D.RarityColor(item));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(196, 190);

            AddItemVisual(go.transform, item, 28, 14, 140, 112);
            CreateText(go.transform, item.DisplayName, 15, FontStyle.Bold, Ink, 10, 128, 176, 24, TextAnchor.MiddleCenter);
            var owned = _flow.MetaState?.GetCount(item.Id) ?? 0;
            CreateText(go.transform, $"持有 {owned}", 12, FontStyle.Normal, Muted, 10, 157, 72, 20);
            var price = _warehouseSellMode ? _flow.MetaState?.GetSellPrice(item) ?? 0 : _flow.MetaState?.GetBuyPrice(item) ?? 0;
            AddCurrencyIcon(go.transform, 92, 159, 16, 16);
            CreateText(go.transform, $"{price:N0}", 13, FontStyle.Bold, Ink, 111, 157, 71, 20, TextAnchor.MiddleRight);

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = go.GetComponent<Image>();
            button.colors = ButtonColors(White, new Color(0.86f, 0.92f, 0.95f), new Color(0.75f, 0.84f, 0.89f));
            button.onClick.AddListener(() => { _selectedItem = item; RefreshWarehouseDetail(); });
            return go;
        }

        private void RefreshWarehouseDetail()
        {
            if (_selectedItem == null)
            {
                SetText(_warehouseDetailName, "选择物品");
                SetText(_warehouseDetailMeta, string.Empty);
                SetText(_warehouseDetailDescription, string.Empty);
                SetText(_warehouseDetailOwned, string.Empty);
                SetText(_warehouseDetailPrice, string.Empty);
                SetText(_warehouseTransactionLabel, _warehouseSellMode ? "选择要出售的物品" : "选择要购买的物品");
                _warehouseTransactionButton.interactable = false;
                _warehouseDetailIcon.enabled = false;
                _warehouseDetailRawIcon.enabled = false;
                return;
            }

            var item = _selectedItem;
            var owned = _flow.MetaState?.GetCount(item.Id) ?? 0;
            var price = _warehouseSellMode ? _flow.MetaState?.GetSellPrice(item) ?? 0 : _flow.MetaState?.GetBuyPrice(item) ?? 0;

            SetText(_warehouseDetailName, item.DisplayName);
            SetText(_warehouseDetailMeta, $"{(item.IsSalvageCommodity ? "普通物资" : "藏品")}  ·  {item.SalvageRarity}");
            SetText(_warehouseDetailDescription, string.IsNullOrWhiteSpace(item.Description) ? "暂无说明" : item.Description);
            SetText(_warehouseDetailOwned, $"持有数量   {owned}");
            SetText(_warehouseDetailPrice, $"{(_warehouseSellMode ? "出售" : "购买")}单价   {price:N0}");
            SetText(_warehouseTransactionLabel, _warehouseSellMode ? "出售 1 件" : "购买 1 件");

            SetDetailIcon(item);
            _warehouseTransactionButton.interactable = _warehouseSellMode
                ? owned > 0
                : _flow.MetaState != null && _flow.MetaState.Lmd >= price;
        }

        private void ExecuteWarehouseTransaction()
        {
            if (_selectedItem == null || _flow.MetaState == null)
                return;

            if (_warehouseSellMode)
                _flow.MetaState.TrySell(_selectedItem, 1);
            else
                _flow.MetaState.TryBuy(_selectedItem, 1);

            if (_warehouseSellMode && _flow.MetaState.GetCount(_selectedItem.Id) <= 0)
                _selectedItem = null;
            RefreshWarehouse();
        }

        private void ExecuteWarehouseBulkSell()
        {
            if (!_warehouseSellMode || _flow?.MetaState == null)
                return;
            _flow.MetaState.SellAllOwned(BuildVisibleWarehouseItems());
            _selectedItem = null;
            RefreshWarehouse();
        }

        private void ExecuteWarehouseLowValueSell()
        {
            if (!_warehouseSellMode || _flow?.MetaState == null)
                return;
            _flow.MetaState.SellLowValue(_flow.MarketCatalog, LowValueSellThreshold);
            _selectedItem = null;
            RefreshWarehouse();
        }

        private void SetDetailIcon(CollectibleDefinition item)
        {
            _warehouseDetailIcon.enabled = false;
            _warehouseDetailRawIcon.enabled = false;
            if (item == null) return;

            if (item.SalvageIcon != null)
            {
                _warehouseDetailIcon.sprite = item.SalvageIcon;
                _warehouseDetailIcon.enabled = true;
            }
            else if (item.SalvageIconTexture != null)
            {
                _warehouseDetailRawIcon.texture = item.SalvageIconTexture;
                FitRawImage(_warehouseDetailRawIcon.rectTransform, item.SalvageIconTexture, 65f, 35f, 250f, 190f);
                _warehouseDetailRawIcon.enabled = true;
            }
        }

        private void AddTopBar(Transform parent, string title, string subtitle, bool dark, bool showIngots, out Text lmd, out Text ingots)
        {
            var color = dark ? new Color(0.035f, 0.045f, 0.052f, 0.97f) : new Color(0.965f, 0.97f, 0.975f, 1f);
            var text = dark ? White : Ink;
            var top = CreatePanel(parent, "TopBar", 0, 0, 1920, 88, color, dark ? new Color(0.18f, 0.20f, 0.21f) : Line);
            CreateText(top.transform, title, 28, FontStyle.Bold, text, 42, 16, 390, 36);
            CreateText(top.transform, subtitle, 12, FontStyle.Bold, dark ? Muted : new Color(0.40f, 0.43f, 0.45f), 44, 51, 330, 20);

            var lmdX = showIngots ? 1290f : 1588f;
            var chip1 = CreatePanel(top.transform, "LMD", lmdX, 14, 286, 60,
                dark ? new Color(0.08f, 0.095f, 0.105f) : new Color(0.86f, 0.875f, 0.885f), Line);
            AddCurrencyIcon(chip1.transform, 14, 9, 42, 42);
            CreateText(chip1.transform, "龙门币", 12, FontStyle.Normal, dark ? Muted : new Color(0.35f, 0.38f, 0.40f), 64, 6, 90, 20);
            lmd = CreateText(chip1.transform, "0", 21, FontStyle.Bold, dark ? White : Ink, 64, 25, 196, 29);

            ingots = null;
            if (!showIngots)
                return;

            var chip2 = CreatePanel(top.transform, "Ingots", 1588, 14, 286, 60,
                dark ? new Color(0.08f, 0.095f, 0.105f) : new Color(0.86f, 0.875f, 0.885f), Line);
            CreateText(chip2.transform, "◆", 29, FontStyle.Bold, Orange, 16, 11, 40, 40, TextAnchor.MiddleCenter);
            CreateText(chip2.transform, "源石锭", 12, FontStyle.Normal, dark ? Muted : new Color(0.35f, 0.38f, 0.40f), 64, 6, 90, 20);
            ingots = CreateText(chip2.transform, "0", 21, FontStyle.Bold, dark ? White : Ink, 64, 25, 190, 29);
        }

        private void AddCurrencyIcon(Transform parent, float x, float y, float w, float h)
        {
            if (_lmdIcon != null)
            {
                var image = CreateImage(parent, "LMDIcon", x, y, w, h, Color.white);
                image.sprite = _lmdIcon;
                image.preserveAspect = true;
                return;
            }

            var bg = CreateImage(parent, "LMDFallback", x, y, w, h, new Color(0.18f, 0.55f, 0.86f));
            CreateText(bg.transform, "L", 18, FontStyle.Bold, White, 0, 0, w, h, TextAnchor.MiddleCenter);
        }

        private GameObject CreateCompactItemCard(Transform parent, CollectibleDefinition item, int count, float x, float y, float w, float h, bool dark)
        {
            var rarity = item != null ? SearchableContainer25D.RarityColor(item) : Muted;
            var card = CreatePanel(parent, "Item", x, y, w, h, dark ? InkSoft : White, rarity);
            if (item != null)
            {
                AddItemVisual(card.transform, item, 12, 8, w - 24, h - 48);
                CreateText(card.transform, item.DisplayName, 13, FontStyle.Bold, dark ? White : Ink, 8, h - 36, w - 55, 26);
                if (count > 1)
                    CreateText(card.transform, $"×{count}", 16, FontStyle.Bold, dark ? White : Ink, w - 48, h - 38, 40, 26, TextAnchor.MiddleRight);
            }
            return card;
        }

        private void AddItemVisual(Transform parent, CollectibleDefinition item, float x, float y, float w, float h)
        {
            if (item == null) return;
            if (item.SalvageIcon != null)
            {
                var image = CreateImage(parent, "ItemIcon", x, y, w, h, Color.white);
                image.sprite = item.SalvageIcon;
                image.preserveAspect = true;
                return;
            }

            if (item.SalvageIconTexture != null)
            {
                var go = CreateRect("ItemTexture", parent, x, y, w, h);
                var raw = go.AddComponent<RawImage>();
                raw.texture = item.SalvageIconTexture;
                raw.color = Color.white;
                raw.raycastTarget = false;
                FitRawImage(go.GetComponent<RectTransform>(), item.SalvageIconTexture, x, y, w, h);
            }
        }

        private static void FitRawImage(RectTransform rt, Texture texture, float x, float y, float w, float h)
        {
            if (rt == null || texture == null || texture.width <= 0 || texture.height <= 0 || w <= 0f || h <= 0f)
                return;

            var textureAspect = texture.width / (float)texture.height;
            var boxAspect = w / h;
            var fittedWidth = w;
            var fittedHeight = h;
            if (boxAspect > textureAspect)
                fittedWidth = h * textureAspect;
            else
                fittedHeight = w / textureAspect;

            rt.anchoredPosition = new Vector2(
                x + (w - fittedWidth) * 0.5f,
                -(y + (h - fittedHeight) * 0.5f));
            rt.sizeDelta = new Vector2(fittedWidth, fittedHeight);
        }

        private GameObject CreateRoot(string name, Transform parent, Color background)
        {
            var go = CreateRect(name, parent, 0, 0, 1920, 1080);
            Stretch(go.GetComponent<RectTransform>());
            var image = go.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;
            return go;
        }

        private GameObject CreatePanel(Transform parent, string name, float x, float y, float w, float h, Color color, Color outline)
        {
            var go = CreateRect(name, parent, x, y, w, h);
            var image = go.AddComponent<Image>();
            image.color = color;
            var border = go.AddComponent<Outline>();
            border.effectColor = outline;
            border.effectDistance = new Vector2(1f, -1f);
            border.useGraphicAlpha = false;
            return go;
        }

        private Button CreateActionButton(
            Transform parent, string title, string subtitle,
            float x, float y, float w, float h,
            Color background, Color accent, int titleSize, bool darkText = false)
        {
            var go = CreatePanel(parent, "Button_" + title, x, y, w, h, background, new Color(accent.r, accent.g, accent.b, 0.75f));
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            var normalText = darkText ? Ink : White;
            button.colors = ButtonColors(background, Mix(background, accent, 0.14f), Mix(background, accent, 0.25f));

            CreateText(go.transform, subtitle, 13, FontStyle.Bold, accent, 34, 28, w - 68, 22);
            // Keep the title in its own bounded band so optional descriptions/status labels cannot
            // overlap it on taller home cards.
            var titleHeight = Mathf.Min(72f, Mathf.Max(44f, h - 72f));
            CreateText(go.transform, title, titleSize, FontStyle.Bold, normalText, 34, 58, w - 68, titleHeight);
            return button;
        }

        private InputField CreateInputField(Transform parent, string placeholderText, float x, float y, float w, float h)
        {
            var go = CreateRect("SearchInput", parent, x, y, w, h);
            var background = go.AddComponent<Image>();
            background.color = White;

            var input = go.AddComponent<InputField>();
            input.targetGraphic = background;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Standard;

            var valueText = CreateText(go.transform, string.Empty, 16, FontStyle.Normal, Ink, 14, 0, w - 28, h, TextAnchor.MiddleLeft);
            valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var placeholder = CreateText(go.transform, placeholderText, 15, FontStyle.Normal, Muted, 14, 0, w - 28, h, TextAnchor.MiddleLeft);
            input.textComponent = valueText;
            input.placeholder = placeholder;
            return input;
        }

        private Button CreateFlatButton(Transform parent, string label, float x, float y, float w, float h, Color background, Color foreground)
        {
            var go = CreatePanel(parent, "Button_" + label, x, y, w, h, background, Line);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.colors = ButtonColors(background, Mix(background, Cyan, 0.14f), Mix(background, Cyan, 0.24f));
            CreateText(go.transform, label, 18, FontStyle.Bold, foreground, 10, 0, w - 20, h, TextAnchor.MiddleCenter);
            return button;
        }

        private static ColorBlock ButtonColors(Color normal, Color highlighted, Color pressed)
        {
            var block = ColorBlock.defaultColorBlock;
            block.normalColor = normal;
            block.highlightedColor = highlighted;
            block.pressedColor = pressed;
            block.selectedColor = highlighted;
            block.disabledColor = new Color(normal.r, normal.g, normal.b, 0.55f);
            block.colorMultiplier = 1f;
            block.fadeDuration = 0.08f;
            return block;
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
            text.raycastTarget = false;
            // Shell text must stay inside its declared rect. Overflow was causing labels to cross
            // panel/screen boundaries and overlap neighbouring labels at several resolutions.
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Image CreateImage(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = CreateRect(name, parent, x, y, w, h);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static void AddAccentBar(Transform parent, float x, float y, float w, float h, Color color)
        {
            var go = CreateRect("Accent", parent, x, y, w, h);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static Color Mix(Color a, Color b, float t) =>
            new(Mathf.Lerp(a.r, b.r, t), Mathf.Lerp(a.g, b.g, t), Mathf.Lerp(a.b, b.b, t), Mathf.Lerp(a.a, b.a, t));

        private static string FormatDuration(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
