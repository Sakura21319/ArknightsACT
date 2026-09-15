#if UNITY_EDITOR
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Prototype25D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Isolated visual prototype for a Don't-Starve-like 2.5D presentation:
    /// real 3D map, fixed orthographic oblique camera, flat Spine characters, XZ movement.
    /// It does not replace or modify PrototypeRun.unity.
    /// </summary>
    public static class Prototype25DSceneBuilder
    {
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string ScenePath = SceneDir + "/Prototype25D.unity";
        private const string DataRoot = "Assets/_Game/Data/Prototype25D";
        private const string MaterialDir = DataRoot + "/Materials";
        private const string TextureDir = DataRoot + "/Textures";
        private static readonly Vector3 CameraOffset = new(-10.5f, 6.8f, -10.5f);

        [MenuItem("ArknightsACT/Build 2.5D Demo Scene")]
        public static void Build()
        {
            EnsureFolder(SceneDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(TextureDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var materials = BuildMaterials();

            ConfigureEnvironment();
            BuildMap(materials);

            var camera = BuildCamera();
            var player = BuildPlayer(camera);
            camera.GetComponent<Prototype25DCameraFollow>()?.Configure(player.transform, CameraOffset);

            BuildStaticCharacter(
                "Preview_Soldier",
                PrtsPrototypeAssetCatalog.PrototypeEnemies[1],
                new Vector3(3.2f, 0.03f, 2.3f),
                camera,
                materials.Accent);
            BuildStaticCharacter(
                "Preview_Hound",
                PrtsPrototypeAssetCatalog.PrototypeEnemies[3],
                new Vector3(-2.8f, 0.03f, 4.1f),
                camera,
                materials.Accent);
            BuildStaticCharacter(
                "Preview_Crossbowman",
                PrtsPrototypeAssetCatalog.PrototypeEnemies[2],
                new Vector3(4.6f, 0.03f, -3.0f),
                camera,
                materials.Accent);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log(
                $"ArknightsACT 2.5D demo generated: {ScenePath}. " +
                "Use WASD / Arrow Keys to move on the 3D XZ ground plane. " +
                "Materials are rebuilt for the active render pipeline and use generated repeat textures.");
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.50f, 0.53f, 0.58f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.11f, 0.125f, 0.15f);
            RenderSettings.fogStartDistance = 20f;
            RenderSettings.fogEndDistance = 46f;

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(43f, -35f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.94f, 0.96f, 1f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
        }

        private static void BuildMap(MaterialSet materials)
        {
            var root = new GameObject("[3D Map]");

            CreateBlock(root.transform, "Ground", new Vector3(0f, -0.30f, 0f), new Vector3(26f, 0.6f, 20f), materials.Ground);
            CreateBlock(root.transform, "Road_Main", new Vector3(0f, 0.015f, 0f), new Vector3(18f, 0.03f, 7f), materials.Road);
            CreateBlock(root.transform, "Road_Cross", new Vector3(-3.5f, 0.025f, 0f), new Vector3(5f, 0.04f, 16f), materials.Road);

            CreateBlock(root.transform, "Sidewalk_North", new Vector3(0f, 0.08f, 4.45f), new Vector3(18f, 0.16f, 1.7f), materials.Sidewalk);
            CreateBlock(root.transform, "Sidewalk_South", new Vector3(0f, 0.08f, -4.45f), new Vector3(18f, 0.16f, 1.7f), materials.Sidewalk);
            CreateBlock(root.transform, "Sidewalk_West", new Vector3(-6.85f, 0.08f, 0f), new Vector3(1.7f, 0.16f, 16f), materials.Sidewalk);

            CreateBuilding(root.transform, "Building_NW_A", new Vector3(-9.2f, 1.9f, 6.8f), new Vector3(4.4f, 3.8f, 4.0f), materials.BuildingDark);
            CreateBuilding(root.transform, "Building_NW_B", new Vector3(-5.3f, 1.35f, 7.5f), new Vector3(2.8f, 2.7f, 2.7f), materials.BuildingLight);
            CreateBuilding(root.transform, "Building_NE", new Vector3(8.9f, 2.3f, 6.7f), new Vector3(5.1f, 4.6f, 4.1f), materials.BuildingDark);
            CreateBuilding(root.transform, "Building_SE_A", new Vector3(9.5f, 1.55f, -6.6f), new Vector3(4.0f, 3.1f, 4.4f), materials.BuildingLight);
            CreateBuilding(root.transform, "Building_SW", new Vector3(-8.9f, 2.1f, -7.0f), new Vector3(4.7f, 4.2f, 3.6f), materials.BuildingDark);

            CreateBlock(root.transform, "Median_01", new Vector3(4.0f, 0.25f, 0.2f), new Vector3(2.2f, 0.5f, 0.45f), materials.Concrete);
            CreateBlock(root.transform, "Median_02", new Vector3(7.0f, 0.25f, 0.2f), new Vector3(2.2f, 0.5f, 0.45f), materials.Concrete);

            CreateBlock(root.transform, "Crate_A", new Vector3(1.8f, 0.45f, 4.0f), new Vector3(0.9f, 0.9f, 0.9f), materials.Crate);
            CreateBlock(root.transform, "Crate_B", new Vector3(2.75f, 0.35f, 4.15f), new Vector3(0.7f, 0.7f, 0.7f), materials.Crate);
            CreateBlock(root.transform, "Crate_C", new Vector3(-5.6f, 0.50f, -3.5f), new Vector3(1.0f, 1.0f, 1.0f), materials.Crate);

            CreateBlock(root.transform, "Barrier_A", new Vector3(-0.4f, 0.32f, -3.0f), new Vector3(2.5f, 0.64f, 0.35f), materials.Accent);
            CreateBlock(root.transform, "Barrier_B", new Vector3(6.3f, 0.32f, 3.1f), new Vector3(2.1f, 0.64f, 0.35f), materials.Accent);

            CreateBlock(root.transform, "MapBound_N", new Vector3(0f, 1f, 9.9f), new Vector3(26f, 2f, 0.2f), materials.Invisible, false);
            CreateBlock(root.transform, "MapBound_S", new Vector3(0f, 1f, -9.9f), new Vector3(26f, 2f, 0.2f), materials.Invisible, false);
            CreateBlock(root.transform, "MapBound_E", new Vector3(12.9f, 1f, 0f), new Vector3(0.2f, 2f, 20f), materials.Invisible, false);
            CreateBlock(root.transform, "MapBound_W", new Vector3(-12.9f, 1f, 0f), new Vector3(0.2f, 2f, 20f), materials.Invisible, false);
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = CameraOffset;
            var lookTarget = new Vector3(0f, 0.75f, 0f);
            go.transform.rotation = Quaternion.LookRotation(lookTarget - CameraOffset, Vector3.up);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.085f, 0.10f, 0.13f);
            camera.allowDynamicResolution = false;
            go.AddComponent<AudioListener>();
            go.AddComponent<Prototype25DCameraFollow>();
            return camera;
        }

        private static GameObject BuildPlayer(Camera camera)
        {
            var player = new GameObject("Player_Chen_25D");
            player.transform.position = Vector3.zero;

            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.34f;
            controller.height = 1.55f;
            controller.center = new Vector3(0f, 0.78f, 0f);
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 45f;

            var motor = player.AddComponent<Prototype25DPlayerMotor>();
            motor.SetCamera(camera);

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(player.transform, false);
            billboard.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var billboardBehaviour = billboard.AddComponent<Prototype25DBillboard>();
            billboardBehaviour.Configure(camera, motor);

            if (PrtsGeneratedPresentation.TryAttach(PrtsPrototypeAssetCatalog.Chen.BaseName, billboard.transform, out var presentation))
            {
                presentation.transform.localPosition = new Vector3(0f, -PrtsPrototypeAssetCatalog.Chen.FeetLocalY, 0f);
            }
            else
            {
                CreateFlatFallback(
                    billboard.transform,
                    "Chen_2D_Fallback",
                    -PrtsPrototypeAssetCatalog.Chen.FeetLocalY,
                    new Color(0.56f, 0.72f, 0.90f));
                Debug.LogWarning(
                    "[ArknightsACT/25D] Ch'en Spine presentation prefab is missing; using a flat fallback card.");
            }

            return player;
        }

        private static void BuildStaticCharacter(
            string objectName,
            PrtsAssetDescriptor descriptor,
            Vector3 position,
            Camera camera,
            Material fallbackMaterial)
        {
            var root = new GameObject(objectName);
            root.transform.position = position;
            var billboard = root.AddComponent<Prototype25DBillboard>();
            billboard.Configure(camera, null);

            if (PrtsGeneratedPresentation.TryAttach(descriptor.BaseName, root.transform, out var presentation))
            {
                presentation.transform.localPosition = new Vector3(0f, -descriptor.FeetLocalY, 0f);
                return;
            }

            CreateFlatFallback(root.transform, descriptor.DisplayName + "_Fallback", -descriptor.FeetLocalY, fallbackMaterial.color);
        }

        private static void CreateFlatFallback(Transform parent, string name, float centerY, Color color)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, centerY, 0f);
            quad.transform.localScale = new Vector3(0.85f, 1.55f, 1f);
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            var renderer = quad.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(FindCompatibleLitShader());
                material.name = name + "_RuntimeMaterial";
                SetMaterialColor(material, color);
                renderer.sharedMaterial = material;
            }
        }

        private static void CreateBuilding(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var building = CreateBlock(parent, name, position, scale, material);
            var cap = CreateBlock(
                parent,
                name + "_Roof",
                position + Vector3.up * (scale.y * 0.5f + 0.08f),
                new Vector3(scale.x * 1.04f, 0.16f, scale.z * 1.04f),
                material);
            cap.transform.SetParent(building.transform, true);
        }

        private static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool visible = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.enabled = visible;
            }
            return go;
        }

        private static MaterialSet BuildMaterials()
        {
            var groundTexture = GetOrCreatePatternTexture("GroundNoise", TexturePattern.Noise);
            var roadTexture = GetOrCreatePatternTexture("RoadAsphalt", TexturePattern.Asphalt);
            var sidewalkTexture = GetOrCreatePatternTexture("SidewalkGrid", TexturePattern.Grid);
            var buildingTexture = GetOrCreatePatternTexture("BuildingPanels", TexturePattern.Panels);
            var concreteTexture = GetOrCreatePatternTexture("ConcreteSpeckle", TexturePattern.Noise);
            var crateTexture = GetOrCreatePatternTexture("CratePlanks", TexturePattern.Planks);
            var barrierTexture = GetOrCreatePatternTexture("BarrierStripes", TexturePattern.Stripes);

            return new MaterialSet
            {
                Ground = GetOrCreateMaterial("Ground", new Color(0.37f, 0.40f, 0.38f), groundTexture, new Vector2(8f, 6f)),
                Road = GetOrCreateMaterial("Road", new Color(0.24f, 0.26f, 0.29f), roadTexture, new Vector2(10f, 4f)),
                Sidewalk = GetOrCreateMaterial("Sidewalk", new Color(0.56f, 0.57f, 0.58f), sidewalkTexture, new Vector2(8f, 2f)),
                BuildingDark = GetOrCreateMaterial("BuildingDark", new Color(0.32f, 0.36f, 0.42f), buildingTexture, new Vector2(3f, 4f)),
                BuildingLight = GetOrCreateMaterial("BuildingLight", new Color(0.48f, 0.51f, 0.55f), buildingTexture, new Vector2(3f, 4f)),
                Concrete = GetOrCreateMaterial("Concrete", new Color(0.60f, 0.60f, 0.58f), concreteTexture, new Vector2(3f, 2f)),
                Crate = GetOrCreateMaterial("Crate", new Color(0.53f, 0.36f, 0.20f), crateTexture, new Vector2(2f, 2f)),
                Accent = GetOrCreateMaterial("Accent", new Color(0.88f, 0.55f, 0.14f), barrierTexture, new Vector2(4f, 2f)),
                Invisible = GetOrCreateMaterial("Invisible", Color.black, null, Vector2.one)
            };
        }

        private static Material GetOrCreateMaterial(
            string name,
            Color color,
            Texture2D texture,
            Vector2 textureScale)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = FindCompatibleLitShader();
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            SetMaterialColor(material, color);
            SetMaterialTexture(material, texture, textureScale);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private static void SetMaterialTexture(Material material, Texture2D texture, Vector2 scale)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", scale);
            }
        }

        private static Shader FindCompatibleLitShader()
        {
            var hasScriptablePipeline = GraphicsSettings.currentRenderPipeline != null;
            if (hasScriptablePipeline)
            {
                var urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null)
                    return urp;
            }

            return Shader.Find("Standard") ??
                   Shader.Find("Unlit/Texture") ??
                   Shader.Find("Unlit/Color");
        }

        private static Texture2D GetOrCreatePatternTexture(string name, TexturePattern pattern)
        {
            var path = $"{TextureDir}/{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            const int size = 64;
            if (texture == null)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = name,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(texture, path);
            }
            else if (texture.width != size || texture.height != size)
            {
                texture.Reinitialize(size, size, TextureFormat.RGBA32, false);
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    texture.SetPixel(x, y, PatternColor(pattern, x, y));
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Color PatternColor(TexturePattern pattern, int x, int y)
        {
            var noise = Hash01(x, y);
            switch (pattern)
            {
                case TexturePattern.Asphalt:
                {
                    var value = 0.72f + (noise - 0.5f) * 0.18f;
                    if ((x + y * 3) % 29 == 0)
                        value -= 0.16f;
                    return new Color(value, value, value, 1f);
                }
                case TexturePattern.Grid:
                {
                    var line = x % 16 == 0 || y % 16 == 0;
                    var value = line ? 0.60f : 0.88f + (noise - 0.5f) * 0.05f;
                    return new Color(value, value, value, 1f);
                }
                case TexturePattern.Panels:
                {
                    var seam = x % 16 == 0 || y % 20 == 0;
                    var window = (x % 16 > 4 && x % 16 < 12) && (y % 20 > 5 && y % 20 < 14);
                    if (seam)
                        return new Color(0.48f, 0.50f, 0.53f, 1f);
                    if (window)
                        return new Color(0.72f, 0.79f, 0.84f, 1f);
                    return new Color(0.90f, 0.91f, 0.92f, 1f);
                }
                case TexturePattern.Planks:
                {
                    var seam = y % 12 == 0 || x % 31 == 0;
                    var value = seam ? 0.55f : 0.88f + (noise - 0.5f) * 0.08f;
                    return new Color(value, value * 0.95f, value * 0.86f, 1f);
                }
                case TexturePattern.Stripes:
                {
                    var stripe = ((x + y) / 9) % 2 == 0;
                    return stripe
                        ? new Color(1f, 0.94f, 0.72f, 1f)
                        : new Color(0.45f, 0.45f, 0.43f, 1f);
                }
                default:
                {
                    var value = 0.84f + (noise - 0.5f) * 0.14f;
                    return new Color(value, value, value, 1f);
                }
            }
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

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private enum TexturePattern
        {
            Noise,
            Asphalt,
            Grid,
            Panels,
            Planks,
            Stripes
        }

        private sealed class MaterialSet
        {
            public Material Ground;
            public Material Road;
            public Material Sidewalk;
            public Material BuildingDark;
            public Material BuildingLight;
            public Material Concrete;
            public Material Crate;
            public Material Accent;
            public Material Invisible;
        }
    }
}
#endif
