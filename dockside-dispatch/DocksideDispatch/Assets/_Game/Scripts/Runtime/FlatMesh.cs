using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Accumulates flat 2D shapes (in the XY plane, facing -Z) into one mesh, for crisp silhouettes,
    /// clouds and decals drawn in a single draw call. All shapes must be convex (they are fan-triangulated).
    /// </summary>
    public class FlatMesh
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<int> _t = new List<int>();
        private readonly List<Vector2> _uv = new List<Vector2>();

        public FlatMesh Poly(params Vector2[] pts)
        {
            // Ensure clockwise winding as seen from -Z (Unity front face).
            float area = 0;
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % pts.Length];
                area += a.x * b.y - b.x * a.y;
            }
            int s = _v.Count;
            foreach (var p in pts) { _v.Add(new Vector3(p.x, p.y, 0)); _uv.Add(p); }
            for (int i = 1; i < pts.Length - 1; i++)
            {
                if (area > 0) { _t.Add(s); _t.Add(s + i + 1); _t.Add(s + i); }
                else { _t.Add(s); _t.Add(s + i); _t.Add(s + i + 1); }
            }
            return this;
        }

        public FlatMesh Rect(float x0, float y0, float x1, float y1) =>
            Poly(new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1));

        /// <summary>A straight bar from a to b with the given thickness.</summary>
        public FlatMesh Line(Vector2 a, Vector2 b, float thickness)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * thickness * 0.5f;
            return Poly(a - n, b - n, b + n, a + n);
        }

        public FlatMesh Ellipse(Vector2 c, float rx, float ry, int segments = 28)
        {
            var pts = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }
            return Poly(pts);
        }

        public FlatMesh Circle(Vector2 c, float r, int segments = 36) => Ellipse(c, r, r, segments);

        /// <summary>Rounded cloud puff row: a flat-bottomed bank of overlapping circles.</summary>
        public FlatMesh CloudBank(Vector2 left, float length, float height, System.Random rng)
        {
            Rect(left.x + height * 0.5f, left.y, left.x + length - height * 0.5f, left.y + height * 0.5f);
            int puffs = Mathf.Max(2, Mathf.RoundToInt(length / (height * 0.9f)));
            for (int i = 0; i < puffs; i++)
            {
                float t = (i + 0.5f) / puffs;
                float r = height * (0.35f + 0.3f * Mathf.Sin(t * Mathf.PI)) * (0.8f + 0.4f * (float)rng.NextDouble());
                float cx = left.x + height * 0.5f + t * (length - height);
                // Clip each puff to the bank's flat base by using a half-ellipse.
                var pts = new List<Vector2>();
                for (int k = 0; k <= 14; k++)
                {
                    float a = Mathf.PI * k / 14f;
                    pts.Add(new Vector2(cx + Mathf.Cos(a) * r * 1.25f, left.y + Mathf.Sin(a) * r * 1.1f + height * 0.1f));
                }
                Poly(pts.ToArray());
            }
            return this;
        }

        /// <summary>Dashed outline of a rounded rectangle centred on the origin (dash and gap measured along the edge).</summary>
        public FlatMesh DashedRoundRect(float w, float h, float r, float thickness, float dash, float gap)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            var path = new List<Vector2>();
            void Arc(Vector2 c, float a0)
            {
                for (int i = 0; i <= 8; i++)
                {
                    float a = (a0 + 90f * i / 8f) * Mathf.Deg2Rad;
                    path.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
            float hw = w * 0.5f - r, hh = h * 0.5f - r;
            Arc(new Vector2(hw, -hh), -90f); Arc(new Vector2(hw, hh), 0f); Arc(new Vector2(-hw, hh), 90f); Arc(new Vector2(-hw, -hh), 180f);
            path.Add(path[0]);
            // Walk the path, emitting short straight pieces while inside a dash.
            float period = dash + gap, s0 = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 a = path[i], b = path[i + 1];
                float len = Vector2.Distance(a, b);
                float t = 0f;
                while (t < len)
                {
                    float phase = (s0 + t) % period;
                    float step = phase < dash ? Mathf.Min(dash - phase, len - t) : Mathf.Min(period - phase, len - t);
                    if (phase < dash && step > 1e-4f)
                        Line(Vector2.Lerp(a, b, t / len), Vector2.Lerp(a, b, (t + step) / len), thickness);
                    t += Mathf.Max(step, 1e-4f);
                }
                s0 += len;
            }
            return this;
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetUVs(0, _uv);
            m.SetTriangles(_t, 0);
            var normals = new Vector3[_v.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;
            m.normals = normals;
            m.RecalculateBounds();
            return m;
        }

        public bool IsEmpty => _v.Count == 0;
    }
}
