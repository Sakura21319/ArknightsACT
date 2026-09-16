using System.Collections.Generic;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Fixed-direction environmental ballista. Projectiles stop on world geometry and damage the
    /// first player actor in their lane. This mirrors the readable lane-pressure role of Arknights
    /// ballistae while remaining dodgeable in real-time 2.5D combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BallistaHazard25D : MonoBehaviour
    {
        [SerializeField] private Vector3 fireDirection = Vector3.right;
        [SerializeField, Min(0.2f)] private float fireInterval = 2.4f;
        [SerializeField, Min(1f)] private float projectileSpeed = 12f;
        [SerializeField, Min(1f)] private float projectileRange = 18f;
        [SerializeField, Min(0.01f)] private float projectileRadius = 0.14f;
        [SerializeField, Min(0f)] private float damage = 16f;
        [SerializeField] private Material projectileMaterial;

        private readonly List<Bolt> _bolts = new();
        private float _nextFireAt;

        private sealed class Bolt
        {
            public GameObject Visual;
            public Vector3 Position;
            public float Travelled;
        }

        public void Configure(Vector3 direction, Material boltMaterial, float interval = 2.4f, float boltDamage = 16f)
        {
            direction.y = 0f;
            fireDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            projectileMaterial = boltMaterial;
            fireInterval = Mathf.Max(0.2f, interval);
            damage = Mathf.Max(0f, boltDamage);
            _nextFireAt = Time.time + fireInterval * 0.65f;
        }

        private void OnEnable()
        {
            _nextFireAt = Time.time + fireInterval * 0.65f;
        }

        private void OnDisable()
        {
            ClearBolts();
        }

        private void Update()
        {
            if (Time.time >= _nextFireAt)
            {
                Fire();
                _nextFireAt = Time.time + fireInterval;
            }

            for (var i = _bolts.Count - 1; i >= 0; i--)
            {
                var bolt = _bolts[i];
                if (bolt == null || bolt.Visual == null)
                {
                    _bolts.RemoveAt(i);
                    continue;
                }

                var distance = projectileSpeed * Time.deltaTime;
                if (distance <= 0f)
                    continue;
                if (TryHit(bolt.Position, distance))
                {
                    Destroy(bolt.Visual);
                    _bolts.RemoveAt(i);
                    continue;
                }

                bolt.Position += fireDirection * distance;
                bolt.Travelled += distance;
                bolt.Visual.transform.position = bolt.Position;
                if (bolt.Travelled >= projectileRange)
                {
                    Destroy(bolt.Visual);
                    _bolts.RemoveAt(i);
                }
            }
        }

        private void Fire()
        {
            var origin = transform.position + Vector3.up * 0.62f + fireDirection * 0.50f;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "BallistaBolt";
            visual.transform.position = origin;
            visual.transform.rotation = Quaternion.LookRotation(fireDirection, Vector3.up);
            visual.transform.localScale = new Vector3(0.09f, 0.09f, 0.65f);
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null && projectileMaterial != null)
                renderer.sharedMaterial = projectileMaterial;
            _bolts.Add(new Bolt { Visual = visual, Position = origin });
        }

        private bool TryHit(Vector3 origin, float distance)
        {
            var hits = Physics.SphereCastAll(origin, projectileRadius, fireDirection, distance, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;
                var entity = collider.GetComponentInParent<CombatEntity>();
                if (entity != null)
                {
                    if (entity.Team != Team.Player)
                        continue;
                    DamageSystem.Apply(new DamageContext(
                        null,
                        null,
                        entity,
                        damage,
                        DamageType.Physical,
                        Vector2.zero,
                        sourceId: "Environment_Ballista"));
                    return true;
                }
                return true;
            }
            return false;
        }

        private void ClearBolts()
        {
            for (var i = 0; i < _bolts.Count; i++)
            {
                if (_bolts[i]?.Visual != null)
                    Destroy(_bolts[i].Visual);
            }
            _bolts.Clear();
        }
    }
}
