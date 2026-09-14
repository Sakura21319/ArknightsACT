using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlaceholderVisual2D : MonoBehaviour
    {
        [SerializeField] private Color color = new(0.25f, 0.65f, 0.95f, 1f);

        private Texture2D _texture;
        private Sprite _sprite;

        public void SetColor(Color value)
        {
            color = value;
            if (TryGetComponent<SpriteRenderer>(out var renderer))
                renderer.color = color;
        }

        private void Awake()
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer.sprite != null)
                return;

            _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "RuntimePlaceholderTexture",
                filterMode = FilterMode.Point
            };
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();

            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            renderer.sprite = _sprite;
            renderer.color = color;
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
