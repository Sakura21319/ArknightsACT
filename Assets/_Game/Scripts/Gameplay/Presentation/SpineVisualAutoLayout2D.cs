using System.Collections;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Keeps imported Spine art visible and aligned without relying on editor-time Renderer bounds.
    /// A conservative scale is applied immediately; after Spine has rendered for a few frames,
    /// a bounded correction is allowed to approach the requested world height.
    /// </summary>
    public sealed class SpineVisualAutoLayout2D : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.05f)] private float targetWorldHeight = 1.5f;
        [SerializeField] private float feetLocalY = -0.7f;
        [SerializeField, Min(0.01f)] private float safeInitialScale = 0.38f;
        [SerializeField, Range(0.5f, 1f)] private float minimumCorrection = 0.65f;
        [SerializeField, Range(1f, 2f)] private float maximumCorrection = 1.55f;

        private bool _calibrated;

        public void Configure(Transform root, float targetHeight, float feetY, float initialScale)
        {
            visualRoot = root;
            targetWorldHeight = Mathf.Max(0.05f, targetHeight);
            feetLocalY = feetY;
            safeInitialScale = Mathf.Max(0.01f, initialScale);
            ApplySafePose();
        }

        private void Awake()
        {
            ApplySafePose();
        }

        private IEnumerator Start()
        {
            // Spine meshes and renderer bounds are not guaranteed to be valid in Awake/OnEnable.
            // Give the runtime two rendered frames before measuring anything.
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForEndOfFrame();
            TryCalibrate();
        }

        private void ApplySafePose()
        {
            if (visualRoot == null)
                return;

            var sign = visualRoot.localScale.x < 0f ? -1f : 1f;
            visualRoot.localScale = new Vector3(sign * safeInitialScale, safeInitialScale, safeInitialScale);
            visualRoot.localPosition = Vector3.zero;
        }

        private void TryCalibrate()
        {
            if (_calibrated || visualRoot == null)
                return;

            var renderer = visualRoot.GetComponent<Renderer>() ?? visualRoot.GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                Debug.LogWarning("[ArknightsACT/Spine] No renderer found for runtime layout; keeping safe scale.", this);
                RefreshPresentationScale();
                return;
            }

            // A presentation renderer can be intentionally hidden while another full-source
            // BaseMotion skeleton is visible. Renderer.bounds remains usable as long as Spine
            // continues updating its mesh, so do not reject calibration solely because
            // renderer.enabled is false. Otherwise entering Move/Sit during the first two frames
            // permanently leaves this presentation at safeInitialScale.

            var bounds = renderer.bounds;
            var height = bounds.size.y;
            if (!IsFinite(height) || height < 0.05f || height > 20f || !IsFinite(bounds.center.x) || !IsFinite(bounds.min.y))
            {
                Debug.LogWarning(
                    $"[ArknightsACT/Spine] Invalid renderer bounds for {name} (height={height}); keeping safe scale {safeInitialScale:0.###}.",
                    this);
                RefreshPresentationScale();
                return;
            }

            var requestedCorrection = targetWorldHeight / height;
            if (!IsFinite(requestedCorrection))
            {
                RefreshPresentationScale();
                return;
            }

            // Never let a bad measurement shrink/expand art by orders of magnitude.
            var correction = Mathf.Clamp(requestedCorrection, minimumCorrection, maximumCorrection);
            var current = visualRoot.localScale;
            var sign = current.x < 0f ? -1f : 1f;
            var next = Mathf.Clamp(Mathf.Abs(current.y) * correction, 0.08f, 1.5f);
            visualRoot.localScale = new Vector3(sign * next, next, next);

            // Waited bounds are world-space, so alignment is also performed in world-space.
            // Gameplay roots are scale=1, therefore the resulting local offset remains stable.
            bounds = renderer.bounds;
            if (IsFinite(bounds.center.x) && IsFinite(bounds.min.y))
            {
                var desiredCenterX = transform.position.x;
                var desiredFeetY = transform.position.y + feetLocalY;
                visualRoot.position += new Vector3(
                    desiredCenterX - bounds.center.x,
                    desiredFeetY - bounds.min.y,
                    0f);
            }

            _calibrated = true;
            RefreshPresentationScale();
            Debug.Log(
                $"[ArknightsACT/Spine] Runtime layout {name}: measuredHeight={height:0.###}, correction={correction:0.###}, finalScale={next:0.###}",
                this);
        }

        private void RefreshPresentationScale()
        {
            var presentation = GetComponent<SpineCharacterPresentation2D>();
            if (presentation != null)
                presentation.SetVisualRoot(visualRoot);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
