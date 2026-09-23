using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.World;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>North-up tactical map drawn as one UI mesh; no extra scene camera or render texture.</summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteMinimapController : MonoBehaviour
    {
        private RogueliteStageMapController _map;
        private RogueliteStageRuntimeController _runtime;
        private GameObject _canvasRoot;
        private RogueliteMinimapGraphic _graphic;
        private Font _ownedFont;
        private Text _title;
        private Text _caption;
        private float _nextRefresh;
        private CityExplorationGuide _guide;
        private Text _details;
        private RectTransform _panel;
        private RectTransform _surface;
        private bool _expanded;
        public bool IsVisible => _canvasRoot != null && _canvasRoot.activeSelf;

        public void Configure(RogueliteStageMapController map, RogueliteStageRuntimeController runtime)
        {
            _map = map; _runtime = runtime;
            _guide = runtime.GetComponent<CityExplorationGuide>() ?? runtime.gameObject.AddComponent<CityExplorationGuide>();
            _guide.Configure(map, runtime);
            if (_canvasRoot != null) return;
            _canvasRoot = new GameObject("[TacticalMinimap]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _canvasRoot.transform.SetParent(transform, false);
            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25;
            var scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("MapPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_canvasRoot.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            _panel = rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-20f, -20f); rect.sizeDelta = new Vector2(270f, 244f);
            var background = panel.GetComponent<Image>();
            background.color = new Color(.025f, .04f, .05f, .88f); background.raycastTarget = false;
            _ownedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 14);
            _title = Label(panel.transform, new Vector2(12f, -8f), new Vector2(246f, 22f), 13);
            _caption = Label(panel.transform, new Vector2(12f, -216f), new Vector2(246f, 20f), 11);
            _details = Label(panel.transform, new Vector2(12f, -240f), new Vector2(276f, 130f), 12);
            var surface = new GameObject("MapGeometry", typeof(RectTransform), typeof(RogueliteMinimapGraphic));
            surface.transform.SetParent(panel.transform, false);
            var surfaceRect = surface.GetComponent<RectTransform>();
            _surface = surfaceRect;
            surfaceRect.anchorMin = surfaceRect.anchorMax = new Vector2(0f, 1f);
            surfaceRect.pivot = new Vector2(0f, 1f); surfaceRect.anchoredPosition = new Vector2(12f, -36f);
            surfaceRect.sizeDelta = new Vector2(246f, 174f);
            _graphic = surface.GetComponent<RogueliteMinimapGraphic>();
            _graphic.raycastTarget = false; _graphic.Configure(map, runtime);
            ApplySize();
            _canvasRoot.SetActive(false);
        }

        private void Update()
        {
            if (_canvasRoot == null || _map == null || _runtime == null) return;
            var flow = RogueliteGameFlowController.Instance;
            var actor = _runtime.PlayerTransform;
            var inventory = actor != null ? actor.GetComponent<ScavengingInventory25D>() : null;
            var show = actor != null && _runtime.CurrentStageRoot != null && (flow == null || flow.IsRunning) && (inventory == null || !inventory.InterfaceOpen);
            _canvasRoot.SetActive(show);
            if (show && Time.timeScale > 0f && !ArknightsACT.Gameplay.Input.GameplayInputBlocker.IsBlocked && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            { _expanded = !_expanded; ApplySize(); }
            if (!show || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .10f;
            // Also works in the isolated editor preview where component Update is not running.
            if (!Application.isPlaying) _guide.Refresh(actor);
            _graphic.Refresh();
            var count = 0;
            foreach (var block in _map.Blocks) if (block.Explored) count++;
            var normalized = RogueliteMinimapGraphic.WorldToNormalized(actor.position, _map.Width, _map.Height);
            _map.TryGetBlock(new Vector2Int(Mathf.FloorToInt(normalized.x * _map.Width), Mathf.FloorToInt(normalized.y * _map.Height)), out var currentBlock);
            var zone = currentBlock != null ? currentBlock.Zone : CityZone.Outskirts;
            _title.text = $"{_map.StageIndex:00} · {CityZoneRules.Label(zone)}    N ↑  {count}/{_map.Blocks.Count}";
            _title.color = Color.Lerp(CityZoneRules.Color(zone), Color.white, .65f);
            var extraction = _runtime.ExtractionTransform;
            var distance = extraction != null ? Vector2.Distance(new Vector2(actor.position.x, actor.position.z), new Vector2(extraction.position.x, extraction.position.z)) : 0f;
            _caption.text = $"<color=#86B99A>外围</color> | <color=#EF935F>核心</color> | <color=#AB958A>废墟</color> | <color=#86ACCD>工业</color> · 撤离 {distance:0}m";
            _details.text = $"{_guide.LocationText}\n\n{_guide.TargetLabel}\n{_guide.Hint}\nM 展开地图 · N 切换探索/返程/下一层";
        }
        private void ApplySize()
        {
            var width = _expanded ? 580f : 300f;
            var mapHeight = _expanded ? 340f : 174f;
            _panel.sizeDelta = new Vector2(width, mapHeight + 210f);
            _surface.sizeDelta = new Vector2(width - 24f, mapHeight);
            _title.rectTransform.sizeDelta = new Vector2(width - 24f, 22f);
            _caption.rectTransform.anchoredPosition = new Vector2(12f, -mapHeight - 42f);
            _caption.rectTransform.sizeDelta = new Vector2(width - 24f, 20f);
            _details.rectTransform.anchoredPosition = new Vector2(12f, -mapHeight - 68f);
            _details.rectTransform.sizeDelta = new Vector2(width - 24f, 132f);
        }
        private void OnDisable() { if (_canvasRoot != null) _canvasRoot.SetActive(false); }
        private void OnDestroy() { if (_canvasRoot != null) Destroy(_canvasRoot); if (_ownedFont != null) Destroy(_ownedFont); }
        private Text Label(Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = _ownedFont != null ? _ownedFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = new Color(.78f, .85f, .86f); text.raycastTarget = false;
            return text;
        }
    }

}
