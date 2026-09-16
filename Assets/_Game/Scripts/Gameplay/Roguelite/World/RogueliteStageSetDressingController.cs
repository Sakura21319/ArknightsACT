using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Adds deterministic Chernobog/mobile-city set dressing to the enlarged combat blocks.
    /// The goal is to stop every block reading as an empty steel rectangle while keeping the central
    /// ACT navigation lanes clear. Dressing is visual-only for now; gameplay cover/nav remain owned by
    /// the existing runtime/layout systems.
    /// </summary>
    [DefaultExecutionOrder(21)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageSetDressingController : MonoBehaviour
    {
        private const float ChunkWidth = 18f;
        private const float ChunkDepth = 14f;

        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private ChernobogEnvironmentKit kit;

        private GameObject _preparedStage;
        private float _nextResolveAt;
        private Material _crystalDark;
        private Material _crystalFacet;
        private Mesh _crystalMeshA;
        private Mesh _crystalMeshB;

        public void Configure(RogueliteStageMapController map, ChernobogEnvironmentKit environmentKit)
        {
            stageMap = map;
            kit = environmentKit;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.16f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null || kit == null || !kit.IsUsable)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            EnsureCrystalAssets();
            BuildDressing(stage.transform);
            _preparedStage = stage;
        }

        private void OnDestroy()
        {
            if (_crystalDark != null)
                Destroy(_crystalDark);
            if (_crystalFacet != null)
                Destroy(_crystalFacet);
            if (_crystalMeshA != null)
                Destroy(_crystalMeshA);
            if (_crystalMeshB != null)
                Destroy(_crystalMeshB);
        }

        private void BuildDressing(Transform stage)
        {
            var old = stage.Find("[Chernobog_SetDressing]");
            if (old != null)
                Destroy(old.gameObject);

            var root = new GameObject("[Chernobog_SetDressing]").transform;
            root.SetParent(stage, false);

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                var block = FindBlockTransform(stage, i);
                if (data == null || block == null)
                    continue;

                var blockRoot = new GameObject($"Block_{i:00}_Dressing").transform;
                blockRoot.SetParent(root, false);
                blockRoot.position = block.position;
                blockRoot.rotation = block.rotation;

                BuildFloorPatch(blockRoot, i, data.Theme);

                var variant = PositiveMod(stageMap.StageIndex * 29 + i * 17 + (int)data.Theme * 11, 4);
                switch (data.Theme)
                {
                    case RogueliteChunkTheme.Facility:
                        BuildScaffold(blockRoot, Anchor(variant), 2.9f + 0.35f * stageMap.StageIndex, i);
                        BuildRubbleCluster(blockRoot, Anchor(variant + 3), i, 7);
                        BuildCargoBlocks(blockRoot, Anchor(variant + 5), i);
                        break;

                    case RogueliteChunkTheme.Street:
                        BuildScaffold(blockRoot, Anchor(variant + 1), 2.5f + 0.30f * stageMap.StageIndex, i);
                        BuildBrokenSlab(blockRoot, Anchor(variant + 4), i);
                        BuildRubbleCluster(blockRoot, Anchor(variant + 6), i, 8);
                        if (PositiveMod(i + stageMap.StageIndex, 2) == 0)
                            BuildBlackCrystalCluster(blockRoot, Anchor(variant + 2), i, 5);
                        break;

                    case RogueliteChunkTheme.CoverLane:
                        BuildCargoBlocks(blockRoot, Anchor(variant + 1), i);
                        BuildRubbleCluster(blockRoot, Anchor(variant + 4), i, 8);
                        BuildBlackCrystalCluster(blockRoot, Anchor(variant + 6), i, 6);
                        break;

                    case RogueliteChunkTheme.BossArena:
                        BuildBrokenSlab(blockRoot, Anchor(variant), i);
                        BuildBlackCrystalCluster(blockRoot, Anchor(variant + 2), i, 8);
                        BuildBlackCrystalCluster(blockRoot, Anchor(variant + 6), i + 91, 6);
                        BuildScaffold(blockRoot, Anchor(variant + 4), 3.8f, i);
                        break;

                    case RogueliteChunkTheme.SafePlaza:
                        BuildCargoBlocks(blockRoot, Anchor(variant + 2), i);
                        BuildScaffold(blockRoot, Anchor(variant + 5), 2.25f, i);
                        BuildRubbleCluster(blockRoot, Anchor(variant + 7), i, 5);
                        break;

                    default:
                        BuildRubbleCluster(blockRoot, Anchor(variant), i, 7);
                        BuildBrokenSlab(blockRoot, Anchor(variant + 3), i);
                        if (PositiveMod(i + stageMap.StageIndex, 3) != 1)
                            BuildBlackCrystalCluster(blockRoot, Anchor(variant + 6), i, 5);
                        break;
                }
            }

            Debug.Log($"[ArknightsACT/SetDressing] Stage {stageMap.StageIndex}: scaffold, rubble, cargo, broken-slab and black-crystal dressing applied.", this);
        }

        private void BuildFloorPatch(Transform parent, int blockIndex, RogueliteChunkTheme theme)
        {
            var material = theme == RogueliteChunkTheme.Facility
                ? kit.deckHeavyMaterial
                : kit.deckSecondaryMaterial != null ? kit.deckSecondaryMaterial : kit.deckHeavyMaterial;
            if (material == null)
                material = kit.deckMaterial;

            var selector = PositiveMod(stageMap.StageIndex * 31 + blockIndex * 13, 4);
            var patchA = Anchor(selector + 1) * 0.62f;
            var patchB = Anchor(selector + 5) * 0.58f;

            CreateBox(parent, "DeckPatch_A", patchA + new Vector3(0f, 0.060f, 0f),
                new Vector3(3.2f, 0.035f, 2.0f), 0.008f, material, false,
                Quaternion.Euler(0f, selector % 2 == 0 ? 0f : 90f, 0f));
            CreateBox(parent, "DeckPatch_B", patchB + new Vector3(0f, 0.058f, 0f),
                new Vector3(2.1f, 0.032f, 1.35f), 0.007f, material, false,
                Quaternion.Euler(0f, selector % 2 == 0 ? 90f : 0f, 0f));
        }

        private void BuildScaffold(Transform parent, Vector3 anchor, float height, int seed)
        {
            var root = new GameObject("Scaffold").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;
            root.localRotation = Quaternion.Euler(0f, PositiveMod(seed * 37, 4) * 90f, 0f);

            var steel = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
            var dark = kit.insetMaterial != null ? kit.insetMaterial : steel;
            var grate = kit.grateMaterial != null ? kit.grateMaterial : dark;
            const float halfX = 1.25f;
            const float halfZ = 0.72f;
            const float pole = 0.12f;

            var corners = new[]
            {
                new Vector3(-halfX, height * 0.5f, -halfZ),
                new Vector3(halfX, height * 0.5f, -halfZ),
                new Vector3(-halfX, height * 0.5f, halfZ),
                new Vector3(halfX, height * 0.5f, halfZ)
            };
            for (var i = 0; i < corners.Length; i++)
                CreateBox(root, $"Pole_{i}", corners[i], new Vector3(pole, height, pole), 0.018f, steel, true);

            var levels = height >= 3.2f ? 2 : 1;
            for (var level = 1; level <= levels; level++)
            {
                var y = height * level / (levels + 1f);
                CreateBox(root, $"Platform_{level}", new Vector3(0f, y, 0f),
                    new Vector3(2.70f, 0.10f, 1.62f), 0.016f, grate, true);
                CreateBox(root, $"RailFront_{level}", new Vector3(0f, y + 0.52f, -halfZ),
                    new Vector3(2.62f, 0.10f, 0.10f), 0.016f, steel, true);
                CreateBox(root, $"RailBack_{level}", new Vector3(0f, y + 0.52f, halfZ),
                    new Vector3(2.62f, 0.10f, 0.10f), 0.016f, steel, true);
            }

            CreateBeamBetween(root, "Brace_Front_A", new Vector3(-halfX, 0.10f, -halfZ), new Vector3(halfX, height * 0.88f, -halfZ), 0.085f, dark);
            CreateBeamBetween(root, "Brace_Front_B", new Vector3(halfX, 0.10f, -halfZ), new Vector3(-halfX, height * 0.88f, -halfZ), 0.075f, steel);
            CreateBeamBetween(root, "Brace_Side", new Vector3(halfX, 0.18f, -halfZ), new Vector3(halfX, height * 0.72f, halfZ), 0.070f, dark);
        }

        private void BuildRubbleCluster(Transform parent, Vector3 anchor, int seed, int count)
        {
            var root = new GameObject("RubbleCluster").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;

            var materialA = kit.deckHeavyMaterial != null ? kit.deckHeavyMaterial : kit.wallMaterial;
            var materialB = kit.insetMaterial != null ? kit.insetMaterial : materialA;
            for (var i = 0; i < count; i++)
            {
                var h1 = Hash01(seed * 101 + i * 17 + 3);
                var h2 = Hash01(seed * 151 + i * 23 + 7);
                var h3 = Hash01(seed * 211 + i * 31 + 11);
                var size = new Vector3(
                    Mathf.Lerp(0.34f, 1.20f, h1),
                    Mathf.Lerp(0.20f, 0.62f, h2),
                    Mathf.Lerp(0.32f, 1.05f, h3));
                var pos = new Vector3(
                    (h2 - 0.5f) * 2.4f,
                    size.y * 0.5f + 0.05f,
                    (h3 - 0.5f) * 1.9f);
                var rot = Quaternion.Euler(
                    Mathf.Lerp(-8f, 8f, h3),
                    h1 * 180f,
                    Mathf.Lerp(-10f, 10f, h2));
                CreateBox(root, $"Rubble_{i:00}", pos, size, Mathf.Min(0.055f, size.y * 0.16f),
                    i % 3 == 0 ? materialB : materialA, true, rot);
            }
        }

        private void BuildCargoBlocks(Transform parent, Vector3 anchor, int seed)
        {
            var root = new GameObject("CargoBlocks").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;
            root.localRotation = Quaternion.Euler(0f, PositiveMod(seed * 53, 4) * 90f, 0f);

            var body = kit.wallMaterial != null ? kit.wallMaterial : kit.deckHeavyMaterial;
            var steel = kit.steelMaterial != null ? kit.steelMaterial : body;
            var accent = kit.accentMaterial != null ? kit.accentMaterial : steel;

            var offsets = new[]
            {
                new Vector3(-0.72f, 0.62f, 0f),
                new Vector3(0.72f, 0.62f, 0.06f),
                new Vector3(0f, 1.86f, 0.02f)
            };
            var count = PositiveMod(seed + stageMap.StageIndex, 3) == 0 ? 3 : 2;
            for (var i = 0; i < count; i++)
            {
                var crate = new GameObject($"Cargo_{i:00}").transform;
                crate.SetParent(root, false);
                crate.localPosition = offsets[i];
                crate.localRotation = Quaternion.Euler(0f, i * 5f, 0f);

                CreateBox(crate, "Body", Vector3.zero, new Vector3(1.22f, 1.18f, 1.10f), 0.055f, body, true);
                CreateBox(crate, "FrameTop", new Vector3(0f, 0.50f, -0.57f), new Vector3(1.10f, 0.08f, 0.05f), 0.012f, steel, false);
                CreateBox(crate, "FrameBottom", new Vector3(0f, -0.50f, -0.57f), new Vector3(1.10f, 0.08f, 0.05f), 0.012f, steel, false);
                CreateBeamBetween(crate, "CrossA", new Vector3(-0.48f, -0.44f, -0.59f), new Vector3(0.48f, 0.44f, -0.59f), 0.065f, steel);
                CreateBeamBetween(crate, "CrossB", new Vector3(0.48f, -0.44f, -0.59f), new Vector3(-0.48f, 0.44f, -0.59f), 0.055f, i == 0 ? accent : steel);
            }
        }

        private void BuildBrokenSlab(Transform parent, Vector3 anchor, int seed)
        {
            var root = new GameObject("BrokenDeckSlab").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;
            root.localRotation = Quaternion.Euler(0f, PositiveMod(seed * 29, 8) * 22.5f, 0f);

            var main = kit.deckHeavyMaterial != null ? kit.deckHeavyMaterial : kit.deckMaterial;
            var underside = kit.insetMaterial != null ? kit.insetMaterial : main;
            CreateBox(root, "Slab_A", new Vector3(-0.58f, 0.14f, 0.12f), new Vector3(2.35f, 0.22f, 1.52f), 0.035f, main, true,
                Quaternion.Euler(3f, -5f, -4f));
            CreateBox(root, "Slab_B", new Vector3(0.88f, 0.20f, -0.36f), new Vector3(1.55f, 0.28f, 1.05f), 0.040f, underside, true,
                Quaternion.Euler(-4f, 12f, 8f));
            BuildRubbleCluster(root, new Vector3(-0.15f, 0f, -0.95f), seed + 61, 4);
        }

        private void BuildBlackCrystalCluster(Transform parent, Vector3 anchor, int seed, int count)
        {
            if (_crystalMeshA == null || _crystalMeshB == null || _crystalDark == null)
                return;

            var root = new GameObject("BlackOriginiumGrowth").transform;
            root.SetParent(parent, false);
            root.localPosition = anchor;
            root.localRotation = Quaternion.Euler(0f, Hash01(seed * 41 + 3) * 180f, 0f);

            for (var i = 0; i < count; i++)
            {
                var a = Hash01(seed * 97 + i * 31 + 5);
                var b = Hash01(seed * 131 + i * 43 + 9);
                var c = Hash01(seed * 173 + i * 59 + 13);
                var height = Mathf.Lerp(0.45f, i == 0 ? 2.25f : 1.55f, a);
                var radius = Mathf.Lerp(0.16f, 0.42f, b) * (height > 1.7f ? 1.12f : 1f);

                var go = new GameObject($"Crystal_{i:00}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3((b - 0.5f) * 2.1f, 0.04f, (c - 0.5f) * 1.65f);
                go.transform.localRotation = Quaternion.Euler(Mathf.Lerp(-12f, 12f, c), a * 180f, Mathf.Lerp(-16f, 16f, b));
                go.transform.localScale = new Vector3(radius, height, radius);

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = i % 2 == 0 ? _crystalMeshA : _crystalMeshB;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = _crystalFacet != null && i % 3 == 0
                    ? new[] { _crystalDark, _crystalFacet }
                    : new[] { _crystalDark, _crystalDark };
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            // Small dark debris ties the crystal growth into the steel deck instead of looking planted.
            BuildRubbleCluster(root, Vector3.zero, seed + 401, Mathf.Clamp(count / 2, 2, 4));
        }

        private void EnsureCrystalAssets()
        {
            if (_crystalDark == null)
            {
                var source = kit.insetMaterial != null ? kit.insetMaterial : kit.wallMaterial;
                if (source != null)
                {
                    _crystalDark = new Material(source) { name = "[Runtime] ChernobogBlackCrystal" };
                    SetColor(_crystalDark, new Color(0.028f, 0.031f, 0.039f, 1f));
                    SetFloat(_crystalDark, "_Metallic", 0.05f);
                    SetSmoothness(_crystalDark, 0.27f);
                }
            }

            if (_crystalFacet == null)
            {
                var source = kit.steelMaterial != null ? kit.steelMaterial : kit.wallMaterial;
                if (source != null)
                {
                    _crystalFacet = new Material(source) { name = "[Runtime] ChernobogCrystalFacet" };
                    SetColor(_crystalFacet, new Color(0.105f, 0.088f, 0.060f, 1f));
                    SetFloat(_crystalFacet, "_Metallic", 0.02f);
                    SetSmoothness(_crystalFacet, 0.34f);
                }
            }

            _crystalMeshA ??= CreateCrystalMesh("BlackCrystal_A", 5, 0.78f, 0.26f);
            _crystalMeshB ??= CreateCrystalMesh("BlackCrystal_B", 6, 0.64f, 0.34f);
        }

        private static Mesh CreateCrystalMesh(string name, int sides, float shoulderY, float shoulderRadius)
        {
            sides = Mathf.Clamp(sides, 4, 8);
            var vertices = new Vector3[1 + sides * 2 + 1];
            vertices[0] = new Vector3(0f, 0f, 0f);
            for (var i = 0; i < sides; i++)
            {
                var angle = i * Mathf.PI * 2f / sides;
                vertices[1 + i] = new Vector3(Mathf.Cos(angle) * 0.62f, 0.08f, Mathf.Sin(angle) * 0.62f);
                vertices[1 + sides + i] = new Vector3(Mathf.Cos(angle + 0.18f) * shoulderRadius, shoulderY, Mathf.Sin(angle + 0.18f) * shoulderRadius);
            }
            var topIndex = vertices.Length - 1;
            vertices[topIndex] = new Vector3(0.09f, 1f, -0.04f);

            var triangles = new int[sides * 12];
            var cursor = 0;
            for (var i = 0; i < sides; i++)
            {
                var next = (i + 1) % sides;
                // bottom fan
                triangles[cursor++] = 0;
                triangles[cursor++] = 1 + next;
                triangles[cursor++] = 1 + i;
                // lower faceted wall
                triangles[cursor++] = 1 + i;
                triangles[cursor++] = 1 + next;
                triangles[cursor++] = 1 + sides + i;
                triangles[cursor++] = 1 + next;
                triangles[cursor++] = 1 + sides + next;
                triangles[cursor++] = 1 + sides + i;
                // upper point
                triangles[cursor++] = 1 + sides + i;
                triangles[cursor++] = 1 + sides + next;
                triangles[cursor++] = topIndex;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Anchor(int index)
        {
            // Keep the central +-4m cross quiet for ACT movement and cardinal block navigation.
            var anchors = new[]
            {
                new Vector3(-6.55f, 0f, 4.70f),
                new Vector3(6.45f, 0f, 4.55f),
                new Vector3(6.50f, 0f, -4.55f),
                new Vector3(-6.45f, 0f, -4.70f),
                new Vector3(-6.75f, 0f, 1.85f),
                new Vector3(6.70f, 0f, -1.65f),
                new Vector3(-2.35f, 0f, 5.25f),
                new Vector3(2.55f, 0f, -5.30f)
            };
            return anchors[PositiveMod(index, anchors.Length)];
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material,
            bool shadows,
            Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, bevel);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        private static void CreateBeamBetween(Transform parent, string name, Vector3 start, Vector3 end, float thickness, Material material)
        {
            var delta = end - start;
            if (delta.sqrMagnitude <= 0.0001f)
                return;
            var length = delta.magnitude;
            var beam = CreateBox(parent, name, (start + end) * 0.5f,
                new Vector3(thickness, thickness, length), Mathf.Min(0.025f, thickness * 0.22f), material, true);
            beam.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static Transform FindBlockTransform(Transform stage, int index)
        {
            var prefix = $"Block_{index:00}_";
            for (var i = 0; i < stage.childCount; i++)
            {
                var child = stage.GetChild(i);
                if (child != null && child.name.StartsWith(prefix, StringComparison.Ordinal))
                    return child;
            }
            return null;
        }

        private static void SetColor(Material material, Color color)
        {
            if (material == null)
                return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetSmoothness(Material material, float value)
        {
            if (material == null)
                return;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", value);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", value);
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= unchecked((int)0x7feb352d);
                value ^= value >> 15;
                value *= unchecked((int)0x846ca68b);
                value ^= value >> 16;
                return (value & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
