using System;
using UnityEngine;

namespace ArknightsACT.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        public event Action<float, float> Changed;
        public event Action Died;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, value);
            if (refill)
                CurrentHealth = maxHealth;
            else
                CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);

            Changed?.Invoke(CurrentHealth, maxHealth);
        }

        public float TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return 0f;

            var before = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            var dealt = before - CurrentHealth;

            Changed?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0f)
                Died?.Invoke();

            return dealt;
        }

        public float Heal(float amount)
        {
            if (IsDead || amount <= 0f)
                return 0f;

            var before = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            var healed = CurrentHealth - before;

            Changed?.Invoke(CurrentHealth, maxHealth);
            return healed;
        }
    }
}
