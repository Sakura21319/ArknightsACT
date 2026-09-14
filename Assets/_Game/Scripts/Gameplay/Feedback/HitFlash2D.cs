using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.Feedback
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HitFlash2D : MonoBehaviour
    {
        [SerializeField] private float duration = 0.06f;

        private SpriteRenderer _renderer;
        private Color _baseColor;
        private Coroutine _routine;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _baseColor = _renderer.color;
        }

        public void Flash()
        {
            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _renderer.color = Color.white;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _renderer.color = _baseColor;
            _routine = null;
        }
    }
}
