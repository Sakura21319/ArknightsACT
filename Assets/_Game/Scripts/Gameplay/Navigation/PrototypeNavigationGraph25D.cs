using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Navigation
{
    /// <summary>
    /// Lightweight waypoint graph for the generated 2.5D prototype. It deliberately avoids a
    /// NavMesh/package dependency while still giving enemies deterministic routes through ramps,
    /// floors and narrow entrances.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeNavigationGraph25D : MonoBehaviour
    {
        [Serializable]
        private struct Edge
        {
            public int A;
            public int B;

            public Edge(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        [SerializeField] private Vector3[] nodes = Array.Empty<Vector3>();
        [SerializeField] private Edge[] edges = Array.Empty<Edge>();
        [SerializeField, Min(0.25f)] private float verticalSelectionWeight = 4f;

        private readonly List<int>[] _emptyAdjacency = Array.Empty<List<int>>();
        private List<int>[] _adjacency;

        public static PrototypeNavigationGraph25D Instance { get; private set; }
        public int NodeCount => nodes?.Length ?? 0;

        public void Configure(Vector3[] nodePositions, int[] edgePairs)
        {
            nodes = nodePositions ?? Array.Empty<Vector3>();
            if (edgePairs == null || edgePairs.Length < 2)
            {
                edges = Array.Empty<Edge>();
            }
            else
            {
                var count = edgePairs.Length / 2;
                edges = new Edge[count];
                for (var i = 0; i < count; i++)
                    edges[i] = new Edge(edgePairs[i * 2], edgePairs[i * 2 + 1]);
            }
            RebuildAdjacency();
        }

        private void Awake()
        {
            Instance = this;
            RebuildAdjacency();
        }

        private void OnEnable()
        {
            Instance = this;
            if (_adjacency == null)
                RebuildAdjacency();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool TryBuildPath(Vector3 from, Vector3 to, List<Vector3> result)
        {
            if (result == null)
                return false;
            result.Clear();

            if (nodes == null || nodes.Length == 0)
                return false;
            if (_adjacency == null || _adjacency.Length != nodes.Length)
                RebuildAdjacency();

            var start = FindNearestNode(from);
            var goal = FindNearestNode(to);
            if (start < 0 || goal < 0)
                return false;

            if (start == goal)
            {
                result.Add(nodes[goal]);
                result.Add(to);
                return true;
            }

            var count = nodes.Length;
            var g = new float[count];
            var parent = new int[count];
            var closed = new bool[count];
            for (var i = 0; i < count; i++)
            {
                g[i] = float.PositiveInfinity;
                parent[i] = -1;
            }
            g[start] = 0f;

            for (var iteration = 0; iteration < count; iteration++)
            {
                var current = -1;
                var best = float.PositiveInfinity;
                for (var i = 0; i < count; i++)
                {
                    if (closed[i] || float.IsPositiveInfinity(g[i]))
                        continue;
                    var score = g[i] + Heuristic(i, goal);
                    if (score < best)
                    {
                        best = score;
                        current = i;
                    }
                }

                if (current < 0)
                    break;
                if (current == goal)
                    break;

                closed[current] = true;
                var neighbors = _adjacency != null && current < _adjacency.Length
                    ? _adjacency[current]
                    : null;
                if (neighbors == null)
                    continue;

                for (var n = 0; n < neighbors.Count; n++)
                {
                    var next = neighbors[n];
                    if (next < 0 || next >= count || closed[next])
                        continue;
                    var candidate = g[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (candidate >= g[next])
                        continue;
                    g[next] = candidate;
                    parent[next] = current;
                }
            }

            if (parent[goal] < 0)
                return false;

            var reverse = new List<int>(count);
            var cursor = goal;
            reverse.Add(cursor);
            while (cursor != start)
            {
                cursor = parent[cursor];
                if (cursor < 0)
                    return false;
                reverse.Add(cursor);
                if (reverse.Count > count + 1)
                    return false;
            }

            for (var i = reverse.Count - 1; i >= 0; i--)
                result.Add(nodes[reverse[i]]);
            result.Add(to);
            return true;
        }

        private int FindNearestNode(Vector3 position)
        {
            var bestIndex = -1;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < nodes.Length; i++)
            {
                var delta = nodes[i] - position;
                var planar = new Vector2(delta.x, delta.z).sqrMagnitude;
                var vertical = delta.y * delta.y * verticalSelectionWeight;
                var score = planar + vertical;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private float Heuristic(int from, int to) => Vector3.Distance(nodes[from], nodes[to]);

        private void RebuildAdjacency()
        {
            if (nodes == null || nodes.Length == 0)
            {
                _adjacency = _emptyAdjacency;
                return;
            }

            _adjacency = new List<int>[nodes.Length];
            for (var i = 0; i < _adjacency.Length; i++)
                _adjacency[i] = new List<int>(4);

            if (edges == null)
                return;

            for (var i = 0; i < edges.Length; i++)
            {
                var a = edges[i].A;
                var b = edges[i].B;
                if (a < 0 || b < 0 || a >= nodes.Length || b >= nodes.Length || a == b)
                    continue;
                if (!_adjacency[a].Contains(b))
                    _adjacency[a].Add(b);
                if (!_adjacency[b].Contains(a))
                    _adjacency[b].Add(a);
            }
        }
    }
}
