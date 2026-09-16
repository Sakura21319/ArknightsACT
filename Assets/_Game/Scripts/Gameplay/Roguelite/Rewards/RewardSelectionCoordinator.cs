using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Rewards
{
    /// <summary>
    /// Grants exclusive ownership of the full-screen reward-selection surface.
    /// Individual reward controllers keep their own request queues and only open UI
    /// after acquiring this coordinator, preventing level-up / skill / collectible screens
    /// from appearing on top of one another.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardSelectionCoordinator : MonoBehaviour
    {
        public static RewardSelectionCoordinator Instance { get; private set; }

        private object _owner;

        public bool IsBusy => _owner != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public bool TryAcquire(object owner)
        {
            if (owner == null)
                return false;
            if (ReferenceEquals(_owner, owner))
                return true;
            if (_owner != null)
                return false;

            _owner = owner;
            return true;
        }

        public void Release(object owner)
        {
            if (owner != null && ReferenceEquals(_owner, owner))
                _owner = null;
        }

        public bool IsOwnedBy(object owner) =>
            owner != null && ReferenceEquals(_owner, owner);

        private void OnDestroy()
        {
            if (Instance != this)
                return;
            _owner = null;
            Instance = null;
        }
    }
}
