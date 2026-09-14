using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    public sealed class SwordRainPresentation2D : MonoBehaviour
    {
        private Texture2D _texture;
        private Sprite _sprite;

        private void Awake()
        {
            _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public void PlayCast(float radius)
        {
            for (var i = 0; i < 9; i++)
            {
                var x = Mathf.Lerp(-radius, radius, i / 8f) + Random.Range(-0.18f, 0.18f);
                StartCoroutine(DropSword(transform.position + new Vector3(x, 2.7f + Random.Range(0f, 0.7f), -0.2f), i * 0.018f));
            }
        }

        public void PlayFieldPulse(Vector2 center, float radius)
        {
            if (_sprite == null)
                return;

            var go = new GameObject("ThunderFieldPulse");
            go.transform.position = new Vector3(center.x, center.y - 0.55f, -0.05f);
            go.transform.localScale = new Vector3(radius * 2f, 0.09f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = 45;
            renderer.color = new Color(0.30f, 0.82f, 1f, 0.60f);
            StartCoroutine(FadePulse(go, renderer, 0.18f));
        }

        private IEnumerator DropSword(Vector3 start, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            var go = new GameObject("SwordRainVFX");
            go.transform.position = start;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -8f);
            go.transform.localScale = new Vector3(0.09f, 1.15f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = 55;
            renderer.color = new Color(0.56f, 0.91f, 1f, 0.96f);

            var from = start;
            var to = start + Vector3.down * 2.55f;
            var elapsed = 0f;
            const float duration = 0.13f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                go.transform.position = Vector3.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            Destroy(go, 0.06f);
        }

        private static IEnumerator FadePulse(GameObject go, SpriteRenderer renderer, float duration)
        {
            var start = renderer.color;
            var elapsed = 0f;
            while (elapsed < duration && go != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                renderer.color = new Color(start.r, start.g, start.b, start.a * (1f - t));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
    }
}
