using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Procedural shipping-container art in the style of the Golden Hour Harbour mockup: a rounded block whose
    /// top shows the colour with fine corrugation stripes, and whose sides read as the darker "lip"
    /// (the Flat shader darkens camera-facing sides). The detail texture is greyscale and is tinted by the
    /// material colour, so every colour shares one texture per container length.
    ///
    /// Texture layout (pixels, v up), for a container of length L cells, width W = 256 * L:
    ///   Top face  v [256, 512)      Sides  v [0, 200)
    /// </summary>
    public static class ContainerArt
    {
        private const int Cell = 256, H = 512, SideH = 200, TopY = 256;
        public const float CornerRadius = 0.12f;      // world units, before yard/ship scaling
        private static readonly Dictionary<int, Texture2D> Textures = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Mesh> Meshes = new Dictionary<int, Mesh>();

        public static Texture2D Texture(int length)
        {
            length = Mathf.Clamp(length, 1, 2);
            if (Textures.TryGetValue(length, out var t) && t) return t;
            int w = Cell * length;
            var px = new float[w * H];
            for (int i = 0; i < px.Length; i++) px[i] = 1f;

            // ---- Top: colour with darker corrugation stripes (mockup: 2px dark in every 9px) ----
            int ribs = 6 * length;
            float period = w / (float)ribs;
            for (int y = TopY; y < H; y++)
                for (int x = 0; x < w; x++)
                {
                    float f = (x % period) / period;               // 0..1 inside a rib
                    float v = 1f;
                    if (f > 0.74f && f < 0.96f) v = 0.86f;          // groove
                    else if (f >= 0.96f || f < 0.04f) v = 0.93f;   // soft edge of the groove
                    else if (f > 0.08f && f < 0.16f) v = 1.04f;     // lit crest
                    px[y * w + x] = v;
                }
            // A thin lighter rim inside the top edge, and a slightly darker band at the far edge, give it volume.
            for (int y = TopY; y < H; y++)
                for (int x = 0; x < w; x++)
                {
                    float ty = (y - TopY) / (float)(H - TopY);
                    if (ty > 0.9f) px[y * w + x] *= 0.94f;          // far edge (away from camera)
                    else if (ty < 0.07f) px[y * w + x] *= 1.05f;    // near edge highlight
                }
            // Mid-span seam for long containers.
            if (length > 1)
                for (int y = TopY; y < H; y++)
                    for (int x = w / 2 - 3; x < w / 2 + 3; x++) px[y * w + x] = 0.78f;

            // ---- Sides: the "lip". Mostly plain, with faint vertical ridges and a darker base rail ----
            for (int y = 0; y < SideH; y++)
                for (int x = 0; x < w; x++)
                {
                    float f = (x % (period * 0.5f)) / (period * 0.5f);
                    float v = f > 0.7f ? 0.93f : 1f;
                    if (y < 26) v *= 0.85f;                          // base rail
                    if (y > SideH - 10) v *= 1.06f;                  // lit top edge of the lip
                    px[y * w + x] = v;
                }

            var cols = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(px[i] * 245f), 0, 255);   // 1.0 maps to ~0.96 so highlights have headroom
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

        /// <summary>
        /// Rounded block in unit space (-0.5..0.5). The container's transform scales it to ContainerScale(length);
        /// the corner radii are pre-divided by that scale so the corners come out circular.
        /// </summary>
        public static Mesh Mesh(int length)
        {
            length = Mathf.Clamp(length, 1, 2);
            if (Meshes.TryGetValue(length, out var m) && m) return m;
            var size = BoardLayout.ContainerScale(length);
            float rx = CornerRadius / size.x, rz = CornerRadius / size.z;
            const int seg = 6;
            const float h = 0.5f;

            // Outline of the top face, counter-clockwise seen from above, with an outward normal per point.
            var outline = new List<Vector2>();
            var normals = new List<Vector2>();
            void Corner(float cx, float cz, float a0)
            {
                for (int i = 0; i <= seg; i++)
                {
                    float a = (a0 + 90f * i / seg) * Mathf.Deg2Rad;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    outline.Add(new Vector2(cx + d.x * rx, cz + d.y * rz));
                    normals.Add(d);
                }
            }
            Corner(h - rx, -h + rz, -90f);   // front-right
            Corner(h - rx, h - rz, 0f);      // back-right
            Corner(-h + rx, h - rz, 90f);    // back-left
            Corner(-h + rx, -h + rz, 180f);  // front-left

            float W = Cell * length;
            Vector2 TopUv(Vector2 p) => new Vector2((p.x + h) * (W - 1f) / W + 0.5f / W, (TopY + 0.5f + (p.y + h) * (H - TopY - 1f)) / H);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var norms = new List<Vector3>();
            var tris = new List<int>();

            // Top cap: fan around the centre.
            int c0 = verts.Count;
            verts.Add(new Vector3(0, h, 0)); uvs.Add(TopUv(Vector2.zero)); norms.Add(Vector3.up);
            foreach (var p in outline) { verts.Add(new Vector3(p.x, h, p.y)); uvs.Add(TopUv(p)); norms.Add(Vector3.up); }
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                int a = c0 + 1 + i, b = c0 + 1 + (i + 1) % n;
                tris.Add(c0); tris.Add(b); tris.Add(a);
            }

            // Walls: a ring of quads, smooth normals round the corners. U runs along the perimeter.
            float perim = 0;
            var cum = new float[n + 1];
            for (int i = 0; i < n; i++) { cum[i] = perim; perim += Vector2.Distance(outline[i], outline[(i + 1) % n]); }
            cum[n] = perim;
            int w0 = verts.Count;
            for (int i = 0; i <= n; i++)
            {
                var p = outline[i % n];
                var nn = normals[i % n];
                float u = cum[i] / perim * (W - 1f) / W + 0.5f / W;
                var normal = new Vector3(nn.x, 0, nn.y);
                verts.Add(new Vector3(p.x, -h, p.y)); uvs.Add(new Vector2(u, 0.5f / H)); norms.Add(normal);
                verts.Add(new Vector3(p.x, h, p.y)); uvs.Add(new Vector2(u, (SideH - 0.5f) / H)); norms.Add(normal);
            }
            for (int i = 0; i < n; i++)
            {
                int b0 = w0 + i * 2, b1 = w0 + (i + 1) * 2;
                // bottom-i, top-i, top-(i+1), bottom-(i+1); outward-facing with Unity's clockwise winding.
                tris.Add(b0); tris.Add(b0 + 1); tris.Add(b1 + 1);
                tris.Add(b0); tris.Add(b1 + 1); tris.Add(b1);
            }

            m = new Mesh { name = $"Container_L{length}" };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            Meshes[length] = m;
            return m;
        }
    }
}
