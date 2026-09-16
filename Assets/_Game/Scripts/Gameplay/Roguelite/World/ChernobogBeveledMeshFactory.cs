using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Small hard-surface mesh cache used by the Chernobog environment kit.
    /// The project started from Unity cube primitives; this factory gives visual-only modules
    /// real chamfered silhouettes so directional light can catch edges instead of reading as flat blocks.
    ///
    /// It is public so the editor-side modular-kit builder can persist the same geometry as reusable
    /// Mesh assets instead of rebuilding everything from primitives at runtime.
    /// </summary>
    public static class ChernobogBeveledMeshFactory
    {
        private static readonly Dictionary<MeshKey, Mesh> Cache = new();

        public static Mesh GetBox(Vector3 requestedSize, float requestedBevel)
        {
            var size = new Vector3(
                Mathf.Max(0.001f, Mathf.Abs(requestedSize.x)),
                Mathf.Max(0.001f, Mathf.Abs(requestedSize.y)),
                Mathf.Max(0.001f, Mathf.Abs(requestedSize.z)));
            var smallestHalf = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f;
            var bevel = Mathf.Clamp(requestedBevel, 0.0005f, Mathf.Max(0.0005f, smallestHalf * 0.45f));
            var key = new MeshKey(size, bevel);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var mesh = BuildBox(size, bevel);
            Cache[key] = mesh;
            return mesh;
        }

        private static Mesh BuildBox(Vector3 size, float bevel)
        {
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            var hz = size.z * 0.5f;
            var ix = Mathf.Max(0f, hx - bevel);
            var iy = Mathf.Max(0f, hy - bevel);
            var iz = Mathf.Max(0f, hz - bevel);

            var vertices = new List<Vector3>(144);
            var normals = new List<Vector3>(144);
            var uvs = new List<Vector2>(144);
            var triangles = new List<int>(216);

            // Six broad faces.
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(hx, -iy, -iz), new Vector3(hx, iy, -iz),
                new Vector3(hx, iy, iz), new Vector3(hx, -iy, iz), Vector3.right);
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(-hx, -iy, iz), new Vector3(-hx, iy, iz),
                new Vector3(-hx, iy, -iz), new Vector3(-hx, -iy, -iz), Vector3.left);
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(-ix, hy, -iz), new Vector3(-ix, hy, iz),
                new Vector3(ix, hy, iz), new Vector3(ix, hy, -iz), Vector3.up);
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(-ix, -hy, iz), new Vector3(-ix, -hy, -iz),
                new Vector3(ix, -hy, -iz), new Vector3(ix, -hy, iz), Vector3.down);
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(-ix, -iy, hz), new Vector3(ix, -iy, hz),
                new Vector3(ix, iy, hz), new Vector3(-ix, iy, hz), Vector3.forward);
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(ix, -iy, -hz), new Vector3(-ix, -iy, -hz),
                new Vector3(-ix, iy, -hz), new Vector3(ix, iy, -hz), Vector3.back);

            // Twelve chamfer strips. Winding is corrected automatically against the requested normal.
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sy = -1; sy <= 1; sy += 2)
            {
                var normal = new Vector3(sx, sy, 0f).normalized;
                AddQuad(vertices, normals, uvs, triangles,
                    new Vector3(sx * hx, sy * iy, -iz),
                    new Vector3(sx * hx, sy * iy, iz),
                    new Vector3(sx * ix, sy * hy, iz),
                    new Vector3(sx * ix, sy * hy, -iz), normal);
            }

            for (var sx = -1; sx <= 1; sx += 2)
            for (var sz = -1; sz <= 1; sz += 2)
            {
                var normal = new Vector3(sx, 0f, sz).normalized;
                AddQuad(vertices, normals, uvs, triangles,
                    new Vector3(sx * hx, -iy, sz * iz),
                    new Vector3(sx * hx, iy, sz * iz),
                    new Vector3(sx * ix, iy, sz * hz),
                    new Vector3(sx * ix, -iy, sz * hz), normal);
            }

            for (var sy = -1; sy <= 1; sy += 2)
            for (var sz = -1; sz <= 1; sz += 2)
            {
                var normal = new Vector3(0f, sy, sz).normalized;
                AddQuad(vertices, normals, uvs, triangles,
                    new Vector3(-ix, sy * hy, sz * iz),
                    new Vector3(ix, sy * hy, sz * iz),
                    new Vector3(ix, sy * iy, sz * hz),
                    new Vector3(-ix, sy * iy, sz * hz), normal);
            }

            // Eight clipped corners.
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sy = -1; sy <= 1; sy += 2)
            for (var sz = -1; sz <= 1; sz += 2)
            {
                var normal = new Vector3(sx, sy, sz).normalized;
                AddTriangle(vertices, normals, uvs, triangles,
                    new Vector3(sx * hx, sy * iy, sz * iz),
                    new Vector3(sx * ix, sy * hy, sz * iz),
                    new Vector3(sx * ix, sy * iy, sz * hz), normal);
            }

            var mesh = new Mesh
            {
                name = $"ChernobogBeveledBox_{size.x:0.###}_{size.y:0.###}_{size.z:0.###}_{bevel:0.###}",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 outward)
        {
            outward.Normalize();
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                var swap = b;
                b = d;
                d = swap;
            }

            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(outward);
            normals.Add(outward);
            normals.Add(outward);
            normals.Add(outward);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            triangles.Add(start + 0);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 0);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void AddTriangle(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 outward)
        {
            outward.Normalize();
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                var swap = b;
                b = c;
                c = swap;
            }

            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(outward);
            normals.Add(outward);
            normals.Add(outward);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            triangles.Add(start + 0);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private struct MeshKey : IEquatable<MeshKey>
        {
            private readonly int x;
            private readonly int y;
            private readonly int z;
            private readonly int bevel;

            public MeshKey(Vector3 size, float amount)
            {
                x = Mathf.RoundToInt(size.x * 1000f);
                y = Mathf.RoundToInt(size.y * 1000f);
                z = Mathf.RoundToInt(size.z * 1000f);
                bevel = Mathf.RoundToInt(amount * 1000f);
            }

            public bool Equals(MeshKey other)
            {
                return x == other.x && y == other.y && z == other.z && bevel == other.bevel;
            }

            public override bool Equals(object obj)
            {
                return obj is MeshKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + x;
                    hash = hash * 31 + y;
                    hash = hash * 31 + z;
                    hash = hash * 31 + bevel;
                    return hash;
                }
            }
        }
    }
}
