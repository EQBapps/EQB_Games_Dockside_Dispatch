using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// The free lane behind the loading bays (Unload mode), where the puzzle trucks drive left to right: coming in to turn
    /// into their bay, or pulling out full. It carries nothing else (the trucks passing by drive through the terminal yard
    /// behind the stacks, YardTraffic), so a truck is never held up by scenery. Vehicles in the lane keep a safe gap to the
    /// one ahead; a truck only pulls out of its bay when the lane beside it is clear; each bay serves its trucks in the
    /// order they were sent, and the next one waits short of the bay until the previous truck has left it.
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
            public float X => t ? t.position.x : float.MaxValue;
        }

        public const float MinGap = 0.45f, Accel = 12f, Brake = 14f, HardBrake = 40f;

        private readonly List<Car> _cars = new List<Car>();
        private readonly Dictionary<int, List<Object>> _bays = new Dictionary<int, List<Object>>();
        private readonly List<(Object who, float priority)> _entryQueue = new List<(Object, float)>();
        private float _halfW;

        // Just off screen (halfW is 1.5 past the screen edge; a truck is about 2.5 long), so trucks are in view at once.
        public float EntryX => -_halfW - 0.5f;
        public float ExitX => _halfW + 0.5f;
        public float LaneZ => EnvironmentBuilder.PassingLaneZ;

        public void Init(float halfW)
        {
            Instance = this;
            _halfW = halfW;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ registry
        public Car Add(Transform t, float half)
        {
            var c = new Car { t = t, half = half };
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
        // Each bay keeps the trucks booked for it in the order they were sent; only the first may turn in. A truck books
        // its bay as soon as it is sent, so the next truck for that bay waits short of it instead of following the
        // previous one in.
        private static int BayKey(float x) => Mathf.RoundToInt(x * 10f);

        private List<Object> Bookings(float x)
        {
            if (!_bays.TryGetValue(BayKey(x), out var list)) _bays[BayKey(x)] = list = new List<Object>();
            list.RemoveAll(o => o == null);
            return list;
        }

        public void BookBay(float x, Object who) { var l = Bookings(x); if (!l.Contains(who)) l.Add(who); }
        public bool BayFree(float x, Object who) { var l = Bookings(x); return l.Count == 0 || l[0] == who; }
        public void ReleaseBay(float x, Object who) => Bookings(x).Remove(who);

        private void Update() => _cars.RemoveAll(c => !c.t);
    }
}
