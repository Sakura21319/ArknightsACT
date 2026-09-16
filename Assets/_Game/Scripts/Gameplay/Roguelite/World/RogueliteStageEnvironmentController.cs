using System;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    public enum RogueliteCatastropheKind
    {
        None,
        DustFront,
        OriginiumFall,
        StructuralCollapse
    }

    /// <summary>
    /// Independent environment layer for generated mobile-city blocks. It decorates each freshly
    /// built StageRuntime with Chernobog-inspired industrial silhouettes and optional tactical
    /// terrain without coupling those rules to block traversal / reward code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteStageEnvironmentController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private bool prototypeHazardsEnabled = true;
        [SerializeField] private bool catastrophesEnabled;

        private GameObject _decoratedStage;
        private float _nextResolveAt;
        private Material _buildingMaterial;
        private Material _buildingAccentMaterial;
        private Material _originiumMaterial;
        private Material _hazardMaterial;
        private Material _pitMaterial;
        private Material _boltMaterial;

        public RogueliteCatastropheKind CurrentCatastrophe { get; private set; }

        public void Configure(Transform playerTransform, RogueliteRunState state, RogueliteStageMapController map)
        {
            player = playerTransform;
            runState = state;
            stageMap = map;
        }

        private void Awake()
        {
            BuildRuntimeMaterials();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.25f;

            runState ??= RogueliteRunState.Instance ?? FindFirstObjectByType<RogueliteRunState>();
            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (runState == null || stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _decoratedStage)
                return;
            _decoratedStage = stage;
            DecorateStage(stage);
        }

        private void DecorateStage(GameObject stage)
        {
            var layer = new GameObject("[MobileCityEnvironment]");
            layer.transform.SetParent(stage.transform, false);
            BuildIndustrialBackdrop(layer.transform);

            CurrentCatastrophe = catastrophesEnabled ? RollPrototypeCatastrophe(stageMap.StageIndex) : RogueliteCatastropheKind.None;
            if (!prototypeHazardsEnabled)
                return;

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                if (data == null || data.Type == RogueliteBlockType.Start || data.Type == RogueliteBlockType.Shop)
                    continue;
                var block = FindBlockTransform(stage.transform, i);
                if (block == null)
                    continue;
                DecorateBlockHazards(block, data, i);
            }

            Debug.Log(
                $"[ArknightsACT/Environment] Stage {stageMap.StageIndex} decorated. Catastrophe={CurrentCatastrophe}.",
                this);
        }

        private void DecorateBlockHazards(Transform block, RogueliteBlockState data, int blockIndex)
        {
            var rng = new System.Random(unchecked(stageMap.StageIndex * 104729 + blockIndex * 8191 + 21319));
            var emergency = data.Type == RogueliteBlockType.EmergencyCombat;
            var boss = data.Type == RogueliteBlockType.Boss;
            var facility = data.Theme == RogueliteChunkTheme.Facility;

            if (boss)
            {
                if (stageMap.StageIndex >= 2)
                    CreateBallista(block, new Vector3(-5.2f, 0f, -1.4f), Vector3.right);
                if (stageMap.StageIndex >= 3)
                    CreateOriginium(block, new Vector3(1.2f, 0f, 1.9f), new Vector2(3.0f, 1.7f));
                return;
            }

            var primaryRoll = rng.NextDouble();
            if (emergency || primaryRoll < 0.33)
            {
                var x = rng.NextDouble() < 0.5 ? -2.7f : 2.7f;
                var z = (float)(rng.NextDouble() * 3.2 - 1.6);
                CreateOriginium(block, new Vector3(x, 0f, z), new Vector2(2.7f, 1.55f));
            }

            if (!facility && (emergency ? rng.NextDouble() < 0.58 : rng.NextDouble() < 0.20))
            {
                var x = rng.NextDouble() < 0.5 ? -3.2f : 3.2f;
                var z = rng.NextDouble() < 0.5 ? -2.2f : 2.2f;
                CreatePit(block, new Vector3(x, 0f, z), block.position + new Vector3(0f, 0.10f, 0f));
            }

            if (emergency ? rng.NextDouble() < 0.70 : rng.NextDouble() < 0.20)
            {
                var fromWest = rng.NextDouble() < 0.5;
                CreateBallista(
                    block,
                    new Vector3(fromWest ? -5.6f : 5.6f, 0f, (float)(rng.NextDouble() * 3.0 - 1.5)),
                    fromWest ? Vector3.right : Vector3.left);
            }
        }

        private void CreateOriginium(Transform parent, Vector3 localPosition, Vector2 footprint)
        {
            var root = new GameObject("Hazard_ActiveOriginium");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.28f, 0f);
            trigger.size = new Vector3(footprint.x, 0.58f, footprint.y);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            root.AddComponent<ActiveOriginiumZone25D>().Configure(0.025f, 0.30f);

            CreateVisual(root.transform, "OriginiumTile", new Vector3(0f, 0.025f, 0f), new Vector3(footprint.x, 0.05f, footprint.y), _originiumMaterial);
            for (var i = 0; i < 5; i++)
            {
                var crystal = CreateVisual(
                    root.transform,
                    "OriginiumCrystal",
                    new Vector3(-footprint.x * 0.35f + i * footprint.x * 0.17f, 0.13f + (i % 2) * 0.05f, -footprint.y * 0.28f + (i % 3) * footprint.y * 0.22f),
                    new Vector3(0.10f, 0.28f + (i % 2) * 0.12f, 0.10f),
                    _originiumMaterial);
                crystal.transform.localRotation = Quaternion.Euler(8f + i * 5f, i * 29f, 11f);
            }
        }

        private void CreatePit(Transform parent, Vector3 localPosition, Vector3 resetPosition)
        {
            var root = new GameObject("Hazard_Hole");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.24f, 0f);
            trigger.size = new Vector3(2.35f, 0.50f, 1.85f);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            root.AddComponent<PitHazard25D>().Configure(resetPosition, 0.20f);

            CreateVisual(root.transform, "PitDepth", new Vector3(0f, 0.018f, 0f), new Vector3(2.35f, 0.035f, 1.85f), _pitMaterial);
            CreateVisual(root.transform, "PitEdgeN", new Vector3(0f, 0.045f, 0.97f), new Vector3(2.55f, 0.08f, 0.10f), _hazardMaterial);
            CreateVisual(root.transform, "PitEdgeS", new Vector3(0f, 0.045f, -0.97f), new Vector3(2.55f, 0.08f, 0.10f), _hazardMaterial);
        }

        private void CreateBallista(Transform parent, Vector3 localPosition, Vector3 direction)
        {
            var root = new GameObject("Hazard_Ballista");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);

            CreateVisual(root.transform, "Base", new Vector3(0f, 0.28f, 0f), new Vector3(0.70f, 0.56f, 0.78f), _buildingMaterial);
            CreateVisual(root.transform, "Bow", new Vector3(0f, 0.62f, 0.18f), new Vector3(1.25f, 0.10f, 0.12f), _hazardMaterial);
            CreateVisual(root.transform, "Rail", new Vector3(0f, 0.64f, 0.42f), new Vector3(0.12f, 0.10f, 0.86f), _buildingAccentMaterial);
            var warning = CreateVisual(root.transform, "FireLane", new Vector3(0f, 0.025f, 5.2f), new Vector3(0.12f, 0.035f, 9.2f), _hazardMaterial);
            warning.transform.localRotation = Quaternion.identity;

            root.AddComponent<BallistaHazard25D>().Configure(root.transform.forward, _boltMaterial, 2.45f, 16f + stageMap.StageIndex * 2f);
        }

        private void BuildIndustrialBackdrop(Transform parent)
        {
            if (stageMap == null)
                return;
            var width = stageMap.Width * 14f;
            var depth = stageMap.Height * 11f;
            var rng = new System.Random(9109 + stageMap.StageIndex * 101);
            var root = new GameObject("ChernobogStyleSkyline").transform;
            root.SetParent(parent, false);

            for (var i = 0; i < 12 + stageMap.StageIndex * 3; i++)
            {
                var north = i % 2 == 0;
                var x = (float)(rng.NextDouble() * (width + 14f) - (width + 14f) * 0.5f);
                var z = north
                    ? depth * 0.5f + 5f + (float)rng.NextDouble() * 7f
                    : (i % 3 == 0 ? -depth * 0.5f - 8f - (float)rng.NextDouble() * 4f : depth * 0.5f + 7f);
                if (!north && i % 3 != 0)
                    x = width * 0.5f + 6f + (float)rng.NextDouble() * 7f;

                var w = 2.8f + (float)rng.NextDouble() * 3.8f;
                var h = 4.0f + (float)rng.NextDouble() * 8.5f;
                var d = 2.3f + (float)rng.NextDouble() * 3.5f;
                var building = CreateVisual(root, "DistantBuilding", new Vector3(x, h * 0.5f - 0.15f, z), new Vector3(w, h, d), _buildingMaterial);
                building.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 8f - 4f, 0f);

                if (i % 3 == 0)
                {
                    CreateVisual(root, "RoofUnit", new Vector3(x + w * 0.18f, h + 0.28f, z), new Vector3(w * 0.38f, 0.56f, d * 0.34f), _buildingAccentMaterial);
                    CreateVisual(root, "Antenna", new Vector3(x - w * 0.18f, h + 1.10f, z), new Vector3(0.08f, 2.1f, 0.08f), _hazardMaterial);
                }
            }
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

        private RogueliteCatastropheKind RollPrototypeCatastrophe(int stage)
        {
            if (stage <= 1)
                return RogueliteCatastropheKind.None;
            var roll = UnityEngine.Random.value;
            if (roll < 0.18f) return RogueliteCatastropheKind.DustFront;
            if (roll < 0.28f) return RogueliteCatastropheKind.OriginiumFall;
            if (roll < 0.34f) return RogueliteCatastropheKind.StructuralCollapse;
            return RogueliteCatastropheKind.None;
        }

        private void BuildRuntimeMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            _buildingMaterial = CreateMaterial(shader, new Color(0.16f, 0.18f, 0.21f));
            _buildingAccentMaterial = CreateMaterial(shader, new Color(0.28f, 0.31f, 0.34f));
            _originiumMaterial = CreateMaterial(shader, new Color(0.48f, 0.18f, 0.62f));
            _hazardMaterial = CreateMaterial(shader, new Color(0.92f, 0.42f, 0.10f));
            _pitMaterial = CreateMaterial(shader, new Color(0.025f, 0.028f, 0.034f));
            _boltMaterial = CreateMaterial(shader, new Color(0.95f, 0.62f, 0.16f));
        }

        private static Material CreateMaterial(Shader shader, Color color)
        {
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.22f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            return material;
        }

        private static GameObject CreateVisual(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            return go;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_buildingMaterial);
            DestroyMaterial(_buildingAccentMaterial);
            DestroyMaterial(_originiumMaterial);
            DestroyMaterial(_hazardMaterial);
            DestroyMaterial(_pitMaterial);
            DestroyMaterial(_boltMaterial);
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
                Destroy(material);
        }
    }
}
