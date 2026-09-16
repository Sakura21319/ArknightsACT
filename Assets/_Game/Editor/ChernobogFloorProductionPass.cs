#if UNITY_EDITOR
using ArknightsACT.Gameplay.Roguelite.World;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Adds persistent multi-size floor assets on top of the base Chernobog kit.
    /// The goal is to break the visible 6x5 socket grid without changing collision or pit ownership.
    /// Only pit-safe central lanes are allowed to merge into large visual plates at runtime.
    /// </summary>
    internal static class ChernobogFloorProductionPass
    {
        private const string Root = "Assets/_Game/Data/ChernobogKit";
        private const string MeshRoot = Root + "/Meshes";
        private const string MaterialRoot = Root + "/Materials";

        private const float CellWidth = 14f / 6f;
        private const float CellDepth = 11f / 5f;
        private const float SurfaceGap = 0.045f;

        [MenuItem("ArknightsACT/Assets/Apply Chernobog Floor Composition Assets")]
        private static void ApplyMenu()
        {
            var kit = ChernobogEnvironmentKitBuilder.EnsureBuilt();
            EnsureApplied(kit, true);
            Selection.activeObject = kit;
            EditorGUIUtility.PingObject(kit);
            Debug.Log("[ArknightsACT/ChernobogKit] Multi-size floor composition assets applied.");
        }

        public static void EnsureApplied(ChernobogEnvironmentKit kit)
        {
            EnsureApplied(kit, false);
        }

        private static void EnsureApplied(ChernobogEnvironmentKit kit, bool force)
        {
            if (kit == null || !kit.IsUsable)
                return;

            if (force)
            {
                DeleteAssetIfExists($"{MeshRoot}/FloorPlate_LongX.asset");
                DeleteAssetIfExists($"{MeshRoot}/FloorPlate_LongZ.asset");
                DeleteAssetIfExists($"{MeshRoot}/FloorJoint_X.asset");
                DeleteAssetIfExists($"{MeshRoot}/FloorJoint_Z.asset");
                DeleteAssetIfExists($"{MaterialRoot}/Kit_DeckSecondary.mat");
            }

            kit.floorPlateLongXMesh = EnsureMesh(
                "FloorPlate_LongX",
                new Vector3(CellWidth * 3f - SurfaceGap, 0.032f, CellDepth - SurfaceGap),
                0.008f);
            kit.floorPlateLongZMesh = EnsureMesh(
                "FloorPlate_LongZ",
                new Vector3(CellWidth - SurfaceGap, 0.032f, CellDepth * 2f - SurfaceGap),
                0.008f);
            kit.floorJointXMesh = EnsureMesh(
                "FloorJoint_X",
                new Vector3(0.44f, 0.038f, 2.34f),
                0.009f);
            kit.floorJointZMesh = EnsureMesh(
                "FloorJoint_Z",
                new Vector3(2.48f, 0.038f, 0.44f),
                0.009f);
            kit.deckSecondaryMaterial = EnsureSecondaryDeckMaterial(kit.deckMaterial, force);

            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
        }

        private static Mesh EnsureMesh(string name, Vector3 size, float bevel)
        {
            var path = $"{MeshRoot}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
                return existing;

            var source = ChernobogBeveledMeshFactory.GetBox(size, bevel);
            var mesh = Object.Instantiate(source);
            mesh.name = name;
            mesh.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material EnsureSecondaryDeckMaterial(Material source, bool force)
        {
            if (source == null)
                return null;

            var path = $"{MaterialRoot}/Kit_DeckSecondary.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && !force)
                return existing;
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            var material = new Material(source)
            {
                name = "Kit_DeckSecondary"
            };
            var color = new Color(0.365f, 0.405f, 0.465f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.37f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.16f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.16f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }
}
#endif
