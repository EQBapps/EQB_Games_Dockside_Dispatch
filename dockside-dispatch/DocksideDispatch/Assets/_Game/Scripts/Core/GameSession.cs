using System;
using System.Collections.Generic;
using System.Text;

namespace Dockside.Core
{
    public enum GameEventType
    {
        ContainerToShip,     // container left the yard straight onto a ship
        ContainerToBuffer,   // container left the yard into the holding bay
        BufferToShip,        // a waiting container moved from the holding bay onto a ship
        BufferCompacted,     // holding bay items slid left to close gaps
        ShipDeparted,
        ShipArrived,
        ContainerRevealed,   // hidden container's colour is now visible
        ContainerUnlocked,
        ReachabilityChanged, // which yard containers can be tapped has changed
        Won,
        Lost,
    }

    public struct GameEvent
    {
        public GameEventType type;
        public int containerId; // -1 when not applicable
        public int berth;       // -1 when not applicable
        public int slot;        // ship slot index or holding-bay slot index
        public int shipIndex;   // index into LevelData.ships

        public override string ToString() =>
            $"{type}(c={containerId}, berth={berth}, slot={slot}, ship={shipIndex})";
    }

    public enum TapResult
    {
        Accepted,
        GameOver,
        NotInYard,
        Blocked,    // no clear path to the quay
        Locked,
        NoRoom,     // holding bay has no space for this container
    }

    public enum LoseReason { None, BufferFull, NoMoves }

    /// <summary>
    /// Pure game rules for one level. No Unity dependencies, so it can be unit-tested and
    /// cloned cheaply by the solver. Every state change is reported as an ordered list of events
    /// that the presentation layer animates.
    /// </summary>
    public sealed class GameSession
    {
        public LevelData Level { get; private set; }
        public int Width => Level.width;
        public int Height => Level.height;

        // ---- yard ----
        private int[,] _cells;            // container index or -1
        private bool[] _inYard;
        private bool[] _locked;
        private bool[] _revealed;
        private bool[] _reachable;
        private int _yardCount;

        // ---- ships ----
        private int[] _berthShip;         // ship index at each berth, or -1
        private List<int>[] _berthLoad;   // container indices loaded on the ship at each berth
        private int _nextShip;            // next ship index in the queue
        private int _shipsDeparted;

        // ---- holding bay ----
        private List<int> _buffer;        // container indices in order (compacted)
        public int BufferCapacity { get; private set; }

        public bool IsWon { get; private set; }
        public bool IsLost { get; private set; }
        public LoseReason LoseReason { get; private set; }
        public bool IsOver => IsWon || IsLost;
        public int MovesMade { get; private set; }

        /// <summary>When false, events are not recorded (used by the solver for speed).</summary>
        public bool RecordEvents = true;
        private readonly List<GameEvent> _events = new List<GameEvent>();

        private GameSession() { }

        public GameSession(LevelData level)
        {
            string err = level.Validate();
            if (err != null) throw new ArgumentException("Invalid level: " + err);
            Level = level.Clone();
            int n = Level.containers.Count;
            _cells = new int[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    _cells[x, y] = -1;
            _inYard = new bool[n];
            _locked = new bool[n];
            _revealed = new bool[n];
            _reachable = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var c = Level.containers[i];
                for (int k = 0; k < c.length; k++) _cells[c.x + k, c.y] = i;
                _inYard[i] = true;
                _locked[i] = c.kind == ContainerKind.Locked;
                _revealed[i] = c.kind != ContainerKind.Hidden;
            }
            _yardCount = n;
            _berthShip = new int[Level.berths];
            _berthLoad = new List<int>[Level.berths];
            for (int b = 0; b < Level.berths; b++) { _berthShip[b] = -1; _berthLoad[b] = new List<int>(); }
            _buffer = new List<int>();
            BufferCapacity = Level.bufferSize;

            // Dock the first ships. Events from setup are discarded: views build from state.
            for (int b = 0; b < Level.berths && _nextShip < Level.ships.Count; b++)
                _berthShip[b] = _nextShip++;
            RecomputeReachability();
            _events.Clear();
        }

