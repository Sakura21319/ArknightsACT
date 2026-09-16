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

            // The rear/right walls are solid sight blockers. The west side stays mostly open so
            // the external switchback ramps can connect cleanly into each floor.
            CreateBlock(root.transform, "Wall_Back", new Vector3(centerX, 2.15f, centerZ + depth * 0.5f),
                new Vector3(width, 4.3f, 0.22f), buildingMaterial);
            CreateBlock(root.transform, "Wall_Right", new Vector3(centerX + width * 0.5f, 2.15f, centerZ),
                new Vector3(0.22f, 4.3f, depth), buildingMaterial);
            CreateBlock(root.transform, "WestColumn_Front", new Vector3(centerX - width * 0.5f, 2.15f, centerZ - 1.55f),
                new Vector3(0.28f, 4.3f, 0.65f), buildingMaterial);
            CreateBlock(root.transform, "WestColumn_Back", new Vector3(centerX - width * 0.5f, 2.15f, centerZ + 1.55f),
                new Vector3(0.28f, 4.3f, 0.65f), buildingMaterial);

            // Front facade is intentionally split to leave a wide ground-floor entrance.
            CreateBlock(root.transform, "FrontColumn_Left", new Vector3(centerX - 2.05f, 1.05f, centerZ - depth * 0.5f),
                new Vector3(0.70f, 2.1f, 0.22f), buildingMaterial);
            CreateBlock(root.transform, "FrontColumn_Right", new Vector3(centerX + 2.05f, 1.05f, centerZ - depth * 0.5f),
                new Vector3(0.70f, 2.1f, 0.22f), buildingMaterial);

            // External switchback ramps avoid the player's capsule ever hitting the underside of
            // a floor slab. Both slopes are ~29 degrees, comfortably under the 45-degree limit.
            CreateRamp(root.transform, "Ramp_Ground_To_L1",
                new Vector3(5.65f, groundY + 0.05f, 4.95f),
                new Vector3(5.65f, level1Y + 0.05f, 8.45f),
                0.68f,
                floorMaterial);
            CreateRamp(root.transform, "Ramp_L1_To_Roof",
                new Vector3(4.75f, level1Y + 0.05f, 8.45f),
                new Vector3(4.75f, level2Y + 0.05f, 4.95f),
                0.68f,
                floorMaterial);

            // Level landings connect the ramp lanes to the building floors.
            CreateBlock(root.transform, "Landing_L1", new Vector3(5.55f, level1Y - 0.07f, 8.30f),
                new Vector3(2.05f, 0.14f, 0.90f), floorMaterial);
            CreateBlock(root.transform, "Landing_Roof", new Vector3(5.55f, level2Y - 0.07f, 5.05f),
                new Vector3(2.05f, 0.14f, 0.90f), floorMaterial);

            // A little cover on the upper levels demonstrates that vertical-space combat and LOS
            // use the same real 3D obstruction rules as the street below.
            CreateBlock(root.transform, "Cover_Level1", new Vector3(9.30f, level1Y + 0.38f, 6.85f),
                new Vector3(1.25f, 0.76f, 0.42f), accentMaterial);
            CreateBlock(root.transform, "Cover_Roof", new Vector3(8.15f, level2Y + 0.42f, 7.15f),
                new Vector3(0.85f, 0.84f, 0.85f), accentMaterial);

            Debug.Log("[ArknightsACT/25D] Added walkable NE building: ground floor -> level 2 -> roof deck via external ramps.");
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
