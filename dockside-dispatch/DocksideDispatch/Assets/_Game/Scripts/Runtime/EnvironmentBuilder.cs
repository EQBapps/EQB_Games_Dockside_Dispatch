using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Builds the Golden Hour Harbour scene: a sunset sky with sun glow and drifting clouds, a two-layer port
    /// silhouette (gantry cranes, container stacks, a moored ship, the pier and lighthouse), animated shader water,
    /// the quay, the yard crane and the container yard.
    /// </summary>
    public static class EnvironmentBuilder
    {
        /// <summary>Z of the far edge of the water; its top edge on screen is the horizon line.</summary>
        public const float WaterEdgeZ = BoardLayout.WaterFarZ + 0.9f;
        public const float WaterY = -0.35f;
        public const float SunX = 1.2f;

        public static Transform Build(Transform parent, BoardLayout layout, Palette pal, Camera cam)
        {
            var root = new GameObject("Environment").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(12345);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = pal.skyZenith;
            Vector3 up = cam.transform.up, fwd = cam.transform.forward;
            Quaternion facing = Quaternion.LookRotation(fwd, up);
            // Backdrop items are flat, camera-facing layers placed by screen height (v) and depth.
            Vector3 Backdrop(float sx, float v, float depth) => new Vector3(sx, 0, 0) + up * v + fwd * depth;
            float horizonV = Vector3.Dot(new Vector3(0, WaterY, WaterEdgeZ), up);
            float topV = Vector3.Dot(cam.transform.position, up) + cam.orthographicSize;
            float halfW = cam.orthographicSize * Mathf.Max(cam.aspect, 0.3f) + 1.5f;

            GameObject Layer(string name, FlatMesh shape, Color color, float depth, float sx = 0f, float dv = 0f)
            {
                var go = Shapes.MakeWith(name, shape.Build(name), Shapes.Mat(color, true), root, Backdrop(sx, horizonV + dv, depth), Vector3.one, false);
                go.transform.rotation = facing;
                return go;
            }

            // ---- Sky: gradient with the sun's glow baked in ----
            float skyH = Mathf.Max(2f, topV - horizonV) + 1.0f;
            var skyTex = SkyTexture(pal, halfW * 2f, skyH, SunX, 0.55f);
            var sky = Shapes.MakeWith("Sky", Shapes.QuadMesh, Shapes.MatTex(Color.white, skyTex, true), root,
                Backdrop(0, horizonV - 0.5f + skyH * 0.5f, 76f), new Vector3(halfW * 2f, skyH, 1f), false);
            sky.transform.rotation = facing;

            // ---- Sun with a soft halo ring, sinking behind the port ----
            Layer("SunHalo", new FlatMesh().Circle(Vector2.zero, 1.35f, 48), Color.Lerp(pal.sunGlow, pal.skyHorizon, 0.35f), 74f, SunX, 0.55f);
            Layer("Sun", new FlatMesh().Circle(Vector2.zero, 0.98f, 64), pal.sun, 73f, SunX, 0.55f);

            // ---- Clouds: shaded banks that drift slowly, and thin streaks across the sun ----
            var cloudSpecs = new[] { (-3.6f, 2.35f, 2.6f, 0.34f, 0.05f), (2.4f, 3.05f, 3.1f, 0.3f, 0.035f), (-1.1f, 1.45f, 1.7f, 0.22f, 0.07f), (3.9f, 1.75f, 1.4f, 0.2f, 0.06f) };
            foreach (var (cx, cy, len, h, speed) in cloudSpecs)
            {
                if (cy > topV - horizonV + 0.3f) continue;
                var shade = Layer("CloudShade", new FlatMesh().CloudBank(new Vector2(-len * 0.5f + 0.08f, -0.06f), len, h, new System.Random((int)(cx * 100))), pal.cloudShade, 71f, cx, cy);
                var light = Layer("Cloud", new FlatMesh().CloudBank(new Vector2(-len * 0.5f, 0f), len, h, new System.Random((int)(cx * 100))), pal.cloudLight, 70.5f, cx, cy);
                shade.AddComponent<Drift>().Init(speed, halfW);
                light.AddComponent<Drift>().Init(speed, halfW);
            }
            var streaks = new FlatMesh()
                .Rect(-1.2f, 0.92f, 3.4f, 0.97f).Rect(-0.4f, 1.12f, 2.6f, 1.155f).Rect(0.4f, 0.74f, 4.6f, 0.78f);
            Layer("SunStreaks", streaks, Color.Lerp(pal.cloudShade, pal.skyMid, 0.35f), 72f);
            // A few gulls.
            var gulls = new FlatMesh();
            foreach (var (gx, gy, s) in new[] { (-2.3f, 2.0f, 1f), (-1.95f, 2.15f, 0.8f), (-2.6f, 2.25f, 0.7f), (3.1f, 2.5f, 0.75f) })
            {
                gulls.Line(new Vector2(gx - 0.1f * s, gy + 0.04f * s), new Vector2(gx, gy), 0.025f * s);
                gulls.Line(new Vector2(gx, gy), new Vector2(gx + 0.1f * s, gy + 0.045f * s), 0.025f * s);
            }
            Layer("Gulls", gulls, pal.silhouetteNear, 69f).AddComponent<Drift>().Init(0.12f, halfW);

            // ---- Port silhouette, far layer: gantry cranes, container stacks, warehouse, light towers ----
            var far = new FlatMesh();
            var farLights = new FlatMesh();
            far.Rect(-halfW - 2f, -0.8f, halfW + 2f, 0.16f);
            void Stacks(float x0, float x1)
            {
                for (float x = x0; x < x1; x += 0.36f)
                {
                    int tiers = 1 + rng.Next(4);
                    far.Rect(x, 0.1f, x + 0.33f, 0.16f + tiers * 0.13f);
                }
            }
            Stacks(-halfW - 0.5f, -3.1f);
            Stacks(-1.0f, -0.05f);
            Stacks(3.0f, halfW + 0.5f);
            // Warehouse with a sawtooth roof.
            far.Rect(-2.95f, 0.1f, -1.15f, 0.55f);
            for (int i = 0; i < 4; i++)
            {
                float x = -2.95f + i * 0.45f;
                far.Poly(new Vector2(x, 0.55f), new Vector2(x + 0.45f, 0.55f), new Vector2(x + 0.45f, 0.72f));
            }
            void StsCrane(float x0, float s, int dir)
            {
                float L(float dx) => x0 + dx * s * dir;
                far.Rect(Mathf.Min(L(0), L(0.06f)), 0.12f, Mathf.Max(L(0), L(0.06f)), 1.3f * s);
                far.Rect(Mathf.Min(L(0.55f), L(0.61f)), 0.12f, Mathf.Max(L(0.55f), L(0.61f)), 1.3f * s);
                far.Line(new Vector2(L(0.03f), 0.45f * s), new Vector2(L(0.58f), 0.95f * s), 0.03f);
                far.Line(new Vector2(L(0.58f), 0.45f * s), new Vector2(L(0.03f), 0.95f * s), 0.03f);
                far.Rect(Mathf.Min(L(-0.05f), L(0.66f)), 1.22f * s, Mathf.Max(L(-0.05f), L(0.66f)), 1.32f * s);
                far.Rect(Mathf.Min(L(0.18f), L(0.7f)), 1.32f * s, Mathf.Max(L(0.18f), L(0.7f)), 1.52f * s);       // machinery house
                far.Rect(Mathf.Min(L(-1.15f), L(1.0f)), 1.38f * s, Mathf.Max(L(-1.15f), L(1.0f)), 1.45f * s);     // boom over the water
                far.Line(new Vector2(L(0.08f), 1.45f * s), new Vector2(L(0.28f), 2.05f * s), 0.035f);             // A-frame
                far.Line(new Vector2(L(0.5f), 1.45f * s), new Vector2(L(0.3f), 2.05f * s), 0.035f);
                far.Line(new Vector2(L(0.29f), 2.05f * s), new Vector2(L(-1.1f), 1.45f * s), 0.022f);             // forestays
                far.Line(new Vector2(L(0.29f), 2.05f * s), new Vector2(L(-0.45f), 1.45f * s), 0.018f);
                far.Line(new Vector2(L(0.29f), 2.05f * s), new Vector2(L(0.95f), 1.45f * s), 0.022f);             // backstay
                far.Rect(Mathf.Min(L(-0.72f), L(-0.52f)), 1.3f * s, Mathf.Max(L(-0.72f), L(-0.52f)), 1.38f * s);  // trolley
                far.Line(new Vector2(L(-0.62f), 1.3f * s), new Vector2(L(-0.62f), 0.95f * s), 0.012f);           // hoist
                far.Rect(Mathf.Min(L(-0.76f), L(-0.48f)), 0.9f * s, Mathf.Max(L(-0.76f), L(-0.48f)), 0.95f * s);  // spreader
                farLights.Circle(new Vector2(L(0.29f), 2.05f * s + 0.035f), 0.03f, 10);
                farLights.Circle(new Vector2(L(-1.12f), 1.47f * s + 0.03f), 0.025f, 10);
            }
            StsCrane(-4.35f, 1.0f, 1);
            StsCrane(-2.25f, 0.82f, 1);
            StsCrane(3.55f, 0.95f, -1);
            void LightTower(float x, float h)
            {
                far.Rect(x - 0.02f, 0.12f, x + 0.02f, h);
                far.Rect(x - 0.1f, h, x + 0.1f, h + 0.06f);
                farLights.Rect(x - 0.08f, h - 0.03f, x + 0.08f, h);
            }
            LightTower(-0.55f, 1.35f);
            LightTower(4.6f, 1.25f);
            Layer("PortFar", far, pal.silhouetteFar, 66f);
            Layer("PortFarLights", farLights, pal.silhouetteLight, 65.8f);

            // ---- Port silhouette, near layer: moored ship, pier on pilings, harbour crane, lamps ----
            var near = new FlatMesh();
            var nearLights = new FlatMesh();
            // Moored cargo ship (left).
            near.Poly(new Vector2(-5.6f, -0.2f), new Vector2(-2.3f, -0.2f), new Vector2(-2.05f, 0.32f), new Vector2(-5.75f, 0.32f));
            for (float x = -4.95f; x < -2.6f; x += 0.3f)
                near.Rect(x, 0.32f, x + 0.27f, 0.32f + (1 + rng.Next(3)) * 0.12f);
            near.Rect(-5.6f, 0.32f, -5.1f, 0.95f);                     // bridge
            near.Rect(-5.68f, 0.82f, -5.02f, 0.9f);                    // bridge wings
            near.Rect(-5.45f, 0.95f, -5.25f, 1.12f);                   // funnel
            near.Rect(-2.42f, 0.32f, -2.38f, 0.9f);                    // foremast
            nearLights.Rect(-5.55f, 0.84f, -5.15f, 0.87f);             // lit bridge windows
            // Pier deck on pilings with a rail.
            float pierL = 0.35f, pierR = halfW + 1f;
            near.Rect(pierL, 0.2f, pierR, 0.29f);
            for (float x = pierL + 0.05f; x < pierR; x += 0.3f)
            {
                near.Rect(x, -0.3f, x + 0.05f, 0.2f);
                near.Rect(x, 0.29f, x + 0.022f, 0.4f);
            }
            near.Rect(pierL, 0.38f, pierR, 0.405f);
            near.Line(new Vector2(pierL + 0.05f, 0.0f), new Vector2(pierL + 0.35f, 0.2f), 0.03f);    // bracing
            // Lamp posts along the pier.
            foreach (float x in new[] { 0.9f, 3.3f, 4.6f })
            {
                near.Rect(x, 0.29f, x + 0.03f, 0.88f);
                near.Rect(x, 0.85f, x + 0.14f, 0.88f);
                nearLights.Circle(new Vector2(x + 0.13f, 0.83f), 0.035f, 12);
            }
            // Luffing harbour crane on the pier.
            near.Rect(3.75f, 0.29f, 4.05f, 0.52f);
            near.Rect(3.85f, 0.52f, 3.95f, 1.08f);
            near.Rect(3.74f, 0.95f, 4.06f, 1.14f);
            near.Line(new Vector2(3.9f, 1.08f), new Vector2(2.95f, 1.6f), 0.04f);
            near.Line(new Vector2(3.0f, 1.58f), new Vector2(3.0f, 1.0f), 0.012f);
            near.Rect(2.95f, 0.96f, 3.05f, 1.02f);
            Layer("PortNear", near, pal.silhouetteNear, 62f);
            Layer("PortNearLights", nearLights, pal.silhouetteLight, 61.8f);

            // ---- Lighthouse at the head of the pier (in colour, as in the art direction) ----
            float lhX = 2.15f, lb = 0.29f, lh = 1.25f;
            var white = new FlatMesh();
            var red = new FlatMesh();
            for (int i = 0; i < 5; i++)
            {
                float y0 = lb + lh * i / 5f, y1 = lb + lh * (i + 1) / 5f;
                float w0 = Mathf.Lerp(0.2f, 0.13f, (y0 - lb) / lh), w1 = Mathf.Lerp(0.2f, 0.13f, (y1 - lb) / lh);
                (i % 2 == 0 ? white : red).Poly(new Vector2(lhX - w0, y0), new Vector2(lhX + w0, y0), new Vector2(lhX + w1, y1), new Vector2(lhX - w1, y1));
            }
            float top = lb + lh;
            red.Rect(lhX - 0.19f, top, lhX + 0.19f, top + 0.05f);                          // gallery
            red.Poly(new Vector2(lhX - 0.15f, top + 0.27f), new Vector2(lhX + 0.15f, top + 0.27f), new Vector2(lhX, top + 0.42f)); // cap
            white.Rect(lhX - 0.11f, top + 0.05f, lhX - 0.08f, top + 0.27f);
            white.Rect(lhX + 0.08f, top + 0.05f, lhX + 0.11f, top + 0.27f);
            Layer("LighthouseWhite", white, pal.lighthouseWhite, 61.5f);
            Layer("LighthouseRed", red, pal.lighthouseRed, 61.4f);
            Layer("LighthouseLamp", new FlatMesh().Rect(lhX - 0.08f, top + 0.05f, lhX + 0.08f, top + 0.27f), pal.lighthouseLamp, 61.45f);
            var beam = Layer("LighthouseBeam", new FlatMesh().Poly(new Vector2(0, 0.06f), new Vector2(0, -0.06f), new Vector2(-1.6f, -0.2f), new Vector2(-1.6f, 0.2f)),
                Color.Lerp(pal.lighthouseLamp, pal.skyMid, 0.55f), 61.6f, lhX, top + 0.16f);
            beam.AddComponent<BeamSweep>();

            // ---- Water: animated shader plane from the quay wall out to the horizon ----
            float waterNear = BoardLayout.QuayFarZ;
            float waterDepth = WaterEdgeZ - waterNear;
            var waterMat = WaterMaterial(pal, waterNear + 0.42f); // foam starts where the quay wall stops hiding the water
            Shapes.MakeWith("Water", Shapes.CubeMesh, waterMat, root, new Vector3(0, WaterY - 0.1f, waterNear + waterDepth * 0.5f),
                new Vector3(halfW * 2f + 4f, 0.2f, waterDepth), false).GetComponent<Renderer>().receiveShadows = false;
            // Two buoys bobbing off the quay.
            foreach (var bx in new[] { -halfW + 1.3f, halfW - 1.3f })
            {
                var buoy = new GameObject("Buoy").transform;
                buoy.SetParent(root, false);
                buoy.localPosition = new Vector3(bx, WaterY, BoardLayout.ShipZ + 1.45f);
                Shapes.Cylinder("Float", buoy, new Vector3(0, 0.05f, 0), new Vector3(0.26f, 0.16f, 0.26f), pal.lighthouseRed);
                Shapes.Cylinder("Band", buoy, new Vector3(0, 0.16f, 0), new Vector3(0.2f, 0.07f, 0.2f), pal.lighthouseWhite);
                Shapes.Box("Mast", buoy, new Vector3(0, 0.3f, 0), new Vector3(0.04f, 0.26f, 0.04f), pal.lighthouseRed);
                Shapes.Cylinder("Lamp", buoy, new Vector3(0, 0.46f, 0), new Vector3(0.07f, 0.06f, 0.07f), pal.lighthouseLamp, true, false);
                buoy.gameObject.AddComponent<Bob>().Init(R(0f, 6f), 0.035f, 4f);
            }

            // ---- Quay (holding bay) ----
            float quayDepth = BoardLayout.QuayFarZ - BoardLayout.QuayNearZ;
            Shapes.Box("Quay", root, new Vector3(0, -0.05f, BoardLayout.QuayNearZ + quayDepth * 0.5f), new Vector3(40f, 0.3f, quayDepth), pal.quay);
            Shapes.Box("QuayEdge", root, new Vector3(0, -0.1f, BoardLayout.QuayFarZ + 0.06f), new Vector3(40f, 0.4f, 0.14f), pal.quayEdge, false, false);
            Shapes.Box("QuayCap", root, new Vector3(0, 0.105f, BoardLayout.QuayFarZ - 0.04f), new Vector3(40f, 0.02f, 0.16f), Palette.Darken(pal.quay, 0.1f), false, false);
            // Painted safety line and paving seams.
            Shapes.Box("SafetyLine", root, new Vector3(0, 0.104f, BoardLayout.QuayFarZ - 0.36f), new Vector3(40f, 0.01f, 0.05f), pal.yardLine, true, false);
            for (float sx = -halfW; sx <= halfW; sx += 1.1f)
                Shapes.Box("Seam", root, new Vector3(sx, 0.103f, BoardLayout.QuayNearZ + quayDepth * 0.5f), new Vector3(0.025f, 0.01f, quayDepth - 0.05f),
                    Palette.Darken(pal.quay, 0.07f), true, false);
            for (float bx = -4.2f; bx <= 4.3f; bx += 1.4f)
            {
                var bp = new Vector3(bx, 0.1f, BoardLayout.QuayFarZ - 0.17f);
                Shapes.Cylinder("Bollard", root, bp + new Vector3(0, 0.1f, 0), new Vector3(0.15f, 0.2f, 0.15f), Palette.Hex("4A3A33"));
                Shapes.Cylinder("BollardCap", root, bp + new Vector3(0, 0.22f, 0), new Vector3(0.22f, 0.05f, 0.22f), Palette.Hex("5C4A42"));
            }

            // ---- Gantry crane (right side of the quay; kept low so it never covers the ships) ----
            var craneBase = new Vector3(4.15f, 0f, 1.9f);
            Shapes.Box("CraneLegA", root, craneBase + new Vector3(-0.18f, 0.75f, -0.3f), new Vector3(0.13f, 1.5f, 0.13f), pal.crane);
            Shapes.Box("CraneLegB", root, craneBase + new Vector3(-0.18f, 0.75f, 0.3f), new Vector3(0.13f, 1.5f, 0.13f), pal.craneDark);
            Shapes.Box("CraneCab", root, craneBase + new Vector3(-0.18f, 1.55f, 0), new Vector3(0.5f, 0.32f, 0.75f), pal.crane);
            Shapes.Box("CraneArm", root, craneBase + new Vector3(-1.0f, 1.78f, 0), new Vector3(2.0f, 0.13f, 0.18f), pal.crane);
            Shapes.Box("CraneCable", root, craneBase + new Vector3(-1.75f, 1.45f, 0), new Vector3(0.03f, 0.55f, 0.03f), Palette.Hex("4A3A33"), false, false);
            Shapes.Box("CraneHook", root, craneBase + new Vector3(-1.75f, 1.15f, 0), new Vector3(0.2f, 0.07f, 0.12f), Palette.Hex("4A3A33"), false, false);

            // ---- Yard ----
            float yardW = layout.Width * layout.CellSize + 0.5f;
            float yardTop = BoardLayout.QuayNearZ, yardBottom = layout.YardBottomZ - 0.3f;
            Shapes.Box("Apron", root, new Vector3(0, -0.2f, (yardTop + yardBottom) * 0.5f - 2f), new Vector3(40f, 0.2f, yardTop - yardBottom + 4f),
                Palette.Darken(pal.quay, 0.12f), false, false);
            Shapes.Box("Yard", root, new Vector3(0, -0.08f, (yardTop + yardBottom) * 0.5f), new Vector3(yardW, 0.16f, yardTop - yardBottom), pal.yard);
            for (int i = 0; i <= layout.Width; i++)
            {
                float lx = (i - layout.Width * 0.5f) * layout.CellSize;
                Shapes.Box("YardLine", root, new Vector3(lx, 0.005f, (yardTop + yardBottom) * 0.5f), new Vector3(0.05f, 0.01f, yardTop - yardBottom - 0.1f),
                    pal.yardLine, true, false);
            }
            // Kerb around the yard.
            var kerb = Palette.Darken(pal.yard, 0.25f);
            float midZ = (yardTop + yardBottom) * 0.5f, depthZ = yardTop - yardBottom;
            Shapes.Box("KerbL", root, new Vector3(-yardW * 0.5f - 0.06f, 0.0f, midZ), new Vector3(0.12f, 0.2f, depthZ + 0.24f), kerb);
            Shapes.Box("KerbR", root, new Vector3(yardW * 0.5f + 0.06f, 0.0f, midZ), new Vector3(0.12f, 0.2f, depthZ + 0.24f), kerb);
            Shapes.Box("KerbB", root, new Vector3(0, 0.0f, yardBottom - 0.06f), new Vector3(yardW + 0.24f, 0.2f, 0.12f), kerb);
            return root;
        }

        private static readonly Dictionary<string, Texture2D> SkyCache = new Dictionary<string, Texture2D>();

        /// <summary>Vertical sunset gradient (horizon gold to dusky rose) with a radial sun glow, sized to the visible sky.</summary>
        private static Texture2D SkyTexture(Palette pal, float width, float height, float sunX, float sunY)
        {
            string key = $"{width:0.0}x{height:0.0}";
            if (SkyCache.TryGetValue(key, out var cached) && cached) return cached;
            const int tw = 128, th = 256;
            var px = new Color[tw * th];
            for (int y = 0; y < th; y++)
            {
                float f = y / (th - 1f);
                float wy = -0.5f + f * height;                 // world units above the horizon
                float g = Mathf.Clamp01(wy / Mathf.Max(3.2f, height - 0.8f));
                Color c = g < 0.3f ? Color.Lerp(pal.skyHorizon, pal.skyMid, g / 0.3f)
                        : g < 0.65f ? Color.Lerp(pal.skyMid, pal.skyHigh, (g - 0.3f) / 0.35f)
                        : Color.Lerp(pal.skyHigh, pal.skyZenith, (g - 0.65f) / 0.35f);
                for (int x = 0; x < tw; x++)
                {
                    float wx = (x / (tw - 1f) - 0.5f) * width;
                    float d = Vector2.Distance(new Vector2(wx, wy), new Vector2(sunX, sunY));
                    float glow = Mathf.Exp(-Mathf.Pow(d / 1.7f, 2f)) * 0.8f + Mathf.Exp(-Mathf.Pow(d / 4.2f, 2f)) * 0.3f;
                    px[y * tw + x] = Color.Lerp(c, pal.sunGlow, Mathf.Clamp01(glow));
                }
            }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false, false)
            {
                name = "SunsetSky",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            SkyCache[key] = tex;
            return tex;
        }

        private static Material _water;

        private static Material WaterMaterial(Palette pal, float nearZ)
        {
            if (_water == null)
            {
                if (pal.waterMaterial != null) _water = new Material(pal.waterMaterial);
                else
                {
                    var shader = Shader.Find("Dockside/Water");
                    _water = shader != null ? new Material(shader) : new Material(Shapes.Mat(pal.water, true));
                }
                _water.name = "Water (runtime)";
            }
            _water.SetColor("_Shallow", pal.waterShallow);
            _water.SetColor("_Deep", pal.waterDeep);
            _water.SetColor("_Horizon", pal.waterHorizon);
            _water.SetColor("_Highlight", pal.waterHighlight);
            _water.SetColor("_Glitter", pal.waterGlitter);
            _water.SetColor("_Foam", pal.foam);
            _water.SetVector("_Params", new Vector4(nearZ, WaterEdgeZ, SunX, 1f));
            return _water;
        }
    }

    /// <summary>Slow horizontal drift that wraps around the screen (clouds, gulls).</summary>
    public class Drift : MonoBehaviour
    {
        private float _speed, _halfWidth;
        public void Init(float speed, float halfWidth) { _speed = speed; _halfWidth = halfWidth + 2.5f; }
        private void Update()
        {
            var p = transform.position;
            p += transform.right * (_speed * Time.deltaTime);
            if (p.x > _halfWidth) p -= transform.right * (_halfWidth * 2f);
            transform.position = p;
        }
    }

    /// <summary>Gentle float on the swell (buoys).</summary>
    public class Bob : MonoBehaviour
    {
        private Vector3 _start;
        private float _phase, _amp, _tilt;
        public void Init(float phase, float amplitude, float tiltDeg) { _phase = phase; _amp = amplitude; _tilt = tiltDeg; _start = transform.localPosition; }
        private void Update()
        {
            float t = Time.time + _phase;
            transform.localPosition = _start + new Vector3(0, Mathf.Sin(t * 1.4f) * _amp, 0);
            transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * _tilt, 0, Mathf.Sin(t * 0.9f) * _tilt);
        }
    }

    /// <summary>Slow sweep of the lighthouse beam.</summary>
    public class BeamSweep : MonoBehaviour
    {
        private Vector3 _baseScale;
        private void Awake() => _baseScale = transform.localScale;
        private void Update()
        {
            float s = Mathf.Cos(Time.time * 0.7f);   // beam swings from left to right, foreshortened
            transform.localScale = new Vector3(_baseScale.x * s, _baseScale.y, _baseScale.z);
        }
    }

    /// <summary>Dashed outlines for the holding-bay slots.</summary>
    public class BufferSlotsView : MonoBehaviour
    {
        private Palette _pal;
        private readonly List<GameObject> _slots = new List<GameObject>();

        public void Init(Palette pal) { _pal = pal; }

        public void SetCapacity(int capacity)
        {
            foreach (var s in _slots) Destroy(s);
            _slots.Clear();
            float pitch = BoardLayout.BufferPitch(capacity);
            float w = pitch - 0.12f, d = 0.95f;
            for (int i = 0; i < capacity; i++)
            {
                var p = BoardLayout.BufferPosition(i, 1, capacity);
                var slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(transform, false);
                slot.transform.localPosition = new Vector3(p.x, 0.11f, p.z);
                var c = Color.Lerp(_pal.quay, _pal.quayEdge, 0.7f);
                // Dashed frame: short dashes along each edge.
                for (int j = 0; j < 4; j++)
                {
                    float fx = -w * 0.5f + w * (j + 0.5f) / 4f;
                    Shapes.Box("Dash", slot.transform, new Vector3(fx, 0, -d * 0.5f), new Vector3(w / 4f * 0.6f, 0.01f, 0.05f), c, true, false);
                    Shapes.Box("Dash", slot.transform, new Vector3(fx, 0, d * 0.5f), new Vector3(w / 4f * 0.6f, 0.01f, 0.05f), c, true, false);
                }
                for (int j = 0; j < 3; j++)
                {
                    float fz = -d * 0.5f + d * (j + 0.5f) / 3f;
                    Shapes.Box("Dash", slot.transform, new Vector3(-w * 0.5f, 0, fz), new Vector3(0.05f, 0.01f, d / 3f * 0.6f), c, true, false);
                    Shapes.Box("Dash", slot.transform, new Vector3(w * 0.5f, 0, fz), new Vector3(0.05f, 0.01f, d / 3f * 0.6f), c, true, false);
                }
                _slots.Add(slot);
            }
        }
    }
}
