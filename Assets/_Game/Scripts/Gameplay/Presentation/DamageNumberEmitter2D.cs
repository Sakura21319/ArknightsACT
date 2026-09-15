using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>Spawns lightweight floating damage numbers from CombatEntity.Damaged events.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class DamageNumberEmitter2D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 0.72f;
        [SerializeField, Min(0.1f)] private float riseSpeed = 1.05f;

        private CombatEntity _entity;
        private Font _font;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnEnable()
        {
            if (_entity == null)
                _entity = GetComponent<CombatEntity>();
            if (_entity != null)
                _entity.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_entity != null)
                _entity.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageContext context, DamageResult result)
        {
            if (!result.Applied || result.Damage <= 0f)
                return;

            var go = new GameObject("DamageNumber");
            var isPlayer = _entity != null && _entity.Team == Team.Player;
            go.transform.position = transform.position + new Vector3(
                Random.Range(-0.12f, 0.12f),
                isPlayer ? 1.42f : 1.27f,
                -0.12f);

            var text = go.AddComponent<TextMesh>();
            text.text = Mathf.Max(1, Mathf.RoundToInt(result.Damage)).ToString();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.032f;
            text.fontStyle = FontStyle.Bold;
            text.color = ResolveColor(context.DamageType);
            if (_font != null)
                text.font = _font;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 100;
                if (_font != null)
                    renderer.sharedMaterial = _font.material;
            }

            var floater = go.AddComponent<FloatingDamageNumber2D>();
            floater.Configure(text, lifetime, new Vector2(Random.Range(-0.10f, 0.10f), riseSpeed));
        }

        private static Color ResolveColor(DamageType type) => type switch
        {
            DamageType.Arts => new Color(0.60f, 0.72f, 1.00f, 1f),
            DamageType.True => new Color(1.00f, 0.82f, 0.28f, 1f),
            _ => Color.white
        };
    }

    public sealed class FloatingDamageNumber2D : MonoBehaviour
    {
        private TextMesh _text;
        private float _lifetime;
        private Vector2 _velocity;
        private float _elapsed;
        private Color _baseColor;

        public void Configure(TextMesh text, float lifetime, Vector2 velocity)
        {
            _text = text;
            _lifetime = Mathf.Max(0.1f, lifetime);
            _velocity = velocity;
            _baseColor = _text != null ? _text.color : Color.white;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            transform.position += (Vector3)(_velocity * dt);

            if (_text != null)
            {
                var normalized = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _lifetime));
                var fade = 1f - Mathf.Clamp01((normalized - 0.45f) / 0.55f);
                var color = _baseColor;
                color.a *= fade;
                _text.color = color;
            }

            if (_elapsed >= _lifetime)
                Destroy(gameObject);
        }
    }
}
