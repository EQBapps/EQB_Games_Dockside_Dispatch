using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Rounded meshes for the ships. A plan outline (rounded stern, elliptical bow, or a rounded rectangle) is lofted
    /// downwards and shrunk towards its centre as it goes, which gives a raked bow and a rounded bilge. Normals are
    /// smooth, so the Flat shader shades the curves softly instead of in hard facets. Meshes are cached by key.
    /// </summary>
    public static class HullMesh
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Ship plan outline (x along the ship, y = local z), counter-clockwise seen from above. Bow towards +x.</summary>
        public static Vector2[] ShipOutline(float sternX, float bodyEndX, float bowLength, float halfWidth, float sternRadius, int seg = 8)
        {
            var pts = new List<Vector2>();
            Arc(pts, new Vector2(sternX + sternRadius, -halfWidth + sternRadius), sternRadius, sternRadius, 180, 270, seg);
            Arc(pts, new Vector2(bodyEndX, 0), bowLength, halfWidth, -90, 90, seg * 2);
            Arc(pts, new Vector2(sternX + sternRadius, halfWidth - sternRadius), sternRadius, sternRadius, 90, 180, seg);
            return pts.ToArray();
        }

        /// <summary>Rounded rectangle centred on the origin, counter-clockwise seen from above.</summary>
        public static Vector2[] RoundedRect(float halfX, float halfZ, float radius, int seg = 6)
        {
            radius = Mathf.Min(radius, halfX, halfZ);
            var pts = new List<Vector2>();
            Arc(pts, new Vector2(halfX - radius, -halfZ + radius), radius, radius, -90, 0, seg);
            Arc(pts, new Vector2(halfX - radius, halfZ - radius), radius, radius, 0, 90, seg);
            Arc(pts, new Vector2(-halfX + radius, halfZ - radius), radius, radius, 90, 180, seg);
            Arc(pts, new Vector2(-halfX + radius, -halfZ + radius), radius, radius, 180, 270, seg);
            return pts.ToArray();
        }

        private static void Arc(List<Vector2> pts, Vector2 c, float rx, float rz, float a0, float a1, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                var p = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz);
                if (pts.Count == 0 || (pts[pts.Count - 1] - p).sqrMagnitude > 1e-8f) pts.Add(p);
            }
        }

        /// <summary>Outline pulled in towards its centre by inset.x at the ends and inset.y at the sides.</summary>
        public static Vector2[] Shrink(Vector2[] outline, Vector2 inset)
        {
            Bounds(outline, out var c, out var e);
            var s = new Vector2((e.x - inset.x) / e.x, (e.y - inset.y) / e.y);
            var r = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++) r[i] = c + Vector2.Scale(outline[i] - c, s);
            return r;
        }

        /// <summary>Heights from top to bottom in equal steps.</summary>
        public static float[] Steps(float top, float bottom, int n)
        {
            var ys = new float[n + 1];
            for (int i = 0; i <= n; i++) ys[i] = Mathf.Lerp(top, bottom, i / (float)n);
            return ys;
        }

        /// <summary>
        /// Walls through the given heights (top first). At height y the outline is shrunk by inset(y) (x = ends, y = sides).
        /// Optional flat caps at the top and bottom ring.
        /// </summary>
        public static Mesh Loft(string key, Vector2[] outline, float[] ys, Func<float, Vector2> inset, bool capTop, bool capBottom, bool inward = false)
        {
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;
            Bounds(outline, out var c, out var e);
            int n = outline.Length;
            Vector3 P(int i, float y)
            {
                var d = inset(y);
                var p = outline[((i % n) + n) % n];
                return new Vector3(c.x + (p.x - c.x) * (e.x - d.x) / e.x, y, c.y + (p.y - c.y) * (e.y - d.y) / e.y);
            }

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            const float eps = 0.004f;

            for (int k = 0; k < ys.Length; k++)
            {
                for (int i = 0; i < n; i++)
                {
                    var p = P(i, ys[k]);
                    var ti = P(i + 1, ys[k]) - P(i - 1, ys[k]);
                    var ty = P(i, ys[k] + eps) - P(i, ys[k] - eps);
                    var nn = Vector3.Cross(ty, ti).normalized;
                    var o = outline[i] - c;
                    if (nn.x * o.x / e.x + nn.z * o.y / e.y < 0) nn = -nn;
                    if (inward) nn = -nn;
                    verts.Add(p); norms.Add(nn);
                }
            }
            for (int k = 0; k < ys.Length - 1; k++)
            {
                int u = k * n, l = (k + 1) * n;
                for (int i = 0; i < n; i++)
                {
                    int i1 = (i + 1) % n;
                    if (inward)
                    {
                        tris.Add(l + i); tris.Add(u + i1); tris.Add(u + i);
                        tris.Add(l + i); tris.Add(l + i1); tris.Add(u + i1);
                    }
                    else
                    {
                        tris.Add(l + i); tris.Add(u + i); tris.Add(u + i1);
                        tris.Add(l + i); tris.Add(u + i1); tris.Add(l + i1);
                    }
                }
            }
            if (capTop) Cap(verts, norms, tris, P, n, ys[0], c, true);
            if (capBottom) Cap(verts, norms, tris, P, n, ys[ys.Length - 1], c, false);

            var mesh = new Mesh { name = "Hull_" + key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count]));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat ring between an outer outline and the same outline shrunk by width (a deck rim), facing up.</summary>
        public static Mesh Ring(string key, Vector2[] outline, float width, float y)
        {
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;
            var inner = Shrink(outline, new Vector2(width, width));
            int n = outline.Length;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                verts.Add(new Vector3(outline[i].x, y, outline[i].y));
                verts.Add(new Vector3(inner[i].x, y, inner[i].y));
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = ((i + 1) % n) * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(b + 1);
                tris.Add(a); tris.Add(b + 1); tris.Add(b);
            }
            var norms = new List<Vector3>();
            for (int i = 0; i < verts.Count; i++) norms.Add(Vector3.up);
            var mesh = new Mesh { name = "Ring_" + key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count]));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Rounded block: rounded-rectangle plan with a rounded top edge.</summary>
        public static Mesh Block(string key, float halfX, float halfZ, float height, float cornerRadius, float topRadius)
        {
            var outline = RoundedRect(halfX, halfZ, cornerRadius);
            var ys = new List<float>();
            for (int i = 0; i <= 4; i++) ys.Add(height - topRadius * (1f - Mathf.Cos(i / 4f * Mathf.PI * 0.5f)));
            ys.Add(0f);
            return Loft("Block" + key, outline, ys.ToArray(), y =>
            {
                float dy = Mathf.Max(0f, y - (height - topRadius));
                float d = topRadius - Mathf.Sqrt(Mathf.Max(0f, topRadius * topRadius - dy * dy));
                return new Vector2(d, d);
            }, true, false);
        }

        private static void Cap(List<Vector3> verts, List<Vector3> norms, List<int> tris, Func<int, float, Vector3> p, int n, float y, Vector2 c, bool top)
        {
            int c0 = verts.Count;
            verts.Add(new Vector3(c.x, y, c.y)); norms.Add(top ? Vector3.up : Vector3.down);
            for (int i = 0; i < n; i++) { verts.Add(p(i, y)); norms.Add(top ? Vector3.up : Vector3.down); }
            for (int i = 0; i < n; i++)
            {
                int a = c0 + 1 + i, b = c0 + 1 + (i + 1) % n;
                if (top) { tris.Add(c0); tris.Add(b); tris.Add(a); }
                else { tris.Add(c0); tris.Add(a); tris.Add(b); }
            }
        }

        private static void Bounds(Vector2[] pts, out Vector2 centre, out Vector2 halfExtents)
        {
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            centre = (min + max) * 0.5f;
            halfExtents = (max - min) * 0.5f;
        }
    }
}
