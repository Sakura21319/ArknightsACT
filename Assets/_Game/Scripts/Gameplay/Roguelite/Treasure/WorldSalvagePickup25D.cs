using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    /// <summary>
    /// Lightweight world representation for items explicitly discarded from the unsecured backpack.
    /// It belongs to the current stage root, can be picked back up with the normal F interaction,
    /// and remembers whether a relic's one-shot acquisition effects have already been consumed.
    /// </summary>
    public sealed class WorldSalvagePickup25D : MonoBehaviour
    {
        private static readonly HashSet<WorldSalvagePickup25D> ActiveSet = new();

        private CollectibleDefinition _item;
        private bool _reacquireDroppedRelic;
        private Transform _billboard;
        private Vector3 _billboardBasePosition;

        public static IReadOnlyCollection<WorldSalvagePickup25D> Active => ActiveSet;
        public CollectibleDefinition Item => _item;
        public string DisplayName => _item != null ? _item.DisplayName : "地面物资";

        public static WorldSalvagePickup25D Spawn(
            CollectibleDefinition item,
            Vector3 position,
            Transform parent,
            bool reacquireDroppedRelic)
        {
            if (item == null)
                return null;

            var go = new GameObject("DroppedLoot_" + item.Id);
            go.transform.position = position;
            if (parent != null)
                go.transform.SetParent(parent, true);

            var pickup = go.AddComponent<WorldSalvagePickup25D>();
            pickup.Initialize(item, reacquireDroppedRelic);
            return pickup;
        }

        public static void ClearAll()
        {
            if (ActiveSet.Count == 0)
                return;

            var snapshot = new List<WorldSalvagePickup25D>(ActiveSet);
            for (var i = 0; i < snapshot.Count; i++)
                if (snapshot[i] != null)
                    Object.Destroy(snapshot[i].gameObject);
            ActiveSet.Clear();
        }

        private void OnEnable()
        {
            ActiveSet.Add(this);
        }

        private void OnDisable()
        {
            ActiveSet.Remove(this);
        }

        private void Initialize(CollectibleDefinition item, bool reacquireDroppedRelic)
        {
            _item = item;
            _reacquireDroppedRelic = reacquireDroppedRelic;
            BuildVisual();
        }

        public bool TryCollect(ScavengingInventory25D inventory)
        {
            if (inventory == null || _item == null)
                return false;
            if (!inventory.TryAcceptWorldPickup(_item, _reacquireDroppedRelic))
                return false;

            Destroy(gameObject);
            return true;
        }

        private void BuildVisual()
        {
            if (_item == null)
                return;

            if (_item.SalvageIcon != null)
            {
                var icon = new GameObject("Icon", typeof(SpriteRenderer));
                icon.transform.SetParent(transform, false);
                var renderer = icon.GetComponent<SpriteRenderer>();
                renderer.sprite = _item.SalvageIcon;
                renderer.sortingOrder = 450;

                var bounds = _item.SalvageIcon.bounds.size;
                var maxSide = Mathf.Max(0.01f, bounds.x, bounds.y);
                icon.transform.localScale = Vector3.one * (0.72f / maxSide);
                _billboard = icon.transform;
                _billboardBasePosition = new Vector3(0f, 0.52f, 0f);
                _billboard.localPosition = _billboardBasePosition;
            }
            else
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "Fallback";
                marker.transform.SetParent(transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.28f, 0f);
                marker.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
                var collider = marker.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = SearchableContainer25D.RarityColor(_item);
                _billboard = marker.transform;
                _billboardBasePosition = marker.transform.localPosition;
            }
        }

        private void LateUpdate()
        {
            if (_billboard == null)
                return;

            var p = _billboardBasePosition;
            p.y += Mathf.Sin(Time.unscaledTime * 3.4f + GetInstanceID() * 0.01f) * 0.055f;
            _billboard.localPosition = p;

            if (_item != null && _item.SalvageIcon != null)
            {
                var camera = Camera.main;
                if (camera != null)
                    _billboard.rotation = camera.transform.rotation;
            }
        }
    }
}
