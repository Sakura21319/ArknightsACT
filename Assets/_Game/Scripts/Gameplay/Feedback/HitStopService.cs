using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.Feedback
{
    public sealed class HitStopService : MonoBehaviour
    {
        public static HitStopService Instance { get; private set; }

        private Coroutine _routine;
        private GameplayPauseService _pause;

        public bool IsActive => _routine != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _pause = GameplayPauseService.Instance;
            if (_pause == null)
                _pause = GetComponent<GameplayPauseService>();
            if (_pause == null)
                _pause = gameObject.AddComponent<GameplayPauseService>();
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

            _pause?.Resume(this);
        }

        private IEnumerator Routine(float duration)
        {
            _pause?.Pause(this);
            yield return new WaitForSecondsRealtime(duration);
            _pause?.Resume(this);
            _routine = null;
        }
    }
}
