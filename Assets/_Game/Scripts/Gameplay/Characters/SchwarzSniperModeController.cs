using System;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.CameraSystem;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArknightsACT.Gameplay.Characters.Schwarz
{
    /// <summary>
    /// Interactive Schwarz S3 sniper stance.
    /// Uses a standard RawImage scope overlay instead of a custom UI mesh so it remains reliable
    /// in GameView. The entire scope follows the mouse. Aimed shooting uses three target paths:
    /// 3D collider raycast, projected collider bounds, and nearest-screen-target assist.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SchwarzSkill2), typeof(PlayerAttackController), typeof(SchwarzRangedBasicAttack))]
    public sealed class SchwarzSniperModeController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField, Range(0.45f, 1f)] private float zoomMultiplier = 0.76f;
        [SerializeField, Min(0f)] private float cameraAimOffsetScale = 0.38f;
        [SerializeField, Min(0.5f)] private float maxCameraAimOffset = 3.2f;

        [Header("Targeting")]
        [SerializeField, Min(20f)] private float screenTargetRadiusPixels = 140f;
        [SerializeField, Min(20f)] private float clickAssistRadiusPixels = 220f;
        [SerializeField, Min(0.1f)] private float baseShotInterval = 0.72f;

        [Header("Scope")]
        [SerializeField, Range(0.24f, 0.49f)] private float scopeRadiusScreenHeight = 0.30f;
        [SerializeField] private Color scopeShade = new(0f, 0f, 0f, 0.58f);
        [SerializeField] private Color scopeLine = new(0.10f, 0.11f, 0.13f, 0.82f);
        [SerializeField] private Color scopeLockColor = new(0.22f, 0.24f, 0.26f, 0.96f);

        private SchwarzSkill2 _skill3;
        private PlayerAttackController _attacks;
        private SchwarzRangedBasicAttack _ranged;
        private PlayerMotor25D _motor;
        private CombatEntity _entity;

        private Camera _camera;
        private CameraFollow25D _cameraFollow;
        private float _baseOrthographicSize;
        private bool _hasStoredCameraSize;
        private float _nextShotAt;
        private bool _cursorWasVisible;
        private bool _modeEntered;

        private Canvas _scopeCanvas;
        private RectTransform _scopeRoot;
        private RectTransform _scopeImageRect;
        private RawImage _scopeImage;
        private RectTransform _statusRect;
        private Text _statusText;
        private Texture2D _scopeTexture;
        private Texture2D _scopeLockedTexture;
        private Vector2 _lastScopeCanvasSize = Vector2.zero;

        public bool IsActive => _skill3 != null && _skill3.IsBuffActive;
        public CombatEntity CurrentAimTarget { get; private set; }
        public event Action<CombatEntity> ShotFired;

        private void Awake()
        {
            _skill3 = GetComponent<SchwarzSkill2>();
            _attacks = GetComponent<PlayerAttackController>();
            _ranged = GetComponent<SchwarzRangedBasicAttack>();
            _motor = GetComponent<PlayerMotor25D>();
            _entity = GetComponent<CombatEntity>();
            ResolveCamera();
            BuildScopeUI();
            // The scope textures are relatively expensive to generate. Build them while the
            // operator initializes instead of paying that CPU/allocation cost on the S3 input frame.
            Canvas.ForceUpdateCanvases();
            EnsureScopeTextures();
            SetScopeVisible(false);
        }

        private void OnEnable()
        {
            if (_skill3 != null)
            {
                _skill3.BuffStarted += EnterSniperMode;
                _skill3.BuffEnded += ExitSniperMode;
            }

            if (_entity?.Health != null)
                _entity.Health.Died += ExitSniperMode;
        }

        private void OnDisable()
        {
            if (_skill3 != null)
            {
                _skill3.BuffStarted -= EnterSniperMode;
                _skill3.BuffEnded -= ExitSniperMode;
            }

            if (_entity?.Health != null)
                _entity.Health.Died -= ExitSniperMode;

            ExitSniperMode();
        }

        private void OnDestroy()
        {
            if (_scopeTexture != null)
                Destroy(_scopeTexture);
            if (_scopeLockedTexture != null)
                Destroy(_scopeLockedTexture);
        }

        private void Update()
        {
            if (!IsActive)
                return;

            ResolveCamera();
            EnsureScopeTextures();

            var mouse = Mouse.current;
            if (mouse == null || _camera == null)
                return;

            var mousePosition = mouse.position.ReadValue();

            UpdateFacingFromMouse(mousePosition);
            CurrentAimTarget = ResolveHoverTarget(mousePosition);
            UpdateScope(mousePosition, CurrentAimTarget != null);
            UpdateAimCamera(mousePosition, CurrentAimTarget);

            if (!mouse.leftButton.wasPressedThisFrame)
                return;
            if (Time.unscaledTime < _nextShotAt)
                return;

            // Do not require the hover UI to have reached TARGET LOCK before allowing a shot.
            // Resolve once more using the more generous click-target path.
            var shotTarget = CurrentAimTarget ?? ResolveClickTarget(mousePosition);
            if (shotTarget == null)
            {
                SetStatus("NO TARGET", false);
                return;
            }

            CurrentAimTarget = shotTarget;
            UpdateScope(mousePosition, true);

            var intervalMultiplier = _skill3 != null
                ? Mathf.Max(0.1f, _skill3.BasicAttackTimingMultiplier)
                : 1f;
            _nextShotAt = Time.unscaledTime + baseShotInterval * intervalMultiplier;

            var distance = Vector3.Distance(
                transform.position + Vector3.up * 0.66f,
                shotTarget.transform.position + Vector3.up * ResolveAimHeight(shotTarget));
            var impactDelay = Mathf.Clamp(distance / 30f, 0.08f, 0.38f);

            if (_attacks != null && _attacks.TryManualTargetAttack(
                    shotTarget,
                    "Schwarz_S3_AimedShot",
                    1f,
                    impactDelay))
            {
                ShotFired?.Invoke(shotTarget);
                SetStatus("FIRE", true);
            }
            else
            {
                SetStatus("SHOT BLOCKED", false);
            }
        }

        private void EnterSniperMode()
        {
            if (_modeEntered)
                return;

            _modeEntered = true;
            ResolveCamera();
            CurrentAimTarget = null;
            _nextShotAt = 0f;
            _motor?.ResetMotion();

            if (_camera != null)
            {
                if (!_hasStoredCameraSize)
                {
                    _baseOrthographicSize = _camera.orthographicSize;
                    _hasStoredCameraSize = true;
                }

                if (_camera.orthographic)
                    _camera.orthographicSize = Mathf.Max(2.2f, _baseOrthographicSize * zoomMultiplier);
            }

            _cursorWasVisible = Cursor.visible;
            Cursor.visible = false;
            SetScopeVisible(true);

            // Awake already prepared the scope texture. EnsureScopeTextures stays in Update as a
            // resolution-change fallback, but S3 activation itself must remain allocation-free.
            if (Mouse.current != null)
                UpdateScope(Mouse.current.position.ReadValue(), false);

            Debug.Log("[ArknightsACT/Schwarz] S3 sniper mode entered.", this);
        }

        private void ExitSniperMode()
        {
            if (!_modeEntered)
            {
                SetScopeVisible(false);
                return;
            }

            _modeEntered = false;
            CurrentAimTarget = null;
            _motor?.ResetMotion();
            _cameraFollow?.ClearViewOffset();

            if (_camera != null && _hasStoredCameraSize && _camera.orthographic)
                _camera.orthographicSize = _baseOrthographicSize;

            Cursor.visible = _cursorWasVisible;
            SetScopeVisible(false);
        }

        private void UpdateFacingFromMouse(Vector2 mousePosition)
        {
            if (_camera == null || _motor == null)
                return;

            var actorScreen = _camera.WorldToScreenPoint(transform.position + Vector3.up * 0.65f);
            if (actorScreen.z <= 0f)
                return;

            _motor.ForceFacingSign(mousePosition.x < actorScreen.x ? -1 : 1);
        }

        private CombatEntity ResolveHoverTarget(Vector2 mousePosition)
        {
            var direct = ResolveRaycastTarget(mousePosition);
            if (direct != null)
                return direct;

            var boundsTarget = ResolveProjectedBoundsTarget(mousePosition);
            if (boundsTarget != null)
                return boundsTarget;

            return ResolveNearestScreenTarget(mousePosition, Mathf.Max(140f, screenTargetRadiusPixels));
        }

        private CombatEntity ResolveClickTarget(Vector2 mousePosition)
        {
            var direct = ResolveRaycastTarget(mousePosition);
            if (direct != null)
                return direct;

            var boundsTarget = ResolveProjectedBoundsTarget(mousePosition);
            if (boundsTarget != null)
                return boundsTarget;

            return ResolveNearestScreenTarget(
                mousePosition,
                Mathf.Max(220f, clickAssistRadiusPixels));
        }

        private CombatEntity ResolveRaycastTarget(Vector2 mousePosition)
        {
            if (_camera == null || _entity == null)
                return null;

            var maxRange = ResolveMaxRange();
            var ray = _camera.ScreenPointToRay(mousePosition);
            var hits = Physics.RaycastAll(
                ray,
                Mathf.Max(40f, _camera.farClipPlane),
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null)
                    continue;

                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (CanAimAt(candidate, maxRange))
                    return candidate;
            }

            return null;
        }

        private CombatEntity ResolveProjectedBoundsTarget(Vector2 mousePosition)
        {
            if (_camera == null || _entity == null)
                return null;

            var maxRange = ResolveMaxRange();
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            CombatEntity best = null;
            var bestDistance = float.PositiveInfinity;

            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (!CanAimAt(candidate, maxRange))
                    continue;

                var collider = candidate.GetComponentInChildren<Collider>();
                if (collider == null)
                    continue;

                if (!TryProjectBounds(collider.bounds, out var screenRect))
                    continue;

                // Add a little forgiveness around large Spine/billboard art.
                screenRect.xMin -= 22f;
                screenRect.xMax += 22f;
                screenRect.yMin -= 22f;
                screenRect.yMax += 22f;

                if (!screenRect.Contains(mousePosition))
                    continue;

                var center = screenRect.center;
                var distance = Vector2.Distance(center, mousePosition);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private CombatEntity ResolveNearestScreenTarget(Vector2 mousePosition, float radius)
        {
            if (_camera == null || _entity == null)
                return null;

            var maxRange = ResolveMaxRange();
            CombatEntity best = null;
            var bestScreenDistance = float.PositiveInfinity;
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);

            for (var i = 0; i < entities.Length; i++)
            {
                var candidate = entities[i];
                if (!CanAimAt(candidate, maxRange))
                    continue;

                var screen = _camera.WorldToScreenPoint(
                    candidate.transform.position + Vector3.up * ResolveAimHeight(candidate));
                if (screen.z <= 0f)
                    continue;

                var screenDistance = Vector2.Distance(
                    new Vector2(screen.x, screen.y),
                    mousePosition);
                if (screenDistance > radius || screenDistance >= bestScreenDistance)
                    continue;

                bestScreenDistance = screenDistance;
                best = candidate;
            }

            return best;
        }

        private bool TryProjectBounds(Bounds bounds, out Rect rect)
        {
            rect = default;
            if (_camera == null)
                return false;

            var min = bounds.min;
            var max = bounds.max;
            var corners = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            var minScreen = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maxScreen = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var anyVisible = false;

            for (var i = 0; i < corners.Length; i++)
            {
                var screen = _camera.WorldToScreenPoint(corners[i]);
                if (screen.z <= 0f)
                    continue;

                anyVisible = true;
                minScreen.x = Mathf.Min(minScreen.x, screen.x);
                minScreen.y = Mathf.Min(minScreen.y, screen.y);
                maxScreen.x = Mathf.Max(maxScreen.x, screen.x);
                maxScreen.y = Mathf.Max(maxScreen.y, screen.y);
            }

            if (!anyVisible)
                return false;

            rect = Rect.MinMaxRect(
                minScreen.x,
                minScreen.y,
                maxScreen.x,
                maxScreen.y);
            return true;
        }

        private bool CanAimAt(CombatEntity candidate, float maxRange)
        {
            if (candidate == null || candidate == _entity ||
                candidate.Team == _entity.Team ||
                candidate.Health == null || candidate.Health.IsDead)
                return false;

            var delta = candidate.transform.position - transform.position;
            delta.y = 0f;
            return delta.magnitude <= maxRange;
        }

        private float ResolveMaxRange() =>
            _ranged != null ? Mathf.Max(1f, _ranged.CurrentVisualRange) : 16f;

        private static float ResolveAimHeight(CombatEntity candidate)
        {
            if (candidate == null)
                return 0.70f;

            var controller = candidate.GetComponent<CharacterController>();
            if (controller != null)
                return Mathf.Max(0.35f, controller.center.y);

            var collider = candidate.GetComponentInChildren<Collider>();
            if (collider != null)
                return Mathf.Max(
                    0.35f,
                    collider.bounds.center.y - candidate.transform.position.y);

            return 0.70f;
        }

        private void UpdateAimCamera(Vector2 mousePosition, CombatEntity target)
        {
            if (_camera == null)
                return;

            Vector3 aimWorld;
            if (target != null)
            {
                aimWorld = target.transform.position;
            }
            else
            {
                var plane = new Plane(Vector3.up, transform.position);
                var ray = _camera.ScreenPointToRay(mousePosition);
                if (!plane.Raycast(ray, out var enter))
                    return;
                aimWorld = ray.GetPoint(enter);
            }

            var aimDirection = aimWorld - transform.position;
            aimDirection.y = 0f;

            if (_cameraFollow != null && aimDirection.sqrMagnitude > 0.001f)
            {
                var offset = Vector3.ClampMagnitude(aimDirection, maxCameraAimOffset);
                _cameraFollow.SetViewOffset(offset * cameraAimOffsetScale);
            }
        }

        private void ResolveCamera()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_cameraFollow == null && _camera != null)
                _cameraFollow = _camera.GetComponentInParent<CameraFollow25D>();
        }

        private void BuildScopeUI()
        {
            var canvasGo = new GameObject(
                "SchwarzSniperScopeCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            _scopeCanvas = canvasGo.GetComponent<Canvas>();
            _scopeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _scopeCanvas.sortingOrder = 900;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _scopeRoot = CreateRect("ScopeRoot", canvasGo.transform);
            Stretch(_scopeRoot);

            var scopeImageGo = new GameObject(
                "MouseFollowScopeImage",
                typeof(RectTransform),
                typeof(RawImage));
            scopeImageGo.transform.SetParent(_scopeRoot, false);
            _scopeImageRect = scopeImageGo.GetComponent<RectTransform>();
            _scopeImageRect.anchorMin = _scopeImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            _scopeImageRect.pivot = new Vector2(0.5f, 0.5f);

            _scopeImage = scopeImageGo.GetComponent<RawImage>();
            _scopeImage.raycastTarget = false;
            _scopeImage.color = Color.white;

            var statusGo = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusGo.transform.SetParent(_scopeRoot, false);
            _statusRect = statusGo.GetComponent<RectTransform>();
            _statusRect.anchorMin = _statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            _statusRect.sizeDelta = new Vector2(220f, 28f);

            _statusText = statusGo.GetComponent<Text>();
            _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _statusText.fontSize = 12;
            _statusText.alignment = TextAnchor.MiddleCenter;
            _statusText.text = string.Empty;
            _statusText.raycastTarget = false;
            _statusText.color = scopeLine;
        }

        private void EnsureScopeTextures()
        {
            if (_scopeRoot == null || _scopeImageRect == null)
                return;

            var size = _scopeRoot.rect.size;
            if (size.x < 10f || size.y < 10f)
                return;

            if (_scopeTexture != null &&
                Vector2.Distance(size, _lastScopeCanvasSize) < 1f)
                return;

            if (_scopeTexture != null)
                Destroy(_scopeTexture);
            if (_scopeLockedTexture != null)
                Destroy(_scopeLockedTexture);

            _lastScopeCanvasSize = size;

            // Oversize the RawImage so it still covers every screen corner when its center is at
            // an edge. The transparent scope hole is intentionally a small fraction of this image.
            var coverSize = Mathf.Max(size.x, size.y) * 3.25f;
            var desiredRadius = Mathf.Min(size.x, size.y) * scopeRadiusScreenHeight;

            // 768 is sufficient with bilinear filtering and cuts procedural texture work
            // to one quarter of the previous 1536x1536 allocation/CPU cost.
            const int textureSize = 768;
            var holeRadiusPixels = Mathf.Clamp(
                desiredRadius / coverSize * textureSize,
                48f,
                textureSize * 0.22f);

            _scopeTexture = BuildScopeTexture(
                textureSize,
                holeRadiusPixels,
                scopeLine);
            _scopeLockedTexture = BuildScopeTexture(
                textureSize,
                holeRadiusPixels,
                scopeLockColor);

            _scopeImageRect.sizeDelta = new Vector2(coverSize, coverSize);
            _scopeImage.texture = _scopeTexture;
        }

        private Texture2D BuildScopeTexture(
            int size,
            float radius,
            Color lineColor)
        {
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Schwarz_S3_Scope",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var transparent = Color.clear;
            var aa = 1.35f;
            var ringHalf = 0.45f;

            // Smooth signed-distance circle: soft outer shade + very thin anti-aliased rim.
            for (var y = 0; y < size; y++)
            {
                var dy = y - center;
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var signed = distance - radius;

                    var shadeT = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(-aa, aa, signed));
                    var pixel = Color.Lerp(transparent, scopeShade, shadeT);

                    var ringDistance = Mathf.Abs(signed);
                    var ringT = 1f - Mathf.SmoothStep(
                        ringHalf,
                        ringHalf + aa,
                        ringDistance);
                    if (ringT > 0f)
                        pixel = Color.Lerp(pixel, lineColor, ringT);

                    pixels[y * size + x] = pixel;
                }
            }

            var c = Mathf.RoundToInt(center);
            var r = Mathf.RoundToInt(radius);
            var gapHalf = Mathf.Max(5, Mathf.RoundToInt(radius * 0.055f));

            // Crosshair deliberately stops before the center.
            DrawHorizontal(pixels, size, c, c - r, c - gapHalf, 0, lineColor);
            DrawHorizontal(pixels, size, c, c + gapHalf, c + r, 0, lineColor);
            DrawVertical(pixels, size, c, c - r, c - gapHalf, 0, lineColor);
            DrawVertical(pixels, size, c, c + gapHalf, c + r, 0, lineColor);

            // Just four restrained ticks.
            var tickHalf = Mathf.Max(2, Mathf.RoundToInt(radius * 0.018f));
            var tickDistance = Mathf.RoundToInt(radius * 0.46f);
            DrawVertical(
                pixels, size, c - tickDistance,
                c - tickHalf, c + tickHalf, 0, lineColor);
            DrawVertical(
                pixels, size, c + tickDistance,
                c - tickHalf, c + tickHalf, 0, lineColor);
            DrawHorizontal(
                pixels, size, c - tickDistance,
                c - tickHalf, c + tickHalf, 0, lineColor);
            DrawHorizontal(
                pixels, size, c + tickDistance,
                c - tickHalf, c + tickHalf, 0, lineColor);

            // Small anti-aliased red center point.
            var dotColor = new Color(0.95f, 0.12f, 0.10f, 1f);
            var dotRadius = Mathf.Max(2.2f, radius * 0.018f);
            var dotExtent = Mathf.CeilToInt(dotRadius + aa);
            for (var y = c - dotExtent; y <= c + dotExtent; y++)
            {
                if (y < 0 || y >= size)
                    continue;

                for (var x = c - dotExtent; x <= c + dotExtent; x++)
                {
                    if (x < 0 || x >= size)
                        continue;

                    var d = Vector2.Distance(
                        new Vector2(x, y),
                        new Vector2(center, center));
                    var coverage = 1f - Mathf.SmoothStep(
                        dotRadius - aa,
                        dotRadius + aa,
                        d);
                    if (coverage <= 0f)
                        continue;

                    var index = y * size + x;
                    var baseColor = (Color)pixels[index];
                    pixels[index] = Color.Lerp(baseColor, dotColor, coverage);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawHorizontal(
            Color32[] pixels,
            int size,
            int y,
            int x0,
            int x1,
            int thickness,
            Color32 color)
        {
            x0 = Mathf.Clamp(x0, 0, size - 1);
            x1 = Mathf.Clamp(x1, 0, size - 1);
            y = Mathf.Clamp(y, 0, size - 1);

            for (var yy = y - thickness; yy <= y + thickness; yy++)
            {
                if (yy < 0 || yy >= size)
                    continue;
                for (var x = x0; x <= x1; x++)
                    pixels[yy * size + x] = color;
            }
        }

        private static void DrawVertical(
            Color32[] pixels,
            int size,
            int x,
            int y0,
            int y1,
            int thickness,
            Color32 color)
        {
            y0 = Mathf.Clamp(y0, 0, size - 1);
            y1 = Mathf.Clamp(y1, 0, size - 1);
            x = Mathf.Clamp(x, 0, size - 1);

            for (var xx = x - thickness; xx <= x + thickness; xx++)
            {
                if (xx < 0 || xx >= size)
                    continue;
                for (var y = y0; y <= y1; y++)
                    pixels[y * size + xx] = color;
            }
        }

        private void UpdateScope(Vector2 mousePosition, bool locked)
        {
            if (_scopeRoot == null || _scopeImageRect == null)
                return;

            EnsureScopeTextures();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _scopeRoot,
                mousePosition,
                null,
                out var local);

            _scopeImageRect.anchoredPosition = local;
            if (_scopeImage != null)
                _scopeImage.texture = locked && _scopeLockedTexture != null
                    ? _scopeLockedTexture
                    : _scopeTexture;

            var radius = Mathf.Min(
                _scopeRoot.rect.width,
                _scopeRoot.rect.height) * scopeRadiusScreenHeight;
            if (_statusRect != null)
                _statusRect.anchoredPosition =
                    local + new Vector2(0f, -radius - 20f);

            SetStatus(locked ? "LOCK" : string.Empty, locked);
        }

        private void SetStatus(string value, bool locked)
        {
            if (_statusText == null)
                return;

            _statusText.text = value;
            _statusText.color = locked ? scopeLockColor : scopeLine;
        }

        private void SetScopeVisible(bool visible)
        {
            if (_scopeCanvas != null)
                _scopeCanvas.enabled = visible;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
