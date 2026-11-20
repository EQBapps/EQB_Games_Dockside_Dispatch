using System.Collections;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// A cargo ship at a berth: a rounded hull in the colour it wants, a cream deck with dashed slots that runs almost
    /// the whole length of the ship, and a compact bridge tower standing behind the stern slot.
    /// </summary>
    public class ShipView : VehicleView
    {
        public override float StowScale => BoardLayout.ShipScale;
        private Transform _deckSlots;
        private float _hullLength;

        // Hull dimensions (local units). The deck is SternPad + slots + BodyPad long, then the elliptical bow.
        private const float SternPad = 0.12f, BodyPad = 0.04f, BowLength = 0.4f, HalfWidth = 0.45f;
        private const float DeckY = 0.32f, RimHeight = 0.05f, RimWidth = 0.05f;
        private const float StripeTop = 0.24f, StripeBottom = 0.19f, Waterline = -0.12f, Bottom = -0.42f;
        /// <summary>Containers sit towards the front of the deck, leaving room behind for the bridge tower.</summary>
        private const float SlotZ = -0.11f;

        /// <summary>Ships lean towards the camera so the hull side and its counter read like the art direction.</summary>
        public const float Tilt = 16f;
        private float _bobPhase;
        private Vector3 _restPos;
        private bool _bobbing;

        public static ShipView Create(Transform parent, int shipIndex, int color, int capacity, Palette palette)
        {
            var go = new GameObject($"Ship_{shipIndex}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ShipView>();
            v.Build(shipIndex, color, capacity, palette);
            return v;
        }

        /// <summary>Overall hull length for a capacity (bow to stern), used to space the berths.</summary>
        public static float HullLength(int capacity) => SternPad + capacity * BoardLayout.ShipSlotPitch + BodyPad + BowLength;

        private void Build(int shipIndex, int color, int capacity, Palette palette)
        {
            ShipIndex = shipIndex;
            Capacity = capacity;
            Color = color;
            _bobPhase = Random.value * 10f;
            Color hull = palette.ContainerColor(color);
            Color hullDark = Palette.Darken(hull, 0.38f);
            Color white = palette.cabin;
            string k = $"{capacity}";

            // Plan: the cargo deck runs almost the whole length; only a short rounded stern and an elliptical bow are
            // added around it. The bridge stands behind the stern slot (as in the mockup), so it costs no deck length.
            _hullLength = HullLength(capacity);
            float sternX = -_hullLength * 0.5f;
            float bodyEndX = sternX + SternPad + capacity * BoardLayout.ShipSlotPitch + BodyPad;
            var outline = HullMesh.ShipOutline(sternX, bodyEndX, BowLength, HalfWidth, 0.3f);

            // Side profile: near-vertical topsides with a slight rake at bow and stern, then a round bilge below the waterline.
            Vector2 Inset(float y)
            {
                float rake = Mathf.Max(0f, DeckY + RimHeight - y) * 0.3f;
                float top = Waterline + 0.04f;
                float u = Mathf.Clamp01((top - y) / (top - Bottom));
                float bilge = 1f - Mathf.Sqrt(1f - u * u);
                return new Vector2(rake + bilge * 0.26f, bilge * 0.2f);
            }
            void Band(string name, float top, float bottom, int steps, Color c, bool capBottom = false) =>
                Shapes.MakeWith(name, HullMesh.Loft(name + k, outline, HullMesh.Steps(top, bottom, steps), Inset, false, capBottom),
                    Shapes.Mat(c), transform, Vector3.zero, Vector3.one, false);
            Band("HullTop", DeckY + RimHeight, StripeTop, 2, hull);
            Band("HullStripe", StripeTop, StripeBottom, 1, white);
            Band("HullSide", StripeBottom, Waterline, 3, hull);
            Band("HullBoot", Waterline, Bottom, 7, hullDark, true);

            // Deck: a rounded white rim (bulwark) round a cream deck with dashed slot outlines, as in the mockup.
            Shapes.MakeWith("Rim", HullMesh.Ring("Rim" + k, outline, RimWidth, DeckY + RimHeight), Shapes.Mat(white), transform, Vector3.zero, Vector3.one, false);
            var deckOutline = HullMesh.Shrink(outline, new Vector2(RimWidth, RimWidth));
            Shapes.MakeWith("RimInner", HullMesh.Loft("RimInner" + k, deckOutline, new[] { DeckY + RimHeight, DeckY }, _ => Vector2.zero, false, false, inward: true),
                Shapes.Mat(Palette.Darken(palette.deck, 0.12f)), transform, Vector3.zero, Vector3.one, false);
            Shapes.MakeWith("Deck", HullMesh.Loft("Deck" + k, deckOutline, new[] { DeckY, DeckY - 0.02f }, _ => Vector2.zero, true, false),
                Shapes.Mat(palette.deck), transform, Vector3.zero, Vector3.one, false);

            _deckSlots = new GameObject("Slots").transform;
            _deckSlots.SetParent(transform, false);
            var slotSize = new Vector2(BoardLayout.ShipSlotPitch - 0.06f, BoardLayout.ContainerSize.z * BoardLayout.ShipScale + 0.04f);
            var slotMesh = new FlatMesh().DashedRoundRect(slotSize.x, slotSize.y, 0.07f, 0.024f, 0.06f, 0.04f).Build("ShipSlot");
            for (int i = 0; i < capacity; i++)
            {
                var p = SlotLocalPosition(i, 1);
                var slot = Shapes.MakeWith("Slot", slotMesh, Shapes.Mat(palette.slotOutline, true), _deckSlots,
                    new Vector3(p.x, DeckY + 0.004f, SlotZ), Vector3.one, false);
                slot.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }

            // Bridge tower behind the stern slot: rounded white block, wider bridge with a window band, funnel in the hull colour.
            float tx = SlotLocalPosition(0, 1).x - 0.02f, tz = 0.255f;
            Shapes.MakeWith("Accommodation", HullMesh.Block("Accom", 0.22f, 0.11f, 0.5f, 0.07f, 0.03f), Shapes.Mat(white),
                transform, new Vector3(tx, DeckY, tz), Vector3.one);
            Shapes.MakeWith("Bridge", HullMesh.Block("Bridge", 0.28f, 0.14f, 0.18f, 0.08f, 0.05f), Shapes.Mat(white),
                transform, new Vector3(tx, DeckY + 0.47f, tz - 0.01f), Vector3.one);
            for (int i = 0; i < 4; i++)
                Shapes.MakeWith("Window", HullMesh.Block("Window", 0.04f, 0.012f, 0.07f, 0.02f, 0.012f), Shapes.Mat(palette.cabinWindow, true),
                    transform, new Vector3(tx - 0.165f + i * 0.11f, DeckY + 0.53f, tz - 0.15f), Vector3.one, false);
            for (int i = 0; i < 2; i++)
                Shapes.MakeWith("Porthole", HullMesh.Block("Porthole", 0.034f, 0.01f, 0.068f, 0.034f, 0.01f), Shapes.Mat(palette.cabinWindow, true),
                    transform, new Vector3(tx - 0.08f + i * 0.16f, DeckY + 0.33f, tz - 0.115f), Vector3.one, false);
            Shapes.MakeWith("Funnel", HullMesh.Block("Funnel", 0.075f, 0.075f, 0.22f, 0.075f, 0.02f), Shapes.Mat(white),
                transform, new Vector3(tx - 0.1f, DeckY + 0.62f, tz + 0.04f), Vector3.one);
            Shapes.MakeWith("FunnelBand", HullMesh.Block("FunnelBand", 0.08f, 0.08f, 0.08f, 0.08f, 0.01f), Shapes.Mat(hull),
                transform, new Vector3(tx - 0.1f, DeckY + 0.72f, tz + 0.04f), Vector3.one);
            Shapes.MakeWith("FunnelTop", HullMesh.Block("FunnelTop", 0.08f, 0.08f, 0.04f, 0.08f, 0.02f), Shapes.Mat(Palette.Hex("3A3133")),
                transform, new Vector3(tx - 0.1f, DeckY + 0.83f, tz + 0.04f), Vector3.one);
            Shapes.Box("Mast", transform, new Vector3(tx + 0.16f, DeckY + 0.76f, tz + 0.04f), new Vector3(0.022f, 0.26f, 0.022f), white, false, false);
            Shapes.Box("MastYard", transform, new Vector3(tx + 0.16f, DeckY + 0.84f, tz + 0.04f), new Vector3(0.14f, 0.018f, 0.018f), white, false, false);

            // Colour symbol and load counter on the hull side ("● 2/4"), readable without relying on colour.
            // A dark drop shadow keeps the white label legible on light hulls (yellow) and at night.
            float labelX = sternX + SternPad + capacity * BoardLayout.ShipSlotPitch * 0.5f;
            float labelY = (StripeBottom + Waterline) * 0.5f;
            float faceZ = -HalfWidth - 0.012f;
            Color shadow = Palette.Darken(hull, 0.5f);
            for (int pass = 0; pass < 2; pass++)
            {
                bool front = pass == 1;
                var off = front ? Vector3.zero : new Vector3(0.016f, -0.016f, 0.006f);
                var sym = Shapes.Symbol(color, transform, new Vector3(labelX - 0.43f, labelY, faceZ - 0.022f) + off, 0.22f, front ? white : shadow);
                sym.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                sym.name = front ? "Symbol" : "SymbolShadow";
                var labelGo = new GameObject(front ? "LoadLabel" : "LoadLabelShadow");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = new Vector3(labelX + 0.15f, labelY, faceZ - 0.03f) + off;
                var tm = labelGo.AddComponent<TextMesh>();
                tm.font = UiFactory.FontBold;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = 96;
                tm.characterSize = 0.05f;
                tm.color = front ? white : shadow.linear;   // TextMesh vertex colours bypass the sRGB conversion
                if (front) _label = tm; else _labelShadow = tm;
            }
            SetLoaded(0);

            // Foam ring where the hull meets the water; kept level with the sea while the ship bobs.
            _foam = Shapes.MakeWith("Foam", new FlatMesh().Ellipse(Vector2.zero, 0.5f, 0.5f, 40).Build("FoamRing"), Shapes.Mat(UnityEngine.Color.Lerp(palette.foam, palette.waterShallow, 0.3f), true),
                transform, Vector3.zero, new Vector3(_hullLength + 0.25f, 1.05f, 1f), false).transform;
            _foam.localRotation = Quaternion.Euler(90, 0, 0);
            _foamScale = _foam.localScale;
            // The hull side faces the camera, so keep it close to full colour (the mockup's flat hulls); the curved bow,
            // stern and bilge turn away from the camera and darken, which is what makes the hull read as rounded.
            var shade = new Vector4(1f, 0.97f, 0.8f, 0.7f);
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                if (r.transform != _foam && r.GetComponent<TextMesh>() == null) r.sharedMaterial = Shapes.Reshade(r.sharedMaterial, shade);
            transform.localRotation = Quaternion.Euler(Tilt, 0, 0);
        }

        private TextMesh _label, _labelShadow;
        private Transform _foam;
        private Vector3 _foamScale;

        /// <summary>Updates the hull counter; called as containers land on deck.</summary>
        public override void SetLoaded(int slots)
        {
            Loaded = Mathf.Clamp(slots, 0, Capacity);
            string text = $"{Loaded}/{Capacity}";
            if (_label) _label.text = text;
            if (_labelShadow) _labelShadow.text = text;
        }

        private void LateUpdate()
        {
            if (!_foam) return;
            // Keep the foam flat on the water surface regardless of bob and roll.
            var p = transform.position;
            _foam.position = new Vector3(p.x, EnvironmentBuilder.WaterY + 0.012f, p.z);
            _foam.rotation = Quaternion.Euler(90, 0, 0);
            float pulse = 1f + Mathf.Sin((Time.time + _bobPhase) * 2.1f) * 0.025f;
            _foam.localScale = new Vector3(_foamScale.x * pulse, _foamScale.y * pulse, 1f);
        }

        /// <summary>Local position (on the deck) for an item starting at slot index, spanning length slots.</summary>
        public override Vector3 SlotLocalPosition(int slot, int length)
        {
            float left = -_hullLength * 0.5f + SternPad + BoardLayout.ShipSlotPitch * 0.5f;
            float x = left + (slot + (length - 1) * 0.5f) * BoardLayout.ShipSlotPitch;
            return new Vector3(x, DeckY + BoardLayout.ContainerSize.y * BoardLayout.ShipScale * 0.5f, SlotZ);
        }

        public void SetRest(Vector3 pos) { _restPos = pos; _bobbing = true; }

        public override void Park(Vector3 pos)
        {
            transform.position = pos;
            transform.rotation = Quaternion.Euler(Tilt, 0, 0);
            SetRest(pos);
        }

        private void Update()
        {
            if (!_bobbing) return;
            float t = Time.time + _bobPhase;
            transform.position = _restPos + new Vector3(0, Mathf.Sin(t * 1.6f) * 0.05f, 0);
            transform.rotation = Quaternion.Euler(Tilt + Mathf.Sin(t * 1.3f) * 1.2f, 0, Mathf.Sin(t * 1.1f) * 1.5f);
        }

        public override IEnumerator Arrive(Vector3 pos, float duration) => SailIn(pos, duration);
        public override IEnumerator Depart(float duration) => SailOut(duration);

        public IEnumerator SailIn(Vector3 berthPos, float duration)
        {
            _bobbing = false;
            transform.position = berthPos + new Vector3(-9f, 0, 0);
            yield return Tween.Move(transform, berthPos, duration, Tween.EaseOutCubic);
            SetRest(berthPos);
        }

        public IEnumerator SailOut(float duration)
        {
            _bobbing = false;
            Vector3 from = transform.position;
            Vector3 to = from + new Vector3(10f, 0, 0.6f);
            yield return Tween.Run(duration, e =>
            {
                transform.position = Vector3.LerpUnclamped(from, to, e);
                transform.rotation = Quaternion.Euler(Tilt, -6f * e, Mathf.Sin(e * Mathf.PI) * 3f);
            }, Tween.EaseInCubic);
        }
    }
}
