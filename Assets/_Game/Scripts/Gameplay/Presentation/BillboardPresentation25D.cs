using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Keeps a flat Spine/sprite presentation facing the fixed 2.5D camera. Character facing
    /// itself is owned by SpineCharacterPresentation2D so the visual is never double-flipped.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BillboardPresentation25D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        public void Configure(Camera cameraValue) => targetCamera = cameraValue;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera != null)
                transform.rotation = targetCamera.transform.rotation;
        }
    }
}
