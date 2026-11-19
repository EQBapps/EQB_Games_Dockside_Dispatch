using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// The free lane behind the loading bays (Unload mode), where every truck drives left to right: puzzle trucks coming
    /// in to turn into their bay or pulling out full, and passing trucks with muted boxes. Vehicles in the lane keep a
    /// safe gap to the one ahead (they brake and queue rather than drive through each other); a truck only pulls out of
    /// its bay when the lane beside it is clear; an incoming truck waits short of its bay until the previous truck has
    /// left it; passing trucks only enter when the entry is clear.
    /// Puzzle trucks queue for the entry off screen and only become lane vehicles once they enter, so two waiting trucks
    /// can never block each other (they used to, when a slow frame let two of them reach the entry together).
    /// </summary>
    public class LaneTraffic : MonoBehaviour
    {
        public static LaneTraffic Instance { get; private set; }

        public class Car
        {
            public Transform t;
            public float half;     // half length along the lane
            public float v;        // current speed (+x)
            public bool ambient;
            public float X => t ? t.position.x : float.MaxValue;
        }

        public const float MinGap = 0.45f, Accel = 12f, Brake = 14f, HardBrake = 40f;
        private const float AmbientSpeed = 2.6f;
        private static readonly Color[] Cabs = { Palette.Hex("ECE7DD"), Palette.Hex("AEB4BC"), Palette.Hex("4E5562"), Palette.Hex("8C4F4A"), Palette.Hex("3F5F7A") };
        private static readonly Color[] Cargo = { Palette.Hex("B0605A"), Palette.Hex("5F7F96"), Palette.Hex("C2A266"), Palette.Hex("6F8E74"), Palette.Hex("9A948E"), Palette.Hex("D6CFC2") };

        private readonly List<Car> _cars = new List<Car>();
        private readonly Dictionary<int, Object> _bays = new Dictionary<int, Object>();
        private readonly List<(Object who, float priority)> _entryQueue = new List<(Object, float)>();
        private float _halfW, _next;
        private Palette _pal;
        private SkyTheme _theme;

        public float EntryX => -_halfW - 3f;
        public float ExitX => _halfW + 3f;
        public float LaneZ => EnvironmentBuilder.PassingLaneZ;

        public void Init(float halfW, Palette pal, SkyTheme theme)
        {
            Instance = this;
            _halfW = halfW;
            _pal = pal;
            _theme = theme;
            _next = Time.time + Random.Range(1.5f, 4f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ registry
        public Car Add(Transform t, float half, bool ambient = false)
        {
            var c = new Car { t = t, half = half, ambient = ambient };
            _cars.Add(c);
            return c;
        }

        public void Remove(Car c) => _cars.Remove(c);

        /// <summary>No vehicle in the lane overlaps [x0, x1] (other than the one asking).</summary>
        public bool ZoneClear(float x0, float x1, Car except = null)
        {
            foreach (var c in _cars)
            {
                if (c == except || !c.t) continue;
                if (c.X + c.half > x0 && c.X - c.half < x1) return false;
            }
            return true;
        }

        /// <summary>Highest speed that keeps a safe gap to the vehicle ahead.</summary>
        public float Allowed(Car me, float desired)
        {
            Car ahead = null;
            float best = float.MaxValue, x = me.X;
            foreach (var c in _cars)
            {
                if (c == me || !c.t) continue;
                float cx = c.X;
                if (cx > x && cx < best) { best = cx; ahead = c; }
            }
            if (ahead == null) return desired;
            float gap = (ahead.X - ahead.half) - (x + me.half);
            if (gap <= MinGap) return 0f;
            return Mathf.Min(desired, Mathf.Sqrt(2f * Brake * (gap - MinGap)) + ahead.v);
        }

        /// <summary>Moves a lane vehicle one frame towards its desired speed, never closer than MinGap to the one ahead.</summary>
        public void Step(Car me, float desired, float maxX = float.MaxValue)
        {
            float allowed = Allowed(me, desired);
            me.v = allowed < me.v ? Mathf.Max(allowed, me.v - HardBrake * Time.deltaTime) : Mathf.Min(allowed, me.v + Accel * Time.deltaTime);
            var p = me.t.position;
            p.x = Mathf.Min(p.x + me.v * Time.deltaTime, maxX);
            me.t.position = p;
        }

        // ------------------------------------------------------------------ entry queue
        /// <summary>
        /// A puzzle truck waiting off screen asks to enter the lane. The queued truck heading furthest (highest priority, the
        /// bay's x) goes first, once the entry is clear. True means "enter now" (the caller then registers with Add).
        /// </summary>
        public bool TryEnter(Object who, float priority, float half)
        {
            _entryQueue.RemoveAll(q => q.who == null);
            if (_entryQueue.FindIndex(q => q.who == who) < 0) _entryQueue.Add((who, priority));
            var head = _entryQueue[0];
            foreach (var q in _entryQueue) if (q.priority > head.priority) head = q;
            if (head.who != who || !ZoneClear(EntryX - half - 0.3f, EntryX + half + 0.8f)) return false;
            LeaveQueue(who);
            return true;
        }

        public void LeaveQueue(Object who) => _entryQueue.RemoveAll(q => q.who == who || q.who == null);

        // ------------------------------------------------------------------ loading bays
        private static int BayKey(float x) => Mathf.RoundToInt(x * 10f);
        public bool BayFree(float x, Object who) => !_bays.TryGetValue(BayKey(x), out var o) || o == null || o == who;
        public void ClaimBay(float x, Object who) => _bays[BayKey(x)] = who;
        public void ReleaseBay(float x, Object who) { if (_bays.TryGetValue(BayKey(x), out var o) && o == who) _bays.Remove(BayKey(x)); }

        // ------------------------------------------------------------------ passing trucks
        private void Update()
        {
            int ambient = 0;
            foreach (var c in _cars) if (c.ambient) ambient++;
            float half = TruckView.Length * 0.5f;
            _entryQueue.RemoveAll(q => q.who == null);
            // Passing trucks never cut in while a puzzle truck is queued for the entry.
            if (Time.time >= _next && ambient < 2 && _entryQueue.Count == 0 && ZoneClear(EntryX - half - 0.3f, EntryX + half + 1.2f))
            {
                var col = Cargo[Random.Range(0, Cargo.Length)];
                var t = TruckView.CreateAmbient(transform, Cabs[Random.Range(0, Cabs.Length)], Color.Lerp(col, _theme.haze, 0.12f),
                    Random.value < 0.5f ? 2 : 1, _pal, _theme);
                t.transform.position = new Vector3(EntryX, 0, LaneZ);
                var car = Add(t.transform, half, true);
                car.v = AmbientSpeed;
                _next = Time.time + Random.Range(5f, 10f);
            }
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                var c = _cars[i];
                if (!c.t) { _cars.RemoveAt(i); continue; }
                if (!c.ambient) continue;   // puzzle trucks drive themselves (TruckView)
                Step(c, AmbientSpeed);
                if (c.X > ExitX) { Destroy(c.t.gameObject); _cars.RemoveAt(i); }
            }
        }
    }
}
