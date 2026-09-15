using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    /// <summary>
    /// Prototype-only diagnostics for comparing PRTS viewer quality with Unity output.
    /// Can live on the global Services object; it discovers the player presentation at runtime.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class PresentationQualityDiagnostics2D : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Wait for Spine initialization + runtime auto layout.
            yield return null;
            yield return null;
            yield return null;
            LogNow(this);
        }

        public static void LogNow(Object context = null)
        {
            var camera = Camera.main;
            var player = FindPlayerEntity();

            var message = $"[ArknightsACT/PresentationQuality] Screen={Screen.width}x{Screen.height}; " +
                          $"Display={Display.main.systemWidth}x{Display.main.systemHeight}; dpi={Screen.dpi:0.#}; " +
                          $"AA={QualitySettings.antiAliasing}x; " +
                          $"BufferScale={ScalableBufferManager.widthScaleFactor:0.##}x{ScalableBufferManager.heightScaleFactor:0.##}";

            if (camera != null)
            {
                message += $"; CameraPixels={camera.pixelWidth}x{camera.pixelHeight}; " +
                           $"Ortho={camera.orthographicSize:0.##}; DynamicRes={camera.allowDynamicResolution}";
            }
            else
            {
                message += "; Camera=MISSING";
            }

            if (player == null)
            {
                Debug.LogWarning(message + "; Player=MISSING. Rebuild/open PrototypeRun before testing presentation quality.", context);
                return;
            }

            var renderers = player.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var combined = new Bounds();
            var textures = new HashSet<Texture>();
            var rendererCount = 0;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || IsMotionSource(renderer.transform))
                    continue;

                rendererCount++;
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
                    var material = materials[j];
                    var texture = material != null ? material.mainTexture : null;
                    if (texture != null)
                        textures.Add(texture);
                }
            }

            message += $"; Player='{player.name}'; VisibleRenderers={rendererCount}";

            if (camera != null && hasBounds)
            {
                var bottom = camera.WorldToScreenPoint(new Vector3(combined.center.x, combined.min.y, combined.center.z));
                var top = camera.WorldToScreenPoint(new Vector3(combined.center.x, combined.max.y, combined.center.z));
                message += $"; WorldHeight={combined.size.y:0.###}; Character≈{Mathf.Abs(top.y - bottom.y):0}px high";
            }
            else if (!hasBounds)
            {
                message += "; CharacterBounds=MISSING";
            }

            Debug.Log(message, context);

            if (textures.Count == 0)
            {
                Debug.LogWarning("[ArknightsACT/PresentationQuality] No atlas texture was found on the player's visible Renderers.", context);
                return;
            }

            foreach (var texture in textures)
            {
                Debug.Log(
                    $"[ArknightsACT/PresentationQuality] Texture '{texture.name}' " +
                    $"{texture.width}x{texture.height}, filter={texture.filterMode}, aniso={texture.anisoLevel}, " +
                    $"mipCount={texture.mipmapCount}.",
                    context);
            }
        }

        private static CombatEntity FindPlayerEntity()
        {
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity != null && entity.Team == Team.Player)
                    return entity;
            }
            return null;
        }

        private static bool IsMotionSource(Transform target)
        {
            var current = target;
            while (current != null)
            {
                if (current.name.Contains("MotionSource"))
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}
