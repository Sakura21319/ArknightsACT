using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>Explicit interior bounds, independent of decorative names and physics ray hits.</summary>
    public sealed class EnterableBuilding25D : MonoBehaviour
    {
        public static readonly HashSet<EnterableBuilding25D> Active = new();
        public Bounds Interior { get; private set; }
        public string DisplayName { get; private set; } = "可进入建筑";
        public bool Visited { get; private set; }
        public int ContainerCount { get; private set; }
        public int RemainingContainers { get; private set; }
        public int UnsearchedContainers { get; private set; }
        private readonly List<ArknightsACT.Gameplay.Roguelite.Treasure.SearchableContainer25D> _containers = new();
        public void SetIdentity(string label) => DisplayName = label;
        public void Visit()
        {
            Visited = true;
            GetComponentsInChildren(false, _containers);
            ContainerCount = _containers.Count; RemainingContainers = 0; UnsearchedContainers = 0;
            foreach (var container in _containers)
            {
                if (!container.Emptied) RemainingContainers++;
                if (!container.Initialized || container.HasUnsearched) UnsearchedContainers++;
            }
        }
        public string SearchStatus => !Visited ? "尚未进入" : ContainerCount == 0 ? "空置房间" : RemainingContainers == 0 ? "物资已清空" :
            UnsearchedContainers == 0 ? "检索完成 · 尚有未取物资" : $"待检索 {UnsearchedContainers} / 未清空 {RemainingContainers}";
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
