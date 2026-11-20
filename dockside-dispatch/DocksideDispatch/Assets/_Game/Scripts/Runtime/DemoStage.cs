using System.Collections;
using Dockside.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Dockside
{
    /// <summary>The mechanics a tutorial card can show in its mini screen.</summary>
    public enum DemoKind { Basics, Bay, Long, Locked, Hidden, ExtraBerth, CraneLift, Undo }

    /// <summary>
    /// The tutorial cards' mini screen: a small harbour built far from the board on its own layer, filmed by its own
    /// camera into a texture, where a short scripted scene loops to show one mechanic with the game's real containers,
    /// ships and trucks. It only exists (and only renders) while a card shows it.
    /// </summary>
    public class DemoStage : MonoBehaviour
    {
        public const int Layer = 31;
        public const int TexWidth = 960, TexHeight = 720;
        private static readonly Vector3 Origin = new Vector3(800f, 0f, 0f);
        private const float FramePitch = 55f;

        public static DemoStage Instance { get; private set; }
        public Camera Cam { get; private set; }
        public RenderTexture Texture { get; private set; }
        /// <summary>Where the demo is tapping (world), for the card's tap rings; null when nothing is being tapped.</summary>
        public Vector3? TapWorld { get; private set; }
        /// <summary>Booster shown in the mini screen's corner (booster demos), or null.</summary>
        public string ChipIcon { get; private set; }
        public bool ChipTapped { get; private set; }
        public float TapTime { get; private set; }

        private Palette _pal;
        private Transform _root;
        private BufferSlotsView _bay;
        private bool _trucks;
        private int _nextId, _layeredCount = -1;

        public static DemoStage Create(Palette pal)
        {
            var go = new GameObject("DemoStage");
            var stage = go.AddComponent<DemoStage>();
            stage._pal = pal;
            var camGo = new GameObject("DemoCamera");
            camGo.transform.SetParent(go.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            stage.Texture = new RenderTexture(TexWidth, TexHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "DemoScreen" };
            cam.targetTexture = stage.Texture;
            cam.enabled = false;
            stage.Cam = cam;
            Instance = stage;
            return stage;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Texture) Texture.Release();
        }

        /// <summary>Starts a demo (looping, or once when motion is reduced); the card shows <see cref="Texture"/>.</summary>
        public void Play(DemoKind kind, GameMode mode)
        {
            Stop();
            _trucks = mode == GameMode.Unload;
            Cam.backgroundColor = _trucks ? Asphalt : _pal.waterShallow;
            Cam.enabled = true;
            StartCoroutine(Loop(kind));
        }

        public void Stop()
        {
            StopAllCoroutines();
            Clear();
            if (Cam) Cam.enabled = false;
        }

        private void Clear()
        {
            if (_root) Destroy(_root.gameObject);
            _root = null;
            _bay = null;
            TapWorld = null;
            ChipIcon = null;
            ChipTapped = false;
            _layeredCount = -1;
        }

        private IEnumerator Loop(DemoKind kind)
        {
            while (true)
            {
                Clear();
                Build();
                if (kind == DemoKind.ExtraBerth) ChipIcon = "berth";
                if (kind == DemoKind.CraneLift) ChipIcon = "crane";
                if (kind == DemoKind.Undo) ChipIcon = "undo";
                yield return Script(kind);
                if (MotionPrefs.Reduced) yield break;   // played once: hold the last frame
                yield return new WaitForSeconds(1.1f);
            }
        }

        // Everything in the stage renders on the demo layer only (objects made mid-scene too).
        private void LateUpdate()
        {
            if (!_root) return;
            int count = _root.hierarchyCount;
            if (count == _layeredCount) return;
            _layeredCount = count;
            foreach (var t in _root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var c in _root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        // ------------------------------------------------------------------ scene
        private static readonly Color Asphalt = Palette.Hex("5E5A57"), Concrete = Palette.Hex("CFC8BC");

        private Vector3 W(Vector3 local) => _root.TransformPoint(local);

        private void Build()
        {
            _root = new GameObject("DemoScene").transform;
            _root.SetParent(transform, false);
            _root.position = Origin;
            _nextId = 0;

            // Ground: the yard, the quay with the holding bay, then water (Load) or the trucks' road (Unload).
            Shapes.Box("Yard", _root, new Vector3(0, -0.06f, -2.2f), new Vector3(14f, 0.1f, 5.9f), _pal.yard, false, false);
            float quayFar = _trucks ? BoardLayout.TruckZ - 1.1f : BoardLayout.WaterNearZ;
            Shapes.Box("Quay", _root, new Vector3(0, -0.05f, (BoardLayout.YardTopZ + quayFar) * 0.5f), new Vector3(14f, 0.1f, quayFar - BoardLayout.YardTopZ), Concrete, false, false);
            if (_trucks)
                Shapes.Box("Road", _root, new Vector3(0, -0.06f, quayFar + 4f), new Vector3(14f, 0.1f, 8f), Asphalt, false, false);
            else
            {
                Shapes.Box("Water", _root, new Vector3(0, -0.45f, quayFar + 5f), new Vector3(14f, 0.1f, 10f), _pal.waterShallow, false, false);
                Shapes.Box("QuayEdge", _root, new Vector3(0, -0.12f, quayFar + 0.06f), new Vector3(14f, 0.35f, 0.14f), Palette.Darken(Concrete, 0.25f), false, false);
            }
            _bay = new GameObject("HoldingBay").AddComponent<BufferSlotsView>();
            _bay.transform.SetParent(_root, false);
            _bay.Init(_pal);
            _bay.SetCapacity(5);

            // Frame the yard's two front rows, the bay and the berth (vehicles included), as the game's camera does.
            float far = _trucks ? BoardLayout.TruckZ + 1.5f : BoardLayout.ShipZ + 1.7f, near = -1.75f;   // headroom for cabins
            var target = W(new Vector3(0, 0, (far + near) * 0.5f));
            Cam.orthographicSize = (far - near) * Mathf.Sin(FramePitch * Mathf.Deg2Rad) * 0.5f + 0.1f;
            Cam.transform.rotation = Quaternion.Euler(FramePitch, 0, 0);
            Cam.transform.position = target - Cam.transform.forward * 30f;
        }

        /// <summary>A yard container: column x (0 is the middle), row 0 next to the quay.</summary>
        private ContainerView Box(int color, float x, int row, int length = 1, ContainerKind kind = ContainerKind.Normal, int keyId = -1, int lockId = -1)
        {
            var spec = new ContainerSpec { id = _nextId++, x = 0, y = row, length = length, color = color, kind = kind, keyId = keyId, lockId = lockId };
            var v = ContainerView.Create(_root, spec, _pal);
            v.transform.localScale = BoardLayout.ContainerScale(length);
            v.transform.localPosition = new Vector3((x + (length - 1) * 0.5f) * BoardLayout.Cell, BoardLayout.ContainerSize.y * 0.5f, -row * BoardLayout.Cell);
            if (row > 0) v.SetDim(true);
            return v;
        }

        private Vector3 Berth(float x) => W(_trucks ? new Vector3(x, 0, BoardLayout.TruckZ) : new Vector3(x, -0.05f, BoardLayout.ShipZ));
        private int Capacity(int shipCapacity) => _trucks ? LevelGenerator.TruckCapacity : shipCapacity;

        private VehicleView Vehicle(int color, int shipCapacity, float x, bool docked = true)
        {
            VehicleView v;
            if (_trucks)
            {
                var truck = TruckView.Create(_root, 0, color, Capacity(shipCapacity), _pal, SkyTheme.Current(_pal));
                truck.Standalone = true;
                v = truck;
            }
            else v = ShipView.Create(_root, 0, color, shipCapacity, _pal);
            if (docked) v.Park(Berth(x));
            else v.transform.position = Berth(x) + new Vector3(-12f, 0, 0);
            return v;
        }

        private void Stow(ContainerView c, VehicleView v, int slot)
        {
            int len = c.Length;
            c.Detach();
            c.transform.SetPositionAndRotation(v.SlotWorldPosition(slot, len), v.transform.rotation);
            c.transform.localScale = BoardLayout.ContainerScale(len) * v.StowScale;
            c.transform.SetParent(v.transform, true);
            c.transform.localPosition = v.SlotLocalPosition(slot, len);
            c.transform.localRotation = Quaternion.identity;
            v.SetLoaded(v.Loaded + len);
        }

        // ------------------------------------------------------------------ actions
        private IEnumerator TapAt(ContainerView c)
        {
            TapWorld = c.transform.position + Vector3.up * 0.4f;
            TapTime = Time.time;
            yield return new WaitForSeconds(0.42f);
            TapWorld = null;
        }

        private IEnumerator TapChip()
        {
            ChipTapped = true;
            TapTime = Time.time;
            yield return new WaitForSeconds(0.45f);
            ChipTapped = false;
        }

        private IEnumerator Send(ContainerView c, VehicleView v, bool tap = true)
        {
            if (tap) yield return TapAt(c);
            int len = c.Length, slot = v.Loaded;
            c.Detach();
            c.SetRevealed(true);
            yield return Tween.Arc(c.transform, v.SlotWorldPosition(slot, len), 1.4f, 0.34f, BoardLayout.ContainerScale(len) * v.StowScale, v.transform.rotation);
            c.transform.SetParent(v.transform, true);
            c.transform.localPosition = v.SlotLocalPosition(slot, len);
            c.transform.localRotation = Quaternion.identity;
            v.SetLoaded(v.Loaded + len);
            StartCoroutine(Tween.Pop(c.transform, 0.25f, 0.18f));
        }

        private IEnumerator ToBay(ContainerView c, int slot, int capacity, bool tap = true)
        {
            if (tap) yield return TapAt(c);
            c.Detach();
            c.SetRevealed(true);
            yield return Tween.Arc(c.transform, W(BoardLayout.BufferPosition(slot, c.Length, capacity)), 1.2f, 0.3f, BoardLayout.BufferScale(c.Length, capacity), Quaternion.identity);
            StartCoroutine(Tween.Pop(c.transform, 0.2f, 0.16f));
        }

        private IEnumerator Leave(VehicleView v)
        {
            StartCoroutine(Tween.Pop(v.transform, 0.12f, 0.25f));
            yield return new WaitForSeconds(0.15f);
            yield return v.Depart(0.85f);
            if (v) Destroy(v.gameObject);
        }

        private static WaitForSeconds Wait(float s) => new WaitForSeconds(s);

        // ------------------------------------------------------------------ storyboards
        private IEnumerator Script(DemoKind kind)
        {
            switch (kind)
            {
                case DemoKind.Basics:
                {
                    var v = Vehicle(0, 3, 0);
                    if (!_trucks) Stow(Box(0, 0, 0), v, 0);   // a ship of 3 starts 1/3 loaded, so two taps fill it
                    var a = Box(0, -1, 0); var b = Box(1, 0, 0); var c = Box(0, 1, 0);
                    Box(1, -1, 1); Box(0, 0, 1); Box(1, 1, 1);
                    yield return Wait(0.6f);
                    yield return Send(a, v);
                    yield return Wait(0.2f);
                    yield return Send(c, v);
                    yield return Wait(0.25f);
                    StartCoroutine(Leave(v));
                    yield return Wait(0.5f);
                    var next = Vehicle(1, 3, 0, docked: false);
                    yield return next.Arrive(Berth(0), 0.6f);
                    yield return Wait(0.25f);
                    yield return Send(b, next);
                    yield return Wait(0.6f);
                    break;
                }
                case DemoKind.Bay:
                {
                    var v = Vehicle(0, 3, 0);
                    for (int i = 0; i < Capacity(3) - 1; i++) Stow(Box(0, 0, 0), v, v.Loaded);
                    var odd = Box(1, -1, 0); var good = Box(0, 1, 0);
                    Box(2, 0, 0); Box(2, -1, 1); Box(1, 0, 1); Box(2, 1, 1);
                    yield return Wait(0.6f);
                    yield return ToBay(odd, 0, 5);          // no vehicle of its colour yet: it waits in the bay
                    _bay.SetUsed(1);
                    yield return Wait(0.5f);
                    yield return Send(good, v);
                    yield return Wait(0.2f);
                    StartCoroutine(Leave(v));
                    yield return Wait(0.5f);
                    var next = Vehicle(1, 3, 0, docked: false);
                    yield return next.Arrive(Berth(0), 0.6f);
                    yield return Wait(0.2f);
                    _bay.SetUsed(0);
                    yield return Send(odd, next, tap: false);   // its vehicle is in: it boards by itself
                    yield return Wait(0.8f);
                    break;
                }
                case DemoKind.Long:
                {
                    var v = Vehicle(0, 4, 0);
                    var front = Box(0, -1, 0, 2); Box(1, 1, 0);
                    var back = Box(0, -1, 1, 2); Box(2, 1, 1);
                    yield return Wait(0.6f);
                    yield return Send(front, v);
                    back.SetDim(false);
                    yield return Wait(0.5f);
                    if (_trucks)
                    {
                        StartCoroutine(Leave(v));
                        yield return Wait(0.5f);
                        v = Vehicle(0, 4, 0, docked: false);
                        yield return v.Arrive(Berth(0), 0.6f);
                    }
                    yield return Send(back, v);
                    yield return Wait(0.8f);
                    break;
                }
                case DemoKind.Locked:
                {
                    var v = Vehicle(0, 3, 0);
                    if (!_trucks) Stow(Box(0, 0, 0), v, 0);
                    var key = Box(0, -1, 0, keyId: 0);
                    var locked = Box(0, 1, 0, kind: ContainerKind.Locked, lockId: 0);
                    locked.SetDim(true);
                    Box(2, 0, 0); Box(1, -1, 1); Box(2, 0, 1); Box(1, 1, 1);
                    yield return Wait(0.6f);
                    yield return TapAt(locked);                  // locked: it won't move
                    yield return Tween.Shake(locked.transform);
                    yield return Wait(0.3f);
                    yield return Send(key, v);                   // the gold key leaves...
                    locked.SetLocked(false);                     // ...and the lock opens
                    locked.SetDim(false);
                    yield return Tween.Pop(locked.transform, 0.3f, 0.3f);
                    yield return Wait(0.35f);
                    yield return Send(locked, v);
                    yield return Wait(0.8f);
                    break;
                }
                case DemoKind.Hidden:
                {
                    var v = Vehicle(0, 3, 0);
                    if (!_trucks) Stow(Box(0, 0, 0), v, 0);
                    var front = Box(0, 0, 0);
                    var mystery = Box(0, 0, 1, kind: ContainerKind.Hidden);
                    Box(1, -1, 0); Box(2, 1, 0); Box(2, -1, 1); Box(1, 1, 1);
                    yield return Wait(0.7f);
                    yield return Send(front, v);
                    yield return Wait(0.15f);
                    mystery.SetDim(false);                       // a clear path: its colour shows
                    mystery.SetRevealed(true);
                    yield return Tween.Pop(mystery.transform, 0.3f, 0.3f);
                    yield return Wait(0.45f);
                    yield return Send(mystery, v);
                    yield return Wait(0.8f);
                    break;
                }
                case DemoKind.ExtraBerth:
                {
                    // A short bay (4 slots, so the scene stays readable) that is nearly full gets one more slot.
                    const int Before = 4, After = 5;
                    _bay.SetCapacity(Before);
                    Vehicle(0, 3, 0);
                    int[] waiting = { 1, 2, 1 };
                    var parked = new System.Collections.Generic.List<ContainerView>();
                    for (int i = 0; i < waiting.Length; i++)
                    {
                        var w = Box(waiting[i], 0, 0);
                        w.Detach();
                        w.transform.localPosition = BoardLayout.BufferPosition(i, 1, Before);
                        w.transform.localScale = BoardLayout.BufferScale(1, Before);
                        parked.Add(w);
                    }
                    _bay.SetUsed(waiting.Length);
                    var next = Box(2, 0, 0); Box(1, -1, 0); Box(2, 1, 0);
                    yield return Wait(0.7f);
                    yield return TapChip();
                    _bay.SetCapacity(After);                     // one more slot; the waiting containers make room
                    _bay.SetUsed(waiting.Length);
                    for (int i = 0; i < parked.Count; i++)
                    {
                        StartCoroutine(Tween.Move(parked[i].transform, W(BoardLayout.BufferPosition(i, 1, After)), 0.25f));
                        StartCoroutine(Tween.ScaleTo(parked[i].transform, BoardLayout.BufferScale(1, After), 0.25f));
                    }
                    yield return Wait(0.6f);
                    yield return ToBay(next, waiting.Length, After);
                    _bay.SetUsed(waiting.Length + 1);
                    yield return Wait(0.9f);
                    break;
                }
                case DemoKind.CraneLift:
                {
                    var v = Vehicle(0, 3, 0);
                    if (!_trucks) Stow(Box(0, 0, 0), v, 0);
                    Box(1, -1, 0); Box(2, 0, 0); Box(1, 1, 0);
                    var boxedIn = Box(0, 0, 1);
                    Box(2, -1, 1); Box(2, 1, 1);
                    yield return Wait(0.6f);
                    yield return TapChip();
                    yield return Wait(0.15f);
                    yield return TapAt(boxedIn);
                    boxedIn.Detach();
                    var rig = CraneRig.Create(_root, _pal);
                    rig.Silent = true;
                    int slot = v.Loaded;
                    yield return rig.Carry(boxedIn.transform, () => v.SlotWorldPosition(slot, 1), BoardLayout.ContainerScale(1) * v.StowScale, v.transform.rotation);
                    boxedIn.transform.SetParent(v.transform, true);
                    boxedIn.transform.localPosition = v.SlotLocalPosition(slot, 1);
                    boxedIn.transform.localRotation = Quaternion.identity;
                    v.SetLoaded(v.Loaded + 1);
                    yield return Wait(1.0f);
                    break;
                }
                case DemoKind.Undo:
                {
                    var v = Vehicle(0, 3, 0);
                    if (!_trucks) Stow(Box(0, 0, 0), v, 0);
                    var wrong = Box(1, -1, 0); var right = Box(0, 0, 0);
                    Box(2, 1, 0); Box(2, -1, 1); Box(1, 0, 1); Box(2, 1, 1);
                    Vector3 home = wrong.transform.position, homeScale = wrong.transform.localScale;
                    yield return Wait(0.6f);
                    yield return ToBay(wrong, 0, 5);
                    _bay.SetUsed(1);
                    yield return Wait(0.5f);
                    yield return TapChip();
                    yield return Tween.Arc(wrong.transform, home, 1.2f, 0.3f, homeScale, Quaternion.identity);   // back to its cell
                    _bay.SetUsed(0);
                    yield return Tween.Pop(wrong.transform, 0.15f, 0.18f);
                    yield return Wait(0.4f);
                    yield return Send(right, v);                  // and the better move instead
                    yield return Wait(0.8f);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Sits on a card's mini screen: draws the demo's tap (pulsing rings, the in-game tap marker's look) over the
    /// container or booster being tapped, and the booster chip in the corner for booster demos.
    /// </summary>
    public class DemoOverlay : MonoBehaviour
    {
        private RectTransform _rt, _marker, _ringA, _ringB, _chip, _chipIconHolder;
        private Image _ringAImg;
        private string _chipShown;
        private Palette _pal;

        public static DemoOverlay Attach(RectTransform screen, Palette pal)
        {
            var o = screen.gameObject.AddComponent<DemoOverlay>();
            o._rt = screen;
            o._pal = pal;
            o.Build();
            return o;
        }

        private void Build()
        {
            var chipImg = UiFactory.Panel("BoosterChip", _rt, _pal.uiCream, Palette.Hex("2E2422"), 6f);
            _chip = (RectTransform)chipImg.transform;
            _chip.anchorMin = _chip.anchorMax = _chip.pivot = new Vector2(1, 0);
            _chip.anchoredPosition = new Vector2(-22, 26);
            _chip.sizeDelta = new Vector2(124, 112);
            chipImg.raycastTarget = false;
            _chipIconHolder = UiFactory.Rect("Icon", _chip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66, 66));
            _chip.gameObject.SetActive(false);

            _marker = UiFactory.Rect("Tap", _rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
            _ringA = Ring("RingA", 120, 10f);
            _ringAImg = _ringA.GetComponent<Image>();
            _ringB = Ring("RingB", 74, 12f);
            var dot = UiFactory.Rect("Dot", _marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
            var di = dot.gameObject.AddComponent<Image>();
            di.sprite = UiFactory.Disc; di.color = Color.white; di.raycastTarget = false;
            _marker.gameObject.SetActive(false);
        }

        private RectTransform Ring(string name, float size, float thickness)
        {
            var r = UiFactory.Rect(name, _marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = UiFactory.RingSprite(thickness / size);
            img.color = Color.white;
            img.raycastTarget = false;
            return r;
        }

        private void LateUpdate()
        {
            var stage = DemoStage.Instance;
            if (stage == null) return;

            if (stage.ChipIcon != _chipShown)
            {
                _chipShown = stage.ChipIcon;
                foreach (Transform c in _chipIconHolder) Destroy(c.gameObject);
                if (_chipShown != null) UiFactory.Icon(_chipShown, _chipIconHolder, _pal.uiText, 66);
                _chip.gameObject.SetActive(_chipShown != null);
            }

            Vector2? at = null;
            var size = _rt.rect.size;
            if (stage.ChipTapped) at = new Vector2(size.x * 0.5f - 22 - 62, -size.y * 0.5f + 26 + 56);
            else if (stage.TapWorld.HasValue)
            {
                var vp = stage.Cam.WorldToViewportPoint(stage.TapWorld.Value);
                at = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
            }
            _marker.gameObject.SetActive(at.HasValue);
            if (!at.HasValue) return;
            _marker.anchoredPosition = at.Value;
            _marker.SetAsLastSibling();
            float e = Mathf.Clamp01((Time.time - stage.TapTime) / 0.42f);
            _ringA.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.25f, e);
            _ringAImg.color = new Color(1, 1, 1, 1f - e);
            _ringB.localScale = Vector3.one * (e < 0.3f ? Mathf.Lerp(1f, 0.8f, e / 0.3f) : Mathf.Lerp(0.8f, 1f, (e - 0.3f) / 0.7f));
            if (stage.ChipTapped) _chip.localScale = Vector3.one * (e < 0.3f ? Mathf.Lerp(1f, 0.9f, e / 0.3f) : Mathf.Lerp(0.9f, 1f, (e - 0.3f) / 0.7f));
        }
    }
}
