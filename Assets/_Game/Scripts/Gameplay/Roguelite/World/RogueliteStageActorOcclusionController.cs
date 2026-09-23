using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Keeps actors readable in the fixed 2.5D camera. Occluding city buildings are faded to an
    /// x-ray silhouette instead of disappearing outright: collision stays solid and the player keeps
    /// a clear mental model of where the building still exists.
    /// </summary>
    [DefaultExecutionOrder(90)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageActorOcclusionController : MonoBehaviour
    {
        private const float ProbeRadius = 0.30f;
        private const float RestoreDelay = 0.18f;
        private const float ActorRefreshInterval = 0.45f;
        private const float OccludedAlpha = 0.20f;

        [SerializeField] private RogueliteStageMapController stageMap;

        private RogueliteStageRuntimeContext _context;
        private readonly List<CombatEntity> _actors = new(24);
        private readonly Dictionary<Transform, OcclusionState> _states = new();
        private float _nextActorRefreshAt;

        private sealed class OcclusionState
        {
            public readonly List<RendererState> Renderers = new();
            public float ReleaseAt;
            public bool Faded;
        }

        private sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public Material[] FadedMaterials;
            public ShadowCastingMode OriginalShadowMode;
        }

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

        private void LateUpdate()
        {
            stageMap ??= _context != null ? _context.StageMap : null;
            var camera = Camera.main;
            if (stageMap == null || camera == null)
            {
                RestoreAll();
                return;
            }

            if (Time.unscaledTime >= _nextActorRefreshAt)
            {
                RefreshActors();
                _nextActorRefreshAt = Time.unscaledTime + ActorRefreshInterval;
            }

            var now = Time.unscaledTime;
            for (var i = 0; i < _actors.Count; i++)
            {
                var actor = _actors[i];
                if (actor == null || !actor.gameObject.activeInHierarchy)
                    continue;
                if (actor.Health != null && actor.Health.IsDead)
                    continue;

                RevealLineOfSight(camera, actor.transform.position + Vector3.up * 0.90f, now);
            }

            RestoreExpired(now);
        }

        private void RefreshActors()
        {
            _actors.Clear();
            var actors = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            for (var i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor != null && actor.gameObject.activeInHierarchy)
                    _actors.Add(actor);
            }
        }

        private void RevealLineOfSight(Camera camera, Vector3 target, float now)
        {
            var origin = camera.orthographic
                ? target - camera.transform.forward * camera.farClipPlane
                : camera.transform.position;
            var delta = target - origin;
            var distance = delta.magnitude;
            if (distance <= 0.01f)
                return;

            var sight = new Ray(origin, delta / distance);
            foreach (var building in EnterableBuilding25D.Active)
                if (building != null && (building.Contains(target) || building.Intersects(sight, distance)))
                    Fade(building.transform, now);

            var hits = Physics.SphereCastAll(
                origin,
                ProbeRadius,
                delta / distance,
                distance - 0.05f,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null)
                    continue;

                var root = FindOccludableRoot(collider.transform);
                if (root != null)
                    Fade(root, now);
            }
        }

        private void Fade(Transform root, float now)
        {
            if (!_states.TryGetValue(root, out var state))
            {
                state = BuildState(root);
                _states.Add(root, state);
            }

            state.ReleaseAt = now + RestoreDelay;
            if (state.Faded)
                return;

            for (var i = 0; i < state.Renderers.Count; i++)
            {
                var entry = state.Renderers[i];
                if (entry.Renderer == null)
                    continue;
                entry.Renderer.sharedMaterials = entry.FadedMaterials;
                entry.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            state.Faded = true;
        }

        private static OcclusionState BuildState(Transform root)
        {
            var state = new OcclusionState();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                // Keep the interior floor and searchable loot readable while walls/roofs fade.
                if (renderer.name == "InteriorFloor" || renderer.name == "Floor_Ground" ||
                    renderer.name.StartsWith("WalkSurface_") || renderer.name.StartsWith("WalkRail_") || renderer.name == "StairTreadMark" ||
                    renderer.GetComponentInParent<ArknightsACT.Gameplay.Roguelite.Treasure.SearchableContainer25D>() != null)
                    continue;

                var originals = renderer.sharedMaterials;
                var faded = new Material[originals.Length];
                for (var m = 0; m < originals.Length; m++)
                    faded[m] = CreateFadedMaterial(originals[m]);

                state.Renderers.Add(new RendererState
                {
                    Renderer = renderer,
                    OriginalMaterials = originals,
                    FadedMaterials = faded,
                    OriginalShadowMode = renderer.shadowCastingMode
                });
            }
            return state;
        }

        private static Material CreateFadedMaterial(Material source)
        {
            if (source == null)
                return null;

            var material = new Material(source)
            {
                name = source.name + "_OcclusionXRay",
                renderQueue = (int)RenderQueue.Transparent
            };

            var color = Color.white;
            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
                color.a = OccludedAlpha;
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
                color.a = OccludedAlpha;
                material.SetColor("_Color", color);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

            material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            return material;
        }

        private void RestoreExpired(float now)
        {
            var remove = ListPool<Transform>.Get();
            foreach (var pair in _states)
            {
                var root = pair.Key;
                var state = pair.Value;
                if (root == null)
                {
                    ReleaseState(state);
                    remove.Add(root);
                    continue;
                }
                if (!state.Faded || now < state.ReleaseAt)
                    continue;

                Restore(state);
            }

            for (var i = 0; i < remove.Count; i++)
                _states.Remove(remove[i]);
            ListPool<Transform>.Release(remove);
        }

        private static void Restore(OcclusionState state)
        {
            for (var i = 0; i < state.Renderers.Count; i++)
            {
                var entry = state.Renderers[i];
                if (entry.Renderer == null)
                    continue;
                entry.Renderer.sharedMaterials = entry.OriginalMaterials;
                entry.Renderer.shadowCastingMode = entry.OriginalShadowMode;
            }
            state.Faded = false;
        }

        private static void ReleaseState(OcclusionState state)
        {
            if (state == null)
                return;
            for (var i = 0; i < state.Renderers.Count; i++)
            {
                var entry = state.Renderers[i];
                if (entry?.FadedMaterials == null)
                    continue;
                for (var m = 0; m < entry.FadedMaterials.Length; m++)
                    if (entry.FadedMaterials[m] != null)
                        Destroy(entry.FadedMaterials[m]);
            }
        }

        private static Transform FindOccludableRoot(Transform start)
        {
            var tower = start.GetComponentInParent<CityTowerLandmark>();
            if (tower != null && tower.Structure != null && start.IsChildOf(tower.Structure)) return tower.Structure;
            var current = start;
            for (var depth = 0; current != null && depth < 8; depth++, current = current.parent)
            {
                switch (current.name)
                {
                    case "WalkInStreetTenement":
                    case "WalkInCommercialUnit":
                    case "WalkInServiceRoom":
                    case "CoveredCheckpoint":
                    case "TwoFloorFacility":
                    case "SealedCityBuilding":
                    case "SealedUtilityBuilding":
                    case "SealedSideBuilding":
                    case "ClosedStorefront":
                    case "ServiceBuilding":
                    case "RuinedBuildingShell":
                    case "UtilityWarehouse":
                    case "UtilityRelayTower":
                        return current;
                }
            }
            return null;
        }

        private void RestoreAll()
        {
            foreach (var pair in _states)
            {
                if (pair.Value != null && pair.Value.Faded)
                    Restore(pair.Value);
            }
        }

        private void ReleaseAll()
        {
            RestoreAll();
            foreach (var pair in _states)
                ReleaseState(pair.Value);
            _states.Clear();
        }

        private void OnDisable()
        {
            RestoreAll();
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>(8);
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
