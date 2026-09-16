using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Keeps actors readable in the fixed 2.5D camera. When a sealed/decorative city building sits
    /// between the camera and a living combat actor, that building's renderers are temporarily hidden
    /// while its colliders remain active. This avoids player/enemy disappearance behind tall facades
    /// without turning the whole city transparent or changing combat collision.
    /// </summary>
    [DefaultExecutionOrder(90)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageActorOcclusionController : MonoBehaviour
    {
        private const float ProbeRadius = 0.30f;
        private const float RestoreDelay = 0.16f;
        private const float ActorRefreshInterval = 0.45f;

        [SerializeField] private RogueliteStageMapController stageMap;

        private readonly List<CombatEntity> _actors = new(24);
        private readonly Dictionary<Transform, OcclusionState> _states = new();
        private float _nextActorRefreshAt;

        private sealed class OcclusionState
        {
            public readonly List<RendererState> Renderers = new();
            public float ReleaseAt;
            public bool Hidden;
        }

        private struct RendererState
        {
            public Renderer Renderer;
            public bool Enabled;
        }

        public void Configure(RogueliteStageMapController map)
        {
            stageMap = map;
        }

        private void LateUpdate()
        {
            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
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
            var origin = camera.transform.position;
            var delta = target - origin;
            var distance = delta.magnitude;
            if (distance <= 0.01f)
                return;

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
                    Hide(root, now);
            }
        }

        private void Hide(Transform root, float now)
        {
            if (!_states.TryGetValue(root, out var state))
            {
                state = new OcclusionState();
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null)
                        continue;
                    state.Renderers.Add(new RendererState
                    {
                        Renderer = renderer,
                        Enabled = renderer.enabled
                    });
                }
                _states.Add(root, state);
            }

            state.ReleaseAt = now + RestoreDelay;
            if (state.Hidden)
                return;

            for (var i = 0; i < state.Renderers.Count; i++)
            {
                var entry = state.Renderers[i];
                if (entry.Renderer != null && entry.Enabled)
                    entry.Renderer.enabled = false;
            }
            state.Hidden = true;
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
                    remove.Add(root);
                    continue;
                }
                if (!state.Hidden || now < state.ReleaseAt)
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
                if (entry.Renderer != null)
                    entry.Renderer.enabled = entry.Enabled;
            }
            state.Hidden = false;
        }

        private static Transform FindOccludableRoot(Transform start)
        {
            var current = start;
            for (var depth = 0; current != null && depth < 8; depth++, current = current.parent)
            {
                switch (current.name)
                {
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
                if (pair.Value != null && pair.Value.Hidden)
                    Restore(pair.Value);
            }
        }

        private void OnDisable()
        {
            RestoreAll();
        }

        private void OnDestroy()
        {
            RestoreAll();
        }

        /// <summary>
        /// Tiny local pool avoids allocating a temporary key list every LateUpdate.
        /// </summary>
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
