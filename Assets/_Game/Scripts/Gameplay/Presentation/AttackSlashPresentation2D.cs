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
                case 0:
                    SpawnSlash(facing, 28f, 1.35f, 0.08f, new Vector2(0.70f, 0.05f));
                    break;
                case 1:
                    SpawnSlash(facing, -24f, 1.50f, 0.09f, new Vector2(0.76f, 0.08f), 0f, new Color(0.72f, 0.94f, 1f, 0.94f));
                    break;
                case 2:
                    SpawnSlash(facing, 4f, 2.15f, 0.15f, new Vector2(1.00f, 0.10f), 0f, new Color(0.90f, 0.98f, 1f, 1f));
                    SpawnSlash(facing, -14f, 1.80f, 0.075f, new Vector2(0.92f, -0.02f), 0.025f, new Color(0.40f, 0.86f, 1f, 0.72f));
                    break;
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

        public void PlayChainLightning(Vector2 from, Vector2 to, float intensity = 1f)
        {
            if (_sprite == null)
                return;

            intensity = Mathf.Clamp(intensity, 0.75f, 1.5f);
            var delta = to - from;
            var length = Mathf.Max(0.05f, delta.magnitude);
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var midpoint = Vector2.Lerp(from, to, 0.5f);

            SpawnWorldLine(
                "ChainLightningCore",
                midpoint,
                angle,
                length,
                0.045f * intensity,
                new Color(0.92f, 0.99f, 1f, 1f),
                68,
                0.10f);
            SpawnWorldLine(
                "ChainLightningGlow",
                midpoint,
                angle,
                length,
                0.12f * intensity,
                new Color(0.25f, 0.78f, 1f, 0.38f),
                67,
                0.13f);

            var perpendicular = delta.sqrMagnitude > 0.001f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;
            var branchPoint = midpoint + perpendicular * Random.Range(-0.22f, 0.22f);
            var branchEnd = branchPoint + perpendicular * Random.Range(-0.35f, 0.35f) + delta.normalized * 0.22f;
            var branchDelta = branchEnd - branchPoint;
            var branchAngle = Mathf.Atan2(branchDelta.y, branchDelta.x) * Mathf.Rad2Deg;
            SpawnWorldLine(
                "ChainLightningBranch",
                Vector2.Lerp(branchPoint, branchEnd, 0.5f),
                branchAngle,
                branchDelta.magnitude,
                0.035f * intensity,
                new Color(0.50f, 0.90f, 1f, 0.72f),
                68,
                0.09f);
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

        private void SpawnWorldLine(
            string name,
            Vector2 position,
            float angle,
            float length,
            float thickness,
            Color color,
            int sortingOrder,
            float duration)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(position.x, position.y, -0.12f);
            root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            root.transform.localScale = new Vector3(length, thickness, 1f);

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
            StartCoroutine(Fade(root, renderer, 0f, duration));
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
            if (_sprite != null)
                Destroy(_sprite);
            if (_texture != null)
                Destroy(_texture);
        }
    }
}
