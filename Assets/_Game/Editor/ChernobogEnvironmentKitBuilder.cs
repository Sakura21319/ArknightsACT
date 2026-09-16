#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Builds the selected Concept-01 direction as persistent reusable Unity assets: meshes,
    /// materials and prefabs. The procedural stage then places these modules rather than constructing
    /// all presentation from transient primitive cubes.
    /// </summary>
    internal static class ChernobogEnvironmentKitBuilder
    {
        public const string Root = "Assets/_Game/Data/ChernobogKit";
        public const string KitPath = Root + "/ChernobogEnvironmentKit.asset";

        private const string MeshRoot = Root + "/Meshes";
        private const string MaterialRoot = Root + "/Materials";
        private const string TextureRoot = Root + "/Textures";
        private const string PrefabRoot = Root + "/Prefabs";
        private const string PrototypeMaterialRoot = "Assets/_Game/Data/Prototype25D/Materials";

        private static readonly Dictionary<string, Mesh> MeshCache = new(StringComparer.Ordinal);

        [MenuItem("ArknightsACT/Assets/Rebuild Chernobog Modular Kit")]
        private static void RebuildMenu()
        {
            var kit = Build(true);
            Selection.activeObject = kit;
            EditorGUIUtility.PingObject(kit);
            Debug.Log("[ArknightsACT/ChernobogKit] Persistent modular environment kit rebuilt.");
        }

        public static ChernobogEnvironmentKit EnsureBuilt()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ChernobogEnvironmentKit>(KitPath);
            return existing != null && existing.IsUsable ? existing : Build(false);
        }

        private static ChernobogEnvironmentKit Build(bool force)
        {
            if (force && AssetDatabase.IsValidFolder(Root))
                AssetDatabase.DeleteAsset(Root);

            EnsureFolder("Assets/_Game/Data", "ChernobogKit");
            EnsureFolder(Root, "Meshes");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Textures");
            EnsureFolder(Root, "Prefabs");
            MeshCache.Clear();

            var floorBase = LoadPrototypeMaterial("Facility_Floor") ?? LoadPrototypeMaterial("Ground_Tactical");
            var wallBase = LoadPrototypeMaterial("Facility_Wall") ?? LoadPrototypeMaterial("CombatCover") ?? floorBase;
            var coverBase = LoadPrototypeMaterial("CombatCover") ?? wallBase;
            var accentBase = LoadPrototypeMaterial("TacticalAccent") ?? floorBase;

            var deckAlbedo = CreateDeckTexture("Deck_Albedo", 256, false);
            var deckHeavyAlbedo = CreateDeckTexture("DeckHeavy_Albedo", 256, true);
            var wallAlbedo = CreatePanelTexture("Wall_Albedo", 256, new Color(0.48f, 0.53f, 0.60f), 52, 84);
            var coverAlbedo = CreatePanelTexture("Cover_Albedo", 256, new Color(0.40f, 0.45f, 0.52f), 64, 76);
            var grateAlbedo = CreateGrateTexture("Grate_Albedo", 256);
            var hardSurfaceNormal = CreateNormalTexture("HardSurface_Normal", 256, false);
            var grateNormal = CreateNormalTexture("Grate_Normal", 256, true);
            var hardSurfaceAo = CreateAoTexture("HardSurface_AO", 256, false);
            var grateAo = CreateAoTexture("Grate_AO", 256, true);

            var deck = CreateMaterial("Kit_Deck", floorBase, new Color(0.43f, 0.47f, 0.53f), 0.34f, 0.18f,
                deckAlbedo, hardSurfaceNormal, hardSurfaceAo);
            var deckHeavy = CreateMaterial("Kit_DeckHeavy", floorBase, new Color(0.33f, 0.37f, 0.43f), 0.38f, 0.16f,
                deckHeavyAlbedo, hardSurfaceNormal, hardSurfaceAo);
            var wall = CreateMaterial("Kit_Wall", wallBase, new Color(0.27f, 0.32f, 0.39f), 0.44f, 0.16f,
                wallAlbedo, hardSurfaceNormal, hardSurfaceAo);
            var inset = CreateMaterial("Kit_Inset", wallBase, new Color(0.085f, 0.105f, 0.135f), 0.30f, 0.09f,
                wallAlbedo, hardSurfaceNormal, hardSurfaceAo);
            var steel = CreateMaterial("Kit_Steel", wallBase, new Color(0.35f, 0.40f, 0.47f), 0.64f, 0.25f,
                null, hardSurfaceNormal, hardSurfaceAo);
            var grate = CreateMaterial("Kit_Grate", wallBase, new Color(0.10f, 0.12f, 0.15f), 0.52f, 0.07f,
                grateAlbedo, grateNormal, grateAo);
            var accent = CreateMaterial("Kit_Orange", accentBase, new Color(0.77f, 0.34f, 0.065f), 0.16f, 0.18f,
                null, null, null);
            var emissive = CreateMaterial("Kit_WarmLight", accentBase, new Color(1.0f, 0.50f, 0.13f), 0.08f, 0.32f,
                null, null, null);
            EnableEmission(emissive, new Color(3.0f, 1.15f, 0.18f, 1f));

            var floorPlate = CreateMeshAsset("FloorPlate", new Vector3(2.2883f, 0.024f, 2.155f), 0.006f);
            var floorPlateHeavy = CreateMeshAsset("FloorPlateHeavy", new Vector3(2.2883f, 0.036f, 2.155f), 0.008f);

            var floorGrate = BuildFloorGrate(grate, steel, inset);
            var serviceHatch = BuildServiceHatch(deckHeavy, steel, accent);
            var wallVent = BuildWallModule("WallVent", 1.55f, true, wall, inset, steel, grate, emissive);
            var wallSolid = BuildWallModule("WallSolid", 1.55f, false, wall, inset, steel, grate, emissive);
            var wallLow = BuildWallModule("WallLow", 0.72f, false, wall, inset, steel, grate, emissive);
            var wallCorner = BuildWallCorner(wall, inset, steel, accent);
            var hvacSmall = BuildHvac("HVAC_S", 1.20f, 0.66f, 0.74f, coverBase, wall, inset, steel, grate, accent);
            var hvacMedium = BuildHvac("HVAC_M", 1.80f, 0.70f, 0.82f, coverBase, wall, inset, steel, grate, accent);
            var hvacLarge = BuildHvac("HVAC_L", 2.60f, 0.76f, 0.90f, coverBase, wall, inset, steel, grate, accent);
            var electrical = BuildElectricalCabinet(wall, inset, steel, grate, accent);
            var pipeRun = BuildPipeRun(steel, inset);
            var catwalk = BuildCatwalk(deckHeavy, grate, steel, accent);
            var supportBeam = BuildSupportBeam(steel, inset);
            var pitFrame = BuildPitFrame(steel, inset, accent, emissive);

            var kit = ScriptableObject.CreateInstance<ChernobogEnvironmentKit>();
            kit.name = "ChernobogEnvironmentKit";
            kit.floorPlateMesh = floorPlate;
            kit.floorPlateHeavyMesh = floorPlateHeavy;
            kit.floorGrate = floorGrate;
            kit.floorServiceHatch = serviceHatch;
            kit.wallVent = wallVent;
            kit.wallSolid = wallSolid;
            kit.wallCorner = wallCorner;
            kit.wallLow = wallLow;
            kit.hvacSmall = hvacSmall;
            kit.hvacMedium = hvacMedium;
            kit.hvacLarge = hvacLarge;
            kit.electricalCabinet = electrical;
            kit.pipeRun = pipeRun;
            kit.catwalk = catwalk;
            kit.supportBeam = supportBeam;
            kit.pitFrame = pitFrame;
            kit.deckMaterial = deck;
            kit.deckHeavyMaterial = deckHeavy;
            kit.wallMaterial = wall;
            kit.insetMaterial = inset;
            kit.steelMaterial = steel;
            kit.grateMaterial = grate;
            kit.accentMaterial = accent;
            kit.emissiveMaterial = emissive;

            var oldKit = AssetDatabase.LoadAssetAtPath<ChernobogEnvironmentKit>(KitPath);
            if (oldKit != null)
                AssetDatabase.DeleteAsset(KitPath);
            AssetDatabase.CreateAsset(kit, KitPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<ChernobogEnvironmentKit>(KitPath);
        }

        private static GameObject BuildFloorGrate(Material grate, Material steel, Material inset)
        {
            return SavePrefab("Floor_Grate", root =>
            {
                AddBox(root.transform, "Recess", new Vector3(0f, 0.012f, 0f), new Vector3(1.18f, 0.024f, 0.62f), 0.008f, inset, false);
                AddBox(root.transform, "Grid", new Vector3(0f, 0.030f, 0f), new Vector3(1.06f, 0.018f, 0.50f), 0.004f, grate, false);
                AddFrame(root.transform, 1.18f, 0.62f, 0.042f, 0.055f, steel);
            });
        }

        private static GameObject BuildServiceHatch(Material deck, Material steel, Material accent)
        {
            return SavePrefab("Floor_ServiceHatch", root =>
            {
                AddBox(root.transform, "Plate", new Vector3(0f, 0.018f, 0f), new Vector3(1.08f, 0.036f, 0.82f), 0.010f, deck, false);
                AddFrame(root.transform, 1.08f, 0.82f, 0.035f, 0.055f, steel);
                AddBox(root.transform, "Latch", new Vector3(0.34f, 0.045f, -0.30f), new Vector3(0.18f, 0.026f, 0.065f), 0.009f, accent, false);
            });
        }

        private static GameObject BuildWallModule(
            string name, float height, bool vented, Material wall, Material inset, Material steel, Material grate, Material light)
        {
            return SavePrefab(name, root =>
            {
                AddBox(root.transform, "Backer", new Vector3(0f, height * 0.5f, 0f), new Vector3(2.36f, height, 0.34f), 0.045f, wall, true);
                AddBox(root.transform, "TopCap", new Vector3(0f, height + 0.055f, 0f), new Vector3(2.40f, 0.11f, 0.40f), 0.025f, steel, true);
                AddBox(root.transform, "PostL", new Vector3(-1.10f, height * 0.52f, 0.02f), new Vector3(0.15f, height + 0.10f, 0.42f), 0.030f, steel, true);
                AddBox(root.transform, "PostR", new Vector3(1.10f, height * 0.52f, 0.02f), new Vector3(0.15f, height + 0.10f, 0.42f), 0.030f, steel, true);
                AddBox(root.transform, "Inset", new Vector3(0f, height * 0.54f, 0.185f), new Vector3(1.88f, height * 0.52f, 0.035f), 0.006f, inset, false);

                if (vented)
                {
                    AddBox(root.transform, "VentFace", new Vector3(0f, height * 0.55f, 0.210f), new Vector3(1.68f, height * 0.36f, 0.020f), 0.003f, grate, false);
                    for (var y = -2; y <= 2; y++)
                        AddBox(root.transform, "VentBlade", new Vector3(0f, height * 0.55f + y * height * 0.055f, 0.226f), new Vector3(1.55f, 0.026f, 0.024f), 0.004f, steel, false);
                }

                AddBox(root.transform, "ServiceLight", new Vector3(0f, 0.13f, 0.225f), new Vector3(0.52f, 0.075f, 0.030f), 0.009f, light, false);
            });
        }

        private static GameObject BuildWallCorner(Material wall, Material inset, Material steel, Material accent)
        {
            return SavePrefab("WallCorner", root =>
            {
                AddBox(root.transform, "Body", new Vector3(0f, 0.84f, 0f), new Vector3(0.64f, 1.68f, 0.64f), 0.060f, inset, true);
                AddBox(root.transform, "OuterShell", new Vector3(0f, 0.84f, 0f), new Vector3(0.50f, 1.54f, 0.50f), 0.045f, wall, true);
                AddBox(root.transform, "Cap", new Vector3(0f, 1.72f, 0f), new Vector3(0.74f, 0.14f, 0.74f), 0.035f, steel, true);
                AddBox(root.transform, "BeaconStem", new Vector3(0f, 1.92f, 0f), new Vector3(0.08f, 0.34f, 0.08f), 0.018f, steel, false);
                AddBox(root.transform, "Beacon", new Vector3(0f, 2.10f, 0f), new Vector3(0.13f, 0.08f, 0.13f), 0.020f, accent, false);
            });
        }

        private static GameObject BuildHvac(
            string name, float width, float height, float depth, Material fallback, Material wall, Material inset,
            Material steel, Material grate, Material accent)
        {
            var bodyMaterial = wall != null ? wall : fallback;
            return SavePrefab(name, root =>
            {
                AddBox(root.transform, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), 0.055f, bodyMaterial, true);
                AddBox(root.transform, "FrontInset", new Vector3(0f, height * 0.50f, -depth * 0.5f - 0.018f), new Vector3(width * 0.74f, height * 0.54f, 0.036f), 0.006f, inset, false);
                AddBox(root.transform, "FrontGrill", new Vector3(0f, height * 0.50f, -depth * 0.5f - 0.041f), new Vector3(width * 0.58f, height * 0.36f, 0.018f), 0.003f, grate, false);
                AddBox(root.transform, "TopCap", new Vector3(0f, height + 0.045f, 0f), new Vector3(width * 0.94f, 0.09f, depth * 0.92f), 0.025f, steel, true);

                var post = Mathf.Min(0.12f, width * 0.11f);
                AddBox(root.transform, "CornerL", new Vector3(-width * 0.5f + post * 0.5f, height * 0.52f, 0f), new Vector3(post, height * 0.95f, depth + 0.08f), 0.026f, steel, true);
                AddBox(root.transform, "CornerR", new Vector3(width * 0.5f - post * 0.5f, height * 0.52f, 0f), new Vector3(post, height * 0.95f, depth + 0.08f), 0.026f, steel, true);
                AddBox(root.transform, "AssetStripe", new Vector3(-width * 0.15f, height + 0.094f, -depth * 0.16f), new Vector3(width * 0.44f, 0.028f, 0.060f), 0.008f, accent, false);
            });
        }

        private static GameObject BuildElectricalCabinet(Material wall, Material inset, Material steel, Material grate, Material accent)
        {
            return SavePrefab("ElectricalCabinet", root =>
            {
                const float width = 0.82f;
                const float height = 1.15f;
                const float depth = 0.62f;
                AddBox(root.transform, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), 0.050f, wall, true);
                AddBox(root.transform, "DoorInset", new Vector3(0f, 0.58f, -0.329f), new Vector3(0.62f, 0.78f, 0.035f), 0.008f, inset, false);
                AddBox(root.transform, "Vent", new Vector3(0f, 0.36f, -0.352f), new Vector3(0.48f, 0.24f, 0.018f), 0.004f, grate, false);
                AddBox(root.transform, "Header", new Vector3(0f, 1.17f, 0f), new Vector3(0.88f, 0.10f, 0.68f), 0.025f, steel, true);
                AddBox(root.transform, "IDStripe", new Vector3(0.20f, 0.93f, -0.355f), new Vector3(0.22f, 0.055f, 0.020f), 0.006f, accent, false);
            });
        }

        private static GameObject BuildPipeRun(Material steel, Material clampMaterial)
        {
            return SavePrefab("PipeRun", root =>
            {
                for (var i = 0; i < 2; i++)
                {
                    var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    cylinder.name = "Pipe";
                    cylinder.transform.SetParent(root.transform, false);
                    cylinder.transform.localPosition = new Vector3(0f, 0.12f + i * 0.20f, 0f);
                    cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    cylinder.transform.localScale = new Vector3(0.075f, 1.20f, 0.075f);
                    var collider = cylinder.GetComponent<Collider>();
                    if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
                    var renderer = cylinder.GetComponent<Renderer>();
                    if (renderer != null) renderer.sharedMaterial = steel;
                }

                for (var x = -1; x <= 1; x++)
                    AddBox(root.transform, "Clamp", new Vector3(x * 0.82f, 0.22f, 0f), new Vector3(0.09f, 0.55f, 0.24f), 0.022f, clampMaterial, true);
            });
        }

        private static GameObject BuildCatwalk(Material deck, Material grate, Material steel, Material accent)
        {
            return SavePrefab("Catwalk", root =>
            {
                AddBox(root.transform, "Deck", Vector3.zero, new Vector3(3.0f, 0.16f, 0.82f), 0.035f, deck, true);
                AddBox(root.transform, "Grate", new Vector3(0f, 0.095f, 0f), new Vector3(2.72f, 0.035f, 0.62f), 0.006f, grate, false);
                AddBox(root.transform, "RailFront", new Vector3(0f, 0.64f, -0.37f), new Vector3(3.0f, 0.07f, 0.07f), 0.020f, steel, true);
                AddBox(root.transform, "RailBack", new Vector3(0f, 0.64f, 0.37f), new Vector3(3.0f, 0.07f, 0.07f), 0.020f, steel, true);
                for (var i = -2; i <= 2; i++)
                {
                    AddBox(root.transform, "RailPost", new Vector3(i * 0.70f, 0.35f, -0.37f), new Vector3(0.06f, 0.62f, 0.06f), 0.018f, steel, true);
                    AddBox(root.transform, "RailPost", new Vector3(i * 0.70f, 0.35f, 0.37f), new Vector3(0.06f, 0.62f, 0.06f), 0.018f, steel, true);
                }
                AddBox(root.transform, "EdgeMark", new Vector3(-1.04f, 0.12f, -0.41f), new Vector3(0.55f, 0.04f, 0.05f), 0.009f, accent, false);
            });
        }

        private static GameObject BuildSupportBeam(Material steel, Material inset)
        {
            return SavePrefab("SupportBeam", root =>
            {
                AddBox(root.transform, "Web", Vector3.zero, new Vector3(2.6f, 0.16f, 0.16f), 0.025f, inset, true);
                AddBox(root.transform, "TopFlange", new Vector3(0f, 0.12f, 0f), new Vector3(2.6f, 0.10f, 0.34f), 0.025f, steel, true);
                AddBox(root.transform, "BottomFlange", new Vector3(0f, -0.12f, 0f), new Vector3(2.6f, 0.10f, 0.34f), 0.025f, steel, true);
            });
        }

        private static GameObject BuildPitFrame(Material steel, Material inset, Material accent, Material light)
        {
            return SavePrefab("PitFrame", root =>
            {
                const float width = 2.33f;
                const float depth = 2.20f;
                const float rail = 0.13f;
                AddBox(root.transform, "North", new Vector3(0f, 0.07f, depth * 0.5f), new Vector3(width + 0.20f, 0.14f, rail), 0.025f, steel, true);
                AddBox(root.transform, "South", new Vector3(0f, 0.07f, -depth * 0.5f), new Vector3(width + 0.20f, 0.14f, rail), 0.025f, steel, true);
                AddBox(root.transform, "East", new Vector3(width * 0.5f, 0.07f, 0f), new Vector3(rail, 0.14f, depth), 0.025f, steel, true);
                AddBox(root.transform, "West", new Vector3(-width * 0.5f, 0.07f, 0f), new Vector3(rail, 0.14f, depth), 0.025f, steel, true);
                AddBox(root.transform, "InnerNorth", new Vector3(0f, 0.045f, depth * 0.5f - 0.10f), new Vector3(width * 0.72f, 0.045f, 0.05f), 0.009f, inset, false);
                AddBox(root.transform, "WarningA", new Vector3(-0.62f, 0.155f, depth * 0.5f), new Vector3(0.42f, 0.025f, 0.06f), 0.007f, accent, false);
                AddBox(root.transform, "WarningB", new Vector3(0.62f, 0.155f, -depth * 0.5f), new Vector3(0.42f, 0.025f, 0.06f), 0.007f, accent, false);
                AddBox(root.transform, "PitLight", new Vector3(width * 0.5f + 0.018f, -0.08f, 0f), new Vector3(0.025f, 0.10f, 0.52f), 0.006f, light, false);
            });
        }

        private static GameObject SavePrefab(string name, Action<GameObject> build)
        {
            var path = $"{PrefabRoot}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var root = new GameObject(name);
            try
            {
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject AddBox(
            Transform parent, string name, Vector3 localPosition, Vector3 size, float bevel, Material material, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateMeshAsset(MeshId(size, bevel), size, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        private static void AddFrame(Transform parent, float width, float depth, float thickness, float height, Material material)
        {
            AddBox(parent, "FrameN", new Vector3(0f, height * 0.5f, depth * 0.5f - thickness * 0.5f), new Vector3(width, height, thickness), 0.010f, material, false);
            AddBox(parent, "FrameS", new Vector3(0f, height * 0.5f, -depth * 0.5f + thickness * 0.5f), new Vector3(width, height, thickness), 0.010f, material, false);
            AddBox(parent, "FrameE", new Vector3(width * 0.5f - thickness * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, depth), 0.010f, material, false);
            AddBox(parent, "FrameW", new Vector3(-width * 0.5f + thickness * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, depth), 0.010f, material, false);
        }

        private static Mesh CreateMeshAsset(string id, Vector3 size, float bevel)
        {
            if (MeshCache.TryGetValue(id, out var cached) && cached != null)
                return cached;

            var path = $"{MeshRoot}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                MeshCache[id] = existing;
                return existing;
            }

            var source = ChernobogBeveledMeshFactory.GetBox(size, bevel);
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = id;
            mesh.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(mesh, path);
            MeshCache[id] = mesh;
            return mesh;
        }

        private static string MeshId(Vector3 size, float bevel)
        {
            return $"B_{Mathf.RoundToInt(size.x * 1000f)}_{Mathf.RoundToInt(size.y * 1000f)}_{Mathf.RoundToInt(size.z * 1000f)}_{Mathf.RoundToInt(bevel * 1000f)}";
        }

        private static Material CreateMaterial(
            string name, Material source, Color tint, float metallic, float smoothness,
            Texture2D albedo, Texture2D normal, Texture2D ao)
        {
            var path = $"{MaterialRoot}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                AssetDatabase.DeleteAsset(path);

            Material material;
            if (source != null)
                material = new Material(source);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
            }

            material.name = name;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            SetTexture(material, "_BaseMap", "_MainTex", albedo);
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0.72f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (ao != null && material.HasProperty("_OcclusionMap"))
            {
                material.SetTexture("_OcclusionMap", ao);
                if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", 0.82f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetTexture(Material material, string primary, string fallback, Texture texture)
        {
            if (texture == null)
                return;
            if (material.HasProperty(primary)) material.SetTexture(primary, texture);
            if (material.HasProperty(fallback)) material.SetTexture(fallback, texture);
        }

        private static void EnableEmission(Material material, Color color)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
                return;
            material.SetColor("_EmissionColor", color);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        private static Texture2D CreateDeckTexture(string name, int size, bool heavy)
        {
            return CreateTextureAsset(name, size, false, (x, y) =>
            {
                var fine = Hash01(x * 11 + (heavy ? 97 : 7), y * 17 + (heavy ? 151 : 13));
                var broad = Hash01(x / 12 + 29, y / 12 + 53);
                var value = (heavy ? 0.64f : 0.74f) + (fine - 0.5f) * 0.055f + (broad - 0.5f) * 0.045f;
                if ((x + y * 3) % 149 == 0) value *= 0.90f;
                if ((x * 7 + y) % 211 == 0) value *= 1.05f;
                return new Color(value * 0.94f, value * 0.98f, value * 1.03f, 1f);
            });
        }

        private static Texture2D CreatePanelTexture(string name, int size, Color baseColor, int seamX, int seamY)
        {
            return CreateTextureAsset(name, size, false, (x, y) =>
            {
                var noise = Hash01(x * 5 + 17, y * 7 + 31);
                var seam = x % seamX < 3 || y % seamY < 3;
                var mul = seam ? 0.62f : 0.90f + (noise - 0.5f) * 0.08f;
                return new Color(baseColor.r * mul, baseColor.g * mul, baseColor.b * mul, 1f);
            });
        }

        private static Texture2D CreateGrateTexture(string name, int size)
        {
            return CreateTextureAsset(name, size, false, (x, y) =>
            {
                var bar = x % 16 < 4 || y % 16 < 4;
                var v = bar ? 0.38f : 0.075f;
                return new Color(v * 0.90f, v * 0.96f, v, 1f);
            });
        }

        private static Texture2D CreateNormalTexture(string name, int size, bool grate)
        {
            var height = new float[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var noise = (Hash01(x * 13 + 19, y * 17 + 23) - 0.5f) * 0.08f;
                height[y * size + x] = grate && x % 16 >= 4 && y % 16 >= 4 ? -0.60f : noise;
            }

            return CreateTextureAsset(name, size, true, (x, y) =>
            {
                var xm = (x - 1 + size) % size;
                var xp = (x + 1) % size;
                var ym = (y - 1 + size) % size;
                var yp = (y + 1) % size;
                var strength = grate ? 4.4f : 2.2f;
                var dx = (height[y * size + xm] - height[y * size + xp]) * strength;
                var dy = (height[ym * size + x] - height[yp * size + x]) * strength;
                var n = new Vector3(dx, dy, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            });
        }

        private static Texture2D CreateAoTexture(string name, int size, bool grate)
        {
            return CreateTextureAsset(name, size, true, (x, y) =>
            {
                var gap = grate && x % 16 >= 4 && y % 16 >= 4;
                var noise = Hash01(x * 3 + 67, y * 5 + 89);
                var ao = gap ? 0.24f : 0.82f + (noise - 0.5f) * 0.10f;
                return new Color(ao, ao, ao, 1f);
            });
        }

        private static Texture2D CreateTextureAsset(string name, int size, bool linear, Func<int, int, Color> pixel)
        {
            var path = $"{TextureRoot}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                texture.SetPixel(x, y, pixel(x, y));
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static Material LoadPrototypeMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{PrototypeMaterialRoot}/{name}.mat");
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