        // =====================================================================
        // Queries
        // =====================================================================
        public int ContainerCount => Level.containers.Count;
        public ContainerSpec Spec(int index) => Level.containers[index];
        public bool InYard(int index) => _inYard[index];
        public bool IsLocked(int index) => _locked[index];
        public bool IsRevealed(int index) => _revealed[index];
        public bool IsReachable(int index) => _inYard[index] && _reachable[index];
        public bool CanTap(int index) => IsReachable(index) && !_locked[index];
        public int YardCount => _yardCount;
        public int CellAt(int x, int y) => _cells[x, y];

        public int BerthCount => Level.berths;
        public int ShipAtBerth(int berth) => _berthShip[berth];
        public IReadOnlyList<int> LoadAtBerth(int berth) => _berthLoad[berth];
        public int ShipsTotal => Level.ships.Count;
        public int ShipsDeparted => _shipsDeparted;
        public int ShipsWaiting => Level.ships.Count - _nextShip;
        public int NextShipIndex => _nextShip;

        public IReadOnlyList<int> Buffer => _buffer;
        public int BufferUsed
        {
            get { int s = 0; foreach (var i in _buffer) s += Level.containers[i].length; return s; }
        }
        public int BufferFree => BufferCapacity - BufferUsed;

        /// <summary>First holding-bay slot index occupied by the buffer item at list position p.</summary>
        public int BufferSlotOfPosition(int p)
        {
            int s = 0;
            for (int i = 0; i < p; i++) s += Level.containers[_buffer[i]].length;
            return s;
        }

        public int FreeSlotsOnBerth(int berth)
        {
            int ship = _berthShip[berth];
            if (ship < 0) return 0;
            int used = 0;
            foreach (var i in _berthLoad[berth]) used += Level.containers[i].length;
            return Level.ships[ship].capacity - used;
        }

        /// <summary>Slot index the next container of this length would occupy on the ship at the berth.</summary>
        private int NextSlotOnBerth(int berth)
        {
            int used = 0;
            foreach (var i in _berthLoad[berth]) used += Level.containers[i].length;
            return used;
        }

        /// <summary>Berth that would take this container right now, or -1.</summary>
        public int BerthFor(int containerIndex)
        {
            var c = Level.containers[containerIndex];
            int best = -1, bestFree = int.MaxValue;
            for (int b = 0; b < Level.berths; b++)
            {
                int ship = _berthShip[b];
                if (ship < 0 || Level.ships[ship].color != c.color) continue;
                int free = FreeSlotsOnBerth(b);
                if (free < c.length) continue;
                // Prefer the ship closest to full so it departs sooner; ties go to the leftmost berth.
                if (free < bestFree) { best = b; bestFree = free; }
            }
            return best;
        }

        /// <summary>All yard containers that can legally be tapped now.</summary>
        public List<int> LegalMoves()
        {
            var list = new List<int>();
            if (IsOver) return list;
            for (int i = 0; i < _inYard.Length; i++)
            {
                if (!CanTap(i)) continue;
                if (BerthFor(i) >= 0 || BufferFree >= Level.containers[i].length) list.Add(i);
            }
            return list;
        }

        // =====================================================================
        // Actions
        // =====================================================================

        /// <summary>Tap a yard container. With ignorePath (Crane Lift booster) any unlocked container can be taken.</summary>
        public TapResult Tap(int containerIndex, List<GameEvent> outEvents = null, bool ignorePath = false)
        {
            _events.Clear();
            if (IsOver) return TapResult.GameOver;
            if (containerIndex < 0 || containerIndex >= _inYard.Length || !_inYard[containerIndex]) return TapResult.NotInYard;
            if (_locked[containerIndex]) return TapResult.Locked;
            if (!ignorePath && !_reachable[containerIndex]) return TapResult.Blocked;

            var c = Level.containers[containerIndex];
            int berth = BerthFor(containerIndex);
            if (berth < 0 && BufferFree < c.length) return TapResult.NoRoom;

            RemoveFromYard(containerIndex);
            if (berth >= 0)
            {
                int slot = NextSlotOnBerth(berth);
                _berthLoad[berth].Add(containerIndex);
                Emit(GameEventType.ContainerToShip, containerIndex, berth, slot, _berthShip[berth]);
            }
            else
            {
                int slot = BufferUsed;
                _buffer.Add(containerIndex);
                Emit(GameEventType.ContainerToBuffer, containerIndex, -1, slot, -1);
            }
            MovesMade++;
            HandleKey(c);
            ResolveCascade();
            RecomputeReachability();
            CheckEnd();
            outEvents?.AddRange(_events);
            return TapResult.Accepted;
        }

