using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    [RequireComponent(typeof(CombatEntity), typeof(Rigidbody2D))]
    public sealed class DummyEnemy : MonoBehaviour
    {
        [SerializeField] private float destroyDelayOnDeath = 1.35f;

        private CombatEntity _entity;
        private bool _dead;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _entity.SetTeam(Team.Enemy);
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

            var brain = GetComponent<PrototypeEnemyCombatBrain2D>();
            if (brain != null)
                brain.enabled = false;

            var reaction = GetComponent<EnemyHitReaction2D>();
            if (reaction != null)
                reaction.enabled = false;

            var collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;

            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            // EnemyPresentationDriver receives the same Health.Died event and plays Die.
            // Keep the GameObject alive long enough for the PRTS death clip to be visible.
            Destroy(gameObject, Mathf.Max(0.8f, destroyDelayOnDeath));
        }
    }
}
