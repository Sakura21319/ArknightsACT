using System;
using System.Collections.Generic;
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
    /// built StageRuntime with industrial silhouettes and recognizable Arknights terrain without
    /// coupling those rules to block traversal / reward code.
    ///
    /// Pit and ballista hazards are intentionally retired from the current prototype direction.
    /// Active Originium is the only authored terrain hazard created by this layer for now.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RogueliteStageEnvironmentController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private RogueliteRunState runState;
        [SerializeField] private RogueliteStageMapController stageMap;
        [SerializeField] private bool prototypeHazardsEnabled = true;
        [SerializeField] private bool catastrophesEnabled;

        private readonly List<Material> _ownedMaterials = new();
        private GameObject _decoratedStage;
        private float _nextResolveAt;
        private Material _buildingMaterial;
        private Material _buildingAccentMaterial;
        private Material _originiumMaterial;
        private Material _hazardMaterial;

        public RogueliteCatastropheKind CurrentCatastrophe { get; private set; }

        public void Configure(Transform playerTransform, RogueliteRunState state, RogueliteStageMapController map)
        {
            player = playerTransform;
            runState = state;
            stageMap = map;
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
            ResolveStageMaterials(stage);
            DecorateStage(stage);
        }

        private void DecorateStage(GameObject stage)
        {
            var layer = new GameObject("[MobileCityEnvironment]");
            layer.transform.SetParent(stage.transform, false);
            BuildIndustrialBackdrop(layer.transform);

            CurrentCatastrophe = catastrophesEnabled
                ? RollPrototypeCatastrophe(stageMap.StageIndex)
                : RogueliteCatastropheKind.None;
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
                $"[ArknightsACT/Environment] Stage {stageMap.StageIndex} decorated. " +
                $"Catastrophe={CurrentCatastrophe}, pits=disabled, ballista=disabled.",
                this);
        }

        private void DecorateBlockHazards(Transform block, RogueliteBlockState data, int blockIndex)
        {
            var rng = new System.Random(unchecked(stageMap.StageIndex * 104729 + blockIndex * 8191 + 21319));
            var emergency = data.Type == RogueliteBlockType.EmergencyCombat;
            var boss = data.Type == RogueliteBlockType.Boss;

            if (boss)
            {
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

            // Keep the early-frame placeholder subdued. The late presentation pass replaces it with
            // the full amber-black shard bed, so do not create the old red plate / spike cluster here.
            CreateVisual(
                root.transform,
                "OriginiumTile",
                new Vector3(0f, 0.028f, 0f),
                new Vector3(footprint.x, 0.055f, footprint.y),
                _originiumMaterial);
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
                    : (i % 3 == 0
                        ? -depth * 0.5f - 8f - (float)rng.NextDouble() * 4f
                        : depth * 0.5f + 7f);
                if (!north && i % 3 != 0)
                    x = width * 0.5f + 6f + (float)rng.NextDouble() * 7f;

                var w = 2.8f + (float)rng.NextDouble() * 3.8f;
                var h = 4.0f + (float)rng.NextDouble() * 8.5f;
                var d = 2.3f + (float)rng.NextDouble() * 3.5f;
                var building = CreateVisual(
                    root,
                    "DistantBuilding",
                    new Vector3(x, h * 0.5f - 0.15f, z),
                    new Vector3(w, h, d),
                    _buildingMaterial);
                building.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 8f - 4f, 0f);

                if (i % 3 == 0)
                {
                    CreateVisual(root, "RoofUnit", new Vector3(x + w * 0.18f, h + 0.28f, z), new Vector3(w * 0.38f, 0.56f, d * 0.34f), _buildingAccentMaterial);
                    CreateVisual(root, "Antenna", new Vector3(x - w * 0.18f, h + 1.10f, z), new Vector3(0.08f, 2.1f, 0.08f), _hazardMaterial);
                }
            }
        }

        private void ResolveStageMaterials(GameObject stage)
        {
            ReleaseOwnedMaterials();

            var renderers = stage != null
                ? stage.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();

            var ground = FindMaterial(renderers, "Ground_Tactical");
            var wall = FindMaterial(renderers, "Facility_Wall");
            var floor = FindMaterial(renderers, "Facility_Floor");
            var cover = FindMaterial(renderers, "CombatCover");
            var accent = FindMaterial(renderers, "TacticalAccent");
            var hazard = FindMaterial(renderers, "HazardBand");
            var sidewalk = FindMaterial(renderers, "Sidewalk_Tactical");

            var fallback = wall ?? cover ?? ground ?? FirstUsableMaterial(renderers);
            _buildingMaterial = wall ?? cover ?? fallback;
            _buildingAccentMaterial = floor ?? sidewalk ?? cover ?? fallback;
            _hazardMaterial = hazard ?? accent ?? _buildingAccentMaterial ?? fallback;

            _originiumMaterial = CloneTint(
                accent ?? _hazardMaterial ?? fallback,
                new Color(0.31f, 0.18f, 0.035f),
                "Runtime_OriginiumAmberPlaceholder");
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

        private static Material FirstUsableMaterial(Renderer[] renderers)
        {
            if (renderers == null)
                return null;
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (material != null && material.shader != null && material.shader.isSupported)
                    return material;
            }
            return null;
        }

        private Material CloneTint(Material source, Color color, string runtimeName)
        {
            if (source == null)
                return null;

            var material = new Material(source) { name = runtimeName };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            _ownedMaterials.Add(material);
            return material;
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
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            return go;
        }

        private void OnDestroy()
        {
            ReleaseOwnedMaterials();
        }

        private void ReleaseOwnedMaterials()
        {
            for (var i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null)
                    Destroy(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
            _originiumMaterial = null;
        }
    }
}
