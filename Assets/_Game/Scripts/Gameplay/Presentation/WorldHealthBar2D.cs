using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Lightweight world-space health bar for the prototype. Presentation only: it observes
    /// Health and never owns combat state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class WorldHealthBar2D : MonoBehaviour
    {
        private CombatEntity _entity;
        private GameObject _root;
        private Transform _fill;
        private Texture2D _texture;
        private Sprite _sprite;
        private float _innerWidth;
        private float _innerHeight;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            BuildVisual();
        }

        private void OnEnable()
        {
            if (_entity == null)
                _entity = GetComponent<CombatEntity>();
            if (_entity?.Health == null)
                return;

            _entity.Health.Changed += OnHealthChanged;
            _entity.Health.Died += OnDied;
            SyncFromHealth();
        }

        private void Start()
        {
            // Component Awake/OnEnable ordering is not guaranteed across siblings. Health.Awake
            // may initialize CurrentHealth after this component first observes it, so sync once
            // more after every Awake has completed to guarantee the initial full bar is visible.
            SyncFromHealth();
        }

        private void OnDisable()
        {
            if (_entity?.Health == null)
                return;
            _entity.Health.Changed -= OnHealthChanged;
            _entity.Health.Died -= OnDied;
        }

        private void SyncFromHealth()
        {
            if (_entity?.Health == null)
                return;
            UpdateVisual(_entity.Health.CurrentHealth, _entity.Health.MaxHealth);
        }

        private void OnHealthChanged(float current, float max) => UpdateVisual(current, max);

        private void OnDied()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        private void BuildVisual()
        {
            _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{name}_HealthBarPixel"
            };
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            var isPlayer = _entity != null && _entity.Team == Team.Player;
            var width = isPlayer ? 1.48f : 1.08f;
            var height = isPlayer ? 0.11f : 0.085f;
            var border = isPlayer ? 0.025f : 0.020f;
            var y = isPlayer ? 1.24f : 1.08f;

            _root = new GameObject("WorldHealthBar");
            _root.transform.SetParent(transform, false);
            _root.transform.localPosition = new Vector3(0f, y, -0.08f);

            var background = new GameObject("Background");
            background.transform.SetParent(_root.transform, false);
            background.transform.localScale = new Vector3(width, height, 1f);
            var backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = _sprite;
            backgroundRenderer.color = new Color(0.04f, 0.045f, 0.055f, 0.92f);
            backgroundRenderer.sortingOrder = 82;

            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(_root.transform, false);
            _fill = fillObject.transform;
            var fillRenderer = fillObject.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = _sprite;
            fillRenderer.color = isPlayer
                ? new Color(0.22f, 0.86f, 0.48f, 0.98f)
                : new Color(0.92f, 0.20f, 0.20f, 0.98f);
            fillRenderer.sortingOrder = 83;

            _innerWidth = Mathf.Max(0.01f, width - border * 2f);
            _innerHeight = Mathf.Max(0.01f, height - border * 2f);
        }

        private void UpdateVisual(float current, float max)
        {
            if (_root == null || _fill == null)
                return;

            var ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            var fillWidth = _innerWidth * ratio;
            _fill.localScale = new Vector3(Mathf.Max(0.001f, fillWidth), _innerHeight, 1f);
            _fill.localPosition = new Vector3(
                -_innerWidth * 0.5f + fillWidth * 0.5f,
                0f,
                -0.01f);
            _root.SetActive(current > 0f);
        }

        private void OnDestroy()
        {
            if (_sprite != null)
                Destroy(_sprite);
            if (_texture != null)
                Destroy(_texture);
        }
    }
}
