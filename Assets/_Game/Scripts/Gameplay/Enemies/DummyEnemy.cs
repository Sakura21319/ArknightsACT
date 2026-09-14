using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    [RequireComponent(typeof(CombatEntity), typeof(Rigidbody2D))]
    public sealed class DummyEnemy : MonoBehaviour
    {
        [SerializeField] private float destroyDelayOnDeath = 0.6f;

        private CombatEntity _entity;

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
            var collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;

            Destroy(gameObject, destroyDelayOnDeath);
        }
    }
}
