using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Lightweight prototype-only world-space lines/arcs. Gameplay never depends on this component.
    /// </summary>
    public sealed class TopDownCombatFx2D : MonoBehaviour
    {
        [SerializeField] private float lineWidth = 0.075f;
        private Material _lineMaterial;

        private void Awake()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
                _lineMaterial = new Material(shader) { name = "TopDownPrototypeLineMaterial" };
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        public void PlaySlash(Vector2 origin, Vector2 direction, float radius, float arcDegrees)
        {
            if (_lineMaterial == null || direction.sqrMagnitude <= 0.001f)
                return;

            const int segments = 12;
            var points = new Vector3[segments + 1];
            var baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var start = baseAngle - arcDegrees * 0.5f;
            for (var i = 0; i <= segments; i++)
            {
                var angle = (start + arcDegrees * i / segments) * Mathf.Deg2Rad;
                points[i] = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            SpawnLine(points, new Color(0.72f, 0.90f, 1f, 0.95f), 0.09f, lineWidth * 1.4f);
        }

        public void PlayWave(Vector2 origin, Vector2 direction, float length)
        {
            if (direction.sqrMagnitude <= 0.001f)
                return;
            direction.Normalize();
            var perpendicular = new Vector2(-direction.y, direction.x);
            var start = origin + perpendicular * 0.35f;
            var end = origin + direction * length - perpendicular * 0.35f;
            SpawnLine(new[] { (Vector3)start, (Vector3)end }, new Color(0.45f, 0.82f, 1f, 0.92f), 0.13f, lineWidth * 1.8f);
        }

        public void PlayChain(Vector2 from, Vector2 to)
        {
            var mid = (from + to) * 0.5f + Random.insideUnitCircle * 0.22f;
            SpawnLine(
                new[] { (Vector3)from, (Vector3)mid, (Vector3)to },
                new Color(0.55f, 0.67f, 1f, 0.95f),
                0.11f,
                lineWidth);
        }

        private void SpawnLine(Vector3[] points, Color color, float lifetime, float width)
        {
            if (_lineMaterial == null)
                return;

            var go = new GameObject("TopDownFxLine");
            go.transform.SetParent(transform, true);
            var line = go.AddComponent<LineRenderer>();
            line.material = _lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = width;
            line.endWidth = width * 0.65f;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 3;
            line.sortingOrder = 300;
            StartCoroutine(DestroyAfter(go, lifetime));
        }

        private static IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null)
                Destroy(go);
        }
    }
}
