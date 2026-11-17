using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>Builds the Golden Hour Harbour backdrop from simple shapes: sky, sun, far-shore town, lighthouse, water, quay, crane, yard.</summary>
    public static class EnvironmentBuilder
    {
        public static Transform Build(Transform parent, BoardLayout layout, Palette pal, Camera cam)
        {
            var root = new GameObject("Environment").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(12345);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ---- Sky: camera background is the upper sky; a glow band sits low behind the horizon ----
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = pal.skyTop;
            Vector3 up = cam.transform.up, fwd = cam.transform.forward;
            Quaternion facing = Quaternion.LookRotation(fwd, up);
            // Place backdrop items by screen height (v) and depth so they sit right for any camera pitch.
            Vector3 Backdrop(float sx, float v, float depth) => new Vector3(sx, 0, 0) + up * v + fwd * depth;
            float horizonV = Vector3.Dot(new Vector3(0, 0, BoardLayout.WaterFarZ + 2.0f), up);
            var glow = Shapes.Make("HorizonGlow", Shapes.QuadMesh, root, Backdrop(0, horizonV + 0.8f, 70f), new Vector3(80f, 4.2f, 1f), pal.skyBottom, true, false);
            glow.transform.rotation = facing;
            var glow2 = Shapes.Make("HorizonGlowSoft", Shapes.QuadMesh, root, Backdrop(0, horizonV + 3.6f, 72f), new Vector3(80f, 2.6f, 1f),
                Color.Lerp(pal.skyBottom, pal.skyTop, 0.5f), true, false);
            glow2.transform.rotation = facing;

            // ---- Sun setting behind the far shore ----
            var sun = Shapes.Cylinder("Sun", root, Backdrop(2.3f, horizonV + 0.6f, 60f), new Vector3(2.6f, 0.02f, 2.6f), pal.sun, true, false);
            sun.transform.rotation = Quaternion.FromToRotation(Vector3.up, -fwd);

            // ---- Water ----
            float waterNear = BoardLayout.WaterNearZ, waterFar = BoardLayout.WaterFarZ;
            float waterDepth = (waterFar + 1.6f) - waterNear;
            Shapes.Box("Water", root, new Vector3(0, -0.45f, waterNear + waterDepth * 0.5f), new Vector3(40f, 0.2f, waterDepth), pal.water, false, false);
            var waves = new GameObject("Waves").transform;
            waves.SetParent(root, false);
            for (int i = 0; i < 26; i++)
            {
                var pos = new Vector3(R(-5.5f, 5.5f), -0.33f, R(waterNear + 0.4f, waterFar - 0.2f));
                // Keep waves out from under the berths so ships stay readable.
                if (Mathf.Abs(pos.z - BoardLayout.ShipZ) < 0.75f) pos.z += pos.z > BoardLayout.ShipZ ? 0.8f : -0.8f;
                var w = Shapes.Box("Wave", waves, pos, new Vector3(R(0.35f, 0.75f), 0.02f, 0.07f), pal.waterHighlight, true, false);
                w.AddComponent<WaveBob>().Init(R(0f, 10f));
            }

            // ---- Far shore with the town and lighthouse ----
            float shoreZ = waterFar + 0.4f;
            Shapes.Box("FarShore", root, new Vector3(0, -0.15f, shoreZ + 0.8f), new Vector3(40f, 0.5f, 1.8f), Palette.Darken(pal.quay, 0.08f), false, false);
            float x = -6.5f;
            int k = 0;
            while (x < 6.5f)
            {
                float w = R(0.55f, 0.95f), h = R(0.5f, 1.25f), d = R(0.5f, 0.8f);
                var col = pal.town[k++ % pal.town.Length];
                float z = shoreZ + R(0.3f, 1.0f);
                if (x + w * 0.5f > 1.6f && x - w * 0.5f < 3.0f) { x += w + 0.05f; continue; } // leave room for the lighthouse
                Shapes.Box("House", root, new Vector3(x + w * 0.5f, 0.1f + h * 0.5f, z), new Vector3(w, h, d), col, false, false);
                if (rng.NextDouble() < 0.75)
                {
                    var roof = Shapes.Prism("Roof", root, new Vector3(x + w * 0.5f, 0.1f + h + 0.17f, z), new Vector3(w + 0.1f, 0.34f, d + 0.06f), pal.roof, false, false);
                    roof.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    roof.transform.localScale = new Vector3(w + 0.1f, d + 0.06f, 0.34f);
                }
                Shapes.Box("Window", root, new Vector3(x + w * 0.5f, 0.1f + h * 0.55f, z - d * 0.5f - 0.01f), new Vector3(0.14f, 0.16f, 0.02f), pal.lighthouseLamp, true, false);
                x += w + R(0.02f, 0.12f);
            }
            var lh = new Vector3(2.3f, 0.1f, shoreZ + 0.5f);
            for (int i = 0; i < 5; i++)
            {
                float t = i / 5f;
                Shapes.Cylinder("LighthouseBand", root, lh + new Vector3(0, 0.22f + i * 0.4f, 0),
                    new Vector3(Mathf.Lerp(0.62f, 0.42f, t), 0.4f, Mathf.Lerp(0.62f, 0.42f, t)),
                    i % 2 == 0 ? pal.lighthouseWhite : pal.lighthouseRed, false, false);
            }
            Shapes.Cylinder("LighthouseLamp", root, lh + new Vector3(0, 2.25f, 0), new Vector3(0.46f, 0.3f, 0.46f), pal.lighthouseLamp, true, false);
            Shapes.Cylinder("LighthouseCap", root, lh + new Vector3(0, 2.5f, 0), new Vector3(0.55f, 0.16f, 0.55f), pal.lighthouseRed, false, false);

            // ---- Quay (holding bay) ----
            float quayDepth = BoardLayout.QuayFarZ - BoardLayout.QuayNearZ;
            Shapes.Box("Quay", root, new Vector3(0, -0.05f, BoardLayout.QuayNearZ + quayDepth * 0.5f), new Vector3(40f, 0.3f, quayDepth), pal.quay);
            Shapes.Box("QuayEdge", root, new Vector3(0, -0.1f, BoardLayout.QuayFarZ + 0.06f), new Vector3(40f, 0.4f, 0.14f), pal.quayEdge, false, false);
            for (float bx = -4.2f; bx <= 4.3f; bx += 1.4f)
                Shapes.Cylinder("Bollard", root, new Vector3(bx, 0.2f, BoardLayout.QuayFarZ - 0.15f), new Vector3(0.16f, 0.22f, 0.16f), Palette.Hex("4A3A33"));

            // ---- Crane (right side of the quay) ----
            var craneBase = new Vector3(4.05f, 0f, 2.2f);
            Shapes.Box("CraneLegA", root, craneBase + new Vector3(-0.2f, 1.3f, 0), new Vector3(0.14f, 2.6f, 0.14f), pal.crane);
            Shapes.Box("CraneLegB", root, craneBase + new Vector3(0.2f, 1.3f, 0), new Vector3(0.14f, 2.6f, 0.14f), pal.craneDark);
            Shapes.Box("CraneCab", root, craneBase + new Vector3(0, 2.55f, 0), new Vector3(0.55f, 0.4f, 0.5f), pal.crane);
            Shapes.Box("CraneArm", root, craneBase + new Vector3(-1.2f, 2.85f, 0), new Vector3(2.9f, 0.16f, 0.2f), pal.crane);
            Shapes.Box("CraneCable", root, craneBase + new Vector3(-2.3f, 2.4f, 0), new Vector3(0.03f, 0.8f, 0.03f), Palette.Hex("4A3A33"), false, false);
            Shapes.Box("CraneHook", root, craneBase + new Vector3(-2.3f, 1.95f, 0), new Vector3(0.22f, 0.08f, 0.12f), Palette.Hex("4A3A33"), false, false);

            // ---- Yard ----
            float yardW = layout.Width * BoardLayout.Cell + 0.5f;
            float yardTop = BoardLayout.QuayNearZ, yardBottom = layout.YardBottomZ - 0.3f;
            Shapes.Box("Apron", root, new Vector3(0, -0.2f, (yardTop + yardBottom) * 0.5f - 2f), new Vector3(40f, 0.2f, yardTop - yardBottom + 4f),
                Palette.Darken(pal.quay, 0.12f), false, false);
            Shapes.Box("Yard", root, new Vector3(0, -0.08f, (yardTop + yardBottom) * 0.5f), new Vector3(yardW, 0.16f, yardTop - yardBottom), pal.yard);
            for (int i = 0; i <= layout.Width; i++)
            {
                float lx = (i - layout.Width * 0.5f) * BoardLayout.Cell;
                Shapes.Box("YardLine", root, new Vector3(lx, 0.005f, (yardTop + yardBottom) * 0.5f), new Vector3(0.05f, 0.01f, yardTop - yardBottom - 0.1f),
                    pal.yardLine, true, false);
            }
            return root;
        }
    }

    /// <summary>Gentle drift for wave highlights.</summary>
    public class WaveBob : MonoBehaviour
    {
        private Vector3 _start;
        private float _phase;
        public void Init(float phase) { _phase = phase; _start = transform.localPosition; }
        private void Update()
        {
            float t = Time.time * 0.6f + _phase;
            transform.localPosition = _start + new Vector3(Mathf.Sin(t) * 0.25f, 0, Mathf.Cos(t * 0.7f) * 0.05f);
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
