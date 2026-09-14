using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    /// <summary>
    /// Prototype-only one-shot diagnostics for comparing PRTS viewer quality with Unity output.
    /// Reports actual backbuffer size, projected character height and atlas texture settings.
    /// </summary>
    public sealed class PresentationQualityDiagnostics2D : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Wait for Spine initialization + runtime auto layout.
            yield return null;
            yield return null;
            yield return null;

            var camera = Camera.main;
            var message = $"[ArknightsACT/PresentationQuality] Screen={Screen.width}x{Screen.height}; " +
                          $"Display={Display.main.systemWidth}x{Display.main.systemHeight}; dpi={Screen.dpi:0.#}";

            if (camera != null)
                message += $"; CameraPixels={camera.pixelWidth}x{camera.pixelHeight}; Ortho={camera.orthographicSize:0.##}";

            var renderers = GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var combined = new Bounds();
            var textures = new HashSet<Texture>();

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled ||
                    renderer.gameObject.name.Contains("MotionSource"))
                    continue;

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }

                var materials = renderer.sharedMaterials;
                for (var j = 0; j < materials.Length; j++)
                {
                    var texture = materials[j] != null ? materials[j].mainTexture : null;
                    if (texture != null)
                        textures.Add(texture);
                }
            }

            if (camera != null && hasBounds)
            {
                var bottom = camera.WorldToScreenPoint(new Vector3(combined.center.x, combined.min.y, combined.center.z));
                var top = camera.WorldToScreenPoint(new Vector3(combined.center.x, combined.max.y, combined.center.z));
                message += $"; Character≈{Mathf.Abs(top.y - bottom.y):0}px high";
            }

            Debug.Log(message, this);

            foreach (var texture in textures)
            {
                Debug.Log(
                    $"[ArknightsACT/PresentationQuality] Texture '{texture.name}' " +
                    $"{texture.width}x{texture.height}, filter={texture.filterMode}, aniso={texture.anisoLevel}.",
                    this);
            }
        }
    }
}
