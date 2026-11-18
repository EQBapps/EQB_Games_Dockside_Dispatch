using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Unload-mode scenery, seen from the port side of a container ship moored alongside the quay. The ship's open
    /// container bay is the board (where Load's yard is, so the camera, taps and animations are shared). Above it: the
    /// fendered quay wall, the quay with the holding bay, the truck lane where trucks wait to be loaded, a far lane where
    /// other trucks pass, the terminal's container stacks with an RTG crane and floodlight masts, and the port city of
    /// the day on the horizon (it changes every 5 levels, as in Load). Ship-to-shore cranes with raised booms frame the
    /// scene at both edges, clear of the play area.
    /// </summary>
    public static partial class EnvironmentBuilder
    {
        // Dock (world z), all relative to the quay so they follow BoardLayout.QuayShift.
        private static float LaneDividerZ => BoardLayout.QuayFarZ + 1.85f;
        private static float RoadFarZ => BoardLayout.QuayFarZ + 3.2f;
        public static float PassingLaneZ => (LaneDividerZ + RoadFarZ) * 0.5f;

        private static readonly Color Asphalt = Palette.Hex("5C5B62"), AsphaltWorn = Palette.Hex("66656C"), RoadPaint = Palette.Hex("F2EEE6");
        private static readonly Color YardGround = Palette.Hex("8D877E");
        private static readonly Color[] HullColors = { Palette.Hex("2E4C6D"), Palette.Hex("8C3A45"), Palette.Hex("24636A"), Palette.Hex("46505E") };
        /// <summary>Real-world container colours, deliberately muted so background boxes never read as puzzle pieces.</summary>
        private static readonly Color[] MutedBoxes =
        {
            Palette.Hex("B0605A"), Palette.Hex("5F7F96"), Palette.Hex("C2A266"), Palette.Hex("6F8E74"), Palette.Hex("9A948E"),
            Palette.Hex("D6CFC2"), Palette.Hex("8A6A5C"), Palette.Hex("4E6474"),
        };

        // =====================================================================
        // Dock: truck lanes, terminal stacks, cranes
        // =====================================================================
        private static void BuildDockside(Transform root, Palette pal, SkyTheme theme, Camera cam, BoardLayout layout, float halfW, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 up = cam.transform.up;
            float horizonV = Vector3.Dot(new Vector3(0, WaterY, WaterEdgeZ), up);
            float groundFarZ = horizonV / Mathf.Max(0.1f, up.z);   // ground at y = 0 meets the backdrop at the horizon line
            float w = halfW * 2f + 4f;
            float roadNear = BoardLayout.QuayFarZ;

            // ---- Truck lanes: loading lane (dashed bays) and the far lane where other trucks pass ----
            Shapes.Box("Road", root, new Vector3(0, -0.04f, (roadNear + RoadFarZ) * 0.5f), new Vector3(w, 0.08f, RoadFarZ - roadNear), Asphalt, false, false);
            Shapes.Box("PassingLane", root, new Vector3(0, -0.0015f, (LaneDividerZ + RoadFarZ) * 0.5f), new Vector3(w, 0.003f, RoadFarZ - LaneDividerZ - 0.05f), AsphaltWorn, false, false);
            for (float x = -halfW - 1f; x < halfW + 1f; x += 0.9f)
                Shapes.Box("LaneDash", root, new Vector3(x, 0.001f, LaneDividerZ), new Vector3(0.5f, 0.002f, 0.06f), RoadPaint, false, false);
            Shapes.Box("EdgeLine", root, new Vector3(0, 0.001f, RoadFarZ - 0.1f), new Vector3(w, 0.002f, 0.05f), pal.yardLine, false, false);
            var bayMesh = new FlatMesh().DashedRoundRect(TruckView.Length + 0.2f, 1.0f, 0.12f, 0.05f, 0.16f, 0.1f).Build("LoadingBay");
            for (int b = 0; b < layout.Berths; b++)
            {
                var bay = Shapes.MakeWith("LoadingBay", bayMesh, Shapes.Mat(pal.yardLine), root, new Vector3(layout.BerthX(b) - 0.04f, 0.003f, BoardLayout.TruckZ), Vector3.one, false);
                bay.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }

            // ---- Terminal yard: ground and two rows of container stacks, hazed so they sit behind the play area ----
            float yardNear = RoadFarZ + 0.05f;
            Shapes.Box("TerminalGround", root, new Vector3(0, -0.05f, (yardNear + groundFarZ) * 0.5f), new Vector3(w, 0.1f, groundFarZ - yardNear), YardGround, false, false);
            Shapes.Box("TerminalKerb", root, new Vector3(0, 0.02f, yardNear + 0.03f), new Vector3(w, 0.06f, 0.08f), ConcreteTones[3], false, false);
            // Low stacks (one or two tiers in front, one behind) so the port city on the horizon stays the backdrop.
            const float BoxScale = 0.66f;
            var rows = new[] { (z: yardNear + 0.6f, haze: 0.25f, maxTier: 2), (z: Mathf.Min(groundFarZ - 0.35f, yardNear + 1.45f), haze: 0.42f, maxTier: 1) };
            foreach (var row in rows)
            {
                for (float x = -halfW - 1.2f; x < halfW + 1.2f;)
                {
                    if (rng.NextDouble() < 0.22) { x += R(0.6f, 1.2f); continue; }      // aisle
                    int len = rng.NextDouble() < 0.45 ? 2 : 1;
                    var size = BoardLayout.ContainerScale(len) * BoxScale;
                    int tiers = rng.NextDouble() < 0.35 ? row.maxTier : 1;
                    for (int t = 0; t < tiers; t++)
                        foreach (float dz in new[] { -size.z * 0.52f, size.z * 0.52f })
                        {
                            var c = Color.Lerp(MutedBoxes[rng.Next(MutedBoxes.Length)], theme.haze, row.haze);
                            Shapes.MakeWith("StackBox", ContainerArt.Mesh(len), Shapes.MatTex(c, ContainerArt.Texture(len)), root,
                                new Vector3(x + size.x * 0.5f, size.y * (t + 0.5f), row.z + dz), size, false);
                        }
                    x += size.x + 0.05f;
                }
            }
            // High-mast floodlights.
            foreach (float mx in new[] { -halfW * 0.45f, halfW * 0.5f })
            {
                var mast = new GameObject("FloodMast").transform;
                mast.SetParent(root, false);
                mast.localPosition = new Vector3(mx, 0, yardNear + 0.25f);
                Color pole = Palette.Hex("5A5D66");
                Shapes.Cylinder("Pole", mast, new Vector3(0, 1.4f, 0), new Vector3(0.07f, 2.8f, 0.07f), pole, false, false);
                Shapes.Box("Frame", mast, new Vector3(0, 2.82f, -0.03f), new Vector3(0.5f, 0.06f, 0.06f), pole, false, false);
                for (int i = 0; i < 3; i++)
                    Shapes.Box("Lamp", mast, new Vector3((i - 1) * 0.17f, 2.74f, -0.06f), new Vector3(0.12f, 0.08f, 0.04f), theme.lightsOn ? theme.lights : Palette.Hex("DCD7CC"), theme.lightsOn, false);
                if (theme.night >= 0.5f)
                {
                    Glow("MastHalo", mast, new Vector3(0, 2.74f, -0.1f), new Vector2(0.9f, 0.9f), theme.lights, 0.45f, cam.transform.rotation, 2.2f, worldRotation: true);
                    Glow("MastPool", mast, new Vector3(0, 0.01f, -0.6f), new Vector2(3.2f, 2.0f), theme.lights, 0.2f, Quaternion.Euler(90, 0, 0));
                }
            }

            // ---- Ship-to-shore cranes at both edges, booms raised, legs straddling the truck lane ----
            float screenHalfW = cam.orthographicSize * Mathf.Max(cam.aspect, 0.3f);
            // Inner legs stay clear of the third truck (x 4.43) and the widest holding bay (x 3.73).
            float cx = Mathf.Max(screenHalfW - 0.55f, 5.1f);
            foreach (float side in new[] { -1f, 1f })
                BuildStsCrane(root, pal, theme, cam, new Vector3(side * cx, 0.1f, 0));

            // ---- The free lane: passing trucks, and puzzle trucks coming in and leaving (LaneTraffic) ----
            var traffic = new GameObject("LaneTraffic").AddComponent<LaneTraffic>();
            traffic.transform.SetParent(root, false);
            traffic.Init(halfW, pal, theme);
        }

        /// <summary>
        /// Ship-to-shore crane: four legs on rails (waterside on the quay, landside past the truck lane), a portal girder,
        /// backreach over the lanes, machinery house, operator cab, and the boom raised (as when a ship is berthing).
        /// </summary>
        private static void BuildStsCrane(Transform root, Palette pal, SkyTheme theme, Camera cam, Vector3 at)
        {
            var crane = new GameObject("StsCrane").transform;
            crane.SetParent(root, false);
            crane.localPosition = at;
            Color body = Color.Lerp(pal.crane, theme.haze, 0.12f), dark = Color.Lerp(pal.craneDark, theme.haze, 0.12f), white = pal.cabin;
            float zWater = BoardLayout.QuayNearZ + 0.2f, zLand = LaneDividerZ, portalY = 2.25f;
            const float HalfX = 0.5f;
            foreach (float sx in new[] { -HalfX, HalfX })
            {
                foreach (float z in new[] { zWater, zLand })
                {
                    Shapes.Box("Leg", crane, new Vector3(sx, portalY * 0.5f, z), new Vector3(0.12f, portalY, 0.12f), body, false, false);
                    Shapes.Box("Bogie", crane, new Vector3(sx, 0.05f, z), new Vector3(0.3f, 0.1f, 0.18f), Palette.Hex("3A3133"), false, false);
                }
                Strut("Brace", crane, new Vector3(sx, 0.9f, zWater), new Vector3(sx, portalY - 0.1f, (zWater + zLand) * 0.5f), 0.05f, dark);
                Strut("Brace", crane, new Vector3(sx, 0.9f, zLand), new Vector3(sx, portalY - 0.1f, (zWater + zLand) * 0.5f), 0.05f, dark);
            }
            // Portal ties across the legs (the waterside one X-braced: the crane's face as seen from the ship),
            // and the main girder running from the waterside to the backreach end.
            foreach (float z in new[] { zWater, zLand })
                Shapes.Box("PortalTie", crane, new Vector3(0, portalY, z), new Vector3(HalfX * 2f + 0.14f, 0.16f, 0.14f), body, false, false);
            Shapes.Box("SillBeam", crane, new Vector3(0, 1.05f, zWater), new Vector3(HalfX * 2f, 0.08f, 0.08f), body, false, false);
            Strut("XBrace", crane, new Vector3(-HalfX, 1.1f, zWater - 0.02f), new Vector3(HalfX, portalY - 0.08f, zWater - 0.02f), 0.045f, dark);
            Strut("XBrace", crane, new Vector3(HalfX, 1.1f, zWater - 0.02f), new Vector3(-HalfX, portalY - 0.08f, zWater - 0.02f), 0.045f, dark);
            float backEnd = zLand + 1.1f;
            foreach (float sx in new[] { -HalfX, HalfX })
                Shapes.Box("Girder", crane, new Vector3(sx, portalY + 0.2f, (zWater + backEnd) * 0.5f), new Vector3(0.12f, 0.2f, backEnd - zWater), body, false, false);
            // Machinery house on the girder, operator cab hanging under it at the waterside.
            Shapes.MakeWith("House", HullMesh.Block("StsHouse", 0.38f, 0.42f, 0.32f, 0.06f, 0.04f), Shapes.Mat(white), crane, new Vector3(0, portalY + 0.3f, zLand + 0.4f), Vector3.one, false);
            Shapes.Box("HouseBand", crane, new Vector3(0, portalY + 0.44f, zLand + 0.4f - 0.43f), new Vector3(0.7f, 0.06f, 0.01f), dark, false, false);
            Shapes.MakeWith("Cab", HullMesh.Block("StsCab", 0.16f, 0.15f, 0.22f, 0.05f, 0.03f), Shapes.Mat(white), crane, new Vector3(0, portalY - 0.2f, zWater + 0.35f), Vector3.one, false);
            Shapes.Box("CabWindow", crane, new Vector3(0, portalY - 0.08f, zWater + 0.35f - 0.155f), new Vector3(0.24f, 0.09f, 0.01f), theme.night >= 0.5f ? theme.lights : pal.cabinWindow, true, false);
            // Raised boom: hinged at the waterside top, standing up with a slight lean over the ship.
            var hinge = new Vector3(0, portalY + 0.3f, zWater);
            var tip = hinge + new Vector3(0, 3.4f, -0.5f);
            foreach (float sx in new[] { -0.24f, 0.24f })
                Strut("Boom", crane, hinge + new Vector3(sx, 0, 0), tip + new Vector3(sx * 0.6f, 0, 0), 0.1f, body);
            // Lattice: zig-zag web between the two chords.
            const int Bays = 7;
            for (int i = 0; i < Bays; i++)
            {
                var a = Vector3.Lerp(hinge, tip, i / (float)Bays);
                var b = Vector3.Lerp(hinge, tip, (i + 1) / (float)Bays);
                float wa = Mathf.Lerp(0.24f, 0.144f, i / (float)Bays), wb = Mathf.Lerp(0.24f, 0.144f, (i + 1) / (float)Bays);
                bool l = i % 2 == 0;
                Strut("BoomWeb", crane, a + new Vector3(l ? -wa : wa, 0, -0.03f), b + new Vector3(l ? wb : -wb, 0, -0.03f), 0.035f, dark);
            }
            Shapes.Box("BoomHead", crane, tip + new Vector3(0, 0.04f, 0), new Vector3(0.36f, 0.1f, 0.12f), body, false, false);
            // A-frame apex with stays to the boom tip and the backreach end.
            var apex = new Vector3(0, portalY + 1.6f, zWater + 0.9f);
            Strut("AFrame", crane, new Vector3(0, portalY + 0.25f, zWater + 0.15f), apex, 0.08f, body);
            Strut("AFrame", crane, new Vector3(0, portalY + 0.25f, zLand + 0.1f), apex, 0.08f, body);
            Strut("Stay", crane, apex, tip, 0.025f, Palette.Hex("3A3133"));
            Strut("Stay", crane, apex, new Vector3(0, portalY + 0.25f, backEnd), 0.025f, Palette.Hex("3A3133"));
            Shapes.Cylinder("AviationLight", crane, tip + new Vector3(0, 0.06f, 0), new Vector3(0.07f, 0.05f, 0.07f), Palette.Hex("E8463F"), true, false);
            var shade = new Vector4(1f, 0.9f, 0.8f, 0.85f);
            foreach (var r in crane.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = Shapes.Reshade(r.sharedMaterial, shade);
            if (theme.night >= 0.5f)
                Glow("AviationHalo", crane, tip + new Vector3(0, 0.06f, -0.05f), new Vector2(0.4f, 0.4f), Palette.Hex("FF6A5A"), 0.5f, cam.transform.rotation, 2.2f, worldRotation: true);
        }

        // =====================================================================
        // The ship alongside: port rail at the top, the open container bay, neighbouring bays at the sides
        // =====================================================================
        private static void BuildHold(Transform root, Palette pal, SkyTheme theme, BoardLayout layout, float halfW, int levelNumber)
        {
            float yardW = layout.Width * layout.CellSize + 0.5f;
            float yardTop = BoardLayout.YardTopZ, yardBottom = layout.YardBottomZ - 0.3f;
            Color hull = HullColors[(PortSkylines.IndexForLevel(levelNumber) + levelNumber) % HullColors.Length];
            Color deck = Palette.Hex("C9C6BE"), rail = pal.cabin, floor = Palette.Hex("4B4E57"), footprint = Palette.Hex("555964"), guide = Palette.Hex("8D939C");
            Color hatch = Color.Lerp(hull, Palette.Hex("8D939C"), 0.55f);
            float w = halfW * 2f + 4f;
            float railZ = BoardLayout.QuayNearZ - 0.2f;

            // ---- Quay wall with black fenders, dropping to the ship ----
            Shapes.Box("QuayWall", root, new Vector3(0, -0.35f, BoardLayout.QuayNearZ + 0.04f), new Vector3(w, 0.9f, 0.1f), Palette.Darken(ConcreteJoint, 0.18f), false, false);
            for (float x = -halfW - 0.4f; x < halfW + 0.5f; x += 1.6f)
                Shapes.MakeWith("Fender", HullMesh.Block("Fender", 0.13f, 0.05f, 0.32f, 0.05f, 0.03f), Shapes.Mat(Palette.Hex("2A2627")), root,
                    new Vector3(x, -0.3f, BoardLayout.QuayNearZ - 0.03f), Vector3.one, false);

            // ---- Ship's deck, gunwale in the hull colour and the white port rail ----
            float deckTopZ = railZ + 0.12f, deckBottomZ = yardBottom - 6f;
            Shapes.Box("Deck", root, new Vector3(0, -0.06f, (deckTopZ + deckBottomZ) * 0.5f), new Vector3(w, 0.08f, deckTopZ - deckBottomZ), deck, false, false);
            Shapes.Box("Gunwale", root, new Vector3(0, -0.04f, railZ + 0.1f), new Vector3(w, 0.06f, 0.12f), hull, false, false);
            Shapes.Box("SheerStripe", root, new Vector3(0, -0.005f, railZ + 0.1f), new Vector3(w, 0.01f, 0.03f), rail, false, false);
            Shapes.Box("RailTop", root, new Vector3(0, 0.13f, railZ), new Vector3(w, 0.03f, 0.03f), rail, false, false);
            Shapes.Box("RailMid", root, new Vector3(0, 0.06f, railZ), new Vector3(w, 0.02f, 0.02f), rail, false, false);
            for (float x = -halfW - 1f; x < halfW + 1f; x += 0.5f)
                Shapes.Box("RailPost", root, new Vector3(x, 0.05f, railZ), new Vector3(0.025f, 0.17f, 0.025f), rail, false, false);
            // Mooring lines from two quay bollards down to bitts on the deck.
            var bitt = Palette.Hex("3A3133");
            foreach (float bx in new[] { -3.5f, 3.5f })
            {
                float dx = bx < 0 ? 0.35f : -0.35f;
                Shapes.Cylinder("Bitt", root, new Vector3(bx + dx, 0.0f, railZ - 0.25f), new Vector3(0.09f, 0.1f, 0.09f), bitt, false, false);
                Strut("MooringLine", root, new Vector3(bx + dx, 0.06f, railZ - 0.25f), new Vector3(bx, 0.22f, BoardLayout.QuayNearZ + 0.13f), 0.022f, Palette.Hex("E8DCC2"));
            }

            // ---- The open container bay: dark tank top, a footprint per cell, cell guides between the columns ----
            float midZ = (yardTop + yardBottom) * 0.5f, depthZ = yardTop - yardBottom;
            Shapes.Box("HoldFloor", root, new Vector3(0, -0.08f, midZ), new Vector3(yardW, 0.16f, depthZ), floor);
            var fpMesh = new FlatMesh().Poly(HullMesh.RoundedRect(layout.CellSize * 0.44f, layout.CellSize * 0.42f, 0.08f)).Build("CellFootprint" + layout.CellSize.ToString("0.00"));
            for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                {
                    var c = layout.CellCenter(x, y, 1);
                    var fp = Shapes.MakeWith("Footprint", fpMesh, Shapes.Mat(footprint), root, new Vector3(c.x, 0.002f, c.z), Vector3.one, false);
                    fp.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
            for (int i = 0; i <= layout.Width; i++)
            {
                float lx = (i - layout.Width * 0.5f) * layout.CellSize;
                Shapes.Box("CellGuide", root, new Vector3(lx, 0.02f, midZ), new Vector3(0.06f, 0.04f, depthZ - 0.1f), guide, false, false);
            }
            Color coaming = Color.Lerp(hull, Color.white, 0.1f);
            Shapes.Box("CoamingL", root, new Vector3(-yardW * 0.5f - 0.06f, 0.0f, midZ), new Vector3(0.12f, 0.2f, depthZ + 0.24f), coaming);
            Shapes.Box("CoamingR", root, new Vector3(yardW * 0.5f + 0.06f, 0.0f, midZ), new Vector3(0.12f, 0.2f, depthZ + 0.24f), coaming);
            Shapes.Box("CoamingB", root, new Vector3(0, 0.0f, yardBottom - 0.06f), new Vector3(yardW + 0.24f, 0.2f, 0.12f), coaming);
            Shapes.Box("CoamingT", root, new Vector3(0, 0.0f, yardTop + 0.06f), new Vector3(yardW + 0.24f, 0.2f, 0.12f), coaming);
            Shapes.Box("CoamingTrim", root, new Vector3(0, 0.101f, yardTop + 0.06f), new Vector3(yardW + 0.24f, 0.002f, 0.03f), pal.yardLine, false, false);

            // ---- Neighbouring bays at the sides: lashing bridge, closed hatch covers, muted containers on deck ----
            var rng = new System.Random(levelNumber * 31 + 7);
            foreach (float s in new[] { -1f, 1f })
            {
                float bridgeX = s * (yardW * 0.5f + 0.3f);
                Shapes.Box("LashingBridge", root, new Vector3(bridgeX, 0.08f, midZ), new Vector3(0.22f, 0.04f, depthZ + 0.3f), pal.yardLine, false, false);
                Shapes.Box("BridgeRail", root, new Vector3(bridgeX + s * 0.1f, 0.2f, midZ), new Vector3(0.025f, 0.025f, depthZ + 0.3f), pal.yardLine, false, false);
                float hx0 = yardW * 0.5f + 0.45f;
                Shapes.Box("HatchCover", root, new Vector3(s * (hx0 + 2f), 0f, midZ), new Vector3(4f, 0.06f, depthZ + 0.2f), hatch, false, false);
                for (int y = 0; y < layout.Height; y++)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    int tiers = 1 + rng.Next(2);
                    var size = BoardLayout.ContainerScale(1) * layout.YardScale * 0.98f;
                    float bx = s * (hx0 + size.x * 0.5f + 0.04f), bz = -y * layout.CellSize;
                    for (int t = 0; t < tiers; t++)
                        Shapes.MakeWith("DeckBox", ContainerArt.Mesh(1), Shapes.MatTex(MutedBoxes[rng.Next(MutedBoxes.Length)], ContainerArt.Texture(1)), root,
                            new Vector3(bx, 0.03f + size.y * (t + 0.5f), bz), size, false);
                }
            }
        }
    }
}
