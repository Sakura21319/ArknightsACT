#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Navigation;
using ArknightsACT.Gameplay.Prototype25D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Editor
{
    public static class Prototype25DSceneBuilder
    {
        private const string SceneDir = "Assets/_Game/Scenes";
        private const string ScenePath = SceneDir + "/Prototype25D.unity";
        private const string DataRoot = "Assets/_Game/Data/Prototype25D";
        private const string MaterialDir = DataRoot + "/Materials";
        private const string TextureDir = DataRoot + "/Textures";

        // One grid of combat cover: too tall for CharacterController.stepOffset, but comfortably
        // below Ch'en's ~1.4 m jump apex. This is now the only generated obstacle height.
        private const float CoverGridHeight = 0.82f;
        private const float GroundSurfaceY = 0.18f;
        private const float SecondFloorY = 2.10f;

        private static readonly Vector3 CameraOffset = new(-10.5f, 6.8f, -10.5f);

        [MenuItem("ArknightsACT/Build 2.5D Demo Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "ArknightsACT",
                    "不能在 Play Mode 中构建场景。请先点击 Unity 顶部的停止按钮，再重新执行构建。",
                    "确定");
                Debug.LogWarning("[ArknightsACT/25D] 已阻止 Play Mode 内的场景构建。请先退出 Play Mode。");
                return;
            }

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
            var playerEntity = player.GetComponent<CombatEntity>();

            BuildEnemy("Enemy_Soldier", PrtsPrototypeAssetCatalog.PrototypeEnemies[1], new Vector3(4.2f, 0.03f, 2.2f), Vector3.forward, camera, playerEntity, materials.Accent, 72f);
            BuildEnemy("Enemy_Hound", PrtsPrototypeAssetCatalog.PrototypeEnemies[3], new Vector3(-4.4f, 0.03f, 4.2f), Vector3.back, camera, playerEntity, materials.Accent, 52f);
            BuildEnemy("Enemy_Crossbowman", PrtsPrototypeAssetCatalog.PrototypeEnemies[2], new Vector3(5.8f, 0.03f, -3.8f), Vector3.left, camera, playerEntity, materials.Accent, 62f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);

            Debug.Log(
                $"ArknightsACT 2.5D combat demo generated: {ScenePath}. " +
                "Unified layout: one walkable two-floor facility plus sparse one-grid combat cover. " +
                "WASD move, Space jump, J/LMB basic attack.");
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.43f, 0.46f, 0.51f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.075f, 0.090f, 0.115f);
            RenderSettings.fogStartDistance = 20f;
            RenderSettings.fogEndDistance = 46f;

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(43f, -35f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.90f, 0.94f, 1f);
            light.intensity = 1.08f;
            light.shadows = LightShadows.Soft;
        }

        private static void BuildMap(MaterialSet m)
        {
            var root = new GameObject("[3D Map]").transform;

            BuildGroundAndRoads(root, m);
            BuildMainFacility(root, m);
            BuildCombatCover(root, m);
            BuildNavigationGraph(root);
            BuildMapBounds(root, m);
        }

        private static void BuildGroundAndRoads(Transform root, MaterialSet m)
        {
            CreateBlock(root, "Ground", new Vector3(0f, -0.30f, 0f), new Vector3(26f, 0.6f, 20f), m.Ground);
            CreateBlock(root, "Road_Main", new Vector3(0f, 0.015f, 0f), new Vector3(19.0f, 0.03f, 7.2f), m.Road);
            CreateBlock(root, "Road_Cross", new Vector3(-3.8f, 0.025f, 0f), new Vector3(4.8f, 0.04f, 16.8f), m.Road);

            CreateBlock(root, "Sidewalk_North", new Vector3(0f, 0.08f, 4.55f), new Vector3(18.8f, 0.16f, 1.65f), m.Sidewalk);
            CreateBlock(root, "Sidewalk_South", new Vector3(0f, 0.08f, -4.55f), new Vector3(18.8f, 0.16f, 1.65f), m.Sidewalk);
            CreateBlock(root, "Sidewalk_West", new Vector3(-6.95f, 0.08f, 0f), new Vector3(1.65f, 0.16f, 16.8f), m.Sidewalk);
        }

        private static void BuildMainFacility(Transform root, MaterialSet m)
        {
            var building = new GameObject("Facility_01_TwoFloor");
            building.transform.SetParent(root, true);
            var parent = building.transform;

            const float centerX = 8.35f;
            const float centerZ = 6.05f;
            const float width = 5.8f;
            const float depth = 5.2f;
            const float slab = 0.18f;
            const float wallHeight = 1.90f;

            // Ground and second floor are the only two walkable levels. No third rooftop route.
            CreateBlock(parent, "Floor_Ground",
                new Vector3(centerX, GroundSurfaceY - slab * 0.5f, centerZ),
                new Vector3(width, slab, depth), m.FacilityFloor);
            CreateBlock(parent, "Floor_Second",
                new Vector3(centerX, SecondFloorY - slab * 0.5f, centerZ),
                new Vector3(width, slab, depth), m.FacilityFloor);

            // Camera-facing southwest sides stay mostly open for readability. North/east walls and
            // structural columns provide real occlusion and a tactical industrial silhouette.
            var groundWallY = GroundSurfaceY + wallHeight * 0.5f;
            CreateBlock(parent, "Wall_North_Ground",
                new Vector3(centerX, groundWallY, centerZ + depth * 0.5f),
                new Vector3(width, wallHeight, 0.24f), m.FacilityWall);
            CreateBlock(parent, "Wall_East_Ground",
                new Vector3(centerX + width * 0.5f, groundWallY, centerZ),
                new Vector3(0.24f, wallHeight, depth), m.FacilityWall);
            CreateBlock(parent, "Column_SW",
                new Vector3(centerX - width * 0.5f, groundWallY, centerZ - depth * 0.5f + 0.45f),
                new Vector3(0.42f, wallHeight, 0.90f), m.FacilityWall);
            CreateBlock(parent, "Column_SE",
                new Vector3(centerX + 1.95f, groundWallY, centerZ - depth * 0.5f),
                new Vector3(0.70f, wallHeight, 0.24f), m.FacilityWall);

            // Second-floor waist walls are low enough to keep the character readable while still
            // acting as physical/LOS cover inside the building.
            const float railHeight = 0.78f;
            var railY = SecondFloorY + railHeight * 0.5f;
            CreateBlock(parent, "Wall_North_Second",
                new Vector3(centerX, railY, centerZ + depth * 0.5f),
                new Vector3(width, railHeight, 0.22f), m.FacilityWall);
            CreateBlock(parent, "Wall_East_Second",
                new Vector3(centerX + width * 0.5f, railY, centerZ),
                new Vector3(0.22f, railHeight, depth), m.FacilityWall);

            // External access ramp: the player cannot simply walk up one-grid cover, but can use
            // this deliberate route to reach floor two. Slope stays safely below 45 degrees.
            CreateRamp(parent, "AccessRamp_To_Second",
                new Vector3(4.95f, GroundSurfaceY + 0.05f, 3.65f),
                new Vector3(4.95f, SecondFloorY + 0.05f, 8.15f),
                0.88f,
                m.FacilityFloor);
            CreateBlock(parent, "RampLanding_Second",
                new Vector3(5.35f, SecondFloorY - 0.07f, 8.05f),
                new Vector3(1.35f, 0.14f, 1.05f), m.FacilityFloor);

            // Original tactical accents inspired by mobile industrial sci-fi spaces: cold metal,
            // modular paneling and sparse hazard-orange identifiers without copying game assets.
            CreateVisualBlock(parent, "Facility_Header_Orange",
                new Vector3(centerX - 0.25f, 1.58f, centerZ - depth * 0.5f - 0.13f),
                new Vector3(2.70f, 0.18f, 0.05f), m.Accent);
            CreateVisualBlock(parent, "Facility_ID_Panel",
                new Vector3(centerX + 1.25f, 1.10f, centerZ - depth * 0.5f - 0.14f),
                new Vector3(0.72f, 0.55f, 0.05f), m.Signal);
            CreateVisualBlock(parent, "Facility_SecondFloor_Stripe",
                new Vector3(centerX + 1.10f, SecondFloorY + 0.08f, centerZ - depth * 0.5f - 0.08f),
                new Vector3(2.25f, 0.12f, 0.04f), m.Accent);

            // A single one-grid internal cover keeps the upper floor tactically useful without
            // turning the building into an obstacle maze.
            CreateGridCover(parent, "Cover_SecondFloor",
                new Vector3(8.55f, SecondFloorY, 6.35f),
                new Vector2(1.55f, 0.72f), 0f, m);
        }

        private static void BuildCombatCover(Transform root, MaterialSet m)
        {
            var covers = new[]
            {
                new CoverSpec("Cover_A", new Vector2(-2.25f, -2.55f), new Vector2(1.90f, 0.72f), 0f),
                new CoverSpec("Cover_B", new Vector2(-4.65f, 2.35f), new Vector2(1.55f, 0.78f), 18f),
                new CoverSpec("Cover_C", new Vector2(1.15f, 4.20f), new Vector2(1.10f, 1.10f), 0f),
                new CoverSpec("Cover_D", new Vector2(6.55f, -1.15f), new Vector2(1.75f, 0.72f), -15f)
            };

            for (var i = 0; i < covers.Length; i++)
            {
                var cover = covers[i];
                CreateGridCover(root, cover.Name,
                    new Vector3(cover.Position.x, 0f, cover.Position.y),
                    cover.Footprint, cover.Yaw, m);
            }
        }

        private static void CreateGridCover(
            Transform parent,
            string name,
            Vector3 basePosition,
            Vector2 footprint,
            float yaw,
            MaterialSet m)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = basePosition;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var body = CreateBlock(root.transform, "Body",
                new Vector3(0f, CoverGridHeight * 0.5f, 0f),
                new Vector3(footprint.x, CoverGridHeight, footprint.y), m.Cover);
            body.transform.localPosition = new Vector3(0f, CoverGridHeight * 0.5f, 0f);
            body.transform.localRotation = Quaternion.identity;

            CreateVisualBlock(root.transform, "TopTrim",
                new Vector3(0f, CoverGridHeight + 0.025f, 0f),
                new Vector3(footprint.x * 0.94f, 0.05f, footprint.y * 0.94f), m.Accent, localSpace: true);

            if (footprint.x >= footprint.y)
            {
                CreateVisualBlock(root.transform, "HazardBand",
                    new Vector3(0f, CoverGridHeight * 0.56f, -footprint.y * 0.5f - 0.011f),
                    new Vector3(footprint.x * 0.72f, 0.16f, 0.02f), m.Hazard, localSpace: true);
            }
            else
            {
                CreateVisualBlock(root.transform, "HazardBand",
                    new Vector3(-footprint.x * 0.5f - 0.011f, CoverGridHeight * 0.56f, 0f),
                    new Vector3(0.02f, 0.16f, footprint.y * 0.72f), m.Hazard, localSpace: true);
            }
        }

        private static void BuildNavigationGraph(Transform map)
        {
            var navObject = new GameObject("[Navigation25D]");
            navObject.transform.SetParent(map, true);
            var graph = navObject.AddComponent<PrototypeNavigationGraph25D>();

            // Sparse street ring + one deterministic building chain. These coordinates are kept
            // alongside the building/cover layout so future map edits have a single source of truth.
            var nodes = new[]
            {
                new Vector3(0.0f, GroundSurfaceY, 0.0f),
                new Vector3(4.3f, GroundSurfaceY, -3.2f),
                new Vector3(8.4f, GroundSurfaceY, -2.9f),
                new Vector3(4.2f, GroundSurfaceY, 2.6f),
                new Vector3(4.80f, GroundSurfaceY, 3.45f),
                new Vector3(7.6f, GroundSurfaceY, 3.65f),
                new Vector3(8.4f, GroundSurfaceY, 5.75f),
                new Vector3(4.95f, GroundSurfaceY, 3.65f),
                new Vector3(4.95f, SecondFloorY, 8.15f),
                new Vector3(5.75f, SecondFloorY, 7.65f),
                new Vector3(8.15f, SecondFloorY, 6.15f),
                new Vector3(-3.8f, GroundSurfaceY, 3.4f),
                new Vector3(-4.1f, GroundSurfaceY, -3.5f),
                new Vector3(0.2f, GroundSurfaceY, -4.6f)
            };

            graph.Configure(nodes, new[]
            {
                0, 1,
                0, 3,
                0, 11,
                0, 12,
                1, 2,
                1, 13,
                11, 12,
                3, 4,
                3, 5,
                4, 7,
                5, 6,
                7, 8,
                8, 9,
                9, 10
            });
        }

        private static void BuildMapBounds(Transform root, MaterialSet m)
        {
            CreateBlock(root, "MapBound_N", new Vector3(0f, 1f, 9.9f), new Vector3(26f, 2f, 0.2f), m.Invisible, false);
            CreateBlock(root, "MapBound_S", new Vector3(0f, 1f, -9.9f), new Vector3(26f, 2f, 0.2f), m.Invisible, false);
            CreateBlock(root, "MapBound_E", new Vector3(12.9f, 1f, 0f), new Vector3(0.2f, 2f, 20f), m.Invisible, false);
            CreateBlock(root, "MapBound_W", new Vector3(-12.9f, 1f, 0f), new Vector3(0.2f, 2f, 20f), m.Invisible, false);
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = CameraOffset;
            go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.75f, 0f) - CameraOffset, Vector3.up);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.070f, 0.083f, 0.105f);
            go.AddComponent<AudioListener>();
            go.AddComponent<Prototype25DCameraFollow>();
            return camera;
        }

        private static GameObject BuildPlayer(Camera camera)
        {
            var player = new GameObject("Player_Chen_25D");
            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.34f;
            controller.height = 1.55f;
            controller.center = new Vector3(0f, 0.78f, 0f);
            controller.stepOffset = 0.28f;

            var health = player.AddComponent<Health>();
            health.SetMaxHealth(100f);
            var entity = player.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);

            var motor = player.AddComponent<Prototype25DPlayerMotor>();
            motor.SetCamera(camera);
            player.AddComponent<Prototype25DPlayerCombat>();

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(player.transform, false);
            billboard.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            billboard.AddComponent<Prototype25DBillboard>().Configure(camera, motor);
            AttachPresentation(billboard.transform, PrtsPrototypeAssetCatalog.Chen, new Color(0.56f, 0.72f, 0.90f));
            return player;
        }

        private static void BuildEnemy(
            string objectName,
            PrtsAssetDescriptor descriptor,
            Vector3 position,
            Vector3 initialForward,
            Camera camera,
            CombatEntity player,
            Material fallbackMaterial,
            float maxHealth)
        {
            var root = new GameObject(objectName);
            root.transform.position = position;

            var controller = root.AddComponent<CharacterController>();
            controller.radius = descriptor.Role == "FastMelee" ? 0.30f : 0.36f;
            controller.height = descriptor.Role == "FastMelee" ? 1.0f : 1.45f;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            controller.stepOffset = 0.2f;

            var health = root.AddComponent<Health>();
            health.SetMaxHealth(maxHealth);
            var entity = root.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);

            var brain = root.AddComponent<Prototype25DEnemyBrain>();
            brain.Configure(player, initialForward, descriptor.Role == "FastMelee" ? 5.4f : 6.3f, descriptor.Role == "Ranged" ? 70f : 82f);
            root.AddComponent<Prototype25DVisionCone>();

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(root.transform, false);
            billboard.AddComponent<Prototype25DBillboard>().Configure(camera, null);
            AttachPresentation(billboard.transform, descriptor, fallbackMaterial.color);

            health.Died += () => Object.Destroy(root, 0.08f);
        }

        private static void AttachPresentation(Transform parent, PrtsAssetDescriptor descriptor, Color fallbackColor)
        {
            if (PrtsGeneratedPresentation.TryAttach(descriptor.BaseName, parent, out var presentation))
            {
                presentation.transform.localPosition = new Vector3(0f, -descriptor.FeetLocalY, 0f);
                return;
            }
            CreateFlatFallback(parent, descriptor.DisplayName + "_Fallback", -descriptor.FeetLocalY, fallbackColor);
        }

        private static void CreateFlatFallback(Transform parent, string name, float centerY, Color color)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, centerY, 0f);
            quad.transform.localScale = new Vector3(0.85f, 1.55f, 1f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<Renderer>();
            var material = new Material(FindCompatibleLitShader()) { name = name + "_RuntimeMaterial" };
            SetMaterialColor(material, color);
            renderer.sharedMaterial = material;
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool visible = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = visible;
            return go;
        }

        private static void CreateVisualBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool localSpace = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (localSpace)
                go.transform.localPosition = position;
            else
                go.transform.position = position;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
        }

        private static void CreateRamp(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float width,
            Material material)
        {
            var delta = end - start;
            var length = delta.magnitude;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = (start + end) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            go.transform.localScale = new Vector3(width, 0.16f, length);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
        }

        private static MaterialSet BuildMaterials()
        {
            var ground = GetOrCreatePatternTexture("TacticalGround", TexturePattern.Noise);
            var road = GetOrCreatePatternTexture("TacticalRoad", TexturePattern.Asphalt);
            var sidewalk = GetOrCreatePatternTexture("TacticalSidewalk", TexturePattern.TacticalGrid);
            var facilityPanels = GetOrCreatePatternTexture("FacilityPanels", TexturePattern.FacilityPanels);
            var metalDeck = GetOrCreatePatternTexture("FacilityDeck", TexturePattern.MetalDeck);
            var coverPanels = GetOrCreatePatternTexture("CombatCoverPanels", TexturePattern.CoverPanels);
            var hazard = GetOrCreatePatternTexture("HazardStripes", TexturePattern.HazardStripes);

            return new MaterialSet
            {
                Ground = GetOrCreateMaterial("Ground_Tactical", new Color(0.28f, 0.30f, 0.30f), ground, new Vector2(8f, 6f), 0.02f, 0.12f),
                Road = GetOrCreateMaterial("Road_Tactical", new Color(0.16f, 0.18f, 0.21f), road, new Vector2(10f, 4f), 0.02f, 0.10f),
                Sidewalk = GetOrCreateMaterial("Sidewalk_Tactical", new Color(0.40f, 0.42f, 0.44f), sidewalk, new Vector2(8f, 2f), 0.08f, 0.18f),
                FacilityWall = GetOrCreateMaterial("Facility_Wall", new Color(0.31f, 0.35f, 0.40f), facilityPanels, new Vector2(3f, 4f), 0.34f, 0.28f),
                FacilityFloor = GetOrCreateMaterial("Facility_Floor", new Color(0.36f, 0.39f, 0.42f), metalDeck, new Vector2(5f, 5f), 0.30f, 0.24f),
                Cover = GetOrCreateMaterial("CombatCover", new Color(0.30f, 0.33f, 0.36f), coverPanels, new Vector2(2f, 2f), 0.38f, 0.24f),
                Accent = GetOrCreateMaterial("TacticalAccent", new Color(0.95f, 0.49f, 0.11f), null, Vector2.one, 0.20f, 0.30f),
                Hazard = GetOrCreateMaterial("HazardBand", Color.white, hazard, new Vector2(3f, 1f), 0.15f, 0.20f),
                Signal = GetOrCreateMaterial("FacilitySignal", new Color(0.55f, 0.82f, 0.88f), facilityPanels, new Vector2(1f, 1f), 0.25f, 0.42f),
                Invisible = GetOrCreateMaterial("Invisible", Color.black, null, Vector2.one, 0f, 0f)
            };
        }

        private static Material GetOrCreateMaterial(
            string name,
            Color color,
            Texture2D texture,
            Vector2 scale,
            float metallic,
            float smoothness)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var shader = FindCompatibleLitShader();
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
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
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static Shader FindCompatibleLitShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null) return urp;
            }
            return Shader.Find("Standard") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Unlit/Color");
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

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                texture.SetPixel(x, y, PatternColor(pattern, x, y));

            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Color PatternColor(TexturePattern pattern, int x, int y)
        {
            var noise = Hash01(x, y);

            switch (pattern)
            {
                case TexturePattern.TacticalGrid:
                {
                    var seam = x % 16 == 0 || y % 16 == 0;
                    var v = seam ? 0.54f : 0.86f + (noise - 0.5f) * 0.06f;
                    return new Color(v * 0.96f, v * 0.99f, v, 1f);
                }
                case TexturePattern.FacilityPanels:
                {
                    var verticalSeam = x % 16 == 0;
                    var horizontalSeam = y % 20 == 0;
                    if (verticalSeam || horizontalSeam)
                        return new Color(0.36f, 0.39f, 0.43f, 1f);

                    var inset = x % 16 > 3 && x % 16 < 13 && y % 20 > 4 && y % 20 < 16;
                    if (inset)
                    {
                        var v = 0.78f + (noise - 0.5f) * 0.05f;
                        return new Color(v * 0.90f, v * 0.96f, v, 1f);
                    }

                    var baseV = 0.64f + (noise - 0.5f) * 0.04f;
                    return new Color(baseV * 0.90f, baseV * 0.94f, baseV, 1f);
                }
                case TexturePattern.MetalDeck:
                {
                    var seam = x % 12 == 0 || y % 12 == 0;
                    var bolt = x % 12 == 2 && y % 12 == 2;
                    if (bolt) return new Color(0.35f, 0.38f, 0.42f, 1f);
                    var v = seam ? 0.52f : 0.82f + (noise - 0.5f) * 0.05f;
                    return new Color(v * 0.94f, v * 0.97f, v, 1f);
                }
                case TexturePattern.CoverPanels:
                {
                    if (y >= 46 && y <= 55)
                    {
                        var yellow = ((x + y * 2) / 8) % 2 == 0;
                        return yellow
                            ? new Color(0.96f, 0.69f, 0.15f, 1f)
                            : new Color(0.18f, 0.19f, 0.20f, 1f);
                    }
                    var seam = x % 24 == 0 || y % 22 == 0;
                    var v = seam ? 0.43f : 0.76f + (noise - 0.5f) * 0.06f;
                    return new Color(v * 0.90f, v * 0.95f, v, 1f);
                }
                case TexturePattern.HazardStripes:
                    return ((x + y) / 9) % 2 == 0
                        ? new Color(0.98f, 0.65f, 0.12f, 1f)
                        : new Color(0.15f, 0.16f, 0.17f, 1f);
            }

            var value = pattern == TexturePattern.Asphalt
                ? 0.70f + (noise - 0.5f) * 0.18f
                : 0.82f + (noise - 0.5f) * 0.14f;
            return new Color(value, value, value, 1f);
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

        private readonly struct CoverSpec
        {
            public readonly string Name;
            public readonly Vector2 Position;
            public readonly Vector2 Footprint;
            public readonly float Yaw;

            public CoverSpec(string name, Vector2 position, Vector2 footprint, float yaw)
            {
                Name = name;
                Position = position;
                Footprint = footprint;
                Yaw = yaw;
            }
        }

        private enum TexturePattern
        {
            Noise,
            Asphalt,
            TacticalGrid,
            FacilityPanels,
            MetalDeck,
            CoverPanels,
            HazardStripes
        }

        private sealed class MaterialSet
        {
            public Material Ground;
            public Material Road;
            public Material Sidewalk;
            public Material FacilityWall;
            public Material FacilityFloor;
            public Material Cover;
            public Material Accent;
            public Material Hazard;
            public Material Signal;
            public Material Invisible;
        }
    }
}
#endif
