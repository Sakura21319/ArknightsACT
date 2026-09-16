#if UNITY_EDITOR
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Adds a first production vertical-space test to the generated 2.5D arena without making
    /// the visual shell builder own gameplay-specific composition. Replaces the solid NE block
    /// with an enterable three-level structure connected by CharacterController-safe ramps.
    /// </summary>
    internal static class Prototype25DVerticalityEnhancer
    {
        public static void Apply()
        {
            var map = GameObject.Find("[3D Map]")?.transform;
            if (map == null)
            {
                Debug.LogWarning("[ArknightsACT/25D] Cannot build vertical test structure: [3D Map] not found.");
                return;
            }

            var original = GameObject.Find("Building_NE");
            var buildingMaterial = original != null
                ? original.GetComponent<Renderer>()?.sharedMaterial
                : null;
            var floorMaterial = GameObject.Find("Median_01")?.GetComponent<Renderer>()?.sharedMaterial
                                ?? buildingMaterial;
            var accentMaterial = GameObject.Find("Barrier_A")?.GetComponent<Renderer>()?.sharedMaterial
                                 ?? floorMaterial;

            if (original != null)
                Object.DestroyImmediate(original);

            var oldGenerated = GameObject.Find("Building_NE_Walkable");
            if (oldGenerated != null)
                Object.DestroyImmediate(oldGenerated);

            var root = new GameObject("Building_NE_Walkable");
            root.transform.SetParent(map, true);

            const float centerX = 8.9f;
            const float centerZ = 6.7f;
            const float width = 5.1f;
            const float depth = 4.1f;
            const float floorThickness = 0.18f;
            const float groundY = 0.18f;
            const float level1Y = 2.18f;
            const float level2Y = 4.18f;

            // Three traversable surfaces: ground floor, second floor, and roof deck.
            CreateBlock(root.transform, "Floor_Ground", new Vector3(centerX, groundY - floorThickness * 0.5f, centerZ),
                new Vector3(width, floorThickness, depth), floorMaterial);
            CreateBlock(root.transform, "Floor_Level1", new Vector3(centerX, level1Y - floorThickness * 0.5f, centerZ),
                new Vector3(width, floorThickness, depth), floorMaterial);
            CreateBlock(root.transform, "Floor_Roof", new Vector3(centerX, level2Y - floorThickness * 0.5f, centerZ),
                new Vector3(width, floorThickness, depth), floorMaterial);

            // Back and side walls remain real 3D colliders, so they also occlude enemy vision.
            CreateBlock(root.transform, "Wall_Back", new Vector3(centerX, 2.15f, centerZ + depth * 0.5f),
                new Vector3(width, 4.3f, 0.22f), buildingMaterial);
            CreateBlock(root.transform, "Wall_Left", new Vector3(centerX - width * 0.5f, 2.15f, centerZ),
                new Vector3(0.22f, 4.3f, depth), buildingMaterial);
            CreateBlock(root.transform, "Wall_Right", new Vector3(centerX + width * 0.5f, 2.15f, centerZ),
                new Vector3(0.22f, 4.3f, depth), buildingMaterial);

            // Front facade is intentionally split to leave a wide entrance from the sidewalk.
            CreateBlock(root.transform, "FrontColumn_Left", new Vector3(centerX - 2.05f, 1.05f, centerZ - depth * 0.5f),
                new Vector3(0.70f, 2.1f, 0.22f), buildingMaterial);
            CreateBlock(root.transform, "FrontColumn_Right", new Vector3(centerX + 2.05f, 1.05f, centerZ - depth * 0.5f),
                new Vector3(0.70f, 2.1f, 0.22f), buildingMaterial);

            // Zig-zag ramps: ~28 degrees, comfortably below the player's 45-degree slope limit.
            CreateRamp(root.transform, "Ramp_Ground_To_L1",
                new Vector3(7.00f, groundY + 0.05f, 5.55f),
                new Vector3(10.75f, level1Y + 0.05f, 5.55f),
                0.92f,
                floorMaterial);
            CreateRamp(root.transform, "Ramp_L1_To_Roof",
                new Vector3(10.75f, level1Y + 0.05f, 7.35f),
                new Vector3(7.00f, level2Y + 0.05f, 7.35f),
                0.92f,
                floorMaterial);

            // Small landing markers make the route readable without blocking traversal.
            CreateBlock(root.transform, "LandingMarker_L1", new Vector3(10.72f, level1Y + 0.025f, 6.48f),
                new Vector3(0.55f, 0.05f, 0.55f), accentMaterial);
            CreateBlock(root.transform, "LandingMarker_Roof", new Vector3(7.03f, level2Y + 0.025f, 7.35f),
                new Vector3(0.55f, 0.05f, 0.55f), accentMaterial);

            Debug.Log("[ArknightsACT/25D] Added walkable NE building: ground floor -> level 2 -> roof deck.");
        }

        private static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            return go;
        }

        private static GameObject CreateRamp(
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
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            return go;
        }
    }
}
#endif
