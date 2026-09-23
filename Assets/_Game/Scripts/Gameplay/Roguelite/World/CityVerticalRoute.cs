using ArknightsACT.Gameplay.Navigation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    // Explicit ordered route: each flight and landing is retained for AI traversal and validation.
    public sealed class CityVerticalRoute : MonoBehaviour
    {
        public Vector3[] Points { get; private set; }
        public void Configure(Vector3[] points) => Points = points;
        private void Start()
        {
            Physics.SyncTransforms();
            // Joining upper landings to nearby roofs by line of sight would create routes through air.
            PrototypeNavigationGraph25D.Instance?.AppendRoomRoute(Points, 1);
        }
    }
}
