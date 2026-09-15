using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.Feedback
{
    public sealed class HitStopService : MonoBehaviour
    {
        public static HitStopService Instance { get; private set; }

        private Coroutine _routine;
        private float _baseFixedDelta;

        public bool IsActive => _routine != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _baseFixedDelta = Time.fixedDeltaTime;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Cancel();
                Instance = null;
            }
        }

        public void Request(float duration)
        {
            if (duration <= 0f)
                return;

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(Routine(duration));
        }

        public void Cancel()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            RestoreNormalTime();
        }

        private IEnumerator Routine(float duration)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            RestoreNormalTime();
            _routine = null;
        }

        private void RestoreNormalTime()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }
    }
}
