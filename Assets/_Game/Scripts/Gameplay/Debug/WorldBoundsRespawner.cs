using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class WorldBoundsRespawner : MonoBehaviour
    {
        [SerializeField] private float minY = -4.5f;

        private Rigidbody2D _body;
        private Vector3 _spawnPosition;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _spawnPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (transform.position.y >= minY)
                return;

            transform.position = _spawnPosition;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }
    }
}
