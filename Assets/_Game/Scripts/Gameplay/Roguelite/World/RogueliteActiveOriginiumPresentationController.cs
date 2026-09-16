using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Production presentation for Active Originium terrain.
    ///
    /// The gameplay zone remains owned by ActiveOriginiumZone25D. This controller only aligns the
    /// zone to one floor socket and replaces the prototype red plate / purple crystal visuals with
    /// a scorched industrial bed full of low amber-black Originium fragments.
    /// </summary>
    [DefaultExecutionOrder(36)]
    [DisallowMultipleComponent]
    public sealed class RogueliteActiveOriginiumPresentationController : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;

        private static readonly Mesh[] ShardMeshes = new Mesh[3];

        private readonly HashSet<int> _skinned = new();
        private readonly List<Material> _ownedMaterials = new();
        private GameObject _stage;
        private float _nextResolveAt;

        private Material _frameMaterial;
        private Material _scorchedMaterial;
        private Material _oreBlackMaterial;
        private Material _oreAmberMaterial;
        private Material _oreGoldMaterial;
        private Material _oreGlowMaterial;

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
            ReleaseMaterials();
        }

        private void UpdateOriginiumTiles(Transform stage)
        {
            var transforms = stage.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var root = transforms[i];
                if (root == null || !root.name.StartsWith("Hazard_ActiveOriginium", StringComparison.Ordinal))
                    continue;

                HideLegacyPresentation(root);

                var id = root.gameObject.GetInstanceID();
                if (_skinned.Contains(id))
                    continue;

                SnapToFloorSocket(root);
                BuildOriginiumShardTile(root);
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
                trigger.center = new Vector3(0f, 0.22f, 0f);
                trigger.size = new Vector3(
                    Mathf.Max(1.4f, nearest.Footprint.x * 0.94f),
                    Mathf.Max(0.46f, trigger.size.y),
                    Mathf.Max(1.4f, nearest.Footprint.y * 0.94f));
            }

            HideUnderlyingFloorSurface(nearest);
            HideNearbyFloorDetail(world);
        }

        private static void HideUnderlyingFloorSurface(RogueliteFloorSocket25D socket)
        {
            if (socket == null || socket.transform.parent == null)
                return;

            const string prefix = "FloorSocket_";
            if (!socket.name.StartsWith(prefix, StringComparison.Ordinal))
                return;

            var suffix = socket.name.Substring(prefix.Length);
            var surface = socket.transform.parent.Find($"FloorSurface_{suffix}");
            var renderer = surface != null ? surface.GetComponent<Renderer>() : null;
            if (renderer != null)
                renderer.enabled = false;
        }

        private void HideNearbyFloorDetail(Vector3 worldPosition)
        {
            if (_stage == null)
                return;

            var root = _stage.transform.Find("[Chernobog_ModularKit]/FloorDetails");
            if (root == null)
                return;

            for (var i = 0; i < root.childCount; i++)
            {
                var detail = root.GetChild(i);
                if (detail == null)
                    continue;
                var delta = detail.position - worldPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.75f * 0.75f)
                    detail.gameObject.SetActive(false);
            }
        }

        private void BuildOriginiumShardTile(Transform root)
        {
            var trigger = root.GetComponent<BoxCollider>();
            var footprint = trigger != null
                ? new Vector2(Mathf.Max(1.5f, trigger.size.x), Mathf.Max(1.5f, trigger.size.z))
                : new Vector2(2.15f, 2.05f);

            var oldSkin = root.Find("[ActiveOriginiumShardTile]");
            if (oldSkin != null)
                Destroy(oldSkin.gameObject);

            var skin = new GameObject("[ActiveOriginiumShardTile]").transform;
            skin.SetParent(root, false);

            CreateBox(skin, "IndustrialFrame", new Vector3(0f, 0.018f, 0f),
                new Vector3(footprint.x, 0.042f, footprint.y), 0.014f, _frameMaterial, true);
            CreateBox(skin, "ScorchedBed", new Vector3(0f, 0.047f, 0f),
                new Vector3(footprint.x * 0.91f, 0.030f, footprint.y * 0.88f), 0.010f, _scorchedMaterial, true);

            BuildBurntOreCrust(skin, footprint);
            BuildAmberVeins(skin, footprint);
            BuildShardClusters(skin, footprint, root);
        }

        private void BuildBurntOreCrust(Transform parent, Vector2 footprint)
        {
            var patches = new[]
            {
                new Vector4(-0.23f, -0.16f, 0.38f, 0.29f),
                new Vector4(0.18f, 0.17f, 0.33f, 0.25f),
                new Vector4(0.26f, -0.22f, 0.27f, 0.22f),
                new Vector4(-0.08f, 0.27f, 0.29f, 0.19f),
                new Vector4(-0.34f, 0.14f, 0.20f, 0.17f)
            };

            for (var i = 0; i < patches.Length; i++)
            {
                var p = patches[i];
                var patch = CreateBox(parent, "CarbonizedOrePatch",
                    new Vector3(p.x * footprint.x, 0.068f + i * 0.0007f, p.y * footprint.y),
                    new Vector3(p.z * footprint.x, 0.015f, p.w * footprint.y),
                    0.006f,
                    i % 2 == 0 ? _oreBlackMaterial : _scorchedMaterial,
                    false);
                patch.transform.localRotation = Quaternion.Euler(0f, -27f + i * 31f, 0f);
            }
        }

        private void BuildAmberVeins(Transform parent, Vector2 footprint)
        {
            CreateVein(parent, new Vector3(-footprint.x * 0.13f, 0.078f, -footprint.y * 0.06f), footprint.x * 0.46f, -19f);
            CreateVein(parent, new Vector3(footprint.x * 0.17f, 0.079f, footprint.y * 0.15f), footprint.x * 0.31f, 31f);
            CreateVein(parent, new Vector3(footprint.x * 0.04f, 0.080f, -footprint.y * 0.25f), footprint.x * 0.22f, -67f);
        }

        private void CreateVein(Transform parent, Vector3 localPosition, float length, float yaw)
        {
            var vein = CreateBox(parent, "AmberVein", localPosition,
                new Vector3(length, 0.008f, 0.028f), 0.003f, _oreGlowMaterial, false);
            vein.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void BuildShardClusters(Transform parent, Vector2 footprint, Transform root)
        {
            var seed = unchecked(
                stageMap.StageIndex * 73856093 ^
                Mathf.RoundToInt(root.position.x * 100f) * 19349663 ^
                Mathf.RoundToInt(root.position.z * 100f) * 83492791);
            var rng = new System.Random(seed);

            var centers = new[]
            {
                new Vector2(-0.24f, -0.16f),
                new Vector2(0.21f, 0.18f),
                new Vector2(0.30f, -0.23f),
                new Vector2(-0.10f, 0.28f)
            };

            for (var i = 0; i < 18; i++)
            {
                var center = centers[i % centers.Length];
                var x = center.x * footprint.x + Range(rng, -0.16f, 0.16f) * footprint.x;
                var z = center.y * footprint.y + Range(rng, -0.13f, 0.13f) * footprint.y;
                var chip = i % 4 == 0 || i % 7 == 0;

                var width = chip ? Range(rng, 0.13f, 0.23f) : Range(rng, 0.075f, 0.14f);
                var depth = chip ? Range(rng, 0.10f, 0.20f) : Range(rng, 0.060f, 0.12f);
                var height = chip ? Range(rng, 0.045f, 0.085f) : Range(rng, 0.12f, 0.30f);
                var y = 0.073f + (chip ? 0.004f : 0.008f);

                var materialRoll = rng.NextDouble();
                var material = materialRoll < 0.47
                    ? _oreBlackMaterial
                    : materialRoll < 0.84
                        ? _oreAmberMaterial
                        : materialRoll < 0.96
                            ? _oreGoldMaterial
                            : _oreGlowMaterial;

                var shard = CreateShard(parent, i % ShardMeshes.Length, new Vector3(x, y, z),
                    new Vector3(width, height, depth), material, true);
                shard.name = chip ? "OriginiumChip" : "OriginiumShard";
                shard.transform.localRotation = Quaternion.Euler(
                    chip ? Range(rng, 58f, 82f) : Range(rng, -12f, 12f),
                    Range(rng, 0f, 360f),
                    chip ? Range(rng, -18f, 18f) : Range(rng, -14f, 14f));
            }
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private static GameObject CreateShard(
            Transform parent,
            int variant,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool castShadows)
        {
            var go = new GameObject("OriginiumShard");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetShardMesh(variant);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        private static Mesh GetShardMesh(int variant)
        {
            variant = Mathf.Abs(variant) % ShardMeshes.Length;
            if (ShardMeshes[variant] != null)
                return ShardMeshes[variant];

            const int sides = 5;
            var bottom = new Vector3[sides];
            var shoulder = new Vector3[sides];
            var angleOffset = variant * 0.17f;
            for (var i = 0; i < sides; i++)
            {
                var angle = angleOffset + Mathf.PI * 2f * i / sides;
                var irregular = 0.86f + 0.10f * Mathf.Sin(i * 2.17f + variant * 1.31f);
                bottom[i] = new Vector3(Mathf.Cos(angle) * 0.50f * irregular, 0f, Mathf.Sin(angle) * 0.43f * irregular);
                shoulder[i] = new Vector3(
                    Mathf.Cos(angle + 0.05f) * 0.30f * irregular,
                    0.64f + 0.04f * Mathf.Sin(i + variant),
                    Mathf.Sin(angle + 0.05f) * 0.26f * irregular);
            }

            var tip = new Vector3(
                variant == 1 ? 0.09f : variant == 2 ? -0.07f : 0.03f,
                1f,
                variant == 2 ? 0.08f : -0.035f);
            var bottomCenter = Vector3.zero;

            var vertices = new List<Vector3>(sides * 18);
            var triangles = new List<int>(sides * 18);
            for (var i = 0; i < sides; i++)
            {
                var next = (i + 1) % sides;
                AddTriangle(vertices, triangles, bottom[i], shoulder[i], shoulder[next]);
                AddTriangle(vertices, triangles, bottom[i], shoulder[next], bottom[next]);
                AddTriangle(vertices, triangles, shoulder[i], tip, shoulder[next]);
                AddTriangle(vertices, triangles, bottomCenter, bottom[next], bottom[i]);
            }

            var mesh = new Mesh
            {
                name = $"Runtime_OriginiumShard_{variant}",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ShardMeshes[variant] = mesh;
            return mesh;
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            var index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
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
                    string.Equals(child.name, "[ActiveOriginiumSkin]", StringComparison.Ordinal) ||
                    string.Equals(child.name, "[ActiveOriginiumRedTile]", StringComparison.Ordinal))
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

            _frameMaterial = Clone(baseMaterial, "Runtime_ActiveOriginium_Frame",
                new Color(0.070f, 0.075f, 0.078f, 1f), 0.18f, 0.07f);
            _scorchedMaterial = Clone(baseMaterial, "Runtime_ActiveOriginium_Scorched",
                new Color(0.095f, 0.074f, 0.040f, 1f), 0.04f, 0.055f);
            _oreBlackMaterial = Clone(baseMaterial, "Runtime_ActiveOriginium_BlackOre",
                new Color(0.050f, 0.041f, 0.025f, 1f), 0.02f, 0.16f);
            _oreAmberMaterial = Clone(accent ?? baseMaterial, "Runtime_ActiveOriginium_Amber",
                new Color(0.34f, 0.205f, 0.045f, 1f), 0.01f, 0.24f);
            _oreGoldMaterial = Clone(accent ?? baseMaterial, "Runtime_ActiveOriginium_GoldFacet",
                new Color(0.62f, 0.39f, 0.075f, 1f), 0.01f, 0.29f);
            _oreGlowMaterial = Clone(accent ?? baseMaterial, "Runtime_ActiveOriginium_AmberGlow",
                new Color(0.78f, 0.47f, 0.070f, 1f), 0.00f, 0.26f);

            StripDeckSurfaceMaps(_oreBlackMaterial);
            StripDeckSurfaceMaps(_oreAmberMaterial);
            StripDeckSurfaceMaps(_oreGoldMaterial);
            StripDeckSurfaceMaps(_oreGlowMaterial);
            EnableEmission(_oreGlowMaterial, new Color(1.25f, 0.62f, 0.08f, 1f));
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

        private static void StripDeckSurfaceMaps(Material material)
        {
            if (material == null)
                return;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
            if (material.HasProperty("_OcclusionMap")) material.SetTexture("_OcclusionMap", null);
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
            _frameMaterial = null;
            _scorchedMaterial = null;
            _oreBlackMaterial = null;
            _oreAmberMaterial = null;
            _oreGoldMaterial = null;
            _oreGlowMaterial = null;
        }
    }
}
