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
            switch (Mathf.Abs(comboIndex) % 3)
            {
                case 0:
                    SpawnLayeredSlash(
                        facing,
                        30f,
                        1.55f,
                        0.12f,
                        new Vector2(0.76f, 0.05f),
                        new Color(0.94f, 0.99f, 1f, 1f),
                        new Color(0.20f, 0.75f, 1f, 0.58f),
                        0.14f);
                    break;

                case 1:
                    // Cross-cut: clearly different from hit 1 even though the underlying Spine
                    // source only provides one Attack_Loop clip.
                    SpawnLayeredSlash(
                        facing,
                        -30f,
                        1.82f,
                        0.14f,
                        new Vector2(0.86f, 0.10f),
                        new Color(0.88f, 0.98f, 1f, 1f),
                        new Color(0.18f, 0.72f, 1f, 0.64f),
                        0.16f);
                    SpawnLayeredSlash(
                        facing,
                        18f,
                        1.48f,
                        0.10f,
                        new Vector2(0.82f, 0.02f),
                        new Color(0.72f, 0.94f, 1f, 0.96f),
                        new Color(0.12f, 0.62f, 1f, 0.52f),
                        0.14f,
                        0.025f);
                    break;

                default:
                    // Heavy finisher: wide three-line fan + impact star. This is intentionally
                    // much louder than hits 1/2 so the 1 -> 2 -> HEAVY rhythm reads instantly.
                    SpawnLayeredSlash(
                        facing,
                        7f,
                        2.55f,
                        0.22f,
                        new Vector2(1.10f, 0.08f),
                        new Color(1f, 1f, 1f, 1f),
                        new Color(0.22f, 0.82f, 1f, 0.82f),
                        0.20f);
                    SpawnLayeredSlash(
                        facing,
                        -18f,
                        2.20f,
                        0.13f,
                        new Vector2(1.02f, -0.02f),
                        new Color(0.68f, 0.94f, 1f, 1f),
                        new Color(0.08f, 0.58f, 1f, 0.62f),
                        0.18f,
                        0.018f);
                    SpawnLayeredSlash(
                        facing,
                        27f,
                        1.95f,
                        0.11f,
                        new Vector2(0.98f, 0.17f),
                        new Color(0.82f, 0.97f, 1f, 0.98f),
                        new Color(0.16f, 0.70f, 1f, 0.55f),
                        0.17f,
                        0.038f);
                    SpawnImpactBurst((Vector2)transform.position + new Vector2(1.20f * facing, 0.08f), 0.95f, 1.25f);
                    break;
            }
        }

        public void PlaySwordWave(int facing)
        {
            SpawnLayeredSlash(
                facing,
                0f,
                3.15f,
                0.18f,
                new Vector2(1.55f, 0.10f),
                new Color(0.90f, 1f, 1f, 1f),
                new Color(0.18f, 0.78f, 1f, 0.72f),
                0.20f);
        }

        public void PlayChainLightning(Vector2 from, Vector2 to, float intensity = 1f)
        {
            if (_sprite == null)
                return;

            intensity = Mathf.Clamp(intensity, 0.8f, 1.8f);
            var delta = to - from;
            var length = delta.magnitude;
            if (length <= 0.03f)
                return;

            var direction = delta / length;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var segments = Mathf.Clamp(Mathf.CeilToInt(length / 0.45f), 5, 9);
            var previous = from;

            for (var i = 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var point = Vector2.Lerp(from, to, t);
                if (i < segments)
                {
                    var taper = Mathf.Sin(t * Mathf.PI);
                    point += perpendicular * Random.Range(-0.22f, 0.22f) * taper * intensity;
                }

                SpawnLightningSegment(previous, point, intensity);
                previous = point;
            }

            SpawnImpactBurst(to, 0.68f * intensity, 1.35f);
        }

        private void SpawnLayeredSlash(
            int facing,
            float angle,
            float length,
            float thickness,
            Vector2 offset,
            Color coreColor,
            Color glowColor,
            float duration,
            float delay = 0f)
        {
            SpawnSlash(facing, angle, length, thickness * 2.5f, offset, delay, glowColor, 58, duration * 1.15f);
            SpawnSlash(facing, angle, length, thickness, offset, delay, coreColor, 61, duration);
        }

        private void SpawnSlash(
            int facing,
            float angle,
            float length,
            float thickness,
            Vector2 offset,
            float delay,
            Color color,
            int sortingOrder,
            float duration)
        {
            if (_sprite == null)
                return;

            var root = new GameObject("SlashVFX");
            root.transform.position = transform.position + new Vector3(offset.x * facing, offset.y, -0.1f);
            root.transform.rotation = Quaternion.Euler(0f, 0f, facing > 0 ? angle : 180f - angle);

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
            root.transform.localScale = new Vector3(length, thickness, 1f);
            StartCoroutine(Fade(root, renderer, delay, duration));
        }

        private void SpawnLightningSegment(Vector2 from, Vector2 to, float intensity)
        {
            var delta = to - from;
            var length = Mathf.Max(0.03f, delta.magnitude);
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var midpoint = Vector2.Lerp(from, to, 0.5f);

            SpawnWorldLine(
                "ChainLightningGlow",
                midpoint,
                angle,
                length,
                0.24f * intensity,
                new Color(0.04f, 0.55f, 1f, 0.52f),
                70,
                0.24f);
            SpawnWorldLine(
                "ChainLightningCore",
                midpoint,
                angle,
                length,
                0.085f * intensity,
                new Color(0.94f, 1f, 1f, 1f),
                72,
                0.20f);
        }

        private void SpawnImpactBurst(Vector2 center, float radius, float intensity)
        {
            if (_sprite == null)
                return;

            var rayCount = 7;
            for (var i = 0; i < rayCount; i++)
            {
                var angle = i * (360f / rayCount) + Random.Range(-8f, 8f);
                var rad = angle * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                var length = radius * Random.Range(0.55f, 1f);
                var midpoint = center + direction * length * 0.5f;
                SpawnWorldLine(
                    "ImpactBurstGlow",
                    midpoint,
                    angle,
                    length,
                    0.11f * intensity,
                    new Color(0.10f, 0.70f, 1f, 0.68f),
                    73,
                    0.18f);
                SpawnWorldLine(
                    "ImpactBurstCore",
                    midpoint,
                    angle,
                    length,
                    0.04f * intensity,
                    new Color(0.96f, 1f, 1f, 1f),
                    74,
                    0.15f);
            }
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
                var alpha = 1f - t * t;
                renderer.color = new Color(start.r, start.g, start.b, start.a * alpha);
                root.transform.localScale *= 1f + Time.deltaTime * 0.85f;
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
