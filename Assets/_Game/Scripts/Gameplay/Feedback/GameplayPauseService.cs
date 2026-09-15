using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Feedback
{
    /// <summary>
    /// Central owner of gameplay pause state. Multiple systems may hold a pause at the same
    /// time (reward UI, hit-stop, future shop/event/menu) without overwriting each other.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayPauseService : MonoBehaviour
    {
        public static GameplayPauseService Instance { get; private set; }

        private readonly HashSet<object> _owners = new();
        private float _baseFixedDelta;

        public bool IsPaused => _owners.Count > 0;
        public int PauseOwnerCount => _owners.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _baseFixedDelta = Time.fixedDeltaTime;
            ApplyTimeState();
        }

        public void Pause(object owner)
        {
            if (owner == null)
                return;
            if (_owners.Add(owner))
                ApplyTimeState();
        }

        public void Resume(object owner)
        {
            if (owner == null)
                return;
            if (_owners.Remove(owner))
                ApplyTimeState();
        }

        public void ClearAll()
        {
            if (_owners.Count == 0)
                return;
            _owners.Clear();
            ApplyTimeState();
        }

        private void ApplyTimeState()
        {
            Time.timeScale = IsPaused ? 0f : 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            _owners.Clear();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
            Instance = null;
        }
    }
}
