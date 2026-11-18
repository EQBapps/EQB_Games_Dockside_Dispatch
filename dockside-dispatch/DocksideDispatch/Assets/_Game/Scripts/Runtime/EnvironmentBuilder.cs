using System.Collections.Generic;
using Dockside.Core;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Builds the Golden Hour Harbour scene: a sunset sky with sun glow and drifting clouds, a two-layer port
    /// silhouette (gantry cranes, container stacks, a moored ship, the pier and lighthouse), animated shader water,
    /// the quay, the yard crane and the container yard. Unload mode swaps the water for a road and the yard for a
    /// ship's hold (EnvironmentBuilder.Unload.cs).
    /// </summary>
    public static partial class EnvironmentBuilder
    {
        /// <summary>Z of the far edge of the water; its top edge on screen is the horizon line.</summary>
        public const float WaterEdgeZ = BoardLayout.WaterFarZ + 0.9f;
        public const float WaterY = -0.35f;

        public static Transform Build(Transform parent, BoardLayout layout, Palette pal, Camera cam, int levelNumber, GameMode mode = GameMode.Load)
        {
            var root = new GameObject("Environment").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(12345);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var theme = SkyTheme.Current(pal);
            Shader.SetGlobalFloat("_DocksideNight", theme.night);   // Flat shader: darken the harbour at night

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme.zenith;
            Vector3 up = cam.transform.up, fwd = cam.transform.forward;
            Quaternion facing = Quaternion.LookRotation(fwd, up);
            // Backdrop items are flat, camera-facing layers placed by screen height (v) and depth.
            Vector3 Backdrop(float sx, float v, float depth) => new Vector3(sx, 0, 0) + up * v + fwd * depth;
            float horizonV = Vector3.Dot(new Vector3(0, WaterY, WaterEdgeZ), up);
            float topV = Vector3.Dot(cam.transform.position, up) + cam.orthographicSize;
            float halfW = cam.orthographicSize * Mathf.Max(cam.aspect, 0.3f) + 1.5f;
            float skyAbove = topV - horizonV;

            GameObject Layer(string name, FlatMesh shape, Color color, float depth, float sx = 0f, float dv = 0f)
            {
                var go = Shapes.MakeWith(name, shape.Build(name), Shapes.Mat(color, true), root, Backdrop(sx, horizonV + dv, depth), Vector3.one, false);
                go.transform.rotation = facing;
                return go;
            }

            // ---- Sky: gradient for the time of day, with the glow of the sun or moon baked in ----
            float skyH = Mathf.Max(2f, skyAbove) + 1.0f;
            var skyTex = SkyTexture(theme, halfW * 2f, skyH);
            var sky = Shapes.MakeWith("Sky", Shapes.QuadMesh, Shapes.MatTex(Color.white, skyTex, true), root,
                Backdrop(0, horizonV - 0.5f + skyH * 0.5f, 76f), new Vector3(halfW * 2f, skyH, 1f), false);
            sky.transform.rotation = facing;

            // ---- Stars (night) ----
            if (theme.stars)
            {
                var stars = new FlatMesh();
                for (int i = 0; i < 70; i++)
                {
                    var p = new Vector2(R(-halfW, halfW), R(0.7f, Mathf.Max(1f, skyAbove + 0.5f)));
                    float s = R(0.012f, 0.03f);
                    stars.Poly(p + new Vector2(0, s * 1.6f), p + new Vector2(s * 0.5f, 0), p + new Vector2(0, -s * 1.6f), p + new Vector2(-s * 0.5f, 0));
                    stars.Poly(p + new Vector2(s * 1.6f, 0), p + new Vector2(0, s * 0.5f), p + new Vector2(-s * 1.6f, 0), p + new Vector2(0, -s * 0.5f));
                }
                Layer("Stars", stars, Color.Lerp(theme.sun, theme.high, 0.25f), 75f);
            }

            // ---- Sun with a soft halo, or a crescent moon at night ----
            if (theme.moon)
            {
                Layer("Moon", new FlatMesh().Circle(Vector2.zero, theme.sunRadius, 48), theme.sun, 73f, theme.sunX, theme.sunV);
                Layer("MoonShadow", new FlatMesh().Circle(new Vector2(theme.sunRadius * 0.45f, theme.sunRadius * 0.25f), theme.sunRadius * 0.92f, 48),
                    Color.Lerp(theme.high, theme.mid, 0.3f), 72.9f, theme.sunX, theme.sunV);
            }
            else
            {
                Layer("SunHalo", new FlatMesh().Circle(Vector2.zero, theme.sunRadius * 1.38f, 48), Color.Lerp(theme.sunGlow, theme.horizon, 0.35f), 74f, theme.sunX, theme.sunV);
                Layer("Sun", new FlatMesh().Circle(Vector2.zero, theme.sunRadius, 64), theme.sun, 73f, theme.sunX, theme.sunV);
            }

            // ---- Clouds: shaded banks that drift slowly (fewer at night), streaks across the setting sun ----
            var cloudSpecs = new[] { (-3.6f, 2.35f, 2.6f, 0.34f, 0.05f), (2.4f, 3.05f, 3.1f, 0.3f, 0.035f), (-1.1f, 1.45f, 1.7f, 0.22f, 0.07f), (3.9f, 1.75f, 1.4f, 0.2f, 0.06f) };
            int ci = 0;
            foreach (var (cx, cy, len, h, speed) in cloudSpecs)
            {
                if (cy > skyAbove + 0.3f || (theme.moon && ci++ % 2 == 1)) continue;
                var shade = Layer("CloudShade", new FlatMesh().CloudBank(new Vector2(-len * 0.5f + 0.08f, -0.06f), len, h, new System.Random((int)(cx * 100))), theme.cloudShade, 71f, cx, cy);
                var light = Layer("Cloud", new FlatMesh().CloudBank(new Vector2(-len * 0.5f, 0f), len, h, new System.Random((int)(cx * 100))), theme.cloudLight, 70.5f, cx, cy);
                shade.AddComponent<Drift>().Init(speed, halfW);
                light.AddComponent<Drift>().Init(speed, halfW);
            }
            if (theme.sunStreaks)
                Layer("SunStreaks", new FlatMesh().Rect(-1.2f, 0.92f, 3.4f, 0.97f).Rect(-0.4f, 1.12f, 2.6f, 1.155f).Rect(0.4f, 0.74f, 4.6f, 0.78f),
                    Color.Lerp(theme.cloudShade, theme.mid, 0.35f), 72f);

            // ---- Birds: a flock flies across every so often ----
            var birds = new GameObject("Birds").AddComponent<BirdFlocks>();
            birds.transform.SetParent(root, false);
            birds.Init(Color.Lerp(theme.silhouetteNear, Color.black, 0.15f), up, fwd, horizonV, topV, halfW, theme.birdInterval);

            // ---- The port of the day: a famous skyline (changes every few levels), with a working dock ----
            int port = PortSkylines.IndexForLevel(levelNumber);
            var skyline = PortSkylines.Build(port, halfW, theme.lightsOn, 1000 + port);
            Layer("PortHaze", skyline.haze, theme.haze, 67f);
            Layer("PortSkyline", skyline.skyline, theme.silhouetteFar, 66f);
            if (theme.lightsOn) Layer("PortLights", skyline.lights, theme.lights, 65.8f);
            foreach (var hub in skyline.windmillHubs)
            {
                var sails = new FlatMesh();
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var n = new Vector2(-d.y, d.x);
                    sails.Line(Vector2.zero, d * 0.72f, 0.03f);
                    sails.Poly(d * 0.18f, d * 0.7f, d * 0.7f + n * 0.13f, d * 0.18f + n * 0.13f);
                }
                Layer("WindmillSails", sails, theme.silhouetteFar, 65.9f, hub.x, hub.y).AddComponent<Spin>().Init(-40f);
            }

            if (mode == GameMode.Unload)
                BuildDockside(root, pal, theme, cam, layout, halfW, rng);
            else
                BuildHarbourFront(root, pal, theme, halfW, Layer, rng);

            // ---- Quay (holding bay): concrete slabs with dark joints, a painted coping at the far edge ----
            BuildQuay(root, pal, theme, cam, halfW, mode);

            // ---- Yard (Load) or the ship's hold (Unload) ----
            if (mode == GameMode.Unload) BuildHold(root, pal, theme, layout, halfW, levelNumber);
            else BuildYard(root, pal, layout, halfW);
            return root;
        }

        /// <summary>Load: moored ship and pier silhouettes, the harbour water and two buoys.</summary>
        private static void BuildHarbourFront(Transform root, Palette pal, SkyTheme theme, float halfW,
            System.Func<string, FlatMesh, Color, float, float, float, GameObject> Layer, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // ---- Near layer: moored ship, pier on pilings, lamps (kept low so the skyline reads) ----
            var near = new FlatMesh();
            var nearLights = new FlatMesh();
            near.Poly(new Vector2(-5.6f, -0.2f), new Vector2(-2.3f, -0.2f), new Vector2(-2.05f, 0.32f), new Vector2(-5.75f, 0.32f));
            for (float x = -4.95f; x < -2.6f; x += 0.3f)
                near.Rect(x, 0.32f, x + 0.27f, 0.32f + (1 + rng.Next(3)) * 0.12f);
            near.Rect(-5.6f, 0.32f, -5.1f, 0.95f);                     // bridge
            near.Rect(-5.68f, 0.82f, -5.02f, 0.9f);                    // bridge wings
            near.Rect(-5.45f, 0.95f, -5.25f, 1.12f);                   // funnel
            near.Rect(-2.42f, 0.32f, -2.38f, 0.9f);                    // foremast
            nearLights.Rect(-5.55f, 0.84f, -5.15f, 0.87f);             // lit bridge windows
            float pierL = 0.35f, pierR = halfW + 1f;
            near.Rect(pierL, 0.2f, pierR, 0.29f);
            for (float x = pierL + 0.05f; x < pierR; x += 0.3f)
            {
                near.Rect(x, -0.3f, x + 0.05f, 0.2f);
                near.Rect(x, 0.29f, x + 0.022f, 0.4f);
            }
            near.Rect(pierL, 0.38f, pierR, 0.405f);
            near.Line(new Vector2(pierL + 0.05f, 0.0f), new Vector2(pierL + 0.35f, 0.2f), 0.03f);
            foreach (float x in new[] { 0.9f, 2.6f, 4.3f })
            {
                near.Rect(x, 0.29f, x + 0.03f, 0.8f);
                near.Rect(x, 0.77f, x + 0.14f, 0.8f);
                nearLights.Circle(new Vector2(x + 0.13f, 0.75f), 0.035f, 12);
            }
            Layer("PortNear", near, theme.silhouetteNear, 62f, 0f, 0f);
            if (theme.lightsOn) Layer("PortNearLights", nearLights, theme.lights, 61.8f, 0f, 0f);

            // ---- Water: animated shader plane from the quay wall out to the horizon ----
            float waterNear = BoardLayout.QuayFarZ;
            float waterDepth = WaterEdgeZ - waterNear;
            var waterMat = WaterMaterial(theme, waterNear + 0.42f); // foam starts where the quay wall stops hiding the water
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
        }

        /// <summary>The quay with the holding bay: slabs, coping, bollards at the water's edge, and the quay crane.</summary>
        private static void BuildQuay(Transform root, Palette pal, SkyTheme theme, Camera cam, float halfW, GameMode mode)
        {
            float quayDepth = BoardLayout.QuayFarZ - BoardLayout.QuayNearZ;
            Shapes.Box("Quay", root, new Vector3(0, -0.05f, BoardLayout.QuayNearZ + quayDepth * 0.5f), new Vector3(40f, 0.3f, quayDepth), ConcreteJoint);
            if (mode == GameMode.Load)
                Shapes.Box("QuayEdge", root, new Vector3(0, -0.1f, BoardLayout.QuayFarZ + 0.06f), new Vector3(40f, 0.4f, 0.14f), Palette.Darken(ConcreteJoint, 0.18f), false, false);
            // Slabs line up with the 5-slot bay (1.25 apart) so each bay slot sits on its own pair of slabs.
            const float SlabPitch = 1.25f, Joint = 0.035f, CopingDepth = 0.2f;
            float slabNear = BoardLayout.QuayNearZ + 0.02f, slabFar = BoardLayout.QuayFarZ - CopingDepth;
            float slabRow = (slabFar - slabNear) * 0.5f;
            var speckle = ConcreteTexture();
            int sxStart = Mathf.FloorToInt(-halfW / SlabPitch) - 1, sxEnd = Mathf.CeilToInt(halfW / SlabPitch) + 1;
            for (int ix = sxStart; ix <= sxEnd; ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    var tone = ConcreteTones[((ix * 7 + iz * 3) % ConcreteTones.Length + ConcreteTones.Length) % ConcreteTones.Length];
                    float cx = (ix + 0.5f) * SlabPitch, cz = slabNear + slabRow * (iz + 0.5f);
                    Shapes.MakeWith("Slab", Shapes.CubeMesh, Shapes.MatTex(tone, speckle), root, new Vector3(cx, 0.099f, cz),
                        new Vector3(SlabPitch - Joint, 0.01f, slabRow - Joint), false);
                }
            // Coping (the raised kerb along the far edge) with a painted yellow line. Bollards stand at the water's edge:
            // on the coping in Load (ships beyond), along the near edge in Unload (the ship is moored below the quay).
            float copingZ = BoardLayout.QuayFarZ - CopingDepth * 0.5f + 0.01f;
            Shapes.Box("Coping", root, new Vector3(0, 0.11f, copingZ), new Vector3(40f, 0.04f, CopingDepth), ConcreteTones[2], false, false);
            Shapes.Box("CopingPaint", root, new Vector3(0, 0.131f, BoardLayout.QuayFarZ - 0.035f), new Vector3(40f, 0.004f, 0.05f), pal.yardLine, true, false);
            bool unload = mode == GameMode.Unload;
            for (float bx = unload ? -4.9f : -4.2f; bx <= (unload ? 4.95f : 3.0f); bx += 1.4f)
            {
                var bp = unload ? new Vector3(bx, 0.1f, BoardLayout.QuayNearZ + 0.13f) : new Vector3(bx, 0.13f, copingZ);
                Shapes.Cylinder("Bollard", root, bp + new Vector3(0, 0.09f, 0), new Vector3(0.14f, 0.18f, 0.14f), Palette.Hex("4A3A33"));
                Shapes.Cylinder("BollardCap", root, bp + new Vector3(0, 0.2f, 0), new Vector3(0.21f, 0.045f, 0.21f), Palette.Hex("5C4A42"));
            }

            // ---- Quay crane: a rail-mounted portal crane at the right edge, boom reaching left over the quay ----
            // Kept low so it never covers the ships; placed just inside the screen edge.
            float screenHalfW = cam.orthographicSize * Mathf.Max(cam.aspect, 0.3f);
            // Unload leaves it out: the boom would reach over the third loading bay and hide that truck.
            if (mode == GameMode.Load)
                BuildQuayCrane(root, pal, theme, cam, new Vector3(Mathf.Min(4.45f, screenHalfW - 0.3f), 0.13f, 1.82f));
        }

        /// <summary>Load: the container yard on the concrete apron.</summary>
        private static void BuildYard(Transform root, Palette pal, BoardLayout layout, float halfW)
        {
            const float SlabPitch = 1.25f;
            var speckle = ConcreteTexture();
            int sxStart = Mathf.FloorToInt(-halfW / SlabPitch) - 1, sxEnd = Mathf.CeilToInt(halfW / SlabPitch) + 1;
            float yardW = layout.Width * layout.CellSize + 0.5f;
            float yardTop = BoardLayout.YardTopZ, yardBottom = layout.YardBottomZ - 0.3f;
            Shapes.Box("Apron", root, new Vector3(0, -0.2f, (yardTop + yardBottom) * 0.5f - 2f), new Vector3(40f, 0.2f, yardTop - yardBottom + 4f),
                Palette.Darken(ConcreteJoint, 0.06f), false, false);
            // Concrete slabs beside the yard, on the quay's 1.25 grid, a step darker than the bay so the bay stays the focus.
            {
                const float Pitch = 1.25f, Gap = 0.035f;
                float edge = yardW * 0.5f + 0.12f;
                int rows = Mathf.CeilToInt((yardTop - (yardBottom - 2.5f)) / Pitch);
                for (int ix = sxStart; ix <= sxEnd; ix++)
                    for (int iz = 0; iz < rows; iz++)
                    {
                        float x0 = ix * Pitch, x1 = x0 + Pitch;
                        // Clip slabs at the kerb so none run under the yard.
                        if (x1 > -edge && x0 < edge)
                        {
                            if (x0 < -edge) x1 = -edge; else if (x1 > edge) x0 = edge; else continue;
                        }
                        if (x1 - x0 < 0.08f) continue;
                        float z1 = yardTop - 0.02f - iz * Pitch, z0 = z1 - Pitch;
                        var tone = Palette.Darken(ConcreteTones[((ix * 5 + iz * 3) % ConcreteTones.Length + ConcreteTones.Length) % ConcreteTones.Length], 0.07f);
                        Shapes.MakeWith("SideSlab", Shapes.CubeMesh, Shapes.MatTex(tone, speckle), root, new Vector3((x0 + x1) * 0.5f, -0.1f, (z0 + z1) * 0.5f),
                            new Vector3(x1 - x0 - Gap, 0.01f, Pitch - Gap), false);
                    }
            }
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
        }

        // Warm light concrete (golden hour), a few slab tones so the paving doesn't look printed, darker joints.
        private static readonly Color[] ConcreteTones = { Palette.Hex("CFC8BC"), Palette.Hex("C8C1B5"), Palette.Hex("D4CDC2"), Palette.Hex("C4BDB1") };
        private static readonly Color ConcreteJoint = Palette.Hex("9C958A");
        private static Texture2D _concrete;

        /// <summary>Soft concrete speckle (white-ish, multiplied by the slab tone).</summary>
        private static Texture2D ConcreteTexture()
        {
            if (_concrete) return _concrete;
            const int W = 96, H = 64;
            var rng = new System.Random(77);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = 0.965f + (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.05f + (float)rng.NextDouble() * 0.025f;
                    double r = rng.NextDouble();
                    if (r < 0.018) n -= 0.09f; else if (r < 0.03) n += 0.03f;
                    // Slight darkening at the slab edges (worn arrises).
                    float edge = Mathf.Min(Mathf.Min(x, W - 1 - x) / 4f, Mathf.Min(y, H - 1 - y) / 4f);
                    n *= Mathf.Lerp(0.93f, 1f, Mathf.Clamp01(edge));
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(n * 255f), 0, 255);
                    px[y * W + x] = new Color32(b, b, b, 255);
                }
            _concrete = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Concrete", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            _concrete.SetPixels32(px);
            _concrete.Apply(true, true);
            return _concrete;
        }

        /// <summary>A thin bar between two points (crane bracing).</summary>
        private static void Strut(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Color color)
        {
            var go = Shapes.Box(name, parent, (a + b) * 0.5f, new Vector3(thickness, Vector3.Distance(a, b), thickness), color, false, false);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }

        /// <summary>
        /// Portal crane on rails: four braced legs on bogies, a rounded machinery house with the operator cab, a lattice
        /// boom reaching left with a trolley and hook, and an A-frame with tie rods and a warning lamp. b = rail level under the portal centre.
        /// </summary>
        private static void BuildQuayCrane(Transform root, Palette pal, SkyTheme theme, Camera cam, Vector3 b)
        {
            var crane = new GameObject("QuayCrane").transform;
            crane.SetParent(root, false);
            crane.localPosition = b;
            Color Y = pal.crane, YD = pal.craneDark, dark = Palette.Hex("3A3133"), steel = Palette.Hex("6E6862"), white = pal.cabin;
            Color cabGlass = theme.lightsOn ? theme.lights : pal.cabinWindow;
            const float LegX = 0.34f, LegZ = 0.56f, PortalY = 1.12f;

            // Rails running off the right edge, under the bogies.
            foreach (float z in new[] { -LegZ, LegZ })
                Shapes.Box("Rail", crane, new Vector3(1.2f, 0.005f, z), new Vector3(3.6f, 0.012f, 0.06f), steel, false, false);

            foreach (float z in new[] { -LegZ, LegZ })
            {
                // Bogie (rounded, dark) with a yellow/black hazard band, carrying two legs.
                Shapes.MakeWith("Bogie", HullMesh.Block("CraneBogie", 0.5f, 0.1f, 0.13f, 0.06f, 0.03f), Shapes.Mat(Palette.Hex("4A4440")),
                    crane, new Vector3(0, 0.0f, z), Vector3.one);
                for (int i = 0; i < 5; i++)
                    Shapes.Box("Hazard", crane, new Vector3(-0.36f + i * 0.18f, 0.07f, z - 0.103f), new Vector3(0.09f, 0.06f, 0.008f), i % 2 == 0 ? pal.yardLine : dark, true, false);
                foreach (float x in new[] { -LegX, LegX })
                    Shapes.MakeWith("Leg", HullMesh.Block("CraneLeg", 0.055f, 0.055f, PortalY - 0.12f, 0.03f, 0.0f), Shapes.Mat(z < 0 ? Y : YD),
                        crane, new Vector3(x, 0.12f, z), Vector3.one);
                // Portal beam joining the legs, and an X brace between them.
                Shapes.MakeWith("PortalBeam", HullMesh.Block("CranePortal", LegX + 0.06f, 0.07f, 0.14f, 0.05f, 0.03f), Shapes.Mat(Y),
                    crane, new Vector3(0, PortalY - 0.06f, z), Vector3.one);
                Strut("Brace", crane, new Vector3(-LegX, 0.25f, z), new Vector3(LegX, PortalY - 0.08f, z), 0.035f, YD);
                Strut("Brace", crane, new Vector3(LegX, 0.25f, z), new Vector3(-LegX, PortalY - 0.08f, z), 0.035f, YD);
            }
            // Side girders (front to back) on top of the portal.
            foreach (float x in new[] { -LegX, LegX })
                Shapes.Box("SideGirder", crane, new Vector3(x, PortalY + 0.02f, 0), new Vector3(0.1f, 0.1f, LegZ * 2f + 0.1f), YD);

            // Machinery house with a darker roof; white operator cab under the boom root, windows facing the player.
            float houseY = PortalY + 0.07f;
            Shapes.MakeWith("House", HullMesh.Block("CraneHouse", 0.34f, 0.32f, 0.3f, 0.09f, 0.06f), Shapes.Mat(Y), crane, new Vector3(0.05f, houseY, 0.08f), Vector3.one);
            Shapes.MakeWith("HouseRoof", HullMesh.Block("CraneRoof", 0.36f, 0.34f, 0.04f, 0.09f, 0.03f), Shapes.Mat(Palette.Darken(Y, 0.07f)), crane, new Vector3(0.05f, houseY + 0.28f, 0.08f), Vector3.one);
            for (int i = 0; i < 3; i++)
                Shapes.Box("Louvre", crane, new Vector3(0.14f + i * 0.08f, houseY + 0.16f, 0.08f - 0.325f), new Vector3(0.05f, 0.12f, 0.01f), YD, false, false);
            Shapes.MakeWith("Cab", HullMesh.Block("CraneCab", 0.15f, 0.15f, 0.26f, 0.06f, 0.05f), Shapes.Mat(white), crane, new Vector3(-0.42f, houseY - 0.02f, -0.2f), Vector3.one);
            Shapes.MakeWith("CabWindow", HullMesh.Block("CraneCabWin", 0.11f, 0.01f, 0.1f, 0.01f, 0.01f), Shapes.Mat(cabGlass, true),
                crane, new Vector3(-0.42f, houseY + 0.11f, -0.355f), Vector3.one, false);
            Shapes.MakeWith("CabWindowSide", HullMesh.Block("CraneCabWinS", 0.01f, 0.09f, 0.1f, 0.01f, 0.01f), Shapes.Mat(cabGlass, true),
                crane, new Vector3(-0.575f, houseY + 0.11f, -0.2f), Vector3.one, false);

            // Lattice boom: top and bottom chords with zig-zag web members, reaching left over the quay.
            float boomZ = 0.08f, root0 = -0.35f, tip = -2.15f, yBot = houseY + 0.18f, yTop = houseY + 0.42f;
            Shapes.Box("BoomTop", crane, new Vector3((root0 + tip) * 0.5f, yTop, boomZ), new Vector3(root0 - tip, 0.06f, 0.16f), Y);
            Shapes.Box("BoomBottom", crane, new Vector3((root0 + tip) * 0.5f, yBot, boomZ), new Vector3(root0 - tip, 0.07f, 0.18f), Y);
            int bays = 6;
            for (int i = 0; i < bays; i++)
            {
                float x0 = Mathf.Lerp(root0, tip, i / (float)bays), x1 = Mathf.Lerp(root0, tip, (i + 1) / (float)bays);
                bool up = i % 2 == 0;
                Strut("Web", crane, new Vector3(x0, up ? yBot : yTop, boomZ - 0.06f), new Vector3(x1, up ? yTop : yBot, boomZ - 0.06f), 0.03f, YD);
            }
            Shapes.MakeWith("BoomTip", HullMesh.Block("CraneTip", 0.08f, 0.1f, 0.3f, 0.04f, 0.04f), Shapes.Mat(Y), crane, new Vector3(tip - 0.02f, yBot - 0.05f, boomZ), Vector3.one);

            // A-frame above the house with tie rods to the boom tip, and an amber warning lamp on the apex.
            var apex = new Vector3(0.05f, yTop + 0.55f, boomZ);
            Strut("AFrame", crane, new Vector3(-0.25f, yTop, boomZ), apex, 0.06f, Y);
            Strut("AFrame", crane, new Vector3(0.3f, houseY + 0.3f, boomZ), apex, 0.06f, Y);
            Strut("TieRod", crane, apex, new Vector3(tip + 0.02f, yTop + 0.02f, boomZ - 0.04f), 0.018f, dark);
            Strut("TieRod", crane, apex, new Vector3(tip + 0.02f, yTop + 0.02f, boomZ + 0.04f), 0.018f, dark);
            Shapes.Cylinder("Lamp", crane, apex + new Vector3(0, 0.06f, 0), new Vector3(0.07f, 0.04f, 0.07f), pal.lighthouseLamp, true, false);

            // Trolley near the tip, twin falls and a small yellow hook block.
            float tx = tip + 0.38f;
            Shapes.MakeWith("Trolley", HullMesh.Block("CraneTrolley", 0.12f, 0.12f, 0.09f, 0.04f, 0.02f), Shapes.Mat(dark), crane, new Vector3(tx, yBot - 0.12f, boomZ), Vector3.one, false);
            foreach (float dx in new[] { -0.04f, 0.04f })
                Shapes.Box("Fall", crane, new Vector3(tx + dx, yBot - 0.33f, boomZ), new Vector3(0.016f, 0.42f, 0.016f), dark, false, false);
            Shapes.MakeWith("HookBlock", HullMesh.Block("CraneHook", 0.09f, 0.06f, 0.1f, 0.04f, 0.03f), Shapes.Mat(Y), crane, new Vector3(tx, yBot - 0.64f, boomZ), Vector3.one, false);
            Shapes.Box("Hook", crane, new Vector3(tx, yBot - 0.68f, boomZ), new Vector3(0.03f, 0.07f, 0.03f), dark, false, false);

            // Like the ships: keep the camera-facing sides near full colour so the crane stays a bright, clean orange.
            var shade = new Vector4(1f, 0.92f, 0.82f, 0.85f);
            foreach (var r in crane.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = Shapes.Reshade(r.sharedMaterial, shade);

            // After dark: floodlights under the portal light the crane's own patch of quay (kept off the bay so no slot
            // looks highlighted), and the warning lamp gets a halo.
            if (theme.night >= 0.5f)
            {
                foreach (float fz in new[] { -LegZ, LegZ })
                    Shapes.Box("Floodlight", crane, new Vector3(0, PortalY - 0.15f, fz - 0.08f), new Vector3(0.14f, 0.05f, 0.06f), theme.lights, true, false);
                Glow("FloodPool", crane, new Vector3(0.05f, 0.007f, -0.1f), new Vector2(1.9f, 1.9f), theme.lights, 0.32f, Quaternion.Euler(90, 0, 0));
                Glow("LampHalo", crane, apex + new Vector3(0, 0.06f, -0.05f), new Vector2(0.42f, 0.42f), pal.lighthouseLamp, 0.55f, cam.transform.rotation, 2.2f, worldRotation: true);
            }
        }

        private static readonly Dictionary<(Color, float), Material> GlowMaterials = new Dictionary<(Color, float), Material>();

        /// <summary>Soft additive light (Dockside/Glow): a radial falloff on a quad. strength = how much light it adds at the centre.</summary>
        public static GameObject Glow(string name, Transform parent, Vector3 pos, Vector2 size, Color color, float strength, Quaternion rotation, float falloff = 1.6f, bool worldRotation = false)
        {
            var key = (new Color(color.r, color.g, color.b, strength), falloff);
            if (!GlowMaterials.TryGetValue(key, out var m) || !m)
            {
                var shader = Shader.Find("Dockside/Glow");
                if (shader == null) return null;      // shader missing from the build: no glow rather than a pink quad
                m = new Material(shader) { name = "Glow (runtime)" };
                m.SetColor("_BaseColor", key.Item1);
                m.SetFloat("_Falloff", falloff);
                GlowMaterials[key] = m;
            }
            var go = Shapes.MakeWith(name, Shapes.QuadMesh, m, parent, pos, new Vector3(size.x, size.y, 1f), false);
            if (worldRotation) go.transform.rotation = rotation; else go.transform.localRotation = rotation;
            return go;
        }

        private static readonly Dictionary<string, Texture2D> SkyCache = new Dictionary<string, Texture2D>();

        /// <summary>Vertical sky gradient (horizon to zenith) with a radial sun/moon glow, sized to the visible sky.</summary>
        private static Texture2D SkyTexture(SkyTheme t, float width, float height)
        {
            string key = $"{t.phase}:{width:0.0}x{height:0.0}";
            if (SkyCache.TryGetValue(key, out var cached) && cached) return cached;
            const int tw = 128, th = 256;
            var px = new Color[tw * th];
            float glowR1 = t.moon ? 1.1f : 1.7f, glowR2 = t.moon ? 2.6f : 4.2f, glowA = t.moon ? 0.45f : 0.8f;
            for (int y = 0; y < th; y++)
            {
                float f = y / (th - 1f);
                float wy = -0.5f + f * height;                 // world units above the horizon
                float g = Mathf.Clamp01(wy / Mathf.Max(3.2f, height - 0.8f));
                Color c = g < 0.3f ? Color.Lerp(t.horizon, t.mid, g / 0.3f)
                        : g < 0.65f ? Color.Lerp(t.mid, t.high, (g - 0.3f) / 0.35f)
                        : Color.Lerp(t.high, t.zenith, (g - 0.65f) / 0.35f);
                for (int x = 0; x < tw; x++)
                {
                    float wx = (x / (tw - 1f) - 0.5f) * width;
                    float d = Vector2.Distance(new Vector2(wx, wy), new Vector2(t.sunX, t.sunV));
                    float glow = Mathf.Exp(-Mathf.Pow(d / glowR1, 2f)) * glowA + Mathf.Exp(-Mathf.Pow(d / glowR2, 2f)) * 0.3f;
                    px[y * tw + x] = Color.Lerp(c, t.sunGlow, Mathf.Clamp01(glow));
                }
            }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false, false)
            {
                name = "Sky_" + t.phase,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            SkyCache[key] = tex;
            return tex;
        }

        private static Material _water;

        private static Material WaterMaterial(SkyTheme t, float nearZ)
        {
            if (_water == null)
            {
                var shader = Shader.Find("Dockside/Water");
                _water = shader != null ? new Material(shader) : new Material(Shapes.Mat(t.waterDeep, true));
                _water.name = "Water (runtime)";
            }
            _water.SetColor("_Shallow", t.waterShallow);
            _water.SetColor("_Deep", t.waterDeep);
            _water.SetColor("_Horizon", t.waterHorizon);
            _water.SetColor("_Highlight", t.waterHighlight);
            _water.SetColor("_Glitter", t.waterGlitter);
            _water.SetColor("_Foam", t.foam);
            _water.SetVector("_Params", new Vector4(nearZ, WaterEdgeZ, t.sunX, 1f));
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

    /// <summary>Constant rotation about the view axis (windmill sails).</summary>
    public class Spin : MonoBehaviour
    {
        private float _degPerSec;
        public void Init(float degreesPerSecond) => _degPerSec = degreesPerSecond;
        private void Update() => transform.Rotate(0f, 0f, _degPerSec * Time.deltaTime, Space.Self);
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

    /// <summary>
    /// The holding bay: painted slot pads with white dashed outlines on the concrete, and a "HOLDING BAY · used / capacity"
    /// label that turns red and pulses when only one slot is left (a full bay loses the level).
    /// </summary>
    public class BufferSlotsView : MonoBehaviour
    {
        private Palette _pal;
        private readonly List<GameObject> _slots = new List<GameObject>();
        private TextMesh _label;
        private int _capacity, _used;

        public void Init(Palette pal)
        {
            _pal = pal;
            var go = new GameObject("BayLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.106f, BoardLayout.BufferZ + 0.6f);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            _label = go.AddComponent<TextMesh>();
            _label.font = UiFactory.FontBold;
            go.GetComponent<MeshRenderer>().sharedMaterial = _label.font.material;
            _label.fontSize = 96;
            _label.characterSize = 0.037f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = LabelColor.linear;
        }

        private static readonly Color LabelColor = Palette.Hex("4A3F38"), WarnColor = Palette.Hex("B0281F");
        private bool _warn;

        private void Update()
        {
            if (!_label) return;
            float s = _warn ? 1f + Mathf.Sin(Time.time * 6f) * 0.045f : 1f;
            _label.transform.localScale = new Vector3(s, s, 1f);
        }

        public void SetCapacity(int capacity)
        {
            foreach (var s in _slots) Destroy(s);
            _slots.Clear();
            _capacity = capacity;
            float pitch = BoardLayout.BufferPitch(capacity);
            float w = pitch - 0.14f, d = 0.92f;
            var outline = new FlatMesh().DashedRoundRect(w - 0.06f, d - 0.06f, 0.11f, 0.04f, 0.11f, 0.07f).Build("BaySlot" + capacity);
            var pad = new FlatMesh().Poly(HullMesh.RoundedRect(w * 0.5f, d * 0.5f, 0.14f)).Build("BayPad" + capacity);
            for (int i = 0; i < capacity; i++)
            {
                var p = BoardLayout.BufferPosition(i, 1, capacity);
                var slot = Shapes.MakeWith($"Slot_{i}", pad, Shapes.Mat(Palette.Hex("B4ADA1")), transform, new Vector3(p.x, 0.105f, p.z), Vector3.one, false);
                slot.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Shapes.MakeWith("Outline", outline, Shapes.Mat(Palette.Hex("F7F4EE")), slot.transform, new Vector3(0, 0, -0.002f), Vector3.one, false);
                _slots.Add(slot);
            }
            Refresh();
        }

        public void SetUsed(int used) { _used = used; Refresh(); }

        private void Refresh()
        {
            _warn = _capacity > 1 && _used >= _capacity - 1;
            if (!_label) return;
            _label.text = $"HOLDING BAY  ·  {_used} / {_capacity}";
            _label.color = (_warn ? WarnColor : LabelColor).linear;   // TextMesh vertex colours bypass the sRGB conversion
        }
    }
}
