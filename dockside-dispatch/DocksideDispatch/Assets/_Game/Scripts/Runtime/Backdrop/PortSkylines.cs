using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Famous port skylines for the backdrop. The port changes every <see cref="LevelsPerPort"/> levels and cycles.
    /// Each skyline is a simplified, stylised silhouette of the city's best-known shapes (not an accurate model),
    /// drawn into flat meshes: a pale haze layer (hills), the skyline itself, and window/landmark lights that are
    /// only shown at sunset and night. Coordinates are world units relative to the horizon (y = 0), x centred on screen.
    /// </summary>
    public static class PortSkylines
    {
        public const int LevelsPerPort = 5;

        public static readonly string[] Names =
            { "Hong Kong", "Singapore", "Dubai", "Mumbai", "Amsterdam", "Los Angeles", "Sydney", "Rotterdam" };

        public static int IndexForLevel(int level) => ((Mathf.Max(1, level) - 1) / LevelsPerPort) % Names.Length;
        public static string NameForLevel(int level) => Names[IndexForLevel(level)];

        public class Result
        {
            public FlatMesh haze = new FlatMesh(), skyline = new FlatMesh(), lights = new FlatMesh();
            public readonly List<Vector2> windmillHubs = new List<Vector2>();   // animated sails (Amsterdam)
        }

        public static Result Build(int index, float halfW, bool lightsOn, int seed)
        {
            var b = new Builder(halfW, lightsOn, seed);
            b.r.skyline.Rect(-halfW - 2f, -0.8f, halfW + 2f, 0.14f);   // the shoreline
            switch (index % Names.Length)
            {
                case 0: b.HongKong(); break;
                case 1: b.Singapore(); break;
                case 2: b.Dubai(); break;
                case 3: b.Mumbai(); break;
                case 4: b.Amsterdam(); break;
                case 5: b.LosAngeles(); break;
                case 6: b.Sydney(); break;
                default: b.Rotterdam(); break;
            }
            // Every port keeps a working dock: a gantry crane at the right edge and low container stacks.
            b.Stacks(-halfW - 0.5f, -4.3f, 2);
            b.GantryCrane(4.75f, 0.78f, -1);
            return b.r;
        }

        private class Builder
        {
            public readonly Result r = new Result();
            private readonly float _halfW;
            private readonly bool _lights;
            private readonly System.Random _rng;

            public Builder(float halfW, bool lights, int seed) { _halfW = halfW; _lights = lights; _rng = new System.Random(seed); }

            private float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
            private FlatMesh S => r.skyline;

            // ---------------------------------------------------------------- primitives
            /// <summary>A plain tower with optional lit windows.</summary>
            private void Tower(float cx, float w, float h, float y0 = 0.1f, bool windows = true)
            {
                S.Rect(cx - w / 2, y0, cx + w / 2, h);
                if (windows) Windows(cx - w / 2, y0 + 0.08f, cx + w / 2, h - 0.06f);
            }

            private void Windows(float x0, float y0, float x1, float y1, float density = 0.32f)
            {
                if (!_lights) return;
                for (float y = y0; y < y1 - 0.05f; y += 0.095f)
                    for (float x = x0 + 0.035f; x < x1 - 0.045f; x += 0.075f)
                        if (_rng.NextDouble() < density) r.lights.Rect(x, y, x + 0.035f, y + 0.045f);
            }

            private void Light(float x, float y, float radius = 0.03f) { if (_lights) r.lights.Circle(new Vector2(x, y), radius, 10); }

            private void Spire(float x, float y0, float y1, float w = 0.025f) { S.Rect(x - w / 2, y0, x + w / 2, y1); Light(x, y1 + 0.02f, 0.025f); }

            /// <summary>Filler towers between landmarks.</summary>
            public void Filler(float x0, float x1, float hMin, float hMax, float wMin = 0.18f, float wMax = 0.34f)
            {
                for (float x = x0; x < x1;)
                {
                    float w = R(wMin, wMax), h = R(hMin, hMax);
                    if (x + w > x1) break;
                    Tower(x + w / 2, w, h);
                    x += w + R(0.0f, 0.05f);
                }
            }

            /// <summary>Hills/ridge in the haze layer, from a list of (x, height) points.</summary>
            private void Ridge(params Vector2[] pts)
            {
                for (int i = 0; i < pts.Length - 1; i++)
                    r.haze.Poly(new Vector2(pts[i].x, 0f), new Vector2(pts[i + 1].x, 0f), pts[i + 1], pts[i]);
            }

            private void Dome(float cx, float y, float radius)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i <= 12; i++) { float a = Mathf.PI * i / 12f; pts.Add(new Vector2(cx + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius)); }
                S.Poly(pts.ToArray());
            }

            public void Stacks(float x0, float x1, int maxTiers)
            {
                for (float x = x0; x < x1; x += 0.36f)
                    S.Rect(x, 0.1f, x + 0.33f, 0.14f + (1 + _rng.Next(maxTiers)) * 0.12f);
            }

            /// <summary>Ship-to-shore gantry crane (dir 1 = boom to the left over the water, -1 = to the right).</summary>
            public void GantryCrane(float x0, float s, int dir)
            {
                float L(float dx) => x0 + dx * s * dir;
                void Bar(float a, float b, float y0, float y1) => S.Rect(Mathf.Min(L(a), L(b)), y0 * s, Mathf.Max(L(a), L(b)), y1 * s);
                Bar(0, 0.06f, 0.12f, 1.3f); Bar(0.55f, 0.61f, 0.12f, 1.3f);
                S.Line(new Vector2(L(0.03f), 0.45f * s), new Vector2(L(0.58f), 0.95f * s), 0.03f);
                S.Line(new Vector2(L(0.58f), 0.45f * s), new Vector2(L(0.03f), 0.95f * s), 0.03f);
                Bar(-0.05f, 0.66f, 1.22f, 1.32f); Bar(0.18f, 0.7f, 1.32f, 1.52f); Bar(-1.15f, 1.0f, 1.38f, 1.45f);
                S.Line(new Vector2(L(0.08f), 1.45f * s), new Vector2(L(0.28f), 2.05f * s), 0.035f);
                S.Line(new Vector2(L(0.5f), 1.45f * s), new Vector2(L(0.3f), 2.05f * s), 0.035f);
                S.Line(new Vector2(L(0.29f), 2.05f * s), new Vector2(L(-1.1f), 1.45f * s), 0.022f);
                S.Line(new Vector2(L(0.29f), 2.05f * s), new Vector2(L(0.95f), 1.45f * s), 0.022f);
                Light(L(0.29f), 2.05f * s + 0.035f);
            }

            /// <summary>Cable fan from a pylon top down to a deck (cable-stayed bridges).</summary>
            private void CableFan(Vector2 top, float deckY, float x0, float x1, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n;
                    S.Line(top, new Vector2(Mathf.Lerp(x0, x1, t), deckY), 0.012f);
                }
            }

            // ---------------------------------------------------------------- ports
            public void HongKong()
            {
                // Victoria Peak and the hills behind the harbour.
                Ridge(new Vector2(-6, 0.7f), new Vector2(-4, 1.25f), new Vector2(-2.2f, 1.85f), new Vector2(-0.8f, 1.6f),
                      new Vector2(0.8f, 2.0f), new Vector2(2.6f, 1.55f), new Vector2(4.2f, 1.2f), new Vector2(6, 0.8f));
                Filler(-4.3f, 4.0f, 0.55f, 1.35f, 0.16f, 0.3f);           // the dense waterfront
                // International Commerce Centre: tall, with a stepped top.
                Tower(-3.1f, 0.36f, 2.55f); S.Rect(-3.24f, 2.55f, -2.96f, 2.68f); S.Rect(-3.16f, 2.68f, -3.04f, 2.76f);
                // Bank of China Tower: faceted, ending in a sharp diagonal point with twin masts.
                S.Poly(new Vector2(-0.52f, 0.1f), new Vector2(-0.12f, 0.1f), new Vector2(-0.12f, 2.05f), new Vector2(-0.52f, 1.65f));
                Windows(-0.5f, 0.2f, -0.14f, 1.6f); Spire(-0.18f, 2.0f, 2.35f, 0.02f); Spire(-0.26f, 1.95f, 2.25f, 0.02f);
                // Two IFC: slim tower with a crown of fins.
                Tower(1.15f, 0.32f, 2.3f);
                S.Rect(1.03f, 2.3f, 1.27f, 2.42f); S.Rect(1.07f, 2.42f, 1.23f, 2.52f);
                for (int i = 0; i < 4; i++) S.Rect(1.02f + i * 0.08f, 2.52f, 1.04f + i * 0.08f, 2.62f);
                // Central Plaza: pyramid top and mast.
                Tower(0.42f, 0.26f, 1.75f); S.Poly(new Vector2(0.29f, 1.75f), new Vector2(0.55f, 1.75f), new Vector2(0.42f, 1.95f)); Spire(0.42f, 1.9f, 2.15f);
            }

            public void Singapore()
            {
                Filler(-0.4f, 2.3f, 0.9f, 2.1f, 0.2f, 0.32f);            // Downtown Core
                // Marina Bay Sands: three towers joined by the SkyPark, which cantilevers past the north tower.
                foreach (float x in new[] { -2.2f, -1.72f, -1.24f })
                {
                    S.Poly(new Vector2(x - 0.2f, 0.1f), new Vector2(x + 0.2f, 0.1f), new Vector2(x + 0.15f, 1.5f), new Vector2(x - 0.15f, 1.5f));
                    Windows(x - 0.14f, 0.2f, x + 0.14f, 1.45f, 0.4f);
                }
                S.Poly(new Vector2(-2.45f, 1.5f), new Vector2(-0.75f, 1.5f), new Vector2(-0.68f, 1.6f), new Vector2(-2.45f, 1.62f));
                // ArtScience Museum: the open "lotus".
                for (int i = 0; i < 5; i++)
                {
                    float x = -3.75f + i * 0.13f, h = 0.35f + Mathf.Sin(i / 4f * Mathf.PI) * 0.3f;
                    S.Poly(new Vector2(x, 0.12f), new Vector2(x + 0.12f, 0.12f), new Vector2(x + 0.06f + (i - 2) * 0.05f, 0.12f + h));
                }
                // Singapore Flyer.
                var hub = new Vector2(3.25f, 1.05f);
                for (int i = 0; i < 28; i++)
                {
                    float a0 = i * Mathf.PI * 2f / 28f, a1 = (i + 1) * Mathf.PI * 2f / 28f;
                    S.Line(hub + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 0.8f, hub + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 0.8f, 0.035f);
                    if (i % 4 == 0) { S.Line(hub, hub + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 0.8f, 0.012f); Light(hub.x + Mathf.Cos(a0) * 0.8f, hub.y + Mathf.Sin(a0) * 0.8f, 0.035f); }
                }
                S.Line(hub, new Vector2(2.9f, 0.12f), 0.05f); S.Line(hub, new Vector2(3.6f, 0.12f), 0.05f);
                // Supertrees.
                foreach (var (x, h) in new[] { (-4.4f, 0.75f), (-4.05f, 0.95f), (-3.0f, 0.6f) })
                {
                    S.Poly(new Vector2(x - 0.04f, 0.1f), new Vector2(x + 0.04f, 0.1f), new Vector2(x + 0.02f, h), new Vector2(x - 0.02f, h));
                    S.Poly(new Vector2(x - 0.02f, h - 0.02f), new Vector2(x + 0.02f, h - 0.02f), new Vector2(x + 0.17f, h + 0.1f), new Vector2(x - 0.17f, h + 0.1f));
                }
            }

            public void Dubai()
            {
                Filler(-4.5f, -2.9f, 0.6f, 1.4f); Filler(0.6f, 2.5f, 0.6f, 1.5f);
                // Burj Khalifa: stepped needle with a long spire.
                float cx = -0.6f;
                var tiers = new[] { (0.56f, 0.7f), (0.44f, 1.3f), (0.34f, 1.85f), (0.24f, 2.3f), (0.15f, 2.65f), (0.08f, 2.9f) };
                float y = 0.1f;
                foreach (var (w, top) in tiers) { S.Rect(cx - w / 2, y, cx + w / 2, top); Windows(cx - w / 2, y + 0.05f, cx + w / 2, top - 0.05f, 0.25f); y = top; }
                Spire(cx, y, 3.35f, 0.03f);
                // Emirates Towers: two slim towers with slanted triangular tops.
                foreach (var (x, h) in new[] { (-2.6f, 1.7f), (-2.18f, 1.5f) })
                {
                    Tower(x, 0.24f, h); S.Poly(new Vector2(x - 0.12f, h), new Vector2(x + 0.12f, h), new Vector2(x + 0.12f, h + 0.28f));
                }
                // Burj Al Arab: the sail, on its own island.
                S.Rect(2.75f, 0.1f, 3.85f, 0.18f);
                S.Poly(new Vector2(3.0f, 0.18f), new Vector2(3.62f, 0.18f), new Vector2(3.6f, 1.55f), new Vector2(3.08f, 1.85f));
                S.Line(new Vector2(3.6f, 0.18f), new Vector2(3.66f, 1.95f), 0.04f);
                S.Ellipse(new Vector2(3.72f, 1.2f), 0.13f, 0.03f, 16);   // helipad
                Windows(3.05f, 0.3f, 3.5f, 1.4f, 0.25f);
            }

            public void Mumbai()
            {
                Filler(-4.6f, -2.3f, 0.8f, 1.8f); Filler(1.25f, 2.35f, 0.7f, 1.5f);
                // Gateway of India: a great arch with four turrets.
                float gx = -1.4f;
                S.Rect(gx - 0.55f, 0.1f, gx - 0.3f, 0.75f); S.Rect(gx + 0.3f, 0.1f, gx + 0.55f, 0.75f);
                for (int i = 0; i < 8; i++)
                {
                    float a0 = Mathf.PI * i / 8f, a1 = Mathf.PI * (i + 1) / 8f;
                    S.Line(new Vector2(gx + Mathf.Cos(a0) * 0.3f, 0.45f + Mathf.Sin(a0) * 0.3f), new Vector2(gx + Mathf.Cos(a1) * 0.3f, 0.45f + Mathf.Sin(a1) * 0.3f), 0.08f);
                }
                S.Rect(gx - 0.6f, 0.75f, gx + 0.6f, 0.86f);
                foreach (float dx in new[] { -0.52f, -0.2f, 0.2f, 0.52f }) { S.Rect(gx + dx - 0.05f, 0.86f, gx + dx + 0.05f, 1.05f); Dome(gx + dx, 1.05f, 0.06f); }
                // Taj Mahal Palace hotel: long facade with a central dome and corner turrets.
                S.Rect(-0.5f, 0.1f, 1.0f, 0.95f); Windows(-0.48f, 0.2f, 0.98f, 0.9f, 0.45f);
                Dome(0.25f, 0.95f, 0.28f); Spire(0.25f, 1.2f, 1.36f, 0.02f);
                foreach (float x in new[] { -0.42f, 0.92f }) { S.Rect(x - 0.07f, 0.95f, x + 0.07f, 1.08f); Dome(x, 1.08f, 0.08f); }
                // Bandra-Worli Sea Link: cable-stayed span.
                S.Rect(2.2f, 0.36f, _halfW + 1f, 0.42f);
                var top = new Vector2(3.6f, 1.75f);
                S.Line(new Vector2(3.45f, 0.36f), top, 0.06f); S.Line(new Vector2(3.75f, 0.36f), top, 0.06f);
                CableFan(top, 0.42f, 2.4f, 3.45f, 6); CableFan(top, 0.42f, 3.75f, 4.8f, 6);
                Light(top.x, top.y + 0.04f);
            }

            public void Amsterdam()
            {
                // Canal houses: narrow, tall, with step, bell and neck gables.
                for (float x = -4.6f; x < 2.5f;)
                {
                    float w = R(0.22f, 0.32f), h = R(0.55f, 0.95f);
                    S.Rect(x, 0.1f, x + w, h); Windows(x, 0.18f, x + w, h - 0.04f, 0.5f);
                    switch (_rng.Next(3))
                    {
                        case 0: S.Rect(x + w * 0.15f, h, x + w * 0.85f, h + 0.08f); S.Rect(x + w * 0.3f, h + 0.08f, x + w * 0.7f, h + 0.16f); break;      // step gable
                        case 1: S.Poly(new Vector2(x, h), new Vector2(x + w, h), new Vector2(x + w * 0.75f, h + 0.12f), new Vector2(x + w / 2, h + 0.2f), new Vector2(x + w * 0.25f, h + 0.12f)); break; // bell gable
                        default: S.Rect(x + w * 0.25f, h, x + w * 0.75f, h + 0.14f); S.Poly(new Vector2(x + w * 0.2f, h + 0.14f), new Vector2(x + w * 0.8f, h + 0.14f), new Vector2(x + w / 2, h + 0.22f)); break; // neck gable
                    }
                    x += w + 0.015f;
                }
                // Westerkerk tower.
                float cx = -1.7f;
                S.Rect(cx - 0.14f, 0.1f, cx + 0.14f, 1.25f); S.Rect(cx - 0.1f, 1.25f, cx + 0.1f, 1.55f); S.Rect(cx - 0.07f, 1.55f, cx + 0.07f, 1.75f);
                S.Poly(new Vector2(cx - 0.06f, 1.75f), new Vector2(cx + 0.06f, 1.75f), new Vector2(cx, 2.1f)); Light(cx, 1.4f, 0.04f);
                // Windmill (its sails are animated separately).
                float mx = 3.6f;
                S.Poly(new Vector2(mx - 0.24f, 0.1f), new Vector2(mx + 0.24f, 0.1f), new Vector2(mx + 0.12f, 1.0f), new Vector2(mx - 0.12f, 1.0f));
                S.Poly(new Vector2(mx - 0.15f, 1.0f), new Vector2(mx + 0.15f, 1.0f), new Vector2(mx, 1.18f));
                r.windmillHubs.Add(new Vector2(mx, 1.02f));
                Light(mx, 0.45f, 0.04f);
            }

            public void LosAngeles()
            {
                Ridge(new Vector2(-6, 0.55f), new Vector2(-3.5f, 0.95f), new Vector2(-1.2f, 0.75f), new Vector2(1.5f, 1.05f), new Vector2(4.2f, 0.7f), new Vector2(6, 0.5f));
                // Downtown: US Bank Tower (stepped crown) and Wilshire Grand (sail top and spire).
                Filler(-4.4f, -1.9f, 0.9f, 1.7f);
                Tower(-3.3f, 0.34f, 2.15f); S.Rect(-3.42f, 2.15f, -3.18f, 2.27f); S.Rect(-3.36f, 2.27f, -3.24f, 2.35f);
                Tower(-2.5f, 0.3f, 2.0f); S.Poly(new Vector2(-2.65f, 2.0f), new Vector2(-2.35f, 2.0f), new Vector2(-2.48f, 2.3f)); Spire(-2.48f, 2.25f, 2.5f);
                // Vincent Thomas Bridge: suspension bridge over the port.
                float deck = 0.72f, t1 = 0.4f, t2 = 2.6f, th = 1.55f;
                S.Rect(-1.4f, deck, 4.2f, deck + 0.06f);
                foreach (float tx in new[] { t1, t2 })
                {
                    S.Rect(tx - 0.1f, 0.1f, tx - 0.05f, th); S.Rect(tx + 0.05f, 0.1f, tx + 0.1f, th);
                    S.Rect(tx - 0.1f, th - 0.08f, tx + 0.1f, th - 0.03f); S.Rect(tx - 0.1f, deck + 0.2f, tx + 0.1f, deck + 0.25f);
                    Light(tx, th + 0.03f);
                }
                Vector2 Cable(float x)
                {
                    if (x < t1) return new Vector2(x, Mathf.Lerp(deck + 0.06f, th, (x + 1.4f) / (t1 + 1.4f)));
                    if (x > t2) return new Vector2(x, Mathf.Lerp(th, deck + 0.06f, (x - t2) / (4.2f - t2)));
                    float u = (x - t1) / (t2 - t1); return new Vector2(x, deck + 0.12f + (th - deck - 0.12f) * Mathf.Pow(2f * u - 1f, 2f));
                }
                for (float x = -1.4f; x < 4.2f - 0.05f; x += 0.1f)
                {
                    S.Line(Cable(x), Cable(x + 0.1f), 0.022f);
                    if (x > t1 + 0.05f && x < t2 - 0.05f) S.Line(Cable(x), new Vector2(x, deck + 0.06f), 0.008f);
                }
                // Palm trees.
                foreach (var (x, h) in new[] { (-1.2f, 0.9f), (-0.9f, 1.05f), (3.6f, 0.85f) })
                {
                    S.Line(new Vector2(x, 0.1f), new Vector2(x + 0.05f, h), 0.035f);
                    for (int i = 0; i < 6; i++) { float a = Mathf.PI * (0.15f + i * 0.14f); S.Line(new Vector2(x + 0.05f, h), new Vector2(x + 0.05f + Mathf.Cos(a) * 0.22f, h + Mathf.Sin(a) * 0.1f - 0.05f), 0.03f); }
                }
                GantryCrane(-4.3f, 0.72f, 1);
            }

            public void Sydney()
            {
                Filler(-4.6f, -2.8f, 0.9f, 1.8f); Filler(4.0f, _halfW + 0.5f, 0.8f, 1.6f);
                // Sydney Tower: thin shaft with the turret.
                Spire(-3.5f, 0.1f, 2.0f, 0.07f); S.Ellipse(new Vector2(-3.5f, 1.85f), 0.16f, 0.12f, 20); Spire(-3.5f, 1.95f, 2.4f, 0.02f);
                // Opera House: podium and overlapping shells.
                S.Rect(-2.5f, 0.1f, -0.6f, 0.3f);
                foreach (var (x, w, h, lean) in new[] { (-2.35f, 0.35f, 0.62f, 0.12f), (-2.0f, 0.42f, 0.85f, 0.15f), (-1.6f, 0.4f, 0.75f, 0.14f), (-1.25f, 0.34f, 0.6f, 0.12f), (-0.95f, 0.28f, 0.45f, 0.1f) })
                    S.Poly(new Vector2(x, 0.3f), new Vector2(x + w, 0.3f), new Vector2(x + w * 0.35f + lean, 0.3f + h));
                // Harbour Bridge: steel arch with granite pylons.
                float a0 = 0.6f, a1 = 3.8f, deck = 0.62f, apex = 1.45f;
                S.Rect(0.2f, deck, 4.3f, deck + 0.07f);
                Vector2 Arch(float x, float lift) { float u = (x - a0) / (a1 - a0); return new Vector2(x, 0.25f + lift + (apex - 0.25f - lift) * (1f - Mathf.Pow(2f * u - 1f, 2f))); }
                for (float x = a0; x < a1 - 0.05f; x += 0.1f)
                {
                    S.Line(Arch(x, 0f), Arch(x + 0.1f, 0f), 0.07f);
                    S.Line(Arch(x, -0.18f), Arch(x + 0.1f, -0.18f), 0.03f);
                    if (Arch(x, 0f).y > deck + 0.1f) S.Line(Arch(x, 0f), new Vector2(x, deck + 0.07f), 0.012f);
                }
                foreach (float px in new[] { 0.45f, 3.95f }) { S.Rect(px - 0.17f, 0.1f, px + 0.17f, 1.05f); S.Rect(px - 0.13f, 1.05f, px + 0.13f, 1.12f); Light(px, 1.15f); }
            }

            public void Rotterdam()
            {
                Filler(-4.6f, -3.8f, 0.7f, 1.4f); Filler(-2.9f, -0.9f, 0.8f, 1.5f);
                // Euromast: shaft, crow's nest and the Space Tower spire.
                Spire(-3.35f, 0.1f, 1.25f, 0.12f); S.Ellipse(new Vector2(-3.35f, 1.15f), 0.2f, 0.07f, 20); Spire(-3.35f, 1.25f, 2.55f, 0.05f);
                // De Rotterdam: three stacked, offset towers.
                foreach (var (x, h) in new[] { (-0.55f, 1.85f), (-0.2f, 2.05f), (0.12f, 1.75f) })
                {
                    S.Rect(x - 0.15f, 0.1f, x + 0.15f, h * 0.55f); S.Rect(x - 0.1f, h * 0.55f, x + 0.2f, h); Windows(x - 0.13f, 0.2f, x + 0.18f, h - 0.05f, 0.35f);
                }
                // Cube houses: tilted cubes on stilts.
                for (int i = 0; i < 4; i++)
                {
                    float cx = -2.4f + i * 0.28f;
                    S.Rect(cx - 0.02f, 0.1f, cx + 0.02f, 0.35f);
                    S.Poly(new Vector2(cx, 0.32f), new Vector2(cx + 0.16f, 0.48f), new Vector2(cx, 0.64f), new Vector2(cx - 0.16f, 0.48f));
                }
                // Erasmus Bridge: the "Swan", an asymmetric bent pylon with a cable fan.
                S.Rect(0.6f, 0.45f, 4.6f, 0.51f);
                var knee = new Vector2(2.25f, 1.4f); var top = new Vector2(2.08f, 2.05f);
                S.Line(new Vector2(2.0f, 0.45f), knee, 0.09f); S.Line(knee, top, 0.07f);
                CableFan(top, 0.51f, 0.8f, 2.0f, 7); CableFan(top, 0.51f, 2.35f, 3.4f, 4);
                Light(top.x, top.y + 0.04f);
            }
        }
    }
}
