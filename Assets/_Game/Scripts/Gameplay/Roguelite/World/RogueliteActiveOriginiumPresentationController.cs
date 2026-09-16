using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Rebuilds Active Originium as a red hazardous floor tile inspired by the classic Arknights
    /// terrain language. The existing ActiveOriginiumZone25D component remains authoritative for
    /// gameplay; this controller only snaps the zone to a floor socket and replaces its presentation.
    /// </summary>
    [DefaultExecutionOrder(36)]
    [DisallowMultipleComponent]
    public sealed class RogueliteActiveOriginiumPresentationController : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;

        private readonly HashSet<int> _skinned = new();
        private readonly List<Material> _ownedMaterials = new();
        private GameObject _stage;
        private float _nextResolveAt;
        private Material _redSteel;
        private Material _redCore;
        private Material _redGlow;
        private Material _charcoal;

        public void Configure(RogueliteStageMapController map)
        {
            stageMap = map;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.10f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null)
                return;

            if (stage != _stage)
            {
                _stage = stage;
                _skinned.Clear();
                ResolveMaterials(stage);
            }

            UpdateOriginiumTiles(stage.transform);
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

        private void UpdateOriginiumTiles(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var root = transforms[i];
                if (root == null || !root.name.StartsWith("Hazard_ActiveOriginium", StringComparison.Ordinal))
                    continue;

                // Keep suppressing earlier purple/orange presentations in case another late pass
                // materializes them a frame after this controller first sees the hazard.
                HideLegacyPresentation(root);

                var id = root.gameObject.GetInstanceID();
                if (_skinned.Contains(id))
                    continue;

                SnapToFloorSocket(root);
                BuildRedTile(root);
                _skinned.Add(id);
            }
        }

        private void SnapToFloorSocket(Transform hazard)
        {
            var block = hazard.parent;
            if (block == null)
                return;

            var sockets = block.GetComponentsInChildren<RogueliteFloorSocket25D>(true);
            RogueliteFloorSocket25D nearest = null;
            var best = float.PositiveInfinity;
            for (var i = 0; i < sockets.Length; i++)
            {
                var socket = sockets[i];
                if (socket == null || !socket.PitEligible)
                    continue;

                var delta = socket.transform.position - hazard.position;
                delta.y = 0f;
                var distance = delta.sqrMagnitude;
                if (distance >= best)
                    continue;
                best = distance;
                nearest = socket;
            }

            if (nearest == null)
                return;

            var world = nearest.transform.position;
            hazard.position = new Vector3(world.x, block.position.y, world.z);

            var trigger = hazard.GetComponent<BoxCollider>();
            if (trigger != null)
            {
                trigger.center = new Vector3(0f, 0.24f, 0f);
                trigger.size = new Vector3(
                    Mathf.Max(1.4f, nearest.Footprint.x * 0.94f),
                    Mathf.Max(0.50f, trigger.size.y),
                    Mathf.Max(1.4f, nearest.Footprint.y * 0.94f));
            }

            HideUnderlyingFloorSurface(nearest);
        }

        private static void HideUnderlyingFloorSurface(RogueliteFloorSocket25D socket)
        {
            if (socket == null || socket.transform.parent == null)
                return;

            var name = socket.name;
            const string prefix = "FloorSocket_";
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
                return;

            var suffix = name.Substring(prefix.Length);
            var surface = socket.transform.parent.Find($"FloorSurface_{suffix}");
            if (surface == null)
                return;

            var renderer = surface.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
        }

        private void BuildRedTile(Transform root)
        {
            var trigger = root.GetComponent<BoxCollider>();
            var footprint = trigger != null
                ? new Vector2(Mathf.Max(1.5f, trigger.size.x), Mathf.Max(1.5f, trigger.size.z))
                : new Vector2(2.15f, 2.05f);

            var skin = new GameObject("[ActiveOriginiumRedTile]").transform;
            skin.SetParent(root, false);

            // The tile is a red plate replacing one deck socket, not a purple crystal patch laid on
            // top of the floor. A dark mechanical frame makes the red field read as an authored map tile.
            CreateBox(skin, "OuterFrame", new Vector3(0f, 0.020f, 0f),
                new Vector3(footprint.x, 0.050f, footprint.y), 0.018f, _charcoal, true);
            CreateBox(skin, "RedPlate", new Vector3(0f, 0.053f, 0f),
                new Vector3(footprint.x * 0.90f, 0.050f, footprint.y * 0.88f), 0.014f, _redSteel, true);
            CreateBox(skin, "HotCore", new Vector3(0f, 0.083f, 0f),
                new Vector3(footprint.x * 0.67f, 0.018f, footprint.y * 0.58f), 0.006f, _redCore, false);

            // Dark framing interrupts the broad red field so it reads as the familiar dangerous
            // special-tile language rather than a plain colored square.
            var hx = footprint.x * 0.5f;
            var hz = footprint.y * 0.5f;
            CreateBox(skin, "FrameN", new Vector3(0f, 0.087f, hz * 0.88f),
                new Vector3(footprint.x * 0.72f, 0.030f, 0.075f), 0.010f, _charcoal, false);
            CreateBox(skin, "FrameS", new Vector3(0f, 0.087f, -hz * 0.88f),
                new Vector3(footprint.x * 0.72f, 0.030f, 0.075f), 0.010f, _charcoal, false);
            CreateBox(skin, "FrameE", new Vector3(hx * 0.88f, 0.087f, 0f),
                new Vector3(0.075f, 0.030f, footprint.y * 0.62f), 0.010f, _charcoal, false);
            CreateBox(skin, "FrameW", new Vector3(-hx * 0.88f, 0.087f, 0f),
                new Vector3(0.075f, 0.030f, footprint.y * 0.62f), 0.010f, _charcoal, false);

            // Four hot fissures and three low Originium fins give the tile energy without creating
            // tall collision-looking crystals that obscure ACT movement readability.
            CreateFissure(skin, new Vector3(-0.32f, 0.101f, -0.12f), 0.78f, -18f);
            CreateFissure(skin, new Vector3(0.30f, 0.103f, 0.18f), 0.64f, 24f);
            CreateFissure(skin, new Vector3(-0.05f, 0.105f, 0.34f), 0.48f, 68f);
            CreateFissure(skin, new Vector3(0.10f, 0.106f, -0.38f), 0.40f, -62f);

            for (var i = 0; i < 3; i++)
            {
                var fin = CreateBox(skin, "OriginiumFin",
                    new Vector3(-0.46f + i * 0.47f, 0.15f + (i % 2) * 0.025f, -0.03f + (i - 1) * 0.15f),
                    new Vector3(0.085f, 0.19f + i * 0.025f, 0.055f), 0.014f, _redGlow, true);
                fin.transform.localRotation = Quaternion.Euler(10f + i * 5f, 24f + i * 47f, i == 1 ? -14f : 11f);
            }
        }

        private void CreateFissure(Transform parent, Vector3 position, float length, float yaw)
        {
            var fissure = CreateBox(parent, "HotFissure", position,
                new Vector3(length, 0.018f, 0.045f), 0.006f, _redGlow, false);
            fissure.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static void HideLegacyPresentation(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;

                if (string.Equals(child.name, "OriginiumTile", StringComparison.Ordinal) ||
                    string.Equals(child.name, "OriginiumCrystal", StringComparison.Ordinal) ||
                    string.Equals(child.name, "[ActiveOriginiumSkin]", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void ResolveMaterials(GameObject stage)
        {
            ReleaseMaterials();
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            var baseMaterial = FindMaterial(renderers, "Kit_Deck") ??
                               FindMaterial(renderers, "Facility_Floor") ??
                               FindMaterial(renderers, "Ground_Tactical") ??
                               FirstMaterial(renderers);
            var accent = FindMaterial(renderers, "TacticalAccent") ?? baseMaterial;

            _charcoal = Clone(baseMaterial, "Runtime_ActiveOriginium_Frame",
                new Color(0.070f, 0.045f, 0.045f, 1f), 0.42f, 0.11f);
            _redSteel = Clone(baseMaterial, "Runtime_ActiveOriginium_RedSteel",
                new Color(0.39f, 0.035f, 0.028f, 1f), 0.28f, 0.18f);
            _redCore = Clone(accent ?? baseMaterial, "Runtime_ActiveOriginium_RedCore",
                new Color(0.67f, 0.050f, 0.026f, 1f), 0.18f, 0.24f);
            _redGlow = Clone(accent ?? baseMaterial, "Runtime_ActiveOriginium_Glow",
                new Color(0.95f, 0.075f, 0.020f, 1f), 0.08f, 0.34f);
            EnableEmission(_redGlow, new Color(3.4f, 0.24f, 0.035f, 1f));
        }

        private Material Clone(Material source, string name, Color color, float metallic, float smoothness)
        {
            if (source == null)
                return null;
            var material = new Material(source) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            _ownedMaterials.Add(material);
            return material;
        }

        private static void EnableEmission(Material material, Color color)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
                return;
            material.SetColor("_EmissionColor", color);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            float bevel,
            Material material,
            bool castShadows)
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

        private static Material FindMaterial(Renderer[] renderers, string name)
        {
            if (renderers == null)
                return null;
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (material != null && string.Equals(material.name, name, StringComparison.OrdinalIgnoreCase))
                    return material;
            }
            return null;
        }

        private static Material FirstMaterial(Renderer[] renderers)
        {
            if (renderers == null)
                return null;
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (material != null)
                    return material;
            }
            return null;
        }

        private void ReleaseMaterials()
        {
            for (var i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null)
                    Destroy(_ownedMaterials[i]);
            }
            _ownedMaterials.Clear();
            _redSteel = null;
            _redCore = null;
            _redGlow = null;
            _charcoal = null;
        }
    }
}
