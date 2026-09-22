#if UNITY_EDITOR
using System;
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Adds a second, high-frequency material layer on top of the production maps.
    /// The first production pass owns broad colour / normal / AO / metal-smooth variation; this pass
    /// only adds fine brushed-metal breakup and tiny roughness cues so closeups stop reading as smooth
    /// painted plastic. All shader interaction is guarded by HasProperty and does not reference URP APIs.
    /// </summary>
    internal static class ChernobogMaterialFineDetailPass
    {
        private const int TextureSize = 256;
        private const string Folder = ChernobogEnvironmentKitBuilder.Root + "/Textures/FineDetail";

        private enum DetailKind
        {
            HorizontalSteel,
            VerticalPaint,
            DarkInset
        }

        private static void ApplyMenu()
        {
            var kit = ChernobogEnvironmentKitBuilder.EnsureBuilt();
            EnsureApplied(kit);
            Selection.activeObject = kit;
            EditorGUIUtility.PingObject(kit);
            Debug.Log("[ArknightsACT/ChernobogMaterials] Fine material detail pass rebuilt.");
        }

        public static void EnsureApplied(ChernobogEnvironmentKit kit)
        {
            if (kit == null)
                return;

            EnsureFolder(ChernobogEnvironmentKitBuilder.Root + "/Textures", "FineDetail");

            var steelAlbedo = CreateDetailAlbedo("Steel_DetailAlbedo", DetailKind.HorizontalSteel);
            var steelNormal = CreateDetailNormal("Steel_DetailNormal", DetailKind.HorizontalSteel);
            var paintAlbedo = CreateDetailAlbedo("Paint_DetailAlbedo", DetailKind.VerticalPaint);
            var paintNormal = CreateDetailNormal("Paint_DetailNormal", DetailKind.VerticalPaint);
            var insetAlbedo = CreateDetailAlbedo("Inset_DetailAlbedo", DetailKind.DarkInset);
            var insetNormal = CreateDetailNormal("Inset_DetailNormal", DetailKind.DarkInset);

            ApplyDetail(kit.deckMaterial, steelAlbedo, steelNormal, 4.4f, 0.34f, 0.20f);
            ApplyDetail(kit.deckHeavyMaterial, steelAlbedo, steelNormal, 3.8f, 0.38f, 0.18f);
            ApplyDetail(kit.deckSecondaryMaterial, steelAlbedo, steelNormal, 4.1f, 0.35f, 0.18f);
            ApplyDetail(kit.steelMaterial, steelAlbedo, steelNormal, 5.2f, 0.46f, 0.16f);
            ApplyDetail(kit.wallMaterial, paintAlbedo, paintNormal, 3.3f, 0.30f, 0.16f);
            ApplyDetail(kit.insetMaterial, insetAlbedo, insetNormal, 2.7f, 0.26f, 0.13f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ApplyDetail(
            Material material,
            Texture2D albedo,
            Texture2D normal,
            float tiling,
            float normalStrength,
            float albedoStrength)
        {
            if (material == null)
                return;

            var hasAnyDetail = false;
            if (material.HasProperty("_DetailAlbedoMap"))
            {
                material.SetTexture("_DetailAlbedoMap", albedo);
                material.SetTextureScale("_DetailAlbedoMap", new Vector2(tiling, tiling));
                if (material.HasProperty("_DetailAlbedoMapScale"))
                    material.SetFloat("_DetailAlbedoMapScale", albedoStrength);
                hasAnyDetail = true;
            }

            if (material.HasProperty("_DetailNormalMap"))
            {
                material.SetTexture("_DetailNormalMap", normal);
                material.SetTextureScale("_DetailNormalMap", new Vector2(tiling, tiling));
                if (material.HasProperty("_DetailNormalMapScale"))
                    material.SetFloat("_DetailNormalMapScale", normalStrength);
                hasAnyDetail = true;
            }

            if (hasAnyDetail)
                material.EnableKeyword("_DETAIL_MULX2");

            // Keep specular/environment response enabled; the low smoothness authored in the production
            // pass controls intensity while the lighting rig provides the readable edge highlights.
            if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 1f);
            if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 1f);

            EditorUtility.SetDirty(material);
        }

        private static Texture2D CreateDetailAlbedo(string name, DetailKind kind)
        {
            return CreateTexture(name, false, (x, y) =>
            {
                var noise = Hash01(x * 17 + (int)kind * 131, y * 23 + 41) - 0.5f;
                var broad = Mathf.PerlinNoise((x + 91 + (int)kind * 37) * 0.026f, (y + 53) * 0.027f) - 0.5f;
                var line = kind == DetailKind.VerticalPaint
                    ? Mathf.Sin(x * 0.41f + Mathf.Sin(y * 0.021f) * 0.7f)
                    : Mathf.Sin(y * 0.47f + Mathf.Sin(x * 0.018f) * 0.8f);

                var value = 0.50f + noise * 0.018f + broad * 0.022f + line * 0.006f;

                // Sparse hairline wear is mostly a roughness/readability cue; keep colour impact tiny.
                var scratchHash = Hash01(x / 2 + 911 + (int)kind * 59, y / 2 + 337);
                if (scratchHash > 0.9975f)
                    value += 0.035f;

                if (kind == DetailKind.DarkInset)
                    value -= 0.010f;

                value = Mathf.Clamp(value, 0.42f, 0.58f);
                return new Color(value, value, value, 1f);
            });
        }

        private static Texture2D CreateDetailNormal(string name, DetailKind kind)
        {
            var height = new float[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            for (var x = 0; x < TextureSize; x++)
            {
                var noise = (Hash01(x * 29 + 17 + (int)kind * 71, y * 31 + 23) - 0.5f) * 0.020f;
                var brush = kind == DetailKind.VerticalPaint
                    ? Mathf.Sin(x * 0.73f + Mathf.Sin(y * 0.031f)) * 0.014f
                    : Mathf.Sin(y * 0.81f + Mathf.Sin(x * 0.026f)) * 0.016f;
                var dent = Mathf.PerlinNoise((x + 37) * 0.055f, (y + 83 + (int)kind * 19) * 0.052f) - 0.5f;
                height[y * TextureSize + x] = noise + brush + dent * 0.025f;
            }

            return CreateTexture(name, true, (x, y) =>
            {
                var xm = (x - 1 + TextureSize) % TextureSize;
                var xp = (x + 1) % TextureSize;
                var ym = (y - 1 + TextureSize) % TextureSize;
                var yp = (y + 1) % TextureSize;
                var strength = kind == DetailKind.DarkInset ? 2.0f : 2.7f;
                var dx = (height[y * TextureSize + xm] - height[y * TextureSize + xp]) * strength;
                var dy = (height[ym * TextureSize + x] - height[yp * TextureSize + x]) * strength;
                var n = new Vector3(dx, dy, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            });
        }

        private static Texture2D CreateTexture(string name, bool linear, Func<int, int, Color> pixel)
        {
            var path = $"{Folder}/{name}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (old != null)
                AssetDatabase.DeleteAsset(path);

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };

            var pixels = new Color[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            for (var x = 0; x < TextureSize; x++)
                pixels[y * TextureSize + x] = pixel(x, y);
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                var n = x * 374761393 + y * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                n ^= n >> 16;
                return (n & 0x7fffffff) / (float)int.MaxValue;
            }
        }
    }
}
#endif
