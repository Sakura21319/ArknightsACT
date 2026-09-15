using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Simple Y-sort for the top-down prototype so upright Spine characters overlap like a
    /// Soul-Knight-style arena instead of a side-view platform scene.
    /// </summary>
    public sealed class TopDownDepthSort2D : MonoBehaviour
    {
        [SerializeField] private int baseSortingOrder = 1000;
        [SerializeField] private float unitsToOrder = 100f;

        private Renderer[] _renderers;
        private int[] _offsets;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _offsets = new int[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
                _offsets[i] = _renderers[i] != null ? _renderers[i].sortingOrder : 0;
        }

        private void LateUpdate()
        {
            var rootOrder = baseSortingOrder - Mathf.RoundToInt(transform.position.y * unitsToOrder);
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer != null)
                    renderer.sortingOrder = rootOrder + _offsets[i];
            }
        }
    }
}
