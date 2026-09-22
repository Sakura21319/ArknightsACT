using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>Explicit interior bounds, independent of decorative names and physics ray hits.</summary>
    public sealed class EnterableBuilding25D : MonoBehaviour
    {
        public static readonly HashSet<EnterableBuilding25D> Active = new();
        public Bounds Interior { get; private set; }
        private Vector3[] _route;
        public void SetNavigationRoute(Vector3[] route) => _route = route;
        private void Start()
        {
            // Generation-time density/cleanup passes must finish before testing clear corridors.
            if (_route == null) return;
            Physics.SyncTransforms();
            ArknightsACT.Gameplay.Navigation.PrototypeNavigationGraph25D.Instance?.AppendRoomRoute(_route);
        }
        public void Configure(Vector3 center, Vector3 size) => Interior = new Bounds(center, size);
        public bool Contains(Vector3 world) => Interior.Contains(transform.InverseTransformPoint(world));
        public bool Intersects(Ray ray, float distance)
        {
            var localRay = new Ray(transform.InverseTransformPoint(ray.origin),
                transform.InverseTransformDirection(ray.direction));
            return Interior.IntersectRay(localRay, out var hit) && hit < distance;
        }
        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);
    }
}
