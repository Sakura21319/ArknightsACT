using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Small procedural ellipse under upright 2D characters. The shadow is intentionally a
    /// presentation-only child and makes side-view Spine rigs read as standing on a 3/4 floor.
    /// </summary>
    public sealed class TopDownGroundShadow2D : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new(0.82f, 0.26f);
        [SerializeField] private Vector2 localOffset = new(0f, -0.08f);
        [SerializeField, Range(0f, 1f)] private float alpha = 0.30f;

        private GameObject _shadowObject;
        private Texture2D _texture;
        private Sprite _sprite;

        public void Configure(Vector2 newSize, Vector2 newOffset, float newAlpha = 0.30f)
        {
            size = newSize;
            localOffset = newOffset;
            alpha = Mathf.Clamp01(newAlpha);
            ApplyLayout();
        }

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (_shadowObject != null)
                return;

            _shadowObject = new GameObject("GroundShadow");
            _shadowObject.transform.SetParent(transform, false);

            const int width = 48;
            const int height = 20;
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "TopDownGroundShadowTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = (x + 0.5f) / width * 2f - 1f;
                    var ny = (y + 0.5f) / height * 2f - 1f;
                    var r = nx * nx + ny * ny;
                    var edge = Mathf.Clamp01((1f - r) * 3.4f);
                    _texture.SetPixel(x, y, new Color(0f, 0f, 0f, edge));
                }
            }
            _texture.Apply();

            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                48f);

            var renderer = _shadowObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.color = new Color(0f, 0f, 0f, alpha);
            renderer.sortingOrder = -12;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (_shadowObject == null)
                return;
            _shadowObject.transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            _shadowObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = _shadowObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.color = new Color(0f, 0f, 0f, alpha);
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
