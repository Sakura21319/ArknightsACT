using System.Collections;
using ArknightsACT.Gameplay.Navigation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    // Explicit ordered route: each flight and landing is retained for AI traversal and validation.
    public sealed class CityVerticalRoute : MonoBehaviour
    {
        public Vector3[] Points { get; private set; }
        private int _deferFrames = 1;
        public void Configure(Vector3[] points, int deferFrames = 1)
        {
            Points = points;
            _deferFrames = Mathf.Max(0, deferFrames);
        }
        private IEnumerator Start()
        {
            for (var frame = 0; frame < _deferFrames; frame++) yield return null;
            Physics.SyncTransforms();
            // Joining upper landings to nearby roofs by line of sight would create routes through air.
            PrototypeNavigationGraph25D.Instance?.AppendRoomRoute(Points, 1);
        }
    }
}
