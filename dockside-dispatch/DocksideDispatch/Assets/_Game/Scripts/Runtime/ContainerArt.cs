using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Procedural shipping-container art: a greyscale detail texture (frame rails, corner posts and castings,
    /// trapezoid corrugation, door bars, ID plate) that the material tints with the container colour,
    /// plus a unit box mesh whose faces map into that texture. One renderer per container instead of a
    /// stack of boxes, so it is both sharper and much cheaper to draw.
    ///
    /// Texture layout (pixels, v up), for a container of length L cells, width W = 256 * L + 256:
    ///   Top face   u [0, 256L)        v [256, 512)
    ///   Long sides u [0, 256L)        v [0, 200)
    ///   Door ends  u [256L, 256L+256) v [0, 200)
    /// </summary>
    public static class ContainerArt
    {
        private const int Cell = 256, H = 512, SideH = 200, TopY = 256, Gutter = 4;
        private static readonly Dictionary<int, Texture2D> Textures = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Mesh> Meshes = new Dictionary<int, Mesh>();

        public static Texture2D Texture(int length)
        {
            length = Mathf.Clamp(length, 1, 2);
            if (Textures.TryGetValue(length, out var t) && t) return t;
            int w = Cell * length + Cell;
            var px = new float[w * H];
            for (int i = 0; i < px.Length; i++) px[i] = 0.9f;
            DrawTop(px, w, 0, TopY, Cell * length, H - TopY, length);
            DrawSide(px, w, 0, 0, Cell * length, SideH, length);
            DrawDoors(px, w, Cell * length, 0, Cell, SideH);

            var cols = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(px[i] * 255f), 0, 255);
                cols[i] = new Color32(b, b, b, 255);
            }
            t = new Texture2D(w, H, TextureFormat.RGBA32, true, false)
            {
                name = $"ContainerDetail_L{length}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            t.SetPixels32(cols);
            t.Apply(true, true);
            Textures[length] = t;
            return t;
        }

        // ------------------------------------------------------------------ drawing helpers
        private static void Fill(float[] px, int w, int x0, int y0, int x1, int y1, float v)
        {
            x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0);
            x1 = Mathf.Min(w, x1); y1 = Mathf.Min(px.Length / w, y1);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) px[y * w + x] = v;
        }

        private static void Mul(float[] px, int w, int x0, int y0, int x1, int y1, float k)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(px.Length / w, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++) px[y * w + x] *= k;
        }

        /// <summary>Trapezoid corrugation profile, t in [0,1): flat high, dark falling slope, flat low, lit rising slope.</summary>
        private static float Corrugation(float t)
        {
            if (t < 0.34f) return 1.0f;
            if (t < 0.46f) return Mathf.Lerp(1.0f, 0.72f, (t - 0.34f) / 0.12f);
            if (t < 0.84f) return 0.84f;
            return Mathf.Lerp(0.84f, 1.05f, (t - 0.84f) / 0.16f);
        }

        private static void DrawTop(float[] px, int w, int ox, int oy, int rw, int rh, int length)
        {
            int ribs = 7 * length;
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                {
                    float t = (x / (float)rw * ribs) % 1f;
                    // Roof panels are shallower than the walls: soften the profile.
                    px[(oy + y) * w + ox + x] = Mathf.Lerp(0.93f, Corrugation(t), 0.45f);
                }
            int g = Gutter, b = 14;
            // Frame rails all round, with a dark crisp inner edge and a lit outer lip.
            Fill(px, w, ox + g, oy + g, ox + rw - g, oy + g + b, 0.7f);
            Fill(px, w, ox + g, oy + rh - g - b, ox + rw - g, oy + rh - g, 0.7f);
            Fill(px, w, ox + g, oy + g, ox + g + b, oy + rh - g, 0.7f);
            Fill(px, w, ox + rw - g - b, oy + g, ox + rw - g, oy + rh - g, 0.7f);
            Fill(px, w, ox + g + b, oy + g + b, ox + rw - g - b, oy + g + b + 3, 0.55f);
            Fill(px, w, ox + g + b, oy + rh - g - b - 3, ox + rw - g - b, oy + rh - g - b, 0.55f);
            Fill(px, w, ox + g + b, oy + g + b, ox + g + b + 3, oy + rh - g - b, 0.55f);
            Fill(px, w, ox + rw - g - b - 3, oy + g + b, ox + rw - g - b, oy + rh - g - b, 0.55f);
            Fill(px, w, ox, oy, ox + rw, oy + g, 0.5f);
            Fill(px, w, ox, oy + rh - g, ox + rw, oy + rh, 0.5f);
            Fill(px, w, ox, oy, ox + g, oy + rh, 0.5f);
            Fill(px, w, ox + rw - g, oy, ox + rw, oy + rh, 0.5f);
            // Corner castings with their oval twist-lock holes.
            int c = 26;
            foreach (var (cx, cy) in new[] { (ox + g, oy + g), (ox + rw - g - c, oy + g), (ox + g, oy + rh - g - c), (ox + rw - g - c, oy + rh - g - c) })
            {
                Fill(px, w, cx, cy, cx + c, cy + c, 0.48f);
                Fill(px, w, cx + 2, cy + c - 4, cx + c - 2, cy + c - 2, 0.62f); // lit top lip
                Fill(px, w, cx + 9, cy + 7, cx + c - 9, cy + c - 7, 0.22f);
            }
            // Mid-span cross member for long containers.
            if (length > 1) Fill(px, w, ox + rw / 2 - 5, oy + g, ox + rw / 2 + 5, oy + rh - g, 0.66f);
        }

        private static void DrawSide(float[] px, int w, int ox, int oy, int rw, int rh, int length)
        {
            int ribs = 9 * length;
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                    px[(oy + y) * w + ox + x] = Corrugation((x / (float)rw * ribs) % 1f) * 0.97f;
            int g = Gutter;
            // Bottom rail (heavy), top rail, corner posts.
            Fill(px, w, ox, oy, ox + rw, oy + g + 22, 0.52f);
            Fill(px, w, ox, oy + g + 22, ox + rw, oy + g + 25, 0.38f);
            Fill(px, w, ox, oy + rh - g - 16, ox + rw, oy + rh, 0.68f);
            Fill(px, w, ox, oy + rh - g - 16, ox + rw, oy + rh - g - 13, 0.95f); // lit lip under the roof edge
            Fill(px, w, ox, oy + rh - g - 19, ox + rw, oy + rh - g - 16, 0.45f);
            int post = 18;
            Fill(px, w, ox, oy, ox + g + post, oy + rh, 0.62f);
            Fill(px, w, ox + rw - g - post, oy, ox + rw, oy + rh, 0.62f);
            Fill(px, w, ox + g + post, oy, ox + g + post + 3, oy + rh, 0.42f);
            Fill(px, w, ox + rw - g - post - 3, oy, ox + rw - g - post, oy + rh, 0.42f);
            // Corner castings at the four corners of the wall.
            foreach (var (cx, cy) in new[] { (ox + g, oy + g), (ox + rw - g - post, oy + g), (ox + g, oy + rh - g - 18), (ox + rw - g - post, oy + rh - g - 18) })
            {
                Fill(px, w, cx, cy, cx + post, cy + 18, 0.45f);
                Fill(px, w, cx + 5, cy + 5, cx + post - 5, cy + 13, 0.2f);
            }
            // ID plate with stencil lines, top left.
            int plateX = ox + g + post + 14, plateY = oy + rh - g - 54;
            Fill(px, w, plateX, plateY, plateX + 58, plateY + 26, 1.08f);
            Fill(px, w, plateX + 6, plateY + 15, plateX + 46, plateY + 19, 0.5f);
            Fill(px, w, plateX + 6, plateY + 7, plateX + 32, plateY + 11, 0.5f);
            // Weathering: a soft darker band near the bottom.
            Mul(px, w, ox, oy + g + 25, ox + rw, oy + g + 45, 0.94f);
            // Mid-span post for long containers.
            if (length > 1) Fill(px, w, ox + rw / 2 - 5, oy, ox + rw / 2 + 5, oy + rh, 0.6f);
        }

        private static void DrawDoors(float[] px, int w, int ox, int oy, int rw, int rh)
        {
            Fill(px, w, ox, oy, ox + rw, oy + rh, 0.9f);
            // Door panels have shallow horizontal ribs.
            for (int y = 0; y < rh; y++)
            {
                float k = Mathf.Lerp(0.95f, Corrugation((y / (float)rh * 5f) % 1f), 0.35f);
                Mul(px, w, ox, oy + y, ox + rw, oy + y + 1, k);
            }
            int g = Gutter, post = 18;
            Fill(px, w, ox, oy, ox + rw, oy + g + 22, 0.52f);
            Fill(px, w, ox, oy + rh - g - 16, ox + rw, oy + rh, 0.68f);
            Fill(px, w, ox, oy, ox + g + post, oy + rh, 0.62f);
            Fill(px, w, ox + rw - g - post, oy, ox + rw, oy + rh, 0.62f);
            // Centre seam between the two doors.
            Fill(px, w, ox + rw / 2 - 2, oy + g + 22, ox + rw / 2 + 2, oy + rh - g - 16, 0.35f);
            // Four locking bars with handles and cam keepers.
            foreach (float f in new[] { 0.24f, 0.4f, 0.6f, 0.76f })
            {
                int bx = ox + Mathf.RoundToInt(rw * f);
                Fill(px, w, bx - 3, oy + g + 22, bx + 3, oy + rh - g - 16, 0.5f);
                Fill(px, w, bx - 2, oy + g + 22, bx - 1, oy + rh - g - 16, 0.75f); // highlight
                Fill(px, w, bx - 8, oy + rh / 2 - 4, bx + 8, oy + rh / 2 + 4, 0.38f);
                Fill(px, w, bx - 6, oy + g + 24, bx + 6, oy + g + 32, 0.4f);
                Fill(px, w, bx - 6, oy + rh - g - 26, bx + 6, oy + rh - g - 18, 0.4f);
            }
            // Hinges on the outer edges.
            foreach (float f in new[] { 0.22f, 0.5f, 0.78f })
            {
                int hy = oy + Mathf.RoundToInt(rh * f);
                Fill(px, w, ox + g + post, hy - 4, ox + g + post + 10, hy + 4, 0.4f);
                Fill(px, w, ox + rw - g - post - 10, hy - 4, ox + rw - g - post, hy + 4, 0.4f);
            }
        }

        // ------------------------------------------------------------------ mesh
        /// <summary>Unit box (-0.5..0.5) with UVs into the container texture for this length.</summary>
        public static Mesh Mesh(int length)
        {
            length = Mathf.Clamp(length, 1, 2);
            if (Meshes.TryGetValue(length, out var m) && m) return m;
            float W = Cell * length + Cell;
            Rect top = PxRect(0, TopY, Cell * length, H - TopY, W);
            Rect side = PxRect(0, 0, Cell * length, SideH, W);
            Rect end = PxRect(Cell * length, 0, Cell, SideH, W);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            // Each face: origin corner, u axis, v axis (so u x v points out of the box in Unity's winding).
            void Face(Vector3 o, Vector3 u, Vector3 v, Vector3 n, Rect r)
            {
                int s = verts.Count;
                verts.Add(o); verts.Add(o + v); verts.Add(o + u + v); verts.Add(o + u);
                uvs.Add(new Vector2(r.xMin, r.yMin)); uvs.Add(new Vector2(r.xMin, r.yMax));
                uvs.Add(new Vector2(r.xMax, r.yMax)); uvs.Add(new Vector2(r.xMax, r.yMin));
                for (int i = 0; i < 4; i++) norms.Add(n);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
            }
            const float h = 0.5f;
            Face(new Vector3(-h, h, -h), Vector3.right, Vector3.forward, Vector3.up, top);         // top
            Face(new Vector3(-h, -h, h), Vector3.right, Vector3.back, Vector3.down, top);          // bottom
            Face(new Vector3(-h, -h, -h), Vector3.right, Vector3.up, Vector3.back, side);          // front (-z, camera side)
            Face(new Vector3(h, -h, h), Vector3.left, Vector3.up, Vector3.forward, side);          // back (+z)
            Face(new Vector3(-h, -h, h), Vector3.back, Vector3.up, Vector3.left, end);             // left end (-x)
            Face(new Vector3(h, -h, -h), Vector3.forward, Vector3.up, Vector3.right, end);         // right end (+x)
            m = new Mesh { name = $"Container_L{length}" };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            Meshes[length] = m;
            return m;
        }

        private static Rect PxRect(float x, float y, float w, float h, float texW)
        {
            // Inset by half a texel so filtering never samples a neighbouring region.
            return Rect.MinMaxRect((x + 0.5f) / texW, (y + 0.5f) / H, (x + w - 0.5f) / texW, (y + h - 0.5f) / H);
        }
    }
}
