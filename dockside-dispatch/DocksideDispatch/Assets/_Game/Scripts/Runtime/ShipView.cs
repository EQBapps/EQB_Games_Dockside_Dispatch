using System.Collections;
using UnityEngine;

namespace Dockside
{
    /// <summary>A cargo ship at a berth: hull in the colour it wants, white deck with slot markers, cabin at the stern.</summary>
    public class ShipView : MonoBehaviour
    {
        public int ShipIndex { get; private set; }
        public int Capacity { get; private set; }
        public int Color { get; private set; }
        private Transform _deckSlots;
        private float _deckLength, _hullLength;
        private const float CabinZone = 0.6f;
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

        private void Build(int shipIndex, int color, int capacity, Palette palette)
        {
            ShipIndex = shipIndex;
            Capacity = capacity;
            Color = color;
            _bobPhase = Random.value * 10f;
            _deckLength = capacity * BoardLayout.ShipSlotPitch + 0.2f;
            _hullLength = _deckLength + CabinZone;
            Color hull = palette.ContainerColor(color);
            Color hullDark = Palette.Darken(hull, 0.38f);
            Color white = palette.cabin;
            float L = _hullLength, hl = L * 0.5f;
            const float Depth = 1.1f, DeckY = 0.3f, Bottom = -0.42f, Waterline = -0.12f;

            // Hull profile (side view): raked stern, flared bow pointing +x (the direction ships sail off).
            float SternX(float y) => Mathf.Lerp(-hl + 0.14f, -hl - 0.04f, (y - Waterline) / (DeckY - Waterline));
            float BowX(float y) => Mathf.Lerp(hl - 0.06f, hl + 0.32f, (y - Waterline) / (DeckY - Waterline));
            string k = $"{capacity}";
            Shapes.MakeWith("Hull", Shapes.Extrude("HullUpper" + k, new[]
            {
                new Vector2(SternX(Waterline), Waterline), new Vector2(BowX(Waterline), Waterline),
                new Vector2(BowX(DeckY), DeckY), new Vector2(SternX(DeckY), DeckY),
            }, Depth), Shapes.Mat(hull), transform, Vector3.zero, Vector3.one);
            // Dark boot-top below the waterline.
            Shapes.MakeWith("HullLower", Shapes.Extrude("HullLower" + k, new[]
            {
                new Vector2(-hl + 0.26f, Bottom), new Vector2(hl - 0.2f, Bottom),
                new Vector2(BowX(Waterline) - 0.01f, Waterline), new Vector2(SternX(Waterline) + 0.01f, Waterline),
            }, Depth - 0.06f), Shapes.Mat(hullDark), transform, Vector3.zero, Vector3.one);
            // White sheer stripe just below the deck, following the raked ends.
            const float s0 = 0.15f, s1 = 0.2f;
            Shapes.MakeWith("Stripe", Shapes.Extrude("HullStripe" + k, new[]
            {
                new Vector2(SternX(s0) - 0.012f, s0), new Vector2(BowX(s0) + 0.012f, s0),
                new Vector2(BowX(s1) + 0.012f, s1), new Vector2(SternX(s1) - 0.012f, s1),
            }, Depth + 0.024f), Shapes.Mat(white), transform, Vector3.zero, Vector3.one, false);
            // Raised forecastle at the bow.
            Shapes.MakeWith("Forecastle", Shapes.Extrude("Forecastle" + k, new[]
            {
                new Vector2(hl - 0.14f, DeckY - 0.01f), new Vector2(BowX(DeckY), DeckY - 0.01f),
                new Vector2(BowX(DeckY) + 0.05f, DeckY + 0.13f), new Vector2(hl - 0.14f, DeckY + 0.13f),
            }, Depth), Shapes.Mat(hull), transform, Vector3.zero, Vector3.one);

            // Cream cargo deck with outlined slots.
            float deckCenter = -hl + CabinZone + _deckLength * 0.5f;
            Shapes.Box("Deck", transform, new Vector3(deckCenter, DeckY + 0.015f, 0), new Vector3(_deckLength + 0.04f, 0.03f, Depth - 0.14f), palette.deck);
            Shapes.Box("Gunwale", transform, new Vector3(deckCenter - 0.05f, DeckY + 0.045f, -Depth * 0.5f + 0.035f),
                new Vector3(_deckLength + 0.1f, 0.06f, 0.05f), white);
            _deckSlots = new GameObject("Slots").transform;
            _deckSlots.SetParent(transform, false);
            for (int i = 0; i < capacity; i++)
            {
                var p = SlotLocalPosition(i, 1);
                Shapes.Box("SlotOutline", _deckSlots, new Vector3(p.x, DeckY + 0.033f, 0), new Vector3(BoardLayout.ShipSlotPitch - 0.05f, 0.008f, 0.52f),
                    palette.slotOutline, false, false);
                Shapes.Box("SlotInner", _deckSlots, new Vector3(p.x, DeckY + 0.036f, 0), new Vector3(BoardLayout.ShipSlotPitch - 0.11f, 0.008f, 0.46f),
                    UnityEngine.Color.Lerp(palette.deck, palette.slotOutline, 0.18f), false, false);
            }

            // Superstructure at the stern: accommodation block, bridge with a window band, funnel, mast, lifeboat.
            float sx = -hl + CabinZone * 0.5f - 0.02f;
            Shapes.Box("Accommodation", transform, new Vector3(sx, DeckY + 0.22f, 0.04f), new Vector3(0.48f, 0.44f, 0.78f), white);
            Shapes.Box("Bridge", transform, new Vector3(sx + 0.02f, DeckY + 0.53f, 0.0f), new Vector3(0.56f, 0.2f, 0.92f), white);
            Shapes.Box("BridgeWindows", transform, new Vector3(sx + 0.02f, DeckY + 0.55f, -0.465f), new Vector3(0.5f, 0.075f, 0.02f), palette.cabinWindow, false, false);
            Shapes.Box("BridgeRoof", transform, new Vector3(sx + 0.02f, DeckY + 0.645f, 0.0f), new Vector3(0.6f, 0.03f, 0.96f), Palette.Darken(white, 0.12f));
            for (int i = 0; i < 2; i++)
                Shapes.Box("Porthole", transform, new Vector3(sx - 0.1f + i * 0.2f, DeckY + 0.28f, -0.355f), new Vector3(0.09f, 0.09f, 0.02f), palette.cabinWindow, false, false);
            Shapes.Cylinder("Funnel", transform, new Vector3(sx - 0.06f, DeckY + 0.82f, 0.18f), new Vector3(0.2f, 0.32f, 0.2f), white);
            Shapes.Cylinder("FunnelBand", transform, new Vector3(sx - 0.06f, DeckY + 0.86f, 0.18f), new Vector3(0.21f, 0.09f, 0.21f), hull);
            Shapes.Cylinder("FunnelTop", transform, new Vector3(sx - 0.06f, DeckY + 0.985f, 0.18f), new Vector3(0.21f, 0.03f, 0.21f), Palette.Hex("3A3133"));
            Shapes.Box("Mast", transform, new Vector3(sx + 0.2f, DeckY + 0.82f, 0.05f), new Vector3(0.025f, 0.32f, 0.025f), white, false, false);
            Shapes.Box("MastYard", transform, new Vector3(sx + 0.2f, DeckY + 0.92f, 0.05f), new Vector3(0.16f, 0.02f, 0.02f), white, false, false);
            Shapes.Box("Lifeboat", transform, new Vector3(sx + 0.02f, DeckY + 0.36f, 0.46f), new Vector3(0.3f, 0.09f, 0.1f), palette.crane);

            // Colour symbol and load counter on the hull side ("● 2/4"), readable without relying on colour.
            float labelX = deckCenter - 0.02f;
            var sym = Shapes.Symbol(color, transform, new Vector3(labelX - 0.4f, 0.06f, -Depth * 0.5f - 0.025f), 0.24f, white);
            sym.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var labelGo = new GameObject("LoadLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(labelX + 0.12f, 0.06f, -Depth * 0.5f - 0.03f);
            _label = labelGo.AddComponent<TextMesh>();
            var font = UiFactory.FontBold;
            _label.font = font;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 96;
            _label.characterSize = 0.042f;
            _label.color = white;
            SetLoaded(0);

            // Foam ring where the hull meets the water; kept level with the sea while the ship bobs.
            _foam = Shapes.MakeWith("Foam", new FlatMesh().Ellipse(Vector2.zero, 0.5f, 0.5f, 40).Build("FoamRing"), Shapes.Mat(UnityEngine.Color.Lerp(palette.foam, palette.waterShallow, 0.3f), true),
                transform, Vector3.zero, new Vector3(L + 0.3f, 1.22f, 1f), false).transform;
            _foam.localRotation = Quaternion.Euler(90, 0, 0);
            _foamScale = _foam.localScale;
            // The hull side faces the camera, so keep it close to full colour (the mockup's flat hulls).
            var shade = new Vector4(1f, 0.97f, 0.86f, 0.9f);
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                if (r.transform != _foam && r.GetComponent<TextMesh>() == null) r.sharedMaterial = Shapes.Reshade(r.sharedMaterial, shade);
            transform.localRotation = Quaternion.Euler(Tilt, 0, 0);
        }

        private TextMesh _label;
        private Transform _foam;
        private Vector3 _foamScale;
        public int Loaded { get; private set; }

        /// <summary>Updates the hull counter; called as containers land on deck.</summary>
        public void SetLoaded(int slots)
        {
            Loaded = Mathf.Clamp(slots, 0, Capacity);
            if (_label) _label.text = $"{Loaded}/{Capacity}";
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
        public Vector3 SlotLocalPosition(int slot, int length)
        {
            float left = -_hullLength * 0.5f + CabinZone + 0.1f + BoardLayout.ShipSlotPitch * 0.5f;
            float x = left + (slot + (length - 1) * 0.5f) * BoardLayout.ShipSlotPitch;
            return new Vector3(x, 0.33f + BoardLayout.ContainerSize.y * BoardLayout.ShipScale * 0.5f, 0);
        }

        public Vector3 SlotWorldPosition(int slot, int length) => transform.TransformPoint(SlotLocalPosition(slot, length));

        public void SetRest(Vector3 pos) { _restPos = pos; _bobbing = true; }

        private void Update()
        {
            if (!_bobbing) return;
            float t = Time.time + _bobPhase;
            transform.position = _restPos + new Vector3(0, Mathf.Sin(t * 1.6f) * 0.05f, 0);
            transform.rotation = Quaternion.Euler(Tilt + Mathf.Sin(t * 1.3f) * 1.2f, 0, Mathf.Sin(t * 1.1f) * 1.5f);
        }

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
