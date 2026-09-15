using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class TopDownEnemyLifecycle2D : MonoBehaviour
    {
        [SerializeField, Min(0.4f)] private float destroyDelay = 1.2f;
        private CombatEntity _entity;
        private bool _dead;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            if (_entity != null && _entity.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_entity != null && _entity.Health != null)
                _entity.Health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (_dead)
                return;
            _dead = true;

            var brain = GetComponent<TopDownEnemyBrain2D>();
            if (brain != null)
                brain.enabled = false;

            var collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;

            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }

            Destroy(gameObject, destroyDelay);
        }
    }
}
