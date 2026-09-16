using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Routing
{
    /// <summary>
    /// One addressable section of a generated chunk floor. The stage-layout layer owns these
    /// sockets; special terrain can align to eligible side/corner sockets without rebuilding route logic.
    /// The legacy Open() API is retained for compatibility, although the current prototype no longer
    /// generates pit hazards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteFloorSocket25D : MonoBehaviour
    {
        [SerializeField] private Vector2 footprint = new(2.3f, 2.2f);
        [SerializeField] private bool pitEligible = true;
        [SerializeField] private Renderer baseRenderer;
        [SerializeField] private Collider floorCollider;
        [SerializeField] private GameObject surfaceVisual;
        [SerializeField] private bool opened;

        public Vector2 Footprint => footprint;
        public bool PitEligible => pitEligible && !opened;
        public bool SpecialTerrainEligible => pitEligible && !opened;
        public bool Opened => opened;

        public void Configure(
            Vector2 size,
            bool allowPit,
            Renderer floorRenderer,
            Collider collider,
            GameObject surface)
        {
            footprint = size;
            pitEligible = allowPit;
            baseRenderer = floorRenderer;
            floorCollider = collider;
            surfaceVisual = surface;
        }

        public void SetFootprint(Vector2 size)
        {
            footprint = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
        }

        public bool Open()
        {
            if (opened || !pitEligible)
                return false;

            opened = true;
            if (baseRenderer != null)
                baseRenderer.enabled = false;
            if (floorCollider != null)
                floorCollider.enabled = false;
            if (surfaceVisual != null)
                surfaceVisual.SetActive(false);
            return true;
        }
    }
}
