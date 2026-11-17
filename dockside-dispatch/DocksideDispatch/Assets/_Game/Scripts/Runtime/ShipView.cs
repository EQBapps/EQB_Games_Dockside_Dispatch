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

            // Hull: coloured block with a darker keel and a pointed bow (towards +x, where ships sail off).
            Shapes.Box("Hull", transform, Vector3.zero, new Vector3(_hullLength, 0.5f, 1.1f), hull);
            Shapes.Box("Keel", transform, new Vector3(0, -0.3f, 0), new Vector3(_hullLength - 0.2f, 0.16f, 1.0f), Palette.Darken(hull, 0.3f));
            var bow = Shapes.Prism("Bow", transform, new Vector3(_hullLength * 0.5f + 0.18f, 0f, 0), new Vector3(1.1f, 0.5f, 0.4f), hull);
            bow.transform.localRotation = Quaternion.Euler(0, 90, 0);
            Shapes.Box("Stripe", transform, new Vector3(0, 0.2f, -0.56f), new Vector3(_hullLength, 0.07f, 0.02f), UnityEngine.Color.white, false, false);

            // Deck (bow side) and slot markers.
            float deckCenter = -_hullLength * 0.5f + CabinZone + _deckLength * 0.5f;
            Shapes.Box("Deck", transform, new Vector3(deckCenter, 0.29f, 0), new Vector3(_deckLength, 0.08f, 0.86f), palette.deck);
            _deckSlots = new GameObject("Slots").transform;
            _deckSlots.SetParent(transform, false);
            for (int i = 0; i < capacity; i++)
            {
                var p = SlotLocalPosition(i, 1);
                Shapes.Box("SlotMark", _deckSlots, new Vector3(p.x, 0.335f, 0), new Vector3(BoardLayout.ShipSlotPitch - 0.07f, 0.01f, 0.46f),
                    palette.slotOutline, false, false);
            }

            // Cabin at the stern (left), with windows facing the camera.
            float sternX = -_hullLength * 0.5f + CabinZone * 0.5f;
            Shapes.Box("Cabin", transform, new Vector3(sternX, 0.62f, 0.05f), new Vector3(0.44f, 0.6f, 0.66f), palette.cabin);
            Shapes.Box("Window1", transform, new Vector3(sternX - 0.1f, 0.72f, -0.29f), new Vector3(0.11f, 0.12f, 0.02f), palette.cabinWindow, false, false);
            Shapes.Box("Window2", transform, new Vector3(sternX + 0.1f, 0.72f, -0.29f), new Vector3(0.11f, 0.12f, 0.02f), palette.cabinWindow, false, false);
            Shapes.Cylinder("Funnel", transform, new Vector3(sternX, 1.05f, 0.15f), new Vector3(0.17f, 0.3f, 0.17f), hull);

            // Colour symbol on the hull side so colour-blind players can match ships too.
            var sym = Shapes.Symbol(color, transform, new Vector3(deckCenter, 0.0f, -0.57f), 0.28f, UnityEngine.Color.white);
            sym.transform.localRotation = Quaternion.Euler(-90, 0, 0);
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
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 1.2f, 0, Mathf.Sin(t * 1.1f) * 1.5f);
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
                transform.rotation = Quaternion.Euler(0, -6f * e, Mathf.Sin(e * Mathf.PI) * 3f);
            }, Tween.EaseInCubic);
        }
    }
}
