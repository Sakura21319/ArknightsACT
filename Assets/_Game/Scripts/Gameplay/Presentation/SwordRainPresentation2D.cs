using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Asset-independent prototype VFX for Texas Sword Rain. The gameplay skill owns damage;
    /// this component only renders telegraph, falling blades and impact/lightning pulses.
    /// </summary>
    public sealed class SwordRainPresentation2D : MonoBehaviour
    {
        private Texture2D _texture;
        private Sprite _sprite;

        private void Awake()
        {
            _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public void PlayCast(float radius)
        {
            if (_sprite == null)
                return;

            var center = (Vector2)transform.position;
            SpawnGroundTelegraph(center, radius);

            const int swordCount = 13;
            for (var i = 0; i < swordCount; i++)
            {
                var t = swordCount <= 1 ? 0.5f : i / (float)(swordCount - 1);
                var x = Mathf.Lerp(-radius, radius, t) + Random.Range(-0.16f, 0.16f);
                var target = center + new Vector2(x, -0.55f + Random.Range(-0.04f, 0.08f));
                StartCoroutine(DropSword(target, i * 0.012f + Random.Range(0f, 0.018f)));
            }
        }

        /// <summary>Visible pulse aligned to each authoritative Sword Rain damage wave.</summary>
        public void PlayImpact(Vector2 center, float radius, float intensity = 1f)
        {
            if (_sprite == null)
                return;

            intensity = Mathf.Clamp(intensity, 0.75f, 1.35f);
            var origin = center + Vector2.down * 0.42f;

            SpawnFadingQuad(
                "SwordRainImpactFloor",
                origin,
                new Vector3(radius * 2.05f, 0.14f * intensity, 1f),
                0f,
                new Color(0.20f, 0.82f, 1f, 0.78f),
                64,
                0.20f);

            SpawnFadingQuad(
                "SwordRainImpactCore",
                origin + Vector2.up * 0.18f,
                new Vector3(radius * 1.25f, 0.06f, 1f),
                0f,
                new Color(0.92f, 0.98f, 1f, 0.98f),
                66,
                0.12f);

            const int rayCount = 10;
            for (var i = 0; i < rayCount; i++)
            {
                var angle = i * (180f / rayCount) + Random.Range(-6f, 6f);
                var length = radius * Random.Range(0.65f, 1.0f) * intensity;
                SpawnFadingQuad(
                    "SwordRainLightningRay",
                    origin + Vector2.up * 0.22f,
                    new Vector3(length, Random.Range(0.025f, 0.055f), 1f),
                    angle,
                    new Color(0.45f, 0.90f, 1f, 0.82f),
                    65,
                    Random.Range(0.10f, 0.18f));
            }
        }

        public void PlayFieldPulse(Vector2 center, float radius)
        {
            if (_sprite == null)
                return;

            SpawnFadingQuad(
                "ThunderFieldPulse",
                center + Vector2.down * 0.55f,
                new Vector3(radius * 2f, 0.09f, 1f),
                0f,
                new Color(0.30f, 0.82f, 1f, 0.60f),
                45,
                0.18f);
        }

        private void SpawnGroundTelegraph(Vector2 center, float radius)
        {
            SpawnFadingQuad(
                "SwordRainTelegraph",
                center + Vector2.down * 0.56f,
                new Vector3(radius * 2.15f, 0.08f, 1f),
                0f,
                new Color(0.30f, 0.70f, 1f, 0.55f),
                48,
                0.30f);

            for (var i = 0; i < 5; i++)
            {
                var x = Mathf.Lerp(-radius * 0.8f, radius * 0.8f, i / 4f);
                SpawnFadingQuad(
                    "SwordRainTelegraphMarker",
                    center + new Vector2(x, 0.15f),
                    new Vector3(0.035f, 1.25f, 1f),
                    0f,
                    new Color(0.42f, 0.86f, 1f, 0.30f),
                    47,
                    0.26f);
            }
        }

        private IEnumerator DropSword(Vector2 target, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (_sprite == null)
                yield break;

            var start = target + new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(3.0f, 3.65f));
            var root = new GameObject("SwordRainBlade");
            root.transform.position = start;
            root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-13f, -5f));
            root.transform.localScale = new Vector3(0.055f, 1.42f, 1f);

            var core = root.AddComponent<SpriteRenderer>();
            core.sprite = _sprite;
            core.sortingOrder = 60;
            core.color = new Color(0.94f, 0.99f, 1f, 1f);

            var glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(root.transform, false);
            glowObject.transform.localScale = new Vector3(3.1f, 1.08f, 1f);
            var glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = _sprite;
            glow.sortingOrder = 59;
            glow.color = new Color(0.25f, 0.78f, 1f, 0.35f);

            var elapsed = 0f;
            const float duration = 0.14f;
            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                // Ease into the impact so the blade reads as a fast downward strike.
                var eased = t * t;
                root.transform.position = Vector2.Lerp(start, target, eased);
                yield return null;
            }

            if (root != null)
            {
                root.transform.position = target;
                SpawnImpactSpark(target);
                Destroy(root, 0.055f);
            }
        }

        private void SpawnImpactSpark(Vector2 position)
        {
            SpawnFadingQuad(
                "SwordRainSparkH",
                position,
                new Vector3(0.70f, 0.055f, 1f),
                0f,
                new Color(0.80f, 0.96f, 1f, 0.92f),
                63,
                0.10f);
            SpawnFadingQuad(
                "SwordRainSparkV",
                position + Vector2.up * 0.16f,
                new Vector3(0.045f, 0.72f, 1f),
                0f,
                new Color(0.58f, 0.90f, 1f, 0.88f),
                63,
                0.11f);
        }

        private void SpawnFadingQuad(
            string objectName,
            Vector2 position,
            Vector3 scale,
            float zRotation,
            Color color,
            int sortingOrder,
            float duration)
        {
            if (_sprite == null)
                return;

            var go = new GameObject(objectName);
            go.transform.position = new Vector3(position.x, position.y, -0.05f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
            go.transform.localScale = scale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
            StartCoroutine(FadeAndDestroy(go, renderer, duration));
        }

        private static IEnumerator FadeAndDestroy(GameObject go, SpriteRenderer renderer, float duration)
        {
            if (go == null || renderer == null)
                yield break;

            var start = renderer.color;
            var elapsed = 0f;
            duration = Mathf.Max(0.02f, duration);
            while (elapsed < duration && go != null && renderer != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                renderer.color = new Color(start.r, start.g, start.b, start.a * (1f - t));
                yield return null;
            }

            if (go != null)
                Destroy(go);
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
