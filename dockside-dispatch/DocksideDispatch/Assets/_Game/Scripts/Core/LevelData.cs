using System;
using System.Collections.Generic;

namespace Dockside.Core
{
    /// <summary>
    /// Load: containers in the yard go onto ships at the berths. Unload: containers in a ship's hold go onto trucks at the
    /// loading bays. The rules are the same; only the vehicles (ShipSpec = ship or truck) and the scenery differ.
    /// </summary>
    public enum GameMode { Load = 0, Unload = 1 }

    public enum ContainerKind
    {
        Normal = 0,
        /// <summary>Cannot be dispatched until the key container with the matching id has left the yard.</summary>
        Locked = 1,
        /// <summary>Colour is hidden until the container has a clear path to the quay.</summary>
        Hidden = 2,
    }

    /// <summary>One container in the yard. Long containers occupy two cells horizontally (x and x+1).</summary>
    [Serializable]
    public class ContainerSpec
    {
        public int id;
        public int x;
        public int y;           // row 0 is the row next to the quay
        public int length = 1;  // 1 or 2 (cells, and ship/holding slots)
        public int color;       // index into the palette
        public ContainerKind kind = ContainerKind.Normal;
        public int lockId = -1; // Locked: unlocked when a key with the same id leaves the yard
        public int keyId = -1;  // >= 0 means this container carries a key

        public ContainerSpec Clone() => (ContainerSpec)MemberwiseClone();
    }

    /// <summary>A vehicle in the queue: a ship (Load) or a truck (Unload).</summary>
    [Serializable]
    public class ShipSpec
    {
        public int color;
        public int capacity = 3; // slots

        public ShipSpec Clone() => (ShipSpec)MemberwiseClone();
    }

    [Serializable]
    public class LevelData
    {
        public int levelNumber;
        public GameMode mode;   // Load for levels baked before Unload existed (JSON default)
        public int seed;
        public int width = 6;
        public int height = 5;
        public int berths = 3;
        public int bufferSize = 5;
        public int colorCount = 4;
        public float casualWinRate = -1f; // generator's difficulty estimate (share of simulated casual players who win)
        public List<ContainerSpec> containers = new List<ContainerSpec>();
        public List<ShipSpec> ships = new List<ShipSpec>();

        public LevelData Clone()
        {
            var c = (LevelData)MemberwiseClone();
            c.containers = new List<ContainerSpec>(containers.Count);
            foreach (var x in containers) c.containers.Add(x.Clone());
            c.ships = new List<ShipSpec>(ships.Count);
            foreach (var s in ships) c.ships.Add(s.Clone());
            return c;
        }

        /// <summary>Checks the level is internally consistent. Returns null when valid, otherwise a reason.</summary>
        public string Validate()
        {
            if (width < 1 || height < 1) return "Grid must be at least 1x1.";
            if (berths < 1) return "Need at least one berth.";
            if (bufferSize < 1) return "Need at least one holding slot.";
            var occupied = new bool[width, height];
            var slotsByColor = new Dictionary<int, int>();
            var ids = new HashSet<int>();
            var keys = new HashSet<int>();
            var locks = new HashSet<int>();
            foreach (var c in containers)
            {
                if (!ids.Add(c.id)) return $"Duplicate container id {c.id}.";
                if (c.length != 1 && c.length != 2) return $"Container {c.id} has invalid length {c.length}.";
                for (int i = 0; i < c.length; i++)
                {
                    int cx = c.x + i;
                    if (cx < 0 || cx >= width || c.y < 0 || c.y >= height) return $"Container {c.id} is outside the grid.";
                    if (occupied[cx, c.y]) return $"Container {c.id} overlaps another container.";
                    occupied[cx, c.y] = true;
                }
                slotsByColor.TryGetValue(c.color, out int n);
                slotsByColor[c.color] = n + c.length;
                if (c.keyId >= 0) keys.Add(c.keyId);
                if (c.kind == ContainerKind.Locked) locks.Add(c.lockId);
            }
            foreach (var l in locks)
                if (!keys.Contains(l)) return $"Lock {l} has no matching key.";
            var shipSlots = new Dictionary<int, int>();
            foreach (var s in ships)
            {
                if (s.capacity < 1) return "Ship capacity must be positive.";
                shipSlots.TryGetValue(s.color, out int n);
                shipSlots[s.color] = n + s.capacity;
            }
            foreach (var kv in slotsByColor)
            {
                shipSlots.TryGetValue(kv.Key, out int n);
                if (n != kv.Value) return $"Colour {kv.Key}: containers fill {kv.Value} slots but ships carry {n}.";
            }
            foreach (var kv in shipSlots)
                if (!slotsByColor.ContainsKey(kv.Key)) return $"Colour {kv.Key} has ships but no containers.";
            return null;
        }
    }
}
