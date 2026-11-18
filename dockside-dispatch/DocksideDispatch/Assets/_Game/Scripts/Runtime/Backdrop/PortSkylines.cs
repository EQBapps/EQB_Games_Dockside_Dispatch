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

        /// <summary>20 ports x 5 levels: every port in the 100-level campaign is different.</summary>
        public static readonly string[] Names =
        {
            "Hong Kong", "Singapore", "Dubai", "Mumbai", "Amsterdam", "Los Angeles", "Sydney", "Rotterdam",
            "Shanghai", "New York", "London", "Hamburg", "Tokyo", "San Francisco", "Rio de Janeiro", "Cape Town",
            "Istanbul", "Venice", "Barcelona", "Seattle",
        };

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
                case 7: b.Rotterdam(); break;
                case 8: b.Shanghai(); break;
                case 9: b.NewYork(); break;
                case 10: b.London(); break;
                case 11: b.Hamburg(); break;
                case 12: b.Tokyo(); break;
                case 13: b.SanFrancisco(); break;
                case 14: b.RioDeJaneiro(); break;
                case 15: b.CapeTown(); break;
                case 16: b.Istanbul(); break;
                case 17: b.Venice(); break;
                case 18: b.Barcelona(); break;
                default: b.Seattle(); break;
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

            // ---------------------------------------------------------------- shared landmark helpers
            private void HazePoly(params Vector2[] pts) => r.haze.Poly(pts);

            /// <summary>Observation wheel: rim, spokes, lit cabins and an A-frame support.</summary>
            private void Wheel(Vector2 hub, float radius, int segments = 26)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    var p0 = hub + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                    S.Line(p0, hub + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, 0.035f);
                    if (i % 3 == 0) { S.Line(hub, p0, 0.012f); Light(p0.x, p0.y, 0.03f); }
                }
                S.Line(hub, new Vector2(hub.x - radius * 0.45f, 0.12f), 0.045f);
                S.Line(hub, new Vector2(hub.x + radius * 0.45f, 0.12f), 0.045f);
            }

            /// <summary>Suspension bridge: two towers, a deck and main cables sagging between them.</summary>
            private void SuspensionBridge(float x0, float x1, float t1, float t2, float deck, float towerH, bool artDeco = false)
            {
                S.Rect(x0, deck, x1, deck + 0.06f);
                foreach (float tx in new[] { t1, t2 })
                {
                    S.Rect(tx - 0.1f, 0.1f, tx - 0.05f, towerH); S.Rect(tx + 0.05f, 0.1f, tx + 0.1f, towerH);
                    int beams = artDeco ? 4 : 2;
                    for (int b = 0; b < beams; b++)
                    {
                        float y = Mathf.Lerp(deck + 0.15f, towerH - 0.05f, b / (float)Mathf.Max(1, beams - 1));
                        S.Rect(tx - 0.1f, y - 0.03f, tx + 0.1f, y + 0.02f);
                    }
                    Light(tx, towerH + 0.03f);
                }
                Vector2 Cable(float x)
                {
                    if (x < t1) return new Vector2(x, Mathf.Lerp(deck + 0.06f, towerH, (x - x0) / Mathf.Max(0.01f, t1 - x0)));
                    if (x > t2) return new Vector2(x, Mathf.Lerp(towerH, deck + 0.06f, (x - t2) / Mathf.Max(0.01f, x1 - t2)));
                    float u = (x - t1) / (t2 - t1);
                    return new Vector2(x, deck + 0.12f + (towerH - deck - 0.12f) * Mathf.Pow(2f * u - 1f, 2f));
                }
                for (float x = x0; x < x1 - 0.05f; x += 0.1f)
                {
                    S.Line(Cable(x), Cable(x + 0.1f), 0.022f);
                    if (x > t1 + 0.05f && x < t2 - 0.05f) S.Line(Cable(x), new Vector2(x, deck + 0.06f), 0.008f);
                }
            }

            private void Minaret(float x, float h)
            {
                S.Rect(x - 0.03f, 0.1f, x + 0.03f, h);
                S.Rect(x - 0.05f, h * 0.7f, x + 0.05f, h * 0.7f + 0.03f);
                S.Poly(new Vector2(x - 0.035f, h), new Vector2(x + 0.035f, h), new Vector2(x, h + 0.2f));
            }

            // ---------------------------------------------------------------- ports 9-20
            public void Shanghai()
            {
                Filler(-4.6f, -2.6f, 0.8f, 1.6f); Filler(2.5f, 4.2f, 0.8f, 1.5f);
                // Oriental Pearl Tower: three legs, two pink "pearls" and a spire.
                float px = -1.6f;
                S.Line(new Vector2(px - 0.3f, 0.1f), new Vector2(px, 0.75f), 0.06f); S.Line(new Vector2(px + 0.3f, 0.1f), new Vector2(px, 0.75f), 0.06f);
                S.Rect(px - 0.04f, 0.1f, px + 0.04f, 2.2f);
                S.Circle(new Vector2(px, 0.78f), 0.24f, 28); S.Circle(new Vector2(px, 1.72f), 0.16f, 24); S.Circle(new Vector2(px, 2.16f), 0.06f, 14);
                Spire(px, 2.2f, 2.65f, 0.025f); Light(px, 0.78f, 0.06f); Light(px, 1.72f, 0.05f);
                // Jin Mao: a stepped, pagoda-like tower.
                float jx = 0.15f, y = 0.1f;
                foreach (var (w, top) in new[] { (0.36f, 1.0f), (0.3f, 1.45f), (0.24f, 1.75f), (0.18f, 1.95f), (0.11f, 2.08f) })
                { S.Rect(jx - w / 2, y, jx + w / 2, top); Windows(jx - w / 2, y, jx + w / 2, top - 0.05f, 0.3f); y = top; }
                Spire(jx, y, 2.4f);
                // Shanghai Tower: tall, tapering, with a twisting top.
                S.Poly(new Vector2(0.62f, 0.1f), new Vector2(1.18f, 0.1f), new Vector2(1.06f, 2.6f), new Vector2(0.84f, 2.78f));
                Windows(0.66f, 0.2f, 1.1f, 2.4f, 0.28f);
                // Shanghai World Financial Center: the "bottle opener" with its open top.
                S.Poly(new Vector2(1.55f, 0.1f), new Vector2(2.05f, 0.1f), new Vector2(1.98f, 2.0f), new Vector2(1.62f, 2.0f));
                S.Rect(1.62f, 2.0f, 1.7f, 2.4f); S.Rect(1.9f, 2.0f, 1.98f, 2.4f); S.Rect(1.62f, 2.38f, 1.98f, 2.46f);
                Windows(1.6f, 0.2f, 1.98f, 1.9f, 0.28f);
            }

            public void NewYork()
            {
                Filler(-4.6f, -2.75f, 0.9f, 1.8f); Filler(0.85f, 2.4f, 0.9f, 1.7f);
                // One World Trade Center: tapering obelisk and a tall spire.
                S.Poly(new Vector2(-2.2f, 0.1f), new Vector2(-1.66f, 0.1f), new Vector2(-1.76f, 2.35f), new Vector2(-2.1f, 2.35f));
                Windows(-2.15f, 0.2f, -1.72f, 2.2f, 0.3f); Spire(-1.93f, 2.35f, 3.0f, 0.03f);
                // Chrysler Building: terraced crown of arches.
                Tower(-0.6f, 0.3f, 1.6f);
                S.Poly(new Vector2(-0.75f, 1.6f), new Vector2(-0.45f, 1.6f), new Vector2(-0.52f, 1.9f), new Vector2(-0.6f, 2.15f), new Vector2(-0.68f, 1.9f));
                Spire(-0.6f, 2.1f, 2.4f, 0.02f);
                // Empire State Building: setbacks and the mast.
                float ex = 0.35f, y = 0.1f;
                foreach (var (w, top) in new[] { (0.5f, 1.35f), (0.36f, 1.72f), (0.25f, 1.98f), (0.15f, 2.12f) })
                { S.Rect(ex - w / 2, y, ex + w / 2, top); Windows(ex - w / 2, y + 0.05f, ex + w / 2, top - 0.05f, 0.3f); y = top; }
                Spire(ex, y, 2.6f, 0.035f);
                // Statue of Liberty on her island.
                S.Rect(2.95f, 0.1f, 4.05f, 0.2f);
                S.Poly(new Vector2(3.32f, 0.2f), new Vector2(3.68f, 0.2f), new Vector2(3.62f, 0.62f), new Vector2(3.38f, 0.62f));
                S.Poly(new Vector2(3.41f, 0.62f), new Vector2(3.59f, 0.62f), new Vector2(3.55f, 1.08f), new Vector2(3.45f, 1.08f));
                S.Circle(new Vector2(3.5f, 1.13f), 0.05f, 12);
                for (int i = 0; i < 5; i++) { float a = Mathf.PI * (0.15f + i * 0.175f); S.Line(new Vector2(3.5f, 1.15f), new Vector2(3.5f + Mathf.Cos(a) * 0.1f, 1.15f + Mathf.Sin(a) * 0.1f), 0.015f); }
                S.Line(new Vector2(3.55f, 1.02f), new Vector2(3.64f, 1.38f), 0.04f);
                S.Poly(new Vector2(3.6f, 1.38f), new Vector2(3.69f, 1.38f), new Vector2(3.665f, 1.47f)); Light(3.645f, 1.46f, 0.035f);
                S.Rect(3.4f, 0.75f, 3.47f, 0.92f);   // the tablet
            }

            public void London()
            {
                Filler(-4.6f, -3.0f, 0.6f, 1.2f); Filler(2.25f, 2.6f, 0.7f, 1.3f);
                // Elizabeth Tower (Big Ben).
                float bx = -2.55f;
                S.Rect(bx - 0.12f, 0.1f, bx + 0.12f, 1.4f); S.Rect(bx - 0.16f, 1.4f, bx + 0.16f, 1.64f); S.Rect(bx - 0.12f, 1.64f, bx + 0.12f, 1.8f);
                S.Poly(new Vector2(bx - 0.12f, 1.8f), new Vector2(bx + 0.12f, 1.8f), new Vector2(bx, 2.15f));
                Light(bx, 1.52f, 0.08f); Spire(bx, 2.1f, 2.25f, 0.015f);
                // Tower Bridge: two Gothic towers, high walkways and the road deck.
                foreach (float tx in new[] { -1.1f, 0.0f })
                {
                    S.Rect(tx - 0.13f, 0.1f, tx + 0.13f, 1.2f);
                    foreach (float dx in new[] { -0.11f, 0.11f }) S.Poly(new Vector2(tx + dx - 0.04f, 1.2f), new Vector2(tx + dx + 0.04f, 1.2f), new Vector2(tx + dx, 1.42f));
                    S.Poly(new Vector2(tx - 0.08f, 1.2f), new Vector2(tx + 0.08f, 1.2f), new Vector2(tx, 1.5f));
                    Windows(tx - 0.11f, 0.5f, tx + 0.11f, 1.1f, 0.5f);
                }
                S.Rect(-0.97f, 1.0f, -0.13f, 1.07f); S.Rect(-0.97f, 0.88f, -0.13f, 0.93f);
                S.Rect(-1.6f, 0.42f, 0.5f, 0.5f);
                S.Line(new Vector2(-1.6f, 0.5f), new Vector2(-1.23f, 0.98f), 0.02f); S.Line(new Vector2(0.5f, 0.5f), new Vector2(0.13f, 0.98f), 0.02f);
                // The Shard and the Gherkin.
                S.Poly(new Vector2(0.95f, 0.1f), new Vector2(1.5f, 0.1f), new Vector2(1.24f, 2.55f)); Windows(1.05f, 0.2f, 1.4f, 1.6f, 0.25f);
                S.Line(new Vector2(1.24f, 2.4f), new Vector2(1.2f, 2.68f), 0.015f); S.Line(new Vector2(1.26f, 2.4f), new Vector2(1.31f, 2.64f), 0.015f);
                S.Ellipse(new Vector2(1.9f, 0.75f), 0.19f, 0.68f, 28); S.Rect(1.71f, 0.1f, 2.09f, 0.75f);
                // London Eye.
                Wheel(new Vector2(3.4f, 0.98f), 0.7f);
            }

            public void Hamburg()
            {
                Filler(-4.6f, -3.0f, 0.6f, 1.1f);
                // St. Michael's church: tower, copper tiers and spire.
                float mx = -2.45f;
                S.Rect(mx - 0.13f, 0.1f, mx + 0.13f, 1.15f); S.Rect(mx - 0.1f, 1.15f, mx + 0.1f, 1.45f);
                Dome(mx, 1.45f, 0.1f); S.Rect(mx - 0.05f, 1.5f, mx + 0.05f, 1.75f);
                S.Poly(new Vector2(mx - 0.05f, 1.75f), new Vector2(mx + 0.05f, 1.75f), new Vector2(mx, 2.25f)); Light(mx, 1.3f, 0.04f);
                // Elbphilharmonie: brick warehouse base, glass top and a wave-shaped roof.
                S.Rect(-1.2f, 0.1f, 0.7f, 0.68f); S.Rect(-1.2f, 0.68f, 0.7f, 1.1f);
                Windows(-1.15f, 0.72f, 0.65f, 1.05f, 0.55f);
                float[] crest = { 1.1f, 1.38f, 1.2f, 1.5f, 1.25f, 1.42f, 1.15f, 1.3f, 1.1f };
                for (int i = 0; i < crest.Length - 1; i++)
                {
                    float x0 = -1.2f + i * 1.9f / (crest.Length - 1), x1 = -1.2f + (i + 1) * 1.9f / (crest.Length - 1);
                    S.Poly(new Vector2(x0, 1.1f), new Vector2(x1, 1.1f), new Vector2(x1, crest[i + 1]), new Vector2(x0, crest[i]));
                }
                // Speicherstadt: gabled red-brick warehouses.
                for (float x = 1.0f; x < 3.0f; x += 0.42f)
                {
                    S.Rect(x, 0.1f, x + 0.38f, 0.75f); Windows(x, 0.18f, x + 0.38f, 0.7f, 0.45f);
                    S.Rect(x + 0.06f, 0.75f, x + 0.32f, 0.85f); S.Rect(x + 0.12f, 0.85f, x + 0.26f, 0.95f);
                }
                GantryCrane(3.6f, 0.82f, -1);
            }

            public void Tokyo()
            {
                // Mt Fuji in the haze.
                HazePoly(new Vector2(-4.6f, 0f), new Vector2(-0.6f, 0f), new Vector2(-2.3f, 1.55f), new Vector2(-2.9f, 1.55f));
                Filler(-4.6f, -2.0f, 0.5f, 1.0f); Filler(0.55f, 1.2f, 0.8f, 1.4f);
                // Tokyo Tower: lattice A-frame with two decks.
                S.Poly(new Vector2(-1.35f, 0.1f), new Vector2(-0.65f, 0.1f), new Vector2(-0.96f, 1.35f), new Vector2(-1.04f, 1.35f));
                S.Rect(-1.2f, 0.62f, -0.8f, 0.72f); S.Rect(-1.08f, 1.2f, -0.92f, 1.28f);
                Spire(-1.0f, 1.35f, 1.95f, 0.03f); Light(-1.0f, 0.67f, 0.05f);
                // Tokyo Skytree: very tall, with two observation decks.
                S.Poly(new Vector2(0.0f, 0.1f), new Vector2(0.36f, 0.1f), new Vector2(0.24f, 2.45f), new Vector2(0.12f, 2.45f));
                S.Ellipse(new Vector2(0.18f, 1.65f), 0.16f, 0.06f, 20); S.Ellipse(new Vector2(0.18f, 2.1f), 0.11f, 0.045f, 18);
                Spire(0.18f, 2.45f, 3.05f, 0.03f); Light(0.18f, 1.65f, 0.05f);
                // Rainbow Bridge.
                SuspensionBridge(1.4f, 4.6f, 2.15f, 3.75f, 0.52f, 1.3f);
            }

            public void SanFrancisco()
            {
                Ridge(new Vector2(-6, 0.5f), new Vector2(-4.2f, 0.9f), new Vector2(-2.6f, 0.65f), new Vector2(-1.0f, 0.85f), new Vector2(1.5f, 1.1f), new Vector2(4.5f, 0.8f), new Vector2(6, 0.6f));
                Filler(-4.6f, -2.6f, 0.6f, 1.3f);
                // Coit Tower on Telegraph Hill.
                S.Ellipse(new Vector2(-3.9f, 0.1f), 0.7f, 0.45f, 24); S.Rect(-3.97f, 0.5f, -3.83f, 1.05f); S.Rect(-4.0f, 1.05f, -3.8f, 1.1f);
                // Transamerica Pyramid with its wings and spire.
                S.Poly(new Vector2(-2.4f, 0.1f), new Vector2(-1.98f, 0.1f), new Vector2(-2.19f, 2.25f));
                S.Rect(-2.42f, 1.1f, -2.32f, 1.4f); S.Rect(-2.06f, 1.1f, -1.96f, 1.4f); Spire(-2.19f, 2.2f, 2.5f, 0.02f);
                Windows(-2.32f, 0.2f, -2.06f, 1.0f, 0.4f);
                // Golden Gate Bridge: Art Deco towers and the great sweep of the cables.
                SuspensionBridge(-1.4f, 4.6f, 0.1f, 3.2f, 0.58f, 2.0f, artDeco: true);
            }

            public void RioDeJaneiro()
            {
                // Corcovado with Christ the Redeemer, in the haze behind the city.
                HazePoly(new Vector2(-4.4f, 0f), new Vector2(-0.6f, 0f), new Vector2(-2.25f, 1.85f), new Vector2(-2.6f, 1.9f));
                Ridge(new Vector2(-6, 0.4f), new Vector2(-4.6f, 0.8f), new Vector2(-3.6f, 0.5f));
                S.Rect(-2.48f, 1.88f, -2.38f, 2.32f); S.Rect(-2.72f, 2.18f, -2.14f, 2.25f); S.Circle(new Vector2(-2.43f, 2.37f), 0.045f, 12);
                S.Poly(new Vector2(-2.5f, 1.88f), new Vector2(-2.36f, 1.88f), new Vector2(-2.38f, 1.98f), new Vector2(-2.48f, 1.98f));
                Light(-2.43f, 1.95f, 0.05f);
                Filler(-4.6f, 1.4f, 0.3f, 0.75f, 0.14f, 0.28f);   // Copacabana's low-rise shoreline
                // Sugarloaf and Morro da Urca, with the cable car.
                S.Ellipse(new Vector2(3.35f, 0.1f), 0.55f, 1.45f, 32); S.Ellipse(new Vector2(2.3f, 0.1f), 0.55f, 0.72f, 28);
                S.Line(new Vector2(2.35f, 0.82f), new Vector2(3.28f, 1.52f), 0.012f);
                S.Rect(2.72f, 1.0f, 2.84f, 1.1f); Light(3.33f, 1.55f, 0.04f);
            }

            public void CapeTown()
            {
                // Table Mountain with Devil's Peak and Lion's Head.
                HazePoly(new Vector2(-3.9f, 0f), new Vector2(2.1f, 0f), new Vector2(1.55f, 1.6f), new Vector2(-3.15f, 1.65f));
                HazePoly(new Vector2(-5.2f, 0f), new Vector2(-3.3f, 0f), new Vector2(-3.85f, 1.5f));
                HazePoly(new Vector2(1.8f, 0f), new Vector2(3.8f, 0f), new Vector2(2.8f, 1.35f));
                Filler(-2.8f, 1.2f, 0.4f, 1.1f);
                Tower(-0.3f, 0.26f, 1.45f); Tower(0.3f, 0.22f, 1.3f);
                // V&A Waterfront clock tower.
                S.Rect(-3.6f, 0.1f, -3.45f, 0.75f); S.Poly(new Vector2(-3.62f, 0.75f), new Vector2(-3.43f, 0.75f), new Vector2(-3.525f, 0.92f)); Light(-3.525f, 0.62f, 0.04f);
                // Cape Town Stadium and the Green Point lighthouse.
                S.Ellipse(new Vector2(2.6f, 0.28f), 0.75f, 0.24f, 28);
                S.Rect(4.0f, 0.1f, 4.16f, 0.95f); S.Rect(3.98f, 0.95f, 4.18f, 1.05f); Light(4.08f, 1.0f, 0.05f);
            }

            public void Istanbul()
            {
                Filler(-4.6f, -2.9f, 0.4f, 0.9f);
                // Blue Mosque: cascading domes and six minarets.
                S.Rect(-2.3f, 0.1f, -0.5f, 0.5f);
                Dome(-1.4f, 0.5f, 0.42f); Dome(-1.95f, 0.5f, 0.18f); Dome(-0.85f, 0.5f, 0.18f); Spire(-1.4f, 0.9f, 1.05f, 0.02f);
                foreach (var (x, h) in new[] { (-2.55f, 1.25f), (-2.3f, 1.35f), (-0.5f, 1.35f), (-0.25f, 1.25f) }) Minaret(x, h);
                Light(-1.4f, 0.65f, 0.05f);
                // Hagia Sophia.
                S.Rect(0.15f, 0.1f, 1.35f, 0.55f); Dome(0.75f, 0.55f, 0.36f); Dome(0.35f, 0.55f, 0.14f); Dome(1.15f, 0.55f, 0.14f);
                Minaret(0.0f, 1.2f); Minaret(1.5f, 1.2f);
                // Galata Tower.
                S.Rect(2.0f, 0.1f, 2.26f, 1.1f); S.Rect(1.97f, 1.1f, 2.29f, 1.16f);
                S.Poly(new Vector2(1.99f, 1.16f), new Vector2(2.27f, 1.16f), new Vector2(2.13f, 1.52f)); Light(2.13f, 1.05f, 0.04f);
                // Bosphorus Bridge.
                SuspensionBridge(2.55f, 5.2f, 3.0f, 4.35f, 0.5f, 1.35f);
            }

            public void Venice()
            {
                Filler(-4.6f, -2.8f, 0.4f, 0.85f);
                // Doge's Palace: long, low and arcaded.
                S.Rect(-2.7f, 0.1f, -1.2f, 0.62f); Windows(-2.65f, 0.15f, -1.25f, 0.58f, 0.6f);
                // St Mark's Campanile.
                float cx = -0.85f;
                S.Rect(cx - 0.12f, 0.1f, cx + 0.12f, 1.55f); S.Rect(cx - 0.15f, 1.55f, cx + 0.15f, 1.78f);
                S.Poly(new Vector2(cx - 0.13f, 1.78f), new Vector2(cx + 0.13f, 1.78f), new Vector2(cx, 2.3f)); Spire(cx, 2.28f, 2.42f, 0.015f);
                Light(cx, 1.66f, 0.05f);
                // St Mark's Basilica.
                S.Rect(-0.5f, 0.1f, 0.85f, 0.55f);
                foreach (var (x, rr) in new[] { (-0.2f, 0.16f), (0.18f, 0.2f), (0.55f, 0.16f) }) { Dome(x, 0.55f, rr); Spire(x, 0.55f + rr, 0.55f + rr + 0.12f, 0.015f); }
                // Santa Maria della Salute.
                S.Rect(1.7f, 0.1f, 2.9f, 0.6f); Dome(2.3f, 0.6f, 0.4f); S.Rect(2.25f, 1.0f, 2.35f, 1.15f); Dome(2.3f, 1.15f, 0.07f);
                Dome(2.85f, 0.6f, 0.15f); S.Rect(2.95f, 0.1f, 3.05f, 0.95f);
                Windows(1.75f, 0.15f, 2.85f, 0.55f, 0.5f);
            }

            public void Barcelona()
            {
                Ridge(new Vector2(-6, 0.4f), new Vector2(-4.4f, 0.75f), new Vector2(-3.0f, 0.95f), new Vector2(-2.0f, 0.6f));   // Montjuïc
                Filler(-2.6f, -1.45f, 0.5f, 1.0f); Filler(0.25f, 1.3f, 0.5f, 1.1f);
                // Torre Jaume I and the port cable car.
                S.Rect(-3.45f, 0.1f, -3.3f, 1.55f); S.Rect(-3.5f, 1.4f, -3.25f, 1.5f);
                S.Line(new Vector2(-3.38f, 1.48f), new Vector2(-4.8f, 1.05f), 0.01f); S.Rect(-4.1f, 1.18f, -3.98f, 1.27f);
                // Sagrada Família: a cluster of tall, tapering spires.
                S.Rect(-1.35f, 0.1f, 0.15f, 0.8f); Windows(-1.3f, 0.2f, 0.1f, 0.75f, 0.4f);
                foreach (var (x, h) in new[] { (-1.2f, 1.6f), (-1.0f, 1.95f), (-0.78f, 2.15f), (-0.6f, 2.55f), (-0.42f, 2.15f), (-0.2f, 1.95f), (0.0f, 1.6f) })
                {
                    S.Poly(new Vector2(x - 0.06f, 0.75f), new Vector2(x + 0.06f, 0.75f), new Vector2(x + 0.02f, h), new Vector2(x - 0.02f, h));
                    S.Circle(new Vector2(x, h + 0.03f), 0.035f, 10);
                }
                Light(-0.6f, 2.6f, 0.04f);
                // Torre Glòries: the rounded "bullet".
                S.Ellipse(new Vector2(1.75f, 0.1f), 0.24f, 1.95f, 32); Windows(1.58f, 0.2f, 1.92f, 1.6f, 0.35f);
                // Columbus monument.
                S.Rect(2.6f, 0.1f, 2.9f, 0.35f); S.Rect(2.71f, 0.35f, 2.79f, 1.45f); S.Circle(new Vector2(2.75f, 1.5f), 0.05f, 12);
                S.Line(new Vector2(2.77f, 1.5f), new Vector2(2.9f, 1.6f), 0.015f);
            }

            public void Seattle()
            {
                // Mount Rainier in the haze.
                HazePoly(new Vector2(-0.6f, 0f), new Vector2(4.8f, 0f), new Vector2(2.4f, 1.6f), new Vector2(1.9f, 1.62f));
                Filler(-0.2f, 1.55f, 0.8f, 1.6f);
                // Columbia Center: tall, dark, stepped.
                Tower(0.45f, 0.34f, 2.05f); S.Rect(0.34f, 2.05f, 0.56f, 2.18f);
                // Space Needle: splayed legs, a waist and the flying-saucer top.
                float nx = -1.45f;
                S.Line(new Vector2(nx - 0.32f, 0.1f), new Vector2(nx - 0.04f, 1.05f), 0.045f); S.Line(new Vector2(nx + 0.32f, 0.1f), new Vector2(nx + 0.04f, 1.05f), 0.045f);
                S.Line(new Vector2(nx - 0.04f, 1.05f), new Vector2(nx - 0.18f, 1.82f), 0.04f); S.Line(new Vector2(nx + 0.04f, 1.05f), new Vector2(nx + 0.18f, 1.82f), 0.04f);
                S.Rect(nx - 0.04f, 0.1f, nx + 0.04f, 1.85f);
                S.Ellipse(new Vector2(nx, 1.86f), 0.38f, 0.08f, 28); S.Ellipse(new Vector2(nx, 1.96f), 0.22f, 0.06f, 24);
                Spire(nx, 1.98f, 2.45f, 0.02f); Light(nx, 1.86f, 0.05f);
                // Seattle Great Wheel on the pier, and the port's container cranes.
                Wheel(new Vector2(2.75f, 0.62f), 0.48f, 22);
                GantryCrane(-4.3f, 0.72f, 1);
                Filler(-3.0f, -2.0f, 0.5f, 1.1f);
            }
        }
    }
}
