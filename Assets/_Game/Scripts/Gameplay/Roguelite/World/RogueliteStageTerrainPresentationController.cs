using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Late presentation pass for iconic stage terrain. Environment hazards are created after the
    /// base stage, so this controller watches direct block children and upgrades their visuals once
    /// they exist. Gameplay colliders and hazard scripts are left untouched.
    /// </summary>
    [DefaultExecutionOrder(30)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageTerrainPresentationController : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;

        private RogueliteStageRuntimeContext _context;
        private readonly HashSet<int> _skinnedObjects = new();
        private readonly List<Material> _ownedMaterials = new();
        private GameObject _stage;
        private float _nextResolveAt;

        private Material _wallMaterial;
        private Material _deckMaterial;
        private Material _coverMaterial;
        private Material _accentMaterial;
        private Material _darkMaterial;
        private Material _activeOriginiumMaterial;
        private Material _blueSignalMaterial;

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
            _nextResolveAt = Time.unscaledTime + 0.25f;

            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null)
                return;

            if (stage != _stage)
            {
                _stage = stage;
                _skinnedObjects.Clear();
                ResolveMaterials(stage);
            }

            EnhanceBlockTerrain(stage.transform);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null)
                    Destroy(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
        }

        private void ResolveMaterials(GameObject stage)
        {
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            _wallMaterial = FindMaterial(renderers, "Facility_Wall") ?? FindMaterial(renderers, "CombatCover");
            _deckMaterial = FindMaterial(renderers, "Facility_Floor") ?? FindMaterial(renderers, "Ground_Tactical") ?? _wallMaterial;
            _coverMaterial = FindMaterial(renderers, "CombatCover") ?? _wallMaterial;
            _accentMaterial = FindMaterial(renderers, "TacticalAccent") ?? _deckMaterial;

            if (_darkMaterial == null)
                _darkMaterial = CloneTint(_wallMaterial ?? _coverMaterial ?? _deckMaterial,
                    new Color(0.075f, 0.090f, 0.115f), "Runtime_TerrainDark", 0.38f, 0.12f);
            if (_activeOriginiumMaterial == null)
                _activeOriginiumMaterial = CloneTint(_accentMaterial ?? _deckMaterial,
                    new Color(0.88f, 0.39f, 0.08f), "Runtime_ActiveOriginiumOrange", 0.18f, 0.34f);
            if (_blueSignalMaterial == null)
                _blueSignalMaterial = CloneTint(_accentMaterial ?? _deckMaterial,
                    new Color(0.16f, 0.52f, 0.76f), "Runtime_ExitSignalBlue", 0.12f, 0.30f);
        }

        private void EnhanceBlockTerrain(Transform stage)
        {
            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var block = FindBlockTransform(stage, i);
                if (block == null)
                    continue;

                for (var c = 0; c < block.childCount; c++)
                {
                    var child = block.GetChild(c);
                    if (child == null)
                        continue;

                    if (child.name.StartsWith("Hazard_ActiveOriginium", StringComparison.Ordinal))
                        TrySkin(child, SkinActiveOriginium);
                    else if (child.name.StartsWith("Hazard_Hole", StringComparison.Ordinal))
                        TrySkin(child, SkinPit);
                    else if (child.name.StartsWith("Hazard_Ballista", StringComparison.Ordinal))
                        TrySkin(child, SkinBallista);
                    else if (string.Equals(child.name, "NextStageEntrance", StringComparison.Ordinal))
                        TrySkin(child, SkinStageExit);
                }
            }
        }

        private void TrySkin(Transform target, Action<Transform> skinAction)
        {
            var id = target.gameObject.GetHashCode();
            if (_skinnedObjects.Contains(id))
                return;

            skinAction(target);
            _skinnedObjects.Add(id);
        }

        private void SkinActiveOriginium(Transform root)
        {
            var trigger = root.GetComponent<BoxCollider>();
            var footprint = trigger != null
                ? new Vector2(Mathf.Max(1.2f, trigger.size.x), Mathf.Max(1.0f, trigger.size.z))
                : new Vector2(2.7f, 1.55f);

            var existingTile = root.Find("OriginiumTile");
            var crystalMaterial = existingTile != null
                ? existingTile.GetComponent<Renderer>()?.sharedMaterial
                : null;
            crystalMaterial ??= _activeOriginiumMaterial;

            var skin = new GameObject("[ActiveOriginiumSkin]").transform;
            skin.SetParent(root, false);

            // Active Originium is an iconic stage tile. Give it a dark recessed base, hot orange
            // safety frame and an uneven crystal cluster instead of a flat purple rectangle.
            CreateVisual(skin, "RecessedBase", new Vector3(0f, 0.016f, 0f),
                new Vector3(footprint.x * 0.94f, 0.025f, footprint.y * 0.90f),
                _darkMaterial ?? _coverMaterial);

            var hx = footprint.x * 0.5f;
            var hz = footprint.y * 0.5f;
            CreateVisual(skin, "FrameN", new Vector3(0f, 0.048f, hz), new Vector3(footprint.x, 0.055f, 0.09f), _activeOriginiumMaterial ?? _accentMaterial);
            CreateVisual(skin, "FrameS", new Vector3(0f, 0.048f, -hz), new Vector3(footprint.x, 0.055f, 0.09f), _activeOriginiumMaterial ?? _accentMaterial);
            CreateVisual(skin, "FrameE", new Vector3(hx, 0.048f, 0f), new Vector3(0.09f, 0.055f, footprint.y), _activeOriginiumMaterial ?? _accentMaterial);
            CreateVisual(skin, "FrameW", new Vector3(-hx, 0.048f, 0f), new Vector3(0.09f, 0.055f, footprint.y), _activeOriginiumMaterial ?? _accentMaterial);

            for (var i = 0; i < 7; i++)
            {
                var x = -footprint.x * 0.30f + (i % 4) * footprint.x * 0.19f;
                var z = -footprint.y * 0.22f + (i / 4) * footprint.y * 0.34f + (i % 2) * 0.08f;
                var height = 0.34f + (i % 3) * 0.12f;
                var crystal = CreateVisual(
                    skin,
                    "CrystalCluster",
                    new Vector3(x, height * 0.50f + 0.04f, z),
                    new Vector3(0.11f + (i % 2) * 0.035f, height, 0.11f),
                    crystalMaterial,
                    castShadows: true);
                crystal.transform.localRotation = Quaternion.Euler(7f + i * 3f, 18f + i * 31f, i % 2 == 0 ? 12f : -10f);
            }
        }

        private void SkinPit(Transform root)
        {
            var trigger = root.GetComponent<BoxCollider>();
            var footprint = trigger != null
                ? new Vector2(Mathf.Max(1.0f, trigger.size.x), Mathf.Max(0.9f, trigger.size.z))
                : new Vector2(2.3f, 2.0f);

            var skin = new GameObject("[PitShaftSkin]").transform;
            skin.SetParent(root, false);

            var hx = footprint.x * 0.5f;
            var hz = footprint.y * 0.5f;
            const float shaftDepth = 0.42f;
            const float thickness = 0.075f;

            CreateVisual(skin, "ShaftN", new Vector3(0f, -shaftDepth * 0.44f, hz - thickness * 0.5f),
                new Vector3(footprint.x * 0.94f, shaftDepth, thickness), _darkMaterial ?? _wallMaterial);
            CreateVisual(skin, "ShaftS", new Vector3(0f, -shaftDepth * 0.44f, -hz + thickness * 0.5f),
                new Vector3(footprint.x * 0.94f, shaftDepth, thickness), _darkMaterial ?? _wallMaterial);
            CreateVisual(skin, "ShaftE", new Vector3(hx - thickness * 0.5f, -shaftDepth * 0.44f, 0f),
                new Vector3(thickness, shaftDepth, footprint.y * 0.88f), _darkMaterial ?? _wallMaterial);
            CreateVisual(skin, "ShaftW", new Vector3(-hx + thickness * 0.5f, -shaftDepth * 0.44f, 0f),
                new Vector3(thickness, shaftDepth, footprint.y * 0.88f), _darkMaterial ?? _wallMaterial);

            CreateVisual(skin, "WarningTabA", new Vector3(-hx * 0.62f, 0.072f, hz + 0.02f),
                new Vector3(footprint.x * 0.20f, 0.035f, 0.12f), _activeOriginiumMaterial ?? _accentMaterial);
            CreateVisual(skin, "WarningTabB", new Vector3(hx * 0.62f, 0.072f, -hz - 0.02f),
                new Vector3(footprint.x * 0.20f, 0.035f, 0.12f), _activeOriginiumMaterial ?? _accentMaterial);
        }

        private void SkinBallista(Transform root)
        {
            var skin = new GameObject("[BallistaIndustrialSkin]").transform;
            skin.SetParent(root, false);

            CreateVisual(skin, "FootL", new Vector3(-0.34f, 0.10f, -0.18f), new Vector3(0.15f, 0.20f, 0.52f), _darkMaterial ?? _wallMaterial, true);
            CreateVisual(skin, "FootR", new Vector3(0.34f, 0.10f, -0.18f), new Vector3(0.15f, 0.20f, 0.52f), _darkMaterial ?? _wallMaterial, true);
            CreateVisual(skin, "ShieldPlate", new Vector3(0f, 0.52f, -0.18f), new Vector3(0.78f, 0.44f, 0.08f), _coverMaterial ?? _wallMaterial, true);
            CreateVisual(skin, "ShieldInset", new Vector3(0f, 0.52f, -0.225f), new Vector3(0.54f, 0.24f, 0.035f), _darkMaterial ?? _wallMaterial);
            CreateVisual(skin, "RailAccent", new Vector3(0f, 0.69f, 0.42f), new Vector3(0.07f, 0.08f, 0.72f), _activeOriginiumMaterial ?? _accentMaterial);
            CreateVisual(skin, "ArmL", new Vector3(-0.43f, 0.64f, 0.14f), new Vector3(0.52f, 0.07f, 0.08f), _wallMaterial ?? _coverMaterial, true)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            CreateVisual(skin, "ArmR", new Vector3(0.43f, 0.64f, 0.14f), new Vector3(0.52f, 0.07f, 0.08f), _wallMaterial ?? _coverMaterial, true)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        }

        private void SkinStageExit(Transform root)
        {
            var renderer = root.GetComponent<Renderer>();
            if (renderer != null && _blueSignalMaterial != null)
                renderer.sharedMaterial = _blueSignalMaterial;

            // Children inherit the marker's 1.15 / 0.06 / 1.15 scale, so create them in metric
            // space with compensation. They stay hidden until the existing exit marker is activated.
            CreateMetricChild(root, "ExitFrameN", new Vector3(0f, 0.075f, 0.92f), new Vector3(1.90f, 0.075f, 0.10f), _blueSignalMaterial ?? _accentMaterial);
            CreateMetricChild(root, "ExitFrameS", new Vector3(0f, 0.075f, -0.92f), new Vector3(1.90f, 0.075f, 0.10f), _blueSignalMaterial ?? _accentMaterial);
            CreateMetricChild(root, "ExitFrameE", new Vector3(0.92f, 0.075f, 0f), new Vector3(0.10f, 0.075f, 1.90f), _blueSignalMaterial ?? _accentMaterial);
            CreateMetricChild(root, "ExitFrameW", new Vector3(-0.92f, 0.075f, 0f), new Vector3(0.10f, 0.075f, 1.90f), _blueSignalMaterial ?? _accentMaterial);
            CreateMetricChild(root, "ExitCenter", new Vector3(0f, 0.070f, 0f), new Vector3(0.86f, 0.055f, 0.86f), _darkMaterial ?? _deckMaterial);
        }

        private static GameObject CreateVisual(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool castShadows = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null)
                    renderer.sharedMaterial = material;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return go;
        }

        private static GameObject CreateMetricChild(
            Transform scaledParent,
            string name,
            Vector3 metricLocalPosition,
            Vector3 metricScale,
            Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(scaledParent, false);

            var parentScale = scaledParent.localScale;
            go.transform.localPosition = new Vector3(
                SafeDivide(metricLocalPosition.x, parentScale.x),
                SafeDivide(metricLocalPosition.y, parentScale.y),
                SafeDivide(metricLocalPosition.z, parentScale.z));
            go.transform.localScale = new Vector3(
                SafeDivide(metricScale.x, parentScale.x),
                SafeDivide(metricScale.y, parentScale.y),
                SafeDivide(metricScale.z, parentScale.z));

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null)
                    renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return go;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
        }

        private Material CloneTint(Material source, Color color, string name, float metallic, float smoothness)
        {
            if (source == null)
                return null;

            var clone = new Material(source) { name = name };
            if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", color);
            if (clone.HasProperty("_Color")) clone.SetColor("_Color", color);
            if (clone.HasProperty("_Metallic")) clone.SetFloat("_Metallic", metallic);
            if (clone.HasProperty("_Smoothness")) clone.SetFloat("_Smoothness", smoothness);
            if (clone.HasProperty("_Glossiness")) clone.SetFloat("_Glossiness", smoothness);
            _ownedMaterials.Add(clone);
            return clone;
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

        private static Transform FindBlockTransform(Transform stage, int index)
        {
            return RogueliteStageBlockUtility.FindBlockTransform(stage, index);
        }
    }
}
