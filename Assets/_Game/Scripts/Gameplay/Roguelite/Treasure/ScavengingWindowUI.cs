using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    /// <summary>
    /// Immediate-mode presentation for the salvage loop.
    /// Layout: a compact recovery chip, a proximity prompt, the container search window (one
    /// circular sweep per entry, entries pop out as they are identified) and the B-key field pack.
    /// Search runs automatically; revealed cells support either number keys or direct mouse pickup.
    /// </summary>
    [RequireComponent(typeof(ScavengingInventory25D))]
    public sealed class ScavengingWindowUI : MonoBehaviour
    {
        private const float RefWidth = 1600f;
        private const float RefHeight = 900f;
        private static readonly Color Accent = new Color(0.43f, 0.87f, 0.77f);
        private static readonly Color Panel = new Color(0.025f, 0.045f, 0.054f, 0.96f);
        private static readonly Color PanelSolid = new Color(0.025f, 0.045f, 0.054f, 0.985f);
        private static readonly Color SlotBack = new Color(0.075f, 0.105f, 0.115f, 1f);
        private static readonly Color SlotMuted = new Color(0.05f, 0.07f, 0.078f, 1f);
        private static readonly Color TextMain = new Color(0.87f, 0.92f, 0.93f);
        private static readonly Color TextDim = new Color(0.55f, 0.62f, 0.64f);
        private static readonly Key[] SlotKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        private ScavengingInventory25D _owner;
        private Font _font;
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _tiny;
        private GUIStyle _center;
        private GUIStyle _centerSmall;
        private GUIStyle _centerBig;
        private GUIStyle _right;
        private CollectibleDefinition _selectedItem;
        private int _selectedContainerSlot = -1;
        private int _selectedBagIndex = -1;
        private int _lastContainerClickSlot = -1;
        private float _lastContainerClickTime = -10f;
        private SearchableContainer25D _lastDrawnContainer;
        private bool _lastBackpackOpen;
        private float _uiScale = 1f;
        private int _dragBagIndex = -1;
        private bool _dragMoved;
        private Vector2 _dragStartMouse;
        private Vector2 _dragMouse;
        private Vector2 _dragOffset;
        private Vector2 _dragPixelSize;
        private readonly List<ScavengingInventory25D.BackpackPlacement> _backpackLayout = new();

        private void Awake() => _owner = GetComponent<ScavengingInventory25D>();

        private void OnDestroy()
        {
            if (_font != null) Destroy(_font);
        }

        private void Update()
        {
            // UI must stay interactive during hit-stop / pause frames caused by being attacked.
            // Search progression itself is still frozen by ScavengingInventory25D.AdvanceSearch.
            if (_owner == null) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_owner.BackpackOpen && keyboard.uKey.wasPressedThisFrame)
                _owner.TryUpgradeBackpack();
            if (_owner.BackpackOpen && keyboard.rKey.wasPressedThisFrame)
                _owner.AutoArrangeBackpack();

            if (_owner.ActiveContainer == null) return;
            for (var i = 0; i < SlotKeys.Length; i++)
                if (keyboard[SlotKeys[i]].wasPressedThisFrame) _owner.TryPickup(i);
            if (keyboard.tKey.wasPressedThisFrame) _owner.TakeAllRevealed();
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 16);
            _title = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 17, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Accent;
            _label = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 15 };
            _label.normal.textColor = TextMain;
            _small = new GUIStyle(_label) { fontSize = 12, wordWrap = true };
            _tiny = new GUIStyle(_label) { fontSize = 11, wordWrap = true };
            _tiny.normal.textColor = TextDim;
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _centerSmall = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _centerBig = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 19, fontStyle = FontStyle.Bold };
            _right = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
        }

        private void OnGUI()
        {
            // Do not draw gameplay HUD over the home / settlement / warehouse shell.
            var flow = RogueliteGameFlowController.Instance;
            if (flow != null && !flow.IsRunning) return;
            // Do not hide the modal interface during damage hit-stop. A hit must never look like
            // it closed the backpack/search window.
            if (_owner == null) return;
            EnsureStyles();

            if (_lastDrawnContainer != _owner.ActiveContainer ||
                _lastBackpackOpen != _owner.BackpackOpen)
            {
                ClearSelection();
                _lastDrawnContainer = _owner.ActiveContainer;
                _lastBackpackOpen = _owner.BackpackOpen;
            }

            var saved = GUI.matrix;
            var scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            _uiScale = Mathf.Max(0.01f, scale);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * _uiScale);

            var windowed = _owner.ActiveContainer != null || _owner.BackpackOpen;
            if (!windowed) DrawRecoveryChip();
            if (_owner.ActiveContainer != null) DrawContainerWindow();
            else if (_owner.BackpackOpen) DrawBackpackWindow();

            GUI.matrix = saved;
        }

        // ---------- Recovery chip + prompt ----------

        private void DrawRecoveryChip()
        {
            // Persistent backpack status moved to GameplayHUDController. Keep only contextual interaction prompts here.
            var candidate = _owner.Candidate;
            var ground = _owner.GroundCandidate;
            if (candidate == null && ground == null) return;

            var prompt = new Rect((RefWidth - 620f) * 0.5f, RefHeight - 172f, 620f, 60f);
            Fill(prompt, new Color(0.02f, 0.04f, 0.048f, 0.82f));
            Fill(new Rect(prompt.x, prompt.y, 3f, prompt.height), Accent);

            var containerDistance = candidate != null
                ? (candidate.transform.position - _owner.transform.position).sqrMagnitude
                : float.PositiveInfinity;
            var groundDistance = ground != null
                ? (ground.transform.position - _owner.transform.position).sqrMagnitude
                : float.PositiveInfinity;

            if (ground != null && groundDistance <= containerDistance)
            {
                GUI.Label(new Rect(prompt.x + 18, prompt.y + 8, 520, 24), $"[ F ]  拾取 {ground.DisplayName}", _label);
                GUI.Label(new Rect(prompt.x + 18, prompt.y + 33, 580, 20), "地面物资 · 拾取后重新占用背包空间", _tiny);
                return;
            }

            GUI.Label(new Rect(prompt.x + 18, prompt.y + 8, 420, 24), $"[ F ]  检索 {candidate.DisplayName}", _label);
            var detail = candidate.SlotCount == 0
                ? "按 F 打开容器 · 打开期间移动/攻击/技能输入锁定"
                : candidate.RevealedCount > 0
                    ? $"{candidate.RevealedCount} 件已识别待取走 · 剩余 {candidate.SlotCount - candidate.TakenCount} 件"
                    : $"{candidate.SlotCount} 件物资待识别 · 打开后逐件自动搜索";
            GUI.Label(new Rect(prompt.x + 18, prompt.y + 33, 580, 20), detail, _tiny);
        }

        // ---------- Container search window ----------

        private void DrawContainerWindow()
        {
            var container = _owner.ActiveContainer;
            var win = new Rect(RefWidth - 690f, 132f, 650f, 558f);
            Fill(win, PanelSolid);
            Fill(new Rect(win.x, win.y, win.width, 3f), Accent);

            GUI.Label(new Rect(win.x + 18f, win.y + 12f, 300f, 26f), "正在搜索物资", _title);
            GUI.Label(new Rect(win.x + 18f, win.y + 37f, 330f, 18f), container.DisplayName, _tiny);
            GUI.Label(new Rect(win.x + win.width - 330f, win.y + 17f, 310f, 20f),
                "[ F / ESC ] 关闭    双击拾取    [ T ] 全部拿取", _right);

            var gridArea = new Rect(win.x + 22f, win.y + 70f, win.width - 44f, win.height - 94f);
            DrawContainerGrid(gridArea, container);

            if (_selectedContainerSlot >= 0)
            {
                if (_selectedContainerSlot >= container.Slots.Count ||
                    container.Slots[_selectedContainerSlot].State != SalvageSlotState.Revealed)
                {
                    ClearSelection();
                }
                else if (_selectedItem != null)
                {
                    DrawItemDetails(
                        new Rect(win.x - 258f, win.y + 70f, 240f, 390f),
                        _selectedItem,
                        _selectedContainerSlot,
                        -1);
                }
            }
        }

        private void DrawContainerGrid(Rect area, SearchableContainer25D container)
        {
            const float gap = 2f;
            const float minCell = 34f;
            const float maxCell = 52f;
            var cell = Mathf.Floor(Mathf.Min(
                maxCell,
                (area.width - gap * (container.GridWidth - 1)) / container.GridWidth,
                (area.height - gap * (container.GridHeight - 1)) / container.GridHeight));
            cell = Mathf.Clamp(cell, minCell, maxCell);

            var gridWidth = container.GridWidth * cell + (container.GridWidth - 1) * gap;
            var gridHeight = container.GridHeight * cell + (container.GridHeight - 1) * gap;
            var origin = new Vector2(
                area.x + Mathf.Floor((area.width - gridWidth) * 0.5f),
                area.y + Mathf.Floor((area.height - gridHeight) * 0.5f));

            Fill(new Rect(origin.x - 3f, origin.y - 3f, gridWidth + 6f, gridHeight + 6f), new Color(0.018f, 0.024f, 0.027f, 0.98f));

            for (var y = 0; y < container.GridHeight; y++)
            for (var x = 0; x < container.GridWidth; x++)
            {
                var cellRect = new Rect(
                    origin.x + x * (cell + gap),
                    origin.y + y * (cell + gap),
                    cell,
                    cell);
                Fill(cellRect, new Color(0.075f, 0.085f, 0.09f, 1f));
                Stroke(cellRect, new Color(0.13f, 0.145f, 0.15f, 1f), 1f);
            }

            for (var i = 0; i < container.Slots.Count; i++)
            {
                var slot = container.Slots[i];
                if (slot.State == SalvageSlotState.Taken) continue;
                var rect = new Rect(
                    origin.x + slot.GridX * (cell + gap),
                    origin.y + slot.GridY * (cell + gap),
                    slot.Width * cell + (slot.Width - 1) * gap,
                    slot.Height * cell + (slot.Height - 1) * gap);
                DrawGridItem(rect, slot, i);
            }
        }

        private void DrawGridItem(Rect rect, SalvageLootSlot slot, int slotIndex)
        {
            if (slot.State == SalvageSlotState.Unsearched)
            {
                // Occupied-but-unidentified cells must read differently from truly empty grid cells.
                Fill(rect, new Color(0.105f, 0.125f, 0.13f, 1f));
                Stroke(rect, new Color(0.28f, 0.32f, 0.33f, 0.95f), 1.5f);
                Fill(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 3f), new Color(0.17f, 0.20f, 0.21f, 1f));
                Fill(new Rect(rect.x + 4f, rect.yMax - 7f, rect.width - 8f, 3f), new Color(0.17f, 0.20f, 0.21f, 1f));
                var unknownStyle = new GUIStyle(_centerBig)
                {
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * 0.28f), 14, 24)
                };
                unknownStyle.normal.textColor = new Color(0.50f, 0.56f, 0.57f);
                GUI.Label(rect, "?", unknownStyle);
                return;
            }

            if (slot.State == SalvageSlotState.Searching)
            {
                // Do not leak the item art or rarity before identification. The occupied footprint
                // itself is visible, with the search spinner centered on that footprint.
                Fill(rect, new Color(0.065f, 0.075f, 0.078f, 1f));
                Stroke(rect, new Color(0.30f, 0.34f, 0.35f, 0.95f), 1.5f);
                DrawSearchGlyph(rect, slot.Progress);
                return;
            }

            var rarity = SearchableContainer25D.RarityColor(slot.Item);
            var saved = GUI.matrix;
            var popping = slot.RevealTime > 0f && Time.unscaledTime - slot.RevealTime < 0.30f;
            if (popping)
            {
                var t = Mathf.Clamp01((Time.unscaledTime - slot.RevealTime) / 0.30f);
                GUIUtility.ScaleAroundPivot(Vector2.one * (0.92f + 0.08f * (1f - Mathf.Pow(1f - t, 3f))), rect.center);
            }

            Fill(rect, new Color(rarity.r * 0.34f, rarity.g * 0.34f, rarity.b * 0.34f, 1f));
            Stroke(rect, new Color(rarity.r, rarity.g, rarity.b, 0.88f), 2f);

            // Only real mapped relics get art for now. Ordinary scavenging/collection goods
            // deliberately stay as rarity-colored blocks until their dedicated artwork is added.
            if (HasItemIcon(slot.Item))
            {
                var iconRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
                DrawItemIcon(iconRect, slot.Item, 1f);
            }

            var selected = _selectedContainerSlot == slotIndex && _selectedItem == slot.Item;
            if (selected)
                Stroke(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Color.white, 2f);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                var now = Time.unscaledTime;
                var doubleClick = _lastContainerClickSlot == slotIndex &&
                                  now - _lastContainerClickTime <= 0.34f;

                _selectedItem = slot.Item;
                _selectedContainerSlot = slotIndex;
                _selectedBagIndex = -1;

                if (doubleClick && _owner.TryPickup(slotIndex))
                {
                    ClearSelection();
                    _lastContainerClickSlot = -1;
                    _lastContainerClickTime = -10f;
                }
                else
                {
                    _lastContainerClickSlot = slotIndex;
                    _lastContainerClickTime = now;
                }
            }

            GUI.matrix = saved;
        }

        private void DrawSearchGlyph(Rect rect, float progress)
        {
            // Rectangle-only spinner: avoids texture/import issues and is visible on every IMGUI pass.
            var radius = Mathf.Clamp(Mathf.Min(rect.width, rect.height) * 0.23f, 12f, 24f);
            var center = rect.center;
            const int segments = 12;
            var head = Mathf.FloorToInt(Time.unscaledTime * 10f) % segments;

            Fill(new Rect(center.x - radius - 7f, center.y - radius - 7f, (radius + 7f) * 2f, (radius + 7f) * 2f),
                new Color(0.012f, 0.016f, 0.018f, 0.78f));

            for (var i = 0; i < segments; i++)
            {
                var angle = (-90f + 360f * i / segments) * Mathf.Deg2Rad;
                var point = new Vector2(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius);
                var distance = (head - i + segments) % segments;
                var alpha = distance <= 4 ? 1f - distance * 0.16f : 0.24f;
                var size = i == head ? 5f : 3.5f;
                Fill(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size),
                    new Color(0.94f, 0.98f, 0.98f, alpha));
            }

            // Search progress is intentionally conveyed only by the rotating pulse.
            // No progress bar: the item stays visually close to Delta-style container search.
        }

        private static bool HasItemIcon(CollectibleDefinition item)
        {
            if (item == null)
                return false;
            return item.SalvageIcon != null || item.SalvageIconTexture != null;
        }

        private void DrawItemIcon(Rect rect, CollectibleDefinition item, float alpha)
        {
            if (item == null)
            {
                DrawMissingIcon(rect);
                return;
            }

            var before = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            var sprite = item.SalvageIcon;
            if (sprite != null)
                DrawSprite(rect, sprite);
            else if (item.SalvageIconTexture != null)
                GUI.DrawTexture(rect, item.SalvageIconTexture, ScaleMode.ScaleToFit, true);
            else
                DrawMissingIcon(rect);
            GUI.color = before;
        }

        private void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            var source = sprite.rect;
            var uv = new Rect(
                source.x / sprite.texture.width,
                source.y / sprite.texture.height,
                source.width / sprite.texture.width,
                source.height / sprite.texture.height);

            var aspect = source.height > 0f ? source.width / source.height : 1f;
            var fitted = rect;
            if (rect.width / rect.height > aspect)
            {
                fitted.width = rect.height * aspect;
                fitted.x = rect.center.x - fitted.width * 0.5f;
            }
            else
            {
                fitted.height = rect.width / Mathf.Max(0.01f, aspect);
                fitted.y = rect.center.y - fitted.height * 0.5f;
            }

            var before = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(fitted, sprite.texture, uv, true);
            GUI.color = before;
        }

        private void DrawMissingIcon(Rect rect)
        {
            var size = Mathf.Min(rect.width, rect.height);
            var body = new Rect(rect.center.x - size * 0.23f, rect.center.y - size * 0.30f, size * 0.46f, size * 0.60f);
            Fill(body, new Color(0.86f, 0.89f, 0.90f, 0.76f));
            Fill(new Rect(body.x + size * 0.08f, body.y + size * 0.08f, body.width - size * 0.16f, body.height - size * 0.16f),
                new Color(0.16f, 0.18f, 0.19f, 1f));
        }

        // ---------- Backpack window ----------

        private void DrawBackpackWindow()
        {
            // Keep the field pack compact: the grid is the focus, not a wide modal frame.
            var win = new Rect(RefWidth - 570f, 132f, 530f, 548f);
            Fill(win, PanelSolid);
            Fill(new Rect(win.x, win.y, win.width, 2f), Accent);

            GUI.Label(new Rect(win.x + 12f, win.y + 10f, 180f, 26f), "搜刮背包", _title);
            GUI.Label(new Rect(win.x + 12f, win.y + 34f, 330f, 18f),
                "拖拽调整位置 · 拖出网格丢弃", _tiny);
            GUI.Label(new Rect(win.x + win.width - 286f, win.y + 12f, 274f, 20f),
                $"[ B / ESC ]  {_owner.BackpackWidth}×{_owner.BackpackHeight} · {_owner.UsedBackpackCells}/{_owner.BackpackCellCapacity} 格", _right);

            var gridArea = new Rect(win.x + 12f, win.y + 60f, win.width - 24f, win.height - 132f);
            DrawBackpackGrid(gridArea);
            DrawBackpackUpgrade(new Rect(win.x + 12f, win.yMax - 54f, win.width - 178f, 36f));
            DrawBackpackArrange(new Rect(win.xMax - 156f, win.yMax - 54f, 144f, 36f));

            if (_selectedBagIndex >= 0)
            {
                if (_selectedBagIndex >= _owner.PendingCount ||
                    _owner.Pending[_selectedBagIndex] != _selectedItem)
                {
                    ClearSelection();
                }
                else if (_selectedItem != null)
                {
                    DrawItemDetails(
                        new Rect(win.x - 258f, win.y + 70f, 240f, 390f),
                        _selectedItem,
                        -1,
                        _selectedBagIndex);
                }
            }
        }

        private void DrawBackpackGrid(Rect area)
        {
            var columns = _owner.BackpackWidth;
            var rows = _owner.BackpackHeight;
            const float gap = 2f;
            const float minCell = 42f;
            const float maxCell = 72f;

            var cell = Mathf.Floor(Mathf.Min(
                maxCell,
                (area.width - gap * (columns - 1)) / columns,
                (area.height - gap * (rows - 1)) / rows));
            cell = Mathf.Clamp(cell, minCell, maxCell);

            var gridWidth = columns * cell + (columns - 1) * gap;
            var gridHeight = rows * cell + (rows - 1) * gap;
            var origin = new Vector2(
                area.x + Mathf.Floor((area.width - gridWidth) * 0.5f),
                area.y + Mathf.Floor((area.height - gridHeight) * 0.5f));

            Fill(new Rect(origin.x - 1.5f, origin.y - 1.5f, gridWidth + 3f, gridHeight + 3f),
                new Color(0.018f, 0.024f, 0.027f, 0.96f));

            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var cellRect = new Rect(
                    origin.x + x * (cell + gap),
                    origin.y + y * (cell + gap),
                    cell,
                    cell);
                Fill(cellRect, new Color(0.075f, 0.085f, 0.09f, 1f));
                Stroke(cellRect, new Color(0.13f, 0.145f, 0.15f, 1f), 1f);
            }

            if (!_owner.BuildBackpackLayout(_backpackLayout))
                return;

            var currentEvent = Event.current;
            var mouse = GetReferenceMousePosition();
            if (_dragBagIndex >= 0)
            {
                _dragMouse = mouse;
                if ((mouse - _dragStartMouse).sqrMagnitude > 16f)
                    _dragMoved = true;
            }

            for (var p = 0; p < _backpackLayout.Count; p++)
            {
                var placement = _backpackLayout[p];
                var i = placement.PendingIndex;
                if (i < 0 || i >= _owner.PendingCount) continue;
                var item = _owner.Pending[i];
                if (item == null) continue;

                var rect = new Rect(
                    origin.x + placement.X * (cell + gap),
                    origin.y + placement.Y * (cell + gap),
                    placement.Width * cell + (placement.Width - 1) * gap,
                    placement.Height * cell + (placement.Height - 1) * gap);

                DrawBackpackItem(rect, item, _dragBagIndex == i ? 0.28f : 1f);

                if (_selectedBagIndex == i && _selectedItem == item)
                    Stroke(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Color.white, 2f);

                if (_dragBagIndex < 0 &&
                    currentEvent.type == EventType.MouseDown &&
                    currentEvent.button == 0 &&
                    rect.Contains(mouse))
                {
                    _selectedItem = item;
                    _selectedContainerSlot = -1;
                    _selectedBagIndex = i;
                    _dragBagIndex = i;
                    _dragStartMouse = mouse;
                    _dragMouse = mouse;
                    _dragOffset = mouse - rect.position;
                    _dragPixelSize = rect.size;
                    _dragMoved = false;
                    currentEvent.Use();
                }
            }

            if (_dragBagIndex < 0 || _dragBagIndex >= _owner.PendingCount)
                return;

            var dragItem = _owner.Pending[_dragBagIndex];
            var floating = new Rect(_dragMouse - _dragOffset, _dragPixelSize);
            DrawBackpackItem(floating, dragItem, 0.92f);
            Stroke(floating, Color.white, 2f);

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                if (_dragMoved)
                {
                    if (!area.Contains(mouse))
                    {
                        _owner.DropPending(_dragBagIndex);
                        _selectedItem = null;
                        _selectedBagIndex = -1;
                    }
                    else
                    {
                        var targetX = Mathf.RoundToInt((floating.x - origin.x) / (cell + gap));
                        var targetY = Mathf.RoundToInt((floating.y - origin.y) / (cell + gap));
                        _owner.TryMovePending(_dragBagIndex, targetX, targetY);
                    }
                }

                _dragBagIndex = -1;
                _dragMoved = false;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
            {
                currentEvent.Use();
            }
        }

        private Vector2 GetReferenceMousePosition()
        {
            var pointer = Mouse.current;
            if (pointer == null)
                return Event.current.mousePosition;

            var screen = pointer.position.ReadValue();
            var scale = Mathf.Max(0.01f, _uiScale);
            return new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
        }

        private void DrawBackpackItem(Rect rect, CollectibleDefinition item, float alpha)
        {
            if (item == null) return;
            var rarity = SearchableContainer25D.RarityColor(item);
            Fill(rect, new Color(rarity.r * 0.34f, rarity.g * 0.34f, rarity.b * 0.34f, Mathf.Clamp01(alpha)));
            Stroke(rect, new Color(rarity.r, rarity.g, rarity.b, 0.88f * Mathf.Clamp01(alpha)), 2f);
            if (HasItemIcon(item))
            {
                var iconRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
                DrawItemIcon(iconRect, item, alpha);
            }
        }

        private void DrawBackpackArrange(Rect rect)
        {
            Fill(rect, new Color(0.06f, 0.09f, 0.095f, 1f));
            Stroke(rect, new Color(0.28f, 0.38f, 0.39f, 1f), 1f);
            GUI.Label(rect, "[ R ] 自动整理", _centerSmall);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                _owner.AutoArrangeBackpack();
        }

        private void DrawBackpackUpgrade(Rect rect)
        {
            var runState = RogueliteRunState.Instance ?? FindFirstObjectByType<RogueliteRunState>();
            var ingots = runState != null ? runState.Ingots : 0;
            if (!_owner.CanUpgradeBackpack)
            {
                Fill(rect, new Color(0.06f, 0.09f, 0.095f, 1f));
                Stroke(rect, new Color(0.20f, 0.27f, 0.28f, 1f), 1f);
                GUI.Label(rect, $"背包已满级 · {_owner.BackpackWidth}×{_owner.BackpackHeight} · {ingots} 源石锭", _centerSmall);
                return;
            }

            var next = _owner.NextBackpackSize;
            var cost = _owner.NextBackpackUpgradeCost;
            var affordable = ingots >= cost;
            Fill(rect, affordable ? new Color(0.09f, 0.20f, 0.19f, 1f) : new Color(0.08f, 0.10f, 0.105f, 1f));
            Stroke(rect, affordable ? Accent : new Color(0.28f, 0.32f, 0.33f, 1f), 1f);
            GUI.Label(rect,
                $"[ U ] 扩容至 {next.x}×{next.y} · {cost} 源石锭    当前：{ingots}",
                _centerSmall);
            GUI.enabled = affordable;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                _owner.TryUpgradeBackpack();
            GUI.enabled = true;
        }

        private void DrawItemDetails(
            Rect rect,
            CollectibleDefinition item,
            int pickupContainerSlot,
            int bagIndex)
        {
            if (item == null) return;

            Fill(rect, new Color(0.035f, 0.052f, 0.058f, 0.99f));
            Stroke(rect, new Color(0.22f, 0.29f, 0.30f, 1f), 1f);
            Fill(new Rect(rect.x, rect.y, 3f, rect.height), SearchableContainer25D.RarityColor(item));

            GUI.Label(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 58f, 22f), "物品详情", _label);
            var closeRect = new Rect(rect.xMax - 36f, rect.y + 8f, 26f, 26f);
            GUI.Label(closeRect, "×", _centerBig);
            if (GUI.Button(closeRect, GUIContent.none, GUIStyle.none))
            {
                ClearSelection();
                return;
            }

            Fill(new Rect(rect.x + 14f, rect.y + 42f, rect.width - 28f, 1f),
                new Color(0.16f, 0.21f, 0.22f, 1f));

            var rarity = SearchableContainer25D.RarityColor(item);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 58f, rect.width - 28f, 44f), item.DisplayName, _center);
            Fill(new Rect(rect.x + 14f, rect.y + 108f, rect.width - 28f, 4f), rarity);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 120f, rect.width - 28f, 20f),
                $"{SearchableContainer25D.RarityName(item)} · {(item.IsSalvageCommodity ? "普通物资" : "藏品")}",
                _centerSmall);

            var detailText = string.IsNullOrWhiteSpace(item.Description) ? "暂无介绍" : item.Description;
            if (!item.IsSalvageCommodity && !string.IsNullOrWhiteSpace(item.EffectDescription))
                detailText += "\n\n效果：" + item.EffectDescription;
            GUI.Label(new Rect(rect.x + 14f, rect.y + 154f, rect.width - 28f, 112f), detailText, _small);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 278f, rect.width - 28f, 26f),
                $"价值  {item.CollectionValue:N0}", _label);

            if (!item.IsSalvageCommodity)
                GUI.Label(new Rect(rect.x + 14f, rect.y + 310f, rect.width - 28f, 44f),
                    _owner.EffectStatus(item), _small);

            if (pickupContainerSlot >= 0)
            {
                var button = new Rect(rect.x + 14f, rect.yMax - 50f, rect.width - 28f, 34f);
                Fill(button, new Color(0.09f, 0.20f, 0.19f, 1f));
                Stroke(button, Accent, 1f);
                GUI.Label(button, "拾取", _center);
                if (GUI.Button(button, GUIContent.none, GUIStyle.none) &&
                    _owner.TryPickup(pickupContainerSlot))
                {
                    ClearSelection();
                }
            }
            else if (bagIndex >= 0)
            {
                var button = new Rect(rect.x + 14f, rect.yMax - 50f, rect.width - 28f, 34f);
                Fill(button, new Color(0.16f, 0.09f, 0.08f, 1f));
                Stroke(button, new Color(0.82f, 0.42f, 0.34f, 1f), 1f);
                GUI.Label(button, "丢弃到地面", _center);
                if (GUI.Button(button, GUIContent.none, GUIStyle.none) &&
                    _owner.DropPending(bagIndex))
                {
                    ClearSelection();
                }
            }
        }

        private void ClearSelection()
        {
            _selectedItem = null;
            _selectedContainerSlot = -1;
            _selectedBagIndex = -1;
            _lastContainerClickSlot = -1;
            _lastContainerClickTime = -10f;
            _dragBagIndex = -1;
            _dragMoved = false;
        }

        // ---------- Drawing helpers ----------

        private static void Fill(Rect rect, Color color)
        {
            var before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }

        private static void Stroke(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

    }
}
