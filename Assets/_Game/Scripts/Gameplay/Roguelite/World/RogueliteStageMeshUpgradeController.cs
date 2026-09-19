using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Geometry-quality pass for Concept-01. It converts the visual hard-surface kit from sharp
    /// Unity cube primitives into chamfered meshes and adds visible under-deck structure on the
    /// camera-facing south/west edges. Gameplay colliders remain authoritative and are preserved.
    /// </summary>
    [DefaultExecutionOrder(16)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageMeshUpgradeController : MonoBehaviour
    {
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;

        [SerializeField] private RogueliteStageMapController stageMap;

        private RogueliteStageRuntimeContext _context;
        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context != null)
                stageMap ??= _context.StageMap;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.10f;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null || stage == _preparedStage)
                return;

            UpgradeConceptVisuals(stage.transform);
            UpgradeFloorPlateEdges(stage.transform);
            UpgradeCombatCoverBodies(stage.transform);
            UpgradeFacilityStructure(stage.transform);
            BuildUnderDeckStructure(stage.transform);
            _preparedStage = stage;

            Debug.Log(
                $"[ArknightsACT/MeshUpgrade] Stage {stageMap.StageIndex}: beveled hard-surface meshes and under-deck structure applied.",
                this);
        }

        private static void UpgradeConceptVisuals(Transform stage)
        {
            var root = stage.Find("[Concept01_ChernobogDeckKit]");
            if (root == null)
                return;

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;
                if (!IsUnityCube(filter.sharedMesh))
                    continue;

                // Tiny light strips/grates stay crisp; structural modules get a stronger chamfer.
                var min = MinAxisAbs(filter.transform.localScale);
                var ratio = min <= 0.055f ? 0.14f : 0.10f;
                BakeScaleIntoBeveledMesh(filter.transform, ratio, false);
            }
        }

        private static void UpgradeFloorPlateEdges(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || !t.name.StartsWith("FloorSurface_", StringComparison.Ordinal))
                    continue;

                var filter = t.GetComponent<MeshFilter>();
                if (filter == null || !IsUnityCube(filter.sharedMesh))
                    continue;

                // The surface is only ~2.4 cm thick; a tiny physical chamfer creates a real edge
                // highlight while preserving the broad clean plate look selected in Concept-01.
                BakeScaleIntoBeveledMesh(t, 0.26f, false);
            }
        }

        private static void UpgradeCombatCoverBodies(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var root = transforms[i];
                if (root == null || !string.Equals(root.name, "GridCover", StringComparison.Ordinal))
                    continue;

                var body = root.Find("Body");
                if (body == null)
                    continue;

                var filter = body.GetComponent<MeshFilter>();
                if (filter == null || !IsUnityCube(filter.sharedMesh))
                    continue;

                // Preserve the original BoxCollider dimensions while replacing only its rendered mesh.
                BakeScaleIntoBeveledMesh(body, 0.085f, true);
            }
        }

        private static void UpgradeFacilityStructure(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var facility = transforms[i];
                if (facility == null || !string.Equals(facility.name, "TwoFloorFacility", StringComparison.Ordinal))
                    continue;

                var parts = facility.GetComponentsInChildren<MeshFilter>(true);
                for (var p = 0; p < parts.Length; p++)
                {
                    var filter = parts[p];
                    if (filter == null || filter.sharedMesh == null || !IsUnityCube(filter.sharedMesh))
                        continue;

                    var n = filter.name;
                    if (n.StartsWith("Wall_", StringComparison.Ordinal) ||
                        n.StartsWith("Rail_", StringComparison.Ordinal) ||
                        n.StartsWith("Column_", StringComparison.Ordinal) ||
                        string.Equals(n, "RampLanding", StringComparison.Ordinal))
                    {
                        BakeScaleIntoBeveledMesh(filter.transform, 0.075f, true);
                    }
                }
            }
        }

        private void BuildUnderDeckStructure(Transform stage)
        {
            if (stage.Find("[Concept01_UnderDeckStructure]") != null)
                return;

            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            var steel = FindMaterial(renderers, "Runtime_Concept01_SteelEdge") ??
                        FindMaterial(renderers, "Runtime_Concept01_Wall");
            var dark = FindMaterial(renderers, "Runtime_Concept01_WallInset") ??
                       FindMaterial(renderers, "Runtime_Concept01_DeckDark") ?? steel;
            var warm = FindMaterial(renderers, "Runtime_Concept01_WarmLight");
            if (steel == null || dark == null)
                return;

            var root = new GameObject("[Concept01_UnderDeckStructure]").transform;
            root.SetParent(stage, false);

            var width = stageMap.Width * ChunkWidth;
            var depth = stageMap.Height * ChunkDepth;
            var southZ = -depth * 0.5f + 0.10f;
            var westX = -width * 0.5f + 0.10f;

            // Camera-facing fascia and lower chords give the platform visible thickness instead of
            // ending in a flat grey slab over a black void.
            CreateBeveled(root, "SouthFascia", new Vector3(0f, -0.48f, southZ),
                new Vector3(width - 0.45f, 0.42f, 0.30f), dark, true, 0.045f);
            CreateBeveled(root, "SouthLowerChord", new Vector3(0f, -1.30f, southZ + 0.02f),
                new Vector3(width - 0.85f, 0.18f, 0.22f), steel, true, 0.040f);
            CreateBeveled(root, "WestFascia", new Vector3(westX, -0.48f, 0f),
                new Vector3(0.30f, 0.42f, depth - 0.45f), dark, true, 0.045f);
            CreateBeveled(root, "WestLowerChord", new Vector3(westX + 0.02f, -1.30f, 0f),
                new Vector3(0.22f, 0.18f, depth - 0.85f), steel, true, 0.040f);

            BuildSouthTruss(root, width, southZ, steel, warm);
            BuildWestTruss(root, depth, westX, steel, warm);
        }

        private static void BuildSouthTruss(Transform parent, float width, float z, Material steel, Material warm)
        {
            const float spacing = 4.6f;
            var count = Mathf.Max(3, Mathf.FloorToInt(width / spacing));
            var actual = width / count;
            for (var i = 0; i <= count; i++)
            {
                var x = -width * 0.5f + actual * i;
                CreateBeveled(parent, "SouthPost", new Vector3(x, -0.88f, z),
                    new Vector3(0.16f, 1.15f, 0.18f), steel, true, 0.035f);

                if (i >= count)
                    continue;

                var nextX = x + actual;
                if ((i & 1) == 0)
                    CreateBrace(parent, "SouthBrace", new Vector3(x + 0.12f, -0.35f, z), new Vector3(nextX - 0.12f, -1.27f, z), 0.12f, steel);
                else
                    CreateBrace(parent, "SouthBrace", new Vector3(x + 0.12f, -1.27f, z), new Vector3(nextX - 0.12f, -0.35f, z), 0.12f, steel);

                if (warm != null && i % 3 == 1)
                {
                    CreateBeveled(parent, "SouthServiceLight",
                        new Vector3((x + nextX) * 0.5f, -0.31f, z - 0.175f),
                        new Vector3(Mathf.Min(0.85f, actual * 0.28f), 0.075f, 0.035f), warm, false, 0.012f);
                }
            }
        }

        private static void BuildWestTruss(Transform parent, float depth, float x, Material steel, Material warm)
        {
            const float spacing = 4.3f;
            var count = Mathf.Max(3, Mathf.FloorToInt(depth / spacing));
            var actual = depth / count;
            for (var i = 0; i <= count; i++)
            {
                var z = -depth * 0.5f + actual * i;
                CreateBeveled(parent, "WestPost", new Vector3(x, -0.88f, z),
                    new Vector3(0.18f, 1.15f, 0.16f), steel, true, 0.035f);

                if (i >= count)
                    continue;

                var nextZ = z + actual;
                if ((i & 1) == 0)
                    CreateBrace(parent, "WestBrace", new Vector3(x, -0.35f, z + 0.12f), new Vector3(x, -1.27f, nextZ - 0.12f), 0.12f, steel);
                else
                    CreateBrace(parent, "WestBrace", new Vector3(x, -1.27f, z + 0.12f), new Vector3(x, -0.35f, nextZ - 0.12f), 0.12f, steel);

                if (warm != null && i % 3 == 1)
                {
                    CreateBeveled(parent, "WestServiceLight",
                        new Vector3(x - 0.175f, -0.31f, (z + nextZ) * 0.5f),
                        new Vector3(0.035f, 0.075f, Mathf.Min(0.85f, actual * 0.28f)), warm, false, 0.012f);
                }
            }
        }

        private static void CreateBrace(Transform parent, string name, Vector3 from, Vector3 to, float thickness, Material material)
        {
            var delta = to - from;
            var length = delta.magnitude;
            if (length < 0.01f)
                return;

            var go = CreateBeveled(parent, name, (from + to) * 0.5f,
                new Vector3(length, thickness, thickness), material, true, Mathf.Min(0.035f, thickness * 0.22f));
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.right, delta.normalized);
        }

        private static GameObject CreateBeveled(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            Material material,
            bool castShadows,
            float bevel)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        private static void BakeScaleIntoBeveledMesh(Transform transform, float bevelRatio, bool preserveBoxCollider)
        {
            if (transform == null)
                return;

            var filter = transform.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !IsUnityCube(filter.sharedMesh))
                return;

            var originalScale = transform.localScale;
            var size = new Vector3(Mathf.Abs(originalScale.x), Mathf.Abs(originalScale.y), Mathf.Abs(originalScale.z));
            var minAxis = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            if (minAxis < 0.001f)
                return;

            var bevel = Mathf.Clamp(minAxis * bevelRatio, 0.003f, 0.085f);
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, bevel);

            if (preserveBoxCollider)
            {
                var box = transform.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = Vector3.Scale(box.center, originalScale);
                    box.size = Vector3.Scale(box.size, size);
                }
            }

            transform.localScale = Vector3.one;
        }

        private static bool IsUnityCube(Mesh mesh)
        {
            if (mesh == null || string.IsNullOrEmpty(mesh.name))
                return false;
            if (mesh.name.StartsWith("ChernobogBeveledBox_", StringComparison.Ordinal))
                return false;
            return mesh.name.IndexOf("Cube", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float MinAxisAbs(Vector3 value)
        {
            return Mathf.Min(Mathf.Abs(value.x), Mathf.Min(Mathf.Abs(value.y), Mathf.Abs(value.z)));
        }

        private static Material FindMaterial(Renderer[] renderers, string materialName)
        {
            if (renderers == null || string.IsNullOrWhiteSpace(materialName))
                return null;

            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (material != null && string.Equals(material.name, materialName, StringComparison.OrdinalIgnoreCase))
                    return material;
            }
            return null;
        }
    }
}
