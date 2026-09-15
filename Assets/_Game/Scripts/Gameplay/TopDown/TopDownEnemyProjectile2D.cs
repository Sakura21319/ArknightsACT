using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    public sealed class TopDownEnemyProjectile2D : MonoBehaviour
    {
        private CombatEntity _source;
        private Vector2 _direction;
        private float _speed;
        private float _damage;
        private float _expiresAt;

        public static void Spawn(CombatEntity source, Vector2 origin, Vector2 direction, float speed, float damage)
        {
            if (source == null || direction.sqrMagnitude <= 0.001f)
                return;

            var go = new GameObject("TopDownEnemyProjectile");
            go.transform.position = origin;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.10f;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1600;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(new Color(1f, 0.42f, 0.28f));

            var projectile = go.AddComponent<TopDownEnemyProjectile2D>();
            projectile._source = source;
            projectile._direction = direction.normalized;
            projectile._speed = Mathf.Max(0.1f, speed);
            projectile._damage = Mathf.Max(0f, damage);
            projectile._expiresAt = Time.time + 3f;
        }

        private void Update()
        {
            transform.position += (Vector3)(_direction * _speed * Time.deltaTime);
            if (Time.time >= _expiresAt)
                Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_source == null || other == null)
                return;

            var target = other.GetComponentInParent<CombatEntity>();
            if (target == null || target == _source || target.Team == _source.Team ||
                target.Health == null || target.Health.IsDead)
                return;

            var context = new DamageContext(
                _source,
                _source,
                target,
                _damage,
                DamageType.Physical,
                _direction * 0.8f,
                sourceId: "TopDown_Enemy_Projectile");
            if (DamageSystem.Apply(context).Applied)
                Destroy(gameObject);
        }
    }
}
