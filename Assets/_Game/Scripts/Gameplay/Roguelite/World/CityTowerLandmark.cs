using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public sealed class CityTowerLandmark : MonoBehaviour
    {
        public Transform Structure { get; private set; }
        public void Configure(Transform structure) => Structure = structure;
    }
}
