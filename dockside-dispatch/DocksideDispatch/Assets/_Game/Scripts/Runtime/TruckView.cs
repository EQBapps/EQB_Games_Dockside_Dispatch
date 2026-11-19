using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// A container truck (Unload mode): a cab-over tractor in the colour it wants, with a white livery stripe and the
    /// colour's symbol on the roof, pulling a flatbed container chassis with two dashed slots (two short containers or
    /// one long one). The load counter sits on a cream plate (dark symbol and text) on the trailer side, between the
    /// wheels and the deck, so it reads on every truck colour (white text straight on a yellow truck would not). It drives in from the left, brakes, and pulls away when full.
    /// The ambient version (no plate, symbol or slots, neutral paint, a muted container) drives past on the far lane.
    /// </summary>
    public class TruckView : VehicleView
    {
        /// <summary>The whole truck is modelled at unit scale and drawn this much bigger (it fills its loading bay).</summary>
        public const float ModelScale = 1.12f;
        /// <summary>Containers ride bigger on a truck than on a ship: a truck only carries two.</summary>
        public const float TruckStowScale = 0.62f * ModelScale;
        public override float StowScale => TruckStowScale;

        // Model frame: x along the truck (cab towards +x, the direction it drives), -z faces the camera, y up from the road.
        private const float SlotPitch = 0.68f, TrailerRear = -1.12f, DeckPad = 0.08f;
        private const float DeckTop = 0.46f, DeckThick = 0.06f, HalfWidth = 0.32f, WheelR = 0.14f;
        private const float CabX = 0.78f, CabHalfX = 0.29f, CabBase = 0.2f, CabHeight = 0.64f;
        /// <summary>Trucks lean towards the camera so the cab side and the counter plate read (like the ships).</summary>
        public const float Tilt = 18f;

        private TextMesh _label;
        private readonly List<Transform> _wheels = new List<Transform>();
        private Transform _model, _body;
        private float _lastX;

        public static TruckView Create(Transform parent, int index, int color, int capacity, Palette palette, SkyTheme theme)
        {
            var go = new GameObject($"Truck_{index}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TruckView>();
            v.ShipIndex = index;
            v.Capacity = capacity;
            v.Color = color;
            v.Build(palette.ContainerColor(color), palette, theme, color, null, 0);
            return v;
        }

        /// <summary>A truck driving past (not part of the puzzle): neutral cab, one muted container of the given length.</summary>
        public static TruckView CreateAmbient(Transform parent, Color paint, Color cargo, int cargoLength, Palette palette, SkyTheme theme)
        {
            var go = new GameObject("PassingTruck");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TruckView>();
            v.Capacity = 2;
            v.Color = -1;
            v.Build(paint, palette, theme, -1, cargo, cargoLength);
            return v;
        }

        /// <summary>Overall length, bumper to tail, for spacing the loading bays.</summary>
        public static float Length => (CabX + CabHalfX + 0.06f - TrailerRear) * ModelScale;

        private void Build(Color paint, Palette pal, SkyTheme theme, int symbolIndex, Color? cargo, int cargoLength)
        {
            bool game = symbolIndex >= 0;
            Color paintDark = Palette.Darken(paint, 0.28f);
            Color white = pal.cabin, steel = Palette.Hex("4A4440"), tyre = Palette.Hex("2E2B2C"), chrome = Palette.Hex("C9CCD1");
            Color plate = Palette.Hex("FBF5EA"), ink = Palette.Hex("2E2422");
            bool night = theme.night >= 0.5f;
            Color glass = night ? theme.lights : Palette.Hex("6FB6D6");

            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
            _model.localScale = Vector3.one * ModelScale;
            // Everything except the wheels sits on a body that dips when the truck brakes.
            _body = new GameObject("Body").transform;
            _body.SetParent(_model, false);

            // ---- Chassis: frame rails under tractor and trailer ----
            Shapes.Box("Frame", _body, new Vector3((TrailerRear + CabX + CabHalfX) * 0.5f, WheelR + 0.06f, 0), new Vector3(CabX + CabHalfX - TrailerRear - 0.05f, 0.07f, HalfWidth * 1.2f), steel, false, false);

            // ---- Trailer: flatbed deck in the truck's colour with dashed slots, dark side rails, landing legs ----
            float deckFront = TrailerRear + DeckPad + Capacity * SlotPitch + 0.06f;
            float deckLen = deckFront - TrailerRear;
            Shapes.MakeWith("Deck", HullMesh.Block("TruckDeck", deckLen * 0.5f, HalfWidth, DeckThick, 0.03f, 0.015f), Shapes.Mat(game ? paint : Palette.Hex("6B6560")),
                _body, new Vector3(TrailerRear + deckLen * 0.5f, DeckTop - DeckThick, 0), Vector3.one, false);
            foreach (float z in new[] { -HalfWidth - 0.005f, HalfWidth + 0.005f })
                Shapes.Box("SideRail", _body, new Vector3(TrailerRear + deckLen * 0.5f, DeckTop - DeckThick - 0.035f, z), new Vector3(deckLen, 0.07f, 0.02f), steel, false, false);
            if (game)
            {
                var slotMesh = new FlatMesh().DashedRoundRect(SlotPitch - 0.08f, HalfWidth * 2f - 0.1f, 0.06f, 0.024f, 0.06f, 0.04f).Build("TruckSlot");
                for (int i = 0; i < Capacity; i++)
                {
                    var slot = Shapes.MakeWith("Slot", slotMesh, Shapes.Mat(UnityEngine.Color.Lerp(paint, white, 0.7f), true), _body,
                        new Vector3(ModelSlot(i, 1).x, DeckTop + 0.003f, 0), Vector3.one, false);
                    slot.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
            }
            foreach (float x in new[] { TrailerRear + 0.05f, deckFront - 0.05f })
                foreach (float z in new[] { -HalfWidth + 0.04f, HalfWidth - 0.04f })
                    Shapes.Box("TwistLock", _body, new Vector3(x, DeckTop + 0.015f, z), new Vector3(0.05f, 0.03f, 0.05f), pal.yardLine, false, false);
            foreach (float z in new[] { -HalfWidth + 0.08f, HalfWidth - 0.08f })
            {
                Shapes.Box("LandingLeg", _body, new Vector3(deckFront - 0.32f, 0.2f, z), new Vector3(0.035f, 0.26f, 0.035f), steel, false, false);
                Shapes.Box("LandingFoot", _body, new Vector3(deckFront - 0.32f, 0.075f, z), new Vector3(0.08f, 0.02f, 0.06f), steel, false, false);
            }
            // Rear: bumper, tail lights, mudflaps behind the tandem.
            Shapes.Box("RearBumper", _body, new Vector3(TrailerRear + 0.02f, 0.2f, 0), new Vector3(0.04f, 0.05f, HalfWidth * 1.9f), steel, false, false);
            foreach (float z in new[] { -HalfWidth + 0.07f, HalfWidth - 0.07f })
            {
                Shapes.Box("TailLight", _body, new Vector3(TrailerRear - 0.005f, DeckTop - 0.09f, z), new Vector3(0.02f, 0.05f, 0.09f), Palette.Hex("E8463F"), true, false);
                Shapes.Box("Mudflap", _body, new Vector3(TrailerRear + 0.06f, 0.15f, z), new Vector3(0.012f, 0.18f, 0.12f), tyre, false, false);
            }
            if (cargo.HasValue)
            {
                var size = BoardLayout.ContainerScale(cargoLength) * (TruckStowScale / ModelScale);
                Shapes.MakeWith("Cargo", ContainerArt.Mesh(cargoLength), Shapes.MatTex(cargo.Value, ContainerArt.Texture(cargoLength)), _body,
                    ModelSlot(0, cargoLength), size, false);
            }

            // ---- Tractor: cab-over with a white livery stripe, big side window, visor, mirror, roof marker lights ----
            Shapes.MakeWith("Cab", HullMesh.Block("TruckCab2", CabHalfX, HalfWidth + 0.01f, CabHeight, 0.09f, 0.09f), Shapes.Mat(paint),
                _body, new Vector3(CabX, CabBase, 0), Vector3.one);
            float sideZ = -HalfWidth - 0.012f;
            Shapes.Box("Stripe", _body, new Vector3(CabX - 0.01f, CabBase + 0.2f, sideZ), new Vector3(CabHalfX * 2f - 0.08f, 0.05f, 0.006f), white, false, false);
            Shapes.MakeWith("SideWindow", HullMesh.Block("TruckWin2", 0.17f, 0.006f, 0.22f, 0.04f, 0.02f), Shapes.Mat(glass, true),
                _body, new Vector3(CabX + 0.07f, CabBase + 0.33f, sideZ), Vector3.one, false);
            Shapes.Box("Visor", _body, new Vector3(CabX + CabHalfX - 0.02f, CabBase + CabHeight - 0.05f, 0), new Vector3(0.08f, 0.035f, HalfWidth * 2f + 0.04f), paintDark, false, false);
            Shapes.Box("Door", _body, new Vector3(CabX - 0.12f, CabBase + 0.28f, sideZ - 0.002f), new Vector3(0.012f, 0.42f, 0.004f), paintDark, false, false);
            Shapes.Box("Step", _body, new Vector3(CabX + 0.04f, CabBase - 0.02f, sideZ + 0.02f), new Vector3(0.22f, 0.03f, 0.06f), steel, false, false);
            Shapes.Box("MirrorArm", _body, new Vector3(CabX + CabHalfX - 0.02f, CabBase + 0.46f, sideZ - 0.05f), new Vector3(0.02f, 0.02f, 0.1f), steel, false, false);
            Shapes.Box("Mirror", _body, new Vector3(CabX + CabHalfX + 0.01f, CabBase + 0.42f, sideZ - 0.1f), new Vector3(0.03f, 0.13f, 0.05f), steel, false, false);
            // Roof: a lighter fairing carrying the symbol, and amber marker lights along its front edge.
            Shapes.MakeWith("Roof", HullMesh.Block("TruckRoof2", CabHalfX - 0.05f, HalfWidth - 0.04f, 0.07f, 0.07f, 0.04f), Shapes.Mat(UnityEngine.Color.Lerp(paint, white, 0.2f)),
                _body, new Vector3(CabX - 0.02f, CabBase + CabHeight - 0.01f, 0), Vector3.one, false);
            for (int i = 0; i < 3; i++)
                Shapes.Box("MarkerLight", _body, new Vector3(CabX + CabHalfX - 0.07f, CabBase + CabHeight + 0.065f, (i - 1) * 0.16f), new Vector3(0.04f, 0.025f, 0.06f), Palette.Hex("FFB547"), true, false);
            if (game) Shapes.Symbol(symbolIndex, _body, new Vector3(CabX - 0.05f, CabBase + CabHeight + 0.075f, 0), 0.3f, white).name = "RoofSymbol";   // lies flat
            // Front: bumper and headlights; chrome fuel tank and exhaust stack behind the cab.
            Shapes.Box("Bumper", _body, new Vector3(CabX + CabHalfX + 0.02f, CabBase + 0.03f, 0), new Vector3(0.06f, 0.09f, HalfWidth * 2f + 0.03f), steel, false, false);
            foreach (float z in new[] { -HalfWidth + 0.07f, HalfWidth - 0.07f })
                Shapes.Box("Headlight", _body, new Vector3(CabX + CabHalfX + 0.006f, CabBase + 0.13f, z), new Vector3(0.02f, 0.05f, 0.08f), Palette.Hex("FFF4D6"), true, false);
            Shapes.Cylinder("FuelTank", _body, new Vector3(CabX - 0.3f, WheelR + 0.05f, -HalfWidth + 0.03f), new Vector3(0.13f, 0.2f, 0.13f), chrome, false, false)
                .transform.localRotation = Quaternion.Euler(0, 0, 90);
            Shapes.Cylinder("Stack", _body, new Vector3(CabX - CabHalfX - 0.04f, CabBase + CabHeight * 0.6f, HalfWidth - 0.08f), new Vector3(0.05f, CabHeight * 0.85f, 0.05f), chrome, false, false);

            // ---- Load counter: big dark "0/2" on a cream side panel that fills the gap between the trailer tandem
            //      and the tractor's drive axle, from just above the road up to the deck (the largest side area the
            //      camera sees). The shape that identifies the colour is on the cab roof, where it reads best. ----
            if (game)
            {
                float gapL = TrailerRear + 0.48f + WheelR + 0.01f, gapR = CabX - 0.42f - WheelR - 0.01f;
                float px = (gapL + gapR) * 0.5f, halfW = (gapR - gapL) * 0.5f, pz = -HalfWidth - 0.03f;
                const float PanelBottom = 0.05f, PanelH = 0.44f;
                float py = PanelBottom + PanelH * 0.5f;
                Shapes.MakeWith("CounterPlate", HullMesh.Block("TruckPlate2", halfW - 0.012f, 0.008f, PanelH - 0.024f, 0.07f, 0.0f), Shapes.Mat(plate, true),
                    _body, new Vector3(px, PanelBottom + 0.012f, pz), Vector3.one, false);
                Shapes.MakeWith("PlateRim", HullMesh.Block("TruckPlateRim2", halfW, 0.006f, PanelH, 0.08f, 0.0f), Shapes.Mat(ink, true),
                    _body, new Vector3(px, PanelBottom, pz + 0.004f), Vector3.one, false);
                var labelGo = new GameObject("LoadLabel");
                labelGo.transform.SetParent(_body, false);
                labelGo.transform.localPosition = new Vector3(px, py, pz - 0.016f);
                _label = labelGo.AddComponent<TextMesh>();
                _label.font = UiFactory.FontBold;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = _label.font.material;
                _label.anchor = TextAnchor.MiddleCenter;
                _label.alignment = TextAlignment.Center;
                _label.fontSize = 96;
                _label.characterSize = 0.038f;
                _label.color = ink.linear;   // TextMesh vertex colours bypass the sRGB conversion
                SetLoaded(0);
            }

            // ---- Wheels: trailer tandem, tractor drive and steer axles, with a hub bar that shows them turning ----
            foreach (float x in new[] { TrailerRear + 0.2f, TrailerRear + 0.48f, CabX - 0.42f, CabX + 0.14f })
                foreach (float z in new[] { -HalfWidth + 0.06f, HalfWidth - 0.06f })
                {
                    var pivot = new GameObject("Wheel").transform;
                    pivot.SetParent(_model, false);
                    pivot.localPosition = new Vector3(x, WheelR, z);
                    Shapes.Cylinder("Tyre", pivot, Vector3.zero, new Vector3(WheelR * 2f, 0.06f, WheelR * 2f), tyre, false, false)
                        .transform.localRotation = Quaternion.Euler(90, 0, 0);
                    float hz = z < 0 ? -0.032f : 0.032f;
                    Shapes.Cylinder("Hub", pivot, new Vector3(0, 0, hz), new Vector3(WheelR * 1.15f, 0.006f, WheelR * 1.15f), chrome, false, false)
                        .transform.localRotation = Quaternion.Euler(90, 0, 0);
                    Shapes.Box("Spoke", pivot, new Vector3(0, 0, hz * 1.12f), new Vector3(WheelR * 1.1f, 0.03f, 0.004f), steel, false, false);
                    _wheels.Add(pivot);
                }

            var shade = new Vector4(1f, 0.95f, 0.82f, 0.75f);
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                if (r.GetComponent<TextMesh>() == null) r.sharedMaterial = Shapes.Reshade(r.sharedMaterial, shade);
            // Night: headlight beams on the road ahead (added after the reshade so they keep their material).
            if (night)
                EnvironmentBuilder.Glow("Headlights", _body, new Vector3(CabX + CabHalfX + 0.75f, 0.01f, 0), new Vector2(1.4f, 0.75f),
                    Palette.Hex("FFF1C9"), 0.3f, Quaternion.Euler(90, 0, 0));

            transform.localRotation = Quaternion.Euler(Tilt, 0, 0);
            _lastX = transform.position.x;
        }

        public override void SetLoaded(int slots)
        {
            Loaded = Mathf.Clamp(slots, 0, Capacity);
            if (_label) _label.text = $"{Loaded}/{Capacity}";
        }

        public override Vector3 SlotLocalPosition(int slot, int length) => ModelSlot(slot, length) * ModelScale;

        /// <summary>Slot centre in the unscaled model's space.</summary>
        private static Vector3 ModelSlot(int slot, int length)
        {
            float x = TrailerRear + DeckPad + (slot + length * 0.5f) * SlotPitch;
            return new Vector3(x, DeckTop + BoardLayout.ContainerSize.y * TruckStowScale / ModelScale * 0.5f, 0);
        }

        private void LateUpdate()
        {
            // Roll the wheels with the distance travelled.
            float x = transform.position.x, dx = x - _lastX;
            _lastX = x;
            if (Mathf.Abs(dx) < 1e-5f) return;
            float deg = -dx / (WheelR * ModelScale) * Mathf.Rad2Deg;
            foreach (var w in _wheels) w.localRotation *= Quaternion.Euler(0, 0, deg);
        }

        // ------------------------------------------------------------------ driving
        // Trucks never drive through the trucks parked in the other bays: they come in along the free lane behind the
        // bays and turn in, and pull out into that lane to leave (LaneTraffic keeps the gaps).
        private const float LaneSpeed = 7.5f, TurnLength = 2.0f;
        // Safety net: a truck that has waited this long for traffic gives up on the lane and drives straight into place,
        // so the puzzle (which waits for its trucks) can never freeze, whatever happens in the traffic animation.
        private const float GiveUpSeconds = 6f;
        private LaneTraffic.Car _car;

        /// <summary>Heading (degrees about +y) applied under the lean towards the camera.</summary>
        private void SetHeading(float yaw) => transform.rotation = Quaternion.Euler(Tilt, 0, 0) * Quaternion.Euler(0, yaw, 0);

        /// <summary>Position and heading on an S-curve between the lane and a bay, s in 0..1.</summary>
        private void AlongTurn(float x0, float z0, float x1, float z1, float s)
        {
            float k = s * s * (3f - 2f * s);
            transform.position = new Vector3(Mathf.Lerp(x0, x1, s), transform.position.y, Mathf.Lerp(z0, z1, k));
            float dzds = (z1 - z0) * 6f * s * (1f - s), dxds = x1 - x0;
            SetHeading(Mathf.Atan2(-dzds, dxds) * Mathf.Rad2Deg);
        }

        public override IEnumerator Arrive(Vector3 pos, float duration)
        {
            var lane = LaneTraffic.Instance;
            if (lane == null)
            {
                transform.position = pos + new Vector3(-9f, 0, 0);
                _lastX = transform.position.x;
                yield return Tween.Move(transform, pos, duration, Tween.EaseOutCubic);
            }
            else
            {
                float half = Length * 0.5f, zLane = lane.LaneZ;
                transform.position = new Vector3(lane.EntryX, pos.y, zLane);
                SetHeading(0f);
                _lastX = transform.position.x;
                float waited = 0f;
                // Queue (off screen, not yet a lane vehicle) for the lane entry.
                while (!lane.TryEnter(this, pos.x, half))
                {
                    if ((waited += Time.deltaTime) > GiveUpSeconds) { lane.LeaveQueue(this); yield return DriveStraightIn(pos); yield break; }
                    yield return null;
                }
                _car = lane.Add(transform, half);
                // Drive along the lane to the turn-in point; if the bay is still being vacated, wait well short of it
                // (outside the leaving truck's pull-out zone).
                float turnX = pos.x - TurnLength, waitX = turnX - Length - 1.0f;
                waited = 0f;
                while (true)
                {
                    if ((waited += Time.deltaTime) > GiveUpSeconds + 4f)
                    {
                        lane.Remove(_car); _car = null;
                        yield return DriveStraightIn(pos);
                        yield break;
                    }
                    bool free = lane.BayFree(pos.x, this);
                    float x = transform.position.x;
                    if (free && x >= turnX - 0.01f) break;
                    float stopX = free ? turnX : waitX;
                    float d = Mathf.Max(0f, stopX - x);
                    float desired = Mathf.Min(LaneSpeed, Mathf.Sqrt(2f * LaneTraffic.Brake * d) + (free ? 2.5f : 0f));
                    lane.Step(_car, desired, stopX);
                    yield return null;
                }
                lane.ClaimBay(pos.x, this);
                // Turn in: S-curve from the lane into the bay, slowing to a stop.
                float v0 = Mathf.Max(2.5f, _car.v), dur = Mathf.Clamp(TurnLength * 1.6f / v0, 0.45f, 0.8f);
                for (float t = 0; t < dur; t += Time.deltaTime)
                {
                    float s = 1f - (1f - t / dur) * (1f - t / dur);   // ease out
                    AlongTurn(turnX, zLane, pos.x, pos.z, s);
                    if (_car != null && s > 0.45f) { lane.Remove(_car); _car = null; }
                    yield return null;
                }
                if (_car != null) { lane.Remove(_car); _car = null; }
            }
            transform.position = pos;
            SetHeading(0f);
            // Brake: the nose dips and settles.
            yield return Tween.Run(0.3f, e => _body.localRotation = Quaternion.Euler(0, 0, -2.2f * Mathf.Sin(e * Mathf.PI)), t => t);
            _body.localRotation = Quaternion.identity;
        }

        public override IEnumerator Depart(float duration)
        {
            var lane = LaneTraffic.Instance;
            if (lane == null)
            {
                Vector3 from = transform.position, to = from + new Vector3(10f, 0, 0);
                yield return Tween.Run(duration, e => transform.position = Vector3.LerpUnclamped(from, to, e), Tween.EaseInCubic);
                yield break;
            }
            float half = Length * 0.5f, zLane = lane.LaneZ;
            Vector3 bay = transform.position;
            float x1 = bay.x + TurnLength;
            // Pull out only when the lane beside the bay (and a little behind it) is clear (or after waiting too long).
            for (float waited = 0f; !lane.ZoneClear(bay.x - half - 2.5f, x1 + half + 0.8f) && waited < GiveUpSeconds; waited += Time.deltaTime)
                yield return null;
            _car = lane.Add(transform, half);
            // Pull out: S-curve into the lane, picking up speed; the body squats as it moves off.
            const float PullOut = 0.7f;
            for (float t = 0; t < PullOut; t += Time.deltaTime)
            {
                float e = t / PullOut, s = e * e;   // ease in
                AlongTurn(bay.x, bay.z, x1, zLane, s);
                _car.v = Mathf.Lerp(0f, 3.5f, e);
                _body.localRotation = Quaternion.Euler(0, 0, 1.5f * Mathf.Sin(Mathf.Min(1f, e * 2f) * Mathf.PI));
                if (s > 0.6f) lane.ReleaseBay(bay.x, this);
                yield return null;
            }
            _body.localRotation = Quaternion.identity;
            lane.ReleaseBay(bay.x, this);
            transform.position = new Vector3(x1, bay.y, zLane);
            SetHeading(0f);
            // Drive off along the lane.
            while (transform.position.x < lane.ExitX)
            {
                lane.Step(_car, LaneSpeed);
                yield return null;
            }
            lane.Remove(_car);
            _car = null;
        }

        /// <summary>Fallback when traffic has held a truck up too long: glide straight into the bay (claiming it) and settle.</summary>
        private IEnumerator DriveStraightIn(Vector3 pos)
        {
            Debug.LogWarning($"[Dockside] Truck {ShipIndex} waited too long for traffic; driving straight in.");
            var lane = LaneTraffic.Instance;
            if (lane != null) lane.ClaimBay(pos.x, this);
            SetHeading(0f);
            Vector3 from = new Vector3(Mathf.Min(transform.position.x, pos.x - 6f), pos.y, pos.z);
            transform.position = from;
            yield return Tween.Move(transform, pos, 0.6f, Tween.EaseOutCubic);
            transform.position = pos;
        }

        private void OnDestroy()
        {
            var lane = LaneTraffic.Instance;
            if (lane == null) return;
            if (_car != null) lane.Remove(_car);
            lane.LeaveQueue(this);
        }
    }
}
