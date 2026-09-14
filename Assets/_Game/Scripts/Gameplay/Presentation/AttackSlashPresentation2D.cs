using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    public sealed class AttackSlashPresentation2D : MonoBehaviour
    {
        private Texture2D _texture;
        private Sprite _sprite;

        private void Awake()
        {
            _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "TexasSlashRuntimeTexture",
                filterMode = FilterMode.Bilinear
            };
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public void PlayBasic(int comboIndex, int facing)
        {
            switch (comboIndex)
            {
                case 0: SpawnSlash(facing, 28f, 1.35f, 0.08f, new Vector2(0.70f, 0.05f)); break;
                case 1: SpawnSlash(facing, -24f, 1.45f, 0.08f, new Vector2(0.75f, 0.08f)); break;
                case 2: SpawnSlash(facing, 3f, 1.65f, 0.09f, new Vector2(0.82f, 0.10f)); break;
                default:
                    SpawnSlash(facing, 42f, 1.85f, 0.11f, new Vector2(0.88f, 0.14f));
                    SpawnSlash(facing, 0f, 1.55f, 0.075f, new Vector2(0.82f, 0.05f), 0.025f);
                    SpawnSlash(facing, -38f, 1.75f, 0.085f, new Vector2(0.88f, -0.05f), 0.045f);
                    break;
            }
        }

        public void PlaySwordWave(int facing)
        {
            SpawnSlash(facing, 0f, 2.6f, 0.13f, new Vector2(1.35f, 0.10f), 0f, new Color(0.55f, 0.95f, 1f, 0.95f));
        }

        private void SpawnSlash(int facing, float angle, float length, float thickness, Vector2 offset, float delay = 0f, Color? color = null)
        {
            if (_sprite == null)
                return;

            var root = new GameObject("SlashVFX");
            root.transform.position = transform.position + new Vector3(offset.x * facing, offset.y, -0.1f);
            root.transform.rotation = Quaternion.Euler(0f, 0f, facing > 0 ? angle : 180f - angle);

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = 60;
            renderer.color = color ?? new Color(0.78f, 0.94f, 1f, 0.92f);
            root.transform.localScale = new Vector3(length, thickness, 1f);
            StartCoroutine(Fade(root, renderer, delay, 0.10f));
        }

        private static IEnumerator Fade(GameObject root, SpriteRenderer renderer, float delay, float duration)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            var start = renderer.color;
            var elapsed = 0f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                renderer.color = new Color(start.r, start.g, start.b, start.a * (1f - t));
                root.transform.localScale *= 1f + Time.deltaTime * 1.8f;
                yield return null;
            }

            if (root != null)
                Destroy(root);
        }

        private void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
    }
}
