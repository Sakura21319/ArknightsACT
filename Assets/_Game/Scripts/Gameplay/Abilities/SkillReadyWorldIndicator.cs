using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.UI;

namespace ArknightsACT.Gameplay.Abilities
{
    /// <summary>
    /// Restores the original manual-skill-ready presentation:
    /// static ready mark + expanding/fading background pulse.
    /// One ready skill uses the normal yellow state; two ready skills use the enhanced orange state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillReadyWorldIndicator : MonoBehaviour
    {
        private const string ReadyMarkResource = "UI/HUD/BattleSkillReady/sprite_skill_ready";
        private const string ReadyPulseResource = "UI/HUD/BattleSkillReady/sprite_skill_bg";

        // Values reconstructed from the extracted battle_skill_ready prefab.
        private const float PulseDuration = 0.5f;
        private const float PulseFromSize = 47f;
        private const float PulseToSize = 112f;
        private const float ReadyMarkSize = 58f;

        private static readonly Color SingleReadyColor =
            new(255f / 255f, 217f / 255f, 0f, 1f);

        private static readonly Color DoubleReadyColor =
            new(255f / 255f, 108f / 255f, 0f, 1f);

        private RectTransform _root;
        private Image _mark;
        private Image _pulse;
        private CanvasGroup _pulseGroup;
        private Sprite _markSprite;
        private Sprite _pulseSprite;
        private int _readyCount;
        private Vector3 _baseScale = Vector3.one * 0.0068f;

        private void Awake() => Build();

        public void SetReadyCount(int count)
        {
            count = Mathf.Clamp(count, 0, 2);
            _readyCount = count;

            if (_root == null)
                Build();
            if (_root == null)
                return;

            if (count <= 0 || _markSprite == null || _pulseSprite == null)
            {
                _root.gameObject.SetActive(false);
                return;
            }

            var color = count >= 2 ? DoubleReadyColor : SingleReadyColor;
            // Keep the extracted ready mark in its original colors; only the pulse layer is tinted.
            _mark.color = Color.white;
            _pulse.color = color;
            _root.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (_root == null || !_root.gameObject.activeSelf)
                return;

            var flow = RogueliteGameFlowController.Instance;
            var health = GetComponent<Health>();
            if ((flow != null && !flow.IsRunning) || (health != null && health.IsDead))
            {
                _root.gameObject.SetActive(false);
                return;
            }

            var camera = Camera.main;
            if (camera != null)
                _root.rotation = camera.transform.rotation;

            var t = Mathf.Repeat(Time.unscaledTime, PulseDuration) / PulseDuration;
            // Extracted easeType 6 corresponds to the original OutQuad-style expansion.
            var eased = 1f - (1f - t) * (1f - t);
            var size = Mathf.Lerp(PulseFromSize, PulseToSize, eased);
            _pulse.rectTransform.sizeDelta = new Vector2(size, size);
            _pulseGroup.alpha = 1f - eased;
            _root.localScale = _baseScale;
        }

        private void Build()
        {
            if (_root != null)
                return;

            _markSprite = Resources.Load<Sprite>(ReadyMarkResource);
            _pulseSprite = Resources.Load<Sprite>(ReadyPulseResource);

            var go = new GameObject("SkillReadyMarker", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            _root = go.GetComponent<RectTransform>();
            _root.sizeDelta = new Vector2(PulseToSize, PulseToSize);
            _root.localPosition = new Vector3(0f, 2.26f, 0f);
            _root.localScale = _baseScale;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 520;

            var pulseGo = new GameObject("Pulse", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            pulseGo.transform.SetParent(go.transform, false);
            _pulse = pulseGo.GetComponent<Image>();
            _pulse.sprite = _pulseSprite;
            _pulse.preserveAspect = true;
            _pulse.raycastTarget = false;
            _pulse.rectTransform.sizeDelta = new Vector2(PulseFromSize, PulseFromSize);
            _pulseGroup = pulseGo.GetComponent<CanvasGroup>();
            _pulseGroup.blocksRaycasts = false;
            _pulseGroup.interactable = false;

            var markGo = new GameObject("ReadyMark", typeof(RectTransform), typeof(Image));
            markGo.transform.SetParent(go.transform, false);
            _mark = markGo.GetComponent<Image>();
            _mark.sprite = _markSprite;
            _mark.preserveAspect = true;
            _mark.raycastTarget = false;
            // The selected original sprite_skill_ready__-3542339109505237889 asset is 72x72.
            // Display it at the prefab's compact ready-mark scale.
            _mark.rectTransform.sizeDelta = new Vector2(ReadyMarkSize, ReadyMarkSize);

            go.SetActive(false);
        }
    }
}
