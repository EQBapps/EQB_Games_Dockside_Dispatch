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

        public static Transform Build(Transform parent, BoardLayout layout, Palette pal, Camera cam, int levelNumber)
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
            Layer("PortNear", near, theme.silhouetteNear, 62f);
            if (theme.lightsOn) Layer("PortNearLights", nearLights, theme.lights, 61.8f);

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

    /// <summary>The holding bay: white dashed slot outlines on the quay and a "HOLDING BAY · used / capacity" label.</summary>
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
            _label.characterSize = 0.033f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = Palette.Hex("6B4126");
        }

        public void SetCapacity(int capacity)
        {
            foreach (var s in _slots) Destroy(s);
            _slots.Clear();
            _capacity = capacity;
            float pitch = BoardLayout.BufferPitch(capacity);
            float w = pitch - 0.14f, d = 0.92f;
            var outline = new FlatMesh().DashedRoundRect(w, d, 0.13f, 0.045f, 0.11f, 0.075f).Build("BaySlot");
            var color = Color.Lerp(_pal.quay, Color.white, 0.78f);
            for (int i = 0; i < capacity; i++)
            {
                var p = BoardLayout.BufferPosition(i, 1, capacity);
                var slot = Shapes.MakeWith($"Slot_{i}", outline, Shapes.Mat(color, true), transform, new Vector3(p.x, 0.105f, p.z), Vector3.one, false);
                slot.transform.localRotation = Quaternion.Euler(90, 0, 0);
                _slots.Add(slot);
            }
            Refresh();
        }

        public void SetUsed(int used) { _used = used; Refresh(); }

        private void Refresh()
        {
            if (_label) _label.text = $"HOLDING BAY  ·  {_used} / {_capacity}";
        }
    }
}