        /// <summary>Booster: one extra holding-bay slot for the rest of the level.</summary>
        public void AddBufferSlots(int n, List<GameEvent> outEvents = null)
        {
            _events.Clear();
            BufferCapacity += n;
            if (IsLost)
            {
                IsLost = false;
                LoseReason = LoseReason.None;
                CheckEnd();
            }
            outEvents?.AddRange(_events);
        }

        /// <summary>Booster: shuffle colours among yard containers of the same length. Counts per colour are kept.</summary>
        public void ShuffleYard(Random rng, List<GameEvent> outEvents = null)
        {
            _events.Clear();
            for (int len = 1; len <= 2; len++)
            {
                var idx = new List<int>();
                var colors = new List<int>();
                for (int i = 0; i < _inYard.Length; i++)
                {
                    if (!_inYard[i] || Level.containers[i].length != len) continue;
                    idx.Add(i);
                    colors.Add(Level.containers[i].color);
                }
                for (int i = colors.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (colors[i], colors[j]) = (colors[j], colors[i]);
                }
                for (int i = 0; i < idx.Count; i++) Level.containers[idx[i]].color = colors[i];
            }
            Emit(GameEventType.ReachabilityChanged, -1, -1, -1, -1);
            CheckEnd();
            outEvents?.AddRange(_events);
        }

        // =====================================================================
        // Internals
        // =====================================================================
        private void Emit(GameEventType t, int c, int berth, int slot, int ship)
        {
            if (!RecordEvents) return;
            _events.Add(new GameEvent { type = t, containerId = c, berth = berth, slot = slot, shipIndex = ship });
        }

        private void RemoveFromYard(int i)
        {
            var c = Level.containers[i];
            for (int k = 0; k < c.length; k++) _cells[c.x + k, c.y] = -1;
            _inYard[i] = false;
            _yardCount--;
        }

        private void HandleKey(ContainerSpec c)
        {
            if (c.keyId < 0) return;
            for (int i = 0; i < _locked.Length; i++)
            {
                if (_locked[i] && Level.containers[i].lockId == c.keyId)
                {
                    _locked[i] = false;
                    Emit(GameEventType.ContainerUnlocked, i, -1, -1, -1);
                }
            }
        }

