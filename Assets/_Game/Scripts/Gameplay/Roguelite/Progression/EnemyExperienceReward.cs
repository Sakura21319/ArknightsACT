using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyExperienceReward : MonoBehaviour
    {
        [SerializeField, Min(0)] private int baseExperience = 28;
        [SerializeField, Min(0f)] private float rewardMultiplier = 1f;

        private Health _health;
        private bool _granted;

        public int BaseExperience => baseExperience;
        public float RewardMultiplier => rewardMultiplier;

        public void Configure(int experience)
        {
            baseExperience = Mathf.Max(0, experience);
        }

        public void SetRewardMultiplier(float multiplier)
        {
            rewardMultiplier = Mathf.Max(0f, multiplier);
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _granted = false;
            _health ??= GetComponent<Health>();
            if (_health != null)
                _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (_granted)
                return;
            _granted = true;

            var amount = Mathf.Max(0, Mathf.RoundToInt(baseExperience * rewardMultiplier));
            if (amount <= 0)
                return;

            var run = RogueliteRunState.Instance;
            if (run == null)
            {
                Debug.LogWarning("[ArknightsACT/Progression] Enemy died before RogueliteRunState was available.", this);
                return;
            }

            run.AddExperience(amount);
            Debug.Log($"[ArknightsACT/Progression] +{amount} EXP from {name}.", this);
        }
    }
}
