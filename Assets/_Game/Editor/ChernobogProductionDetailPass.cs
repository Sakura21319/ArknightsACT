#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Production-detail pass layered on top of the deterministic Chernobog kit builder.
    ///
    /// The base kit establishes stable reusable prefabs. This pass enriches the most visible modules
    /// (vent walls and medium/large HVAC units) with authored hard-surface sub-assemblies so the scene
    /// stops reading as beveled boxes while the roguelite/map code remains unchanged.
    /// </summary>
    internal static class ChernobogProductionDetailPass
    {
        private const string GeneratedRoot = "Assets/_Game/Data/ChernobogKit";
        private const string MeshRoot = GeneratedRoot + "/Meshes";
        private const string MarkerName = "[ProductionDetail_V2]";

        private static readonly Dictionary<string, Mesh> MeshCache = new(StringComparer.Ordinal);

        private static void ApplyMenu()
        {
            var kit = ChernobogEnvironmentKitBuilder.EnsureBuilt();
            Apply(kit, true);
            Selection.activeObject = kit;
            EditorGUIUtility.PingObject(kit);
            Debug.Log("[ArknightsACT/ChernobogKit] Production detail pass applied to wall/HVAC modules.");
        }

        public static void EnsureApplied(ChernobogEnvironmentKit kit)
        {
            Apply(kit, false);
        }

        private static void Apply(ChernobogEnvironmentKit kit, bool force)
        {
            if (kit == null || !kit.IsUsable)
                return;

            MeshCache.Clear();
            EnhanceWallVent(kit.wallVent, kit, force);
            EnhanceHvac(kit.hvacMedium, 1.80f, 0.70f, 0.82f, kit, force);
            EnhanceHvac(kit.hvacLarge, 2.60f, 0.76f, 0.90f, kit, force);
            EnhanceElectrical(kit.electricalCabinet, kit, force);
            EnhanceCatwalk(kit.catwalk, kit, force);
            AssetDatabase.SaveAssets();
        }

        private static void EnhanceWallVent(GameObject prefab, ChernobogEnvironmentKit kit, bool force)
        {
            EditPrefab(prefab, force, detail =>
            {
                const float height = 1.55f;

                // A second structural frame sits proud of the original shell, giving the wall a
                // layered mobile-city panel silhouette rather than a flat repeated rectangle.
                AddBox(detail, "UpperShoulder", new Vector3(0f, 1.34f, 0.245f),
                    new Vector3(1.86f, 0.14f, 0.11f), 0.022f, kit.steelMaterial, true);
                AddBox(detail, "LowerKick", new Vector3(0f, 0.24f, 0.242f),
                    new Vector3(1.90f, 0.22f, 0.105f), 0.020f, kit.insetMaterial, true);
                AddBox(detail, "MidRail", new Vector3(0f, 0.91f, 0.248f),
                    new Vector3(1.84f, 0.075f, 0.085f), 0.016f, kit.steelMaterial, true);

                // Real louver depth: individual angled blades catch key light and create self-shadow.
                for (var i = -3; i <= 3; i++)
                {
                    AddBox(detail, "Louver", new Vector3(0f, 0.88f + i * 0.092f, 0.273f),
                        new Vector3(1.48f, 0.045f, 0.095f), 0.010f, kit.steelMaterial, true,
                        Quaternion.Euler(-18f, 0f, 0f));
                }

                AddBox(detail, "ServiceBay", new Vector3(0.72f, 0.43f, 0.305f),
                    new Vector3(0.34f, 0.33f, 0.10f), 0.018f, kit.wallMaterial, true);
                AddBox(detail, "ServiceBayInset", new Vector3(0.72f, 0.43f, 0.361f),
                    new Vector3(0.22f, 0.20f, 0.024f), 0.006f, kit.insetMaterial, false);
                AddBox(detail, "Optional_IDStrip", new Vector3(-0.66f, 0.35f, 0.307f),
                    new Vector3(0.33f, 0.055f, 0.026f), 0.008f, kit.accentMaterial, false);

                // A restrained conduit breaks perfect repetition without turning the wall into noise.
                AddCylinder(detail, "Optional_Conduit", new Vector3(0.94f, 0.84f, 0.286f),
                    0.034f, 0.78f, kit.steelMaterial, true);
                AddBox(detail, "ConduitClampA", new Vector3(0.94f, 0.58f, 0.298f),
                    new Vector3(0.13f, 0.055f, 0.075f), 0.012f, kit.insetMaterial, false);
                AddBox(detail, "ConduitClampB", new Vector3(0.94f, 1.09f, 0.298f),
                    new Vector3(0.13f, 0.055f, 0.075f), 0.012f, kit.insetMaterial, false);

                // Four visible fasteners are enough. Avoid the "every tile has a symbol" problem.
                AddBolt(detail, new Vector3(-0.84f, 1.25f, 0.314f), kit.insetMaterial);
                AddBolt(detail, new Vector3(0.84f, 1.25f, 0.314f), kit.insetMaterial);
                AddBolt(detail, new Vector3(-0.84f, 0.34f, 0.314f), kit.insetMaterial);
                AddBolt(detail, new Vector3(0.84f, 0.34f, 0.314f), kit.insetMaterial);

                // Tiny roof-side channel adds a silhouette break when viewed from the isometric camera.
                AddBox(detail, "TopCableChannel", new Vector3(-0.55f, height + 0.125f, -0.05f),
                    new Vector3(0.72f, 0.065f, 0.16f), 0.014f, kit.insetMaterial, true);
            });
        }

        private static void EnhanceHvac(
            GameObject prefab,
            float width,
            float height,
            float depth,
            ChernobogEnvironmentKit kit,
            bool force)
        {
            EditPrefab(prefab, force, detail =>
            {
                // Lift the visual mass on four feet so the unit reads as installed equipment rather
                // than a solid box extruded directly from the floor.
                var footX = width * 0.36f;
                var footZ = depth * 0.34f;
                AddFoot(detail, new Vector3(-footX, 0.055f, -footZ), kit);
                AddFoot(detail, new Vector3(footX, 0.055f, -footZ), kit);
                AddFoot(detail, new Vector3(-footX, 0.055f, footZ), kit);
                AddFoot(detail, new Vector3(footX, 0.055f, footZ), kit);

                AddBox(detail, "LowerShadowGap", new Vector3(0f, 0.11f, 0f),
                    new Vector3(width * 0.82f, 0.08f, depth * 0.84f), 0.015f, kit.insetMaterial, true);

                // Side service door with real frame, hinges and a small handle.
                var sideX = width * 0.5f + 0.028f;
                AddBox(detail, "SidePanel", new Vector3(sideX, height * 0.53f, 0f),
                    new Vector3(0.045f, height * 0.55f, depth * 0.62f), 0.008f, kit.insetMaterial, false);
                AddBox(detail, "SidePanelFrameTop", new Vector3(sideX + 0.018f, height * 0.78f, 0f),
                    new Vector3(0.045f, 0.055f, depth * 0.66f), 0.008f, kit.steelMaterial, false);
                AddBox(detail, "SidePanelFrameBottom", new Vector3(sideX + 0.018f, height * 0.29f, 0f),
                    new Vector3(0.045f, 0.055f, depth * 0.66f), 0.008f, kit.steelMaterial, false);
                AddBox(detail, "SideHandle", new Vector3(sideX + 0.055f, height * 0.53f, -depth * 0.20f),
                    new Vector3(0.055f, 0.20f, 0.055f), 0.012f, kit.steelMaterial, false);
                AddBolt(detail, new Vector3(sideX + 0.06f, height * 0.72f, depth * 0.24f), kit.steelMaterial, Quaternion.Euler(0f, 0f, 90f));
                AddBolt(detail, new Vector3(sideX + 0.06f, height * 0.34f, depth * 0.24f), kit.steelMaterial, Quaternion.Euler(0f, 0f, 90f));

                // Rear intake bank adds a second readable face if the cover is viewed from behind.
                AddBox(detail, "RearInset", new Vector3(0f, height * 0.52f, depth * 0.5f + 0.024f),
                    new Vector3(width * 0.72f, height * 0.48f, 0.040f), 0.006f, kit.insetMaterial, false);
                for (var i = -2; i <= 2; i++)
                {
                    AddBox(detail, "RearLouver", new Vector3(0f, height * 0.52f + i * height * 0.072f, depth * 0.5f + 0.055f),
                        new Vector3(width * 0.58f, 0.030f, 0.070f), 0.007f, kit.steelMaterial, true,
                        Quaternion.Euler(16f, 0f, 0f));
                }

                // Medium gets one rooftop fan, large gets two. Cylindrical fan housings and actual
                // blades produce a much stronger equipment read than another rectangular top box.
                var fanCount = width >= 2.2f ? 2 : 1;
                for (var fan = 0; fan < fanCount; fan++)
                {
                    var x = fanCount == 1 ? 0f : (fan == 0 ? -width * 0.22f : width * 0.22f);
                    BuildTopFan(detail, new Vector3(x, height + 0.12f, 0f),
                        Mathf.Min(depth * 0.29f, width * 0.18f), kit);
                }

                // Service conduit + one tiny ID strip, deliberately sparse.
                AddCylinder(detail, "Optional_ServicePipe", new Vector3(-width * 0.44f, height * 0.48f, depth * 0.38f),
                    0.035f, height * 0.58f, kit.steelMaterial, true);
                AddBox(detail, "Optional_IDStrip", new Vector3(-width * 0.18f, height * 0.84f, -depth * 0.5f - 0.060f),
                    new Vector3(width * 0.22f, 0.050f, 0.025f), 0.007f, kit.accentMaterial, false);
            });
        }

        private static void EnhanceElectrical(GameObject prefab, ChernobogEnvironmentKit kit, bool force)
        {
            EditPrefab(prefab, force, detail =>
            {
                AddBox(detail, "Plinth", new Vector3(0f, 0.075f, 0f),
                    new Vector3(0.70f, 0.15f, 0.54f), 0.020f, kit.insetMaterial, true);
                AddBox(detail, "DoorRib", new Vector3(-0.19f, 0.62f, -0.367f),
                    new Vector3(0.055f, 0.67f, 0.028f), 0.007f, kit.steelMaterial, false);
                AddBox(detail, "Handle", new Vector3(0.20f, 0.62f, -0.384f),
                    new Vector3(0.045f, 0.20f, 0.035f), 0.009f, kit.steelMaterial, false);
                AddBox(detail, "Optional_ServiceBox", new Vector3(0.31f, 0.26f, 0.34f),
                    new Vector3(0.24f, 0.28f, 0.16f), 0.020f, kit.wallMaterial, true);
                AddCylinder(detail, "CableDrop", new Vector3(-0.25f, 0.16f, 0.34f),
                    0.028f, 0.30f, kit.steelMaterial, true);
            });
        }

        private static void EnhanceCatwalk(GameObject prefab, ChernobogEnvironmentKit kit, bool force)
        {
            EditPrefab(prefab, force, detail =>
            {
                // Under-frame gives distant catwalks visible thickness and shadow at gameplay scale.
                AddBox(detail, "UnderChord", new Vector3(0f, -0.18f, 0f),
                    new Vector3(2.84f, 0.10f, 0.18f), 0.022f, kit.insetMaterial, true);
                for (var i = -2; i <= 2; i++)
                {
                    AddBox(detail, "CrossMember", new Vector3(i * 0.63f, -0.10f, 0f),
                        new Vector3(0.075f, 0.13f, 0.76f), 0.016f, kit.steelMaterial, true);
                }
            });
        }

        private static void BuildTopFan(Transform parent, Vector3 center, float radius, ChernobogEnvironmentKit kit)
        {
            AddCylinder(parent, "FanHousing", center, radius, 0.095f, kit.steelMaterial, true);
            AddCylinder(parent, "FanWell", center + Vector3.up * 0.052f, radius * 0.78f, 0.045f, kit.insetMaterial, false);
            AddCylinder(parent, "FanHub", center + Vector3.up * 0.095f, radius * 0.16f, 0.075f, kit.steelMaterial, true);

            for (var i = 0; i < 4; i++)
            {
                var blade = AddBox(parent, "FanBlade", center + Vector3.up * 0.090f,
                    new Vector3(radius * 1.20f, 0.025f, radius * 0.18f), 0.006f, kit.grateMaterial, false);
                blade.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
            }
        }

        private static void AddFoot(Transform parent, Vector3 position, ChernobogEnvironmentKit kit)
        {
            AddBox(parent, "EquipmentFoot", position, new Vector3(0.18f, 0.11f, 0.18f),
                0.025f, kit.steelMaterial, true);
        }

        private static void AddBolt(Transform parent, Vector3 position, Material material, Quaternion? rotation = null)
        {
            AddCylinder(parent, "Bolt", position, 0.025f, 0.024f, material, false,
                rotation ?? Quaternion.Euler(90f, 0f, 0f));
        }

        private static void EditPrefab(GameObject prefab, bool force, Action<Transform> build)
        {
            if (prefab == null || build == null)
                return;

            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old = root.transform.Find(MarkerName);
                if (old != null)
                {
                    if (!force)
                        return;
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                }

                var detail = new GameObject(MarkerName).transform;
                detail.SetParent(root.transform, false);
                build(detail);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject AddBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material,
            bool castShadows,
            Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetPersistentBox(size, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject AddCylinder(
            Transform parent,
            string name,
            Vector3 localPosition,
            float radius,
            float height,
            Material material,
            bool castShadows,
            Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = new Vector3(radius, Mathf.Max(0.001f, height * 0.5f), radius);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return go;
        }

        private static Mesh GetPersistentBox(Vector3 size, float bevel)
        {
            var id = $"P_{Mathf.RoundToInt(size.x * 1000f)}_{Mathf.RoundToInt(size.y * 1000f)}_{Mathf.RoundToInt(size.z * 1000f)}_{Mathf.RoundToInt(bevel * 1000f)}";
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
    }
}
#endif