        /// <summary>Departs full ships, docks the next ones and pulls matching containers from the holding bay, repeatedly.</summary>
        private void ResolveCascade()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int b = 0; b < Level.berths; b++)
                {
                    int ship = _berthShip[b];
                    if (ship >= 0 && FreeSlotsOnBerth(b) == 0)
                    {
                        Emit(GameEventType.ShipDeparted, -1, b, -1, ship);
                        _berthShip[b] = -1;
                        _berthLoad[b].Clear();
                        _shipsDeparted++;
                        changed = true;
                    }
                    if (_berthShip[b] < 0 && _nextShip < Level.ships.Count)
                    {
                        _berthShip[b] = _nextShip++;
                        Emit(GameEventType.ShipArrived, -1, b, -1, _berthShip[b]);
                        changed = true;
                    }
                }
                // Pull waiting containers onto any ship that wants them (in holding-bay order).
                bool pulled = false;
                for (int p = 0; p < _buffer.Count; p++)
                {
                    int ci = _buffer[p];
                    int berth = BerthFor(ci);
                    if (berth < 0) continue;
                    int slot = NextSlotOnBerth(berth);
                    _buffer.RemoveAt(p);
                    _berthLoad[berth].Add(ci);
                    Emit(GameEventType.BufferToShip, ci, berth, slot, _berthShip[berth]);
                    pulled = true;
                    changed = true;
                    p--;
                }
                if (pulled) Emit(GameEventType.BufferCompacted, -1, -1, -1, -1);
            }
        }

        private void RecomputeReachability()
        {
            // Flood fill empty cells connected to the quay (above row 0).
            var open = new bool[Width, Height];
            var queue = new Queue<(int x, int y)>();
            for (int x = 0; x < Width; x++)
            {
                if (_cells[x, 0] == -1) { open[x, 0] = true; queue.Enqueue((x, 0)); }
            }
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);
            }
            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                if (open[x, y] || _cells[x, y] != -1) return;
                open[x, y] = true;
                queue.Enqueue((x, y));
            }

            bool changed = false;
            for (int i = 0; i < _inYard.Length; i++)
            {
                bool r = false;
                if (_inYard[i])
                {
                    var c = Level.containers[i];
                    for (int k = 0; k < c.length && !r; k++)
                    {
                        int x = c.x + k, y = c.y;
                        if (y == 0) r = true;
                        else if (IsOpen(x, y - 1) || IsOpen(x, y + 1) || IsOpen(x - 1, y) || IsOpen(x + 1, y)) r = true;
                    }
                }
                if (r != _reachable[i]) { _reachable[i] = r; changed = true; }
                if (r && !_revealed[i])
                {
                    _revealed[i] = true;
                    Emit(GameEventType.ContainerRevealed, i, -1, -1, -1);
                }
            }
            if (changed) Emit(GameEventType.ReachabilityChanged, -1, -1, -1, -1);

            bool IsOpen(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && open[x, y];
        }

        private void CheckEnd()
        {
            if (IsWon || IsLost) return;
            if (_shipsDeparted == Level.ships.Count)
            {
                IsWon = true;
                Emit(GameEventType.Won, -1, -1, -1, -1);
                return;
            }
            if (BufferFree <= 0)
            {
                IsLost = true;
                LoseReason = LoseReason.BufferFull;
                Emit(GameEventType.Lost, -1, -1, -1, -1);
                return;
            }
            if (LegalMoves().Count == 0)
            {
                IsLost = true;
                LoseReason = LoseReason.NoMoves;
                Emit(GameEventType.Lost, -1, -1, -1, -1);
            }
        }

        // =====================================================================
        // Solver support
        // =====================================================================
        public GameSession Clone()
        {
            var s = new GameSession
            {
                Level = Level, // containers' colours only change via ShuffleYard, which the solver never calls
                _cells = (int[,])_cells.Clone(),
                _inYard = (bool[])_inYard.Clone(),
                _locked = (bool[])_locked.Clone(),
                _revealed = (bool[])_revealed.Clone(),
                _reachable = (bool[])_reachable.Clone(),
                _yardCount = _yardCount,
                _berthShip = (int[])_berthShip.Clone(),
                _berthLoad = new List<int>[_berthLoad.Length],
                _nextShip = _nextShip,
                _shipsDeparted = _shipsDeparted,
                _buffer = new List<int>(_buffer),
                BufferCapacity = BufferCapacity,
                IsWon = IsWon,
                IsLost = IsLost,
                LoseReason = LoseReason,
                MovesMade = MovesMade,
                RecordEvents = RecordEvents,
            };
            for (int b = 0; b < _berthLoad.Length; b++) s._berthLoad[b] = new List<int>(_berthLoad[b]);
            return s;
        }

        /// <summary>Compact key describing the state, for the solver's visited set.</summary>
        public string StateKey()
        {
            var sb = new StringBuilder(64);
            for (int i = 0; i < _inYard.Length; i++) sb.Append(_inYard[i] ? '1' : '0');
            sb.Append('|').Append(_nextShip);
            for (int b = 0; b < _berthShip.Length; b++) sb.Append('|').Append(_berthShip[b]).Append(':').Append(FreeSlotsOnBerth(b));
            // The holding bay's order does not matter for the outcome; sort its colours/lengths.
            var bay = new List<int>(_buffer.Count);
            foreach (var i in _buffer) bay.Add(Level.containers[i].color * 4 + Level.containers[i].length);
            bay.Sort();
            sb.Append('|');
            foreach (var v in bay) sb.Append(v).Append(',');
            return sb.ToString();
        }
    }
}
