#if UNITY_EDITOR
using System;
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Replaces the early flat/noisy prototype material maps with a more restrained industrial
    /// response: broad cool-steel colour variation, directional brushing, sparse wear, micro-normal,
    /// cavity AO and per-pixel metallic/smoothness variation. No URP package types are referenced;
    /// only shader properties that exist on the material are touched.
    /// </summary>
    internal static class ChernobogMaterialProductionPass
    {
        private const int TextureSize = 512;
        private const string TextureFolder = ChernobogEnvironmentKitBuilder.Root + "/Textures/Production";

        private static void ApplyMenu()
        {
            var kit = ChernobogEnvironmentKitBuilder.EnsureBuilt();
            EnsureApplied(kit);
            Selection.activeObject = kit;
            EditorGUIUtility.PingObject(kit);
            Debug.Log("[ArknightsACT/ChernobogMaterials] Production material maps rebuilt.");
        }

        public static void EnsureApplied(ChernobogEnvironmentKit kit)
        {
            if (kit == null)
                return;

            EnsureFolder(ChernobogEnvironmentKitBuilder.Root + "/Textures", "Production");

            ApplySurface(kit.deckMaterial, "Deck", SurfaceKind.Deck,
                new Color(0.31f, 0.345f, 0.395f, 1f), 0.42f, 0.14f, 0.62f, 0.78f);
            ApplySurface(kit.deckHeavyMaterial, "DeckHeavy", SurfaceKind.DeckHeavy,
                new Color(0.245f, 0.285f, 0.34f, 1f), 0.47f, 0.12f, 0.70f, 0.84f);
            ApplySurface(kit.deckSecondaryMaterial, "DeckSecondary", SurfaceKind.DeckSecondary,
                new Color(0.275f, 0.31f, 0.365f, 1f), 0.44f, 0.125f, 0.66f, 0.81f);
            ApplySurface(kit.wallMaterial, "Wall", SurfaceKind.Wall,
                new Color(0.205f, 0.245f, 0.305f, 1f), 0.38f, 0.105f, 0.78f, 0.88f);
            ApplySurface(kit.insetMaterial, "Inset", SurfaceKind.Inset,
                new Color(0.070f, 0.087f, 0.112f, 1f), 0.16f, 0.045f, 0.82f, 0.94f);
            ApplySurface(kit.steelMaterial, "Steel", SurfaceKind.Steel,
                new Color(0.325f, 0.365f, 0.425f, 1f), 0.72f, 0.19f, 0.56f, 0.73f);
            ApplySurface(kit.grateMaterial, "Grate", SurfaceKind.Grate,
                new Color(0.075f, 0.090f, 0.115f, 1f), 0.52f, 0.055f, 0.95f, 0.96f);

            TuneAccent(kit.accentMaterial);
            TuneEmission(kit.emissiveMaterial);

            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ApplySurface(
            Material material,
            string id,
            SurfaceKind kind,
            Color baseColor,
            float metallic,
            float smoothness,
            float bumpScale,
            float occlusionStrength)
        {
            if (material == null)
                return;

            var albedo = CreateAlbedo(id + "_Prod_Albedo", kind, baseColor);
            var normal = CreateNormal(id + "_Prod_Normal", kind);
            var ao = CreateAo(id + "_Prod_AO", kind);
            var mask = CreateMetallicSmoothness(id + "_Prod_MetalSmooth", kind, metallic, smoothness);

            // The texture already owns the authored base colour. White tint prevents the old kit tint
            // from multiplying it twice and producing the flat dark/plastic look seen in screenshots.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            SetTexture(material, "_BaseMap", "_MainTex", albedo);

            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", bumpScale);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_OcclusionMap"))
            {
                material.SetTexture("_OcclusionMap", ao);
                if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", occlusionStrength);
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);

            if (material.HasProperty("_MetallicGlossMap"))
            {
                material.SetTexture("_MetallicGlossMap", mask);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (material.HasProperty("_SmoothnessTextureChannel"))
                material.SetFloat("_SmoothnessTextureChannel", 0f);

            EditorUtility.SetDirty(material);
        }

        private static void TuneAccent(Material material)
        {
            if (material == null)
                return;
            var orange = new Color(0.64f, 0.255f, 0.052f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", orange);
            if (material.HasProperty("_Color")) material.SetColor("_Color", orange);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.08f);
            SetSmoothness(material, 0.10f);
            EditorUtility.SetDirty(material);
        }

        private static void TuneEmission(Material material)
        {
            if (material == null)
                return;
            var warm = new Color(0.88f, 0.37f, 0.085f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", warm);
            if (material.HasProperty("_Color")) material.SetColor("_Color", warm);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", new Color(2.3f, 0.72f, 0.10f, 1f));
                material.EnableKeyword("_EMISSION");
            }
            SetSmoothness(material, 0.16f);
            EditorUtility.SetDirty(material);
        }

        private static Texture2D CreateAlbedo(string name, SurfaceKind kind, Color baseColor)
        {
            var seed = 137 + (int)kind * 97;
            return CreateTexture(name, false, (x, y) =>
            {
                var u = x / (float)(TextureSize - 1);
                var v = y / (float)(TextureSize - 1);
                var broad = Mathf.PerlinNoise((u + seed) * 3.1f, (v + seed * 0.37f) * 3.1f) - 0.5f;
                var medium = Mathf.PerlinNoise((u + seed * 0.13f) * 12.0f, (v + seed) * 11.0f) - 0.5f;
                var micro = Hash01(x * 7 + seed, y * 11 + seed * 3) - 0.5f;

                var brush = 0f;
                if (kind == SurfaceKind.Deck || kind == SurfaceKind.DeckHeavy || kind == SurfaceKind.DeckSecondary || kind == SurfaceKind.Steel)
                    brush = Mathf.Sin(y * 0.31f + Mathf.Sin(x * 0.025f) * 1.7f) * 0.010f;
                else if (kind == SurfaceKind.Wall)
                    brush = Mathf.Sin(x * 0.22f + y * 0.015f) * 0.008f;

                var grime = Grime(kind, x, y);
                var wear = SparseWear(kind, x, y);
                var value = 1f + broad * 0.10f + medium * 0.045f + micro * 0.020f + brush - grime + wear;

                if (kind == SurfaceKind.Grate)
                {
                    var bar = x % 22 < 5 || y % 22 < 5;
                    value *= bar ? 0.88f : 0.30f;
                }
                else if (kind == SurfaceKind.Inset)
                {
                    value *= 0.88f;
                }

                return new Color(
                    Mathf.Clamp01(baseColor.r * value),
                    Mathf.Clamp01(baseColor.g * value),
                    Mathf.Clamp01(baseColor.b * value),
                    1f);
            });
        }

        private static Texture2D CreateNormal(string name, SurfaceKind kind)
        {
            return CreateTexture(name, true, (x, y) =>
            {
                var xm = Wrap(x - 1);
                var xp = Wrap(x + 1);
                var ym = Wrap(y - 1);
                var yp = Wrap(y + 1);
                var strength = kind == SurfaceKind.Grate ? 4.8f : kind == SurfaceKind.Wall ? 2.5f : 1.9f;
                var dx = (Height(kind, xm, y) - Height(kind, xp, y)) * strength;
                var dy = (Height(kind, x, ym) - Height(kind, x, yp)) * strength;
                var n = new Vector3(dx, dy, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            });
        }

        private static Texture2D CreateAo(string name, SurfaceKind kind)
        {
            return CreateTexture(name, true, (x, y) =>
            {
                var broad = Mathf.PerlinNoise((x + (int)kind * 41) * 0.020f, (y + 113) * 0.019f);
                var cavity = Mathf.Clamp01(0.94f - Grime(kind, x, y) * 2.1f - (1f - broad) * 0.08f);
                if (kind == SurfaceKind.Grate && x % 22 >= 5 && y % 22 >= 5)
                    cavity *= 0.28f;
                return new Color(cavity, cavity, cavity, 1f);
            });
        }

        private static Texture2D CreateMetallicSmoothness(
            string name, SurfaceKind kind, float metallic, float smoothness)
        {
            return CreateTexture(name, true, (x, y) =>
            {
                var fine = Hash01(x * 17 + (int)kind * 53, y * 19 + 29) - 0.5f;
                var broad = Mathf.PerlinNoise((x + 41) * 0.018f, (y + (int)kind * 61) * 0.018f) - 0.5f;
                var grime = Grime(kind, x, y);
                var localMetal = Mathf.Clamp01(metallic + fine * 0.045f - grime * 0.16f);
                var localSmooth = Mathf.Clamp01(smoothness + broad * 0.055f + fine * 0.025f - grime * 0.20f);
                if (kind == SurfaceKind.Grate)
                    localSmooth *= 0.70f;
                return new Color(localMetal, 0f, 0f, localSmooth);
            });
        }

        private static float Height(SurfaceKind kind, int x, int y)
        {
            var seed = (int)kind * 73 + 19;
            var broad = (Mathf.PerlinNoise((x + seed) * 0.040f, (y + seed * 3) * 0.038f) - 0.5f) * 0.11f;
            var micro = (Hash01(x * 13 + seed, y * 17 + seed) - 0.5f) * 0.045f;
            var h = broad + micro;

            if (kind == SurfaceKind.Grate)
                return x % 22 < 5 || y % 22 < 5 ? 0.22f + micro : -0.52f;

            if (kind == SurfaceKind.Wall)
            {
                if (x % 118 < 3 || y % 154 < 3) h -= 0.16f;
            }
            else if (kind == SurfaceKind.Deck || kind == SurfaceKind.DeckHeavy || kind == SurfaceKind.DeckSecondary)
            {
                if ((y + seed) % 137 < 2) h -= 0.055f;
            }
            else if (kind == SurfaceKind.Inset)
            {
                h *= 1.4f;
            }

            return h;
        }

        private static float Grime(SurfaceKind kind, int x, int y)
        {
            var seed = 31 + (int)kind * 47;
            var n = Mathf.PerlinNoise((x + seed) * 0.012f, (y + seed * 2) * 0.014f);
            var grime = Mathf.Max(0f, n - 0.67f) * 0.18f;

            if (kind == SurfaceKind.Wall)
            {
                var streak = Mathf.PerlinNoise((x + seed) * 0.026f, seed * 0.01f);
                grime += Mathf.Max(0f, streak - 0.72f) * (0.035f + y / (float)TextureSize * 0.025f);
            }
            return grime;
        }

        private static float SparseWear(SurfaceKind kind, int x, int y)
        {
            if (kind == SurfaceKind.Inset || kind == SurfaceKind.Grate)
                return 0f;

            var h = Hash01(x / 3 + (int)kind * 101, y / 3 + 79);
            if (h < 0.994f)
                return 0f;

            // Tiny bright chips/scratches only; avoid the noisy speckled prototype look.
            return 0.035f + (h - 0.994f) * 2.5f;
        }

        private static Texture2D CreateTexture(string name, bool linear, Func<int, int, Color> pixel)
        {
            var path = $"{TextureFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
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

        private static void SetTexture(Material material, string primary, string fallback, Texture texture)
        {
            if (material == null || texture == null)
                return;
            if (material.HasProperty(primary)) material.SetTexture(primary, texture);
            if (material.HasProperty(fallback)) material.SetTexture(fallback, texture);
        }

        private static void SetSmoothness(Material material, float value)
        {
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", value);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", value);
        }

        private static int Wrap(int value)
        {
            value %= TextureSize;
            return value < 0 ? value + TextureSize : value;
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

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private enum SurfaceKind
        {
            Deck,
            DeckHeavy,
            DeckSecondary,
            Wall,
            Inset,
            Steel,
            Grate
        }
    }
}
#endif
