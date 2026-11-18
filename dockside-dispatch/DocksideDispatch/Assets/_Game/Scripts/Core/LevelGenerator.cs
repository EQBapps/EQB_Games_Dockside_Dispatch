using System;
using System.Collections.Generic;

namespace Dockside.Core
{
    /// <summary>Knobs that shape one level. LevelGenerator.ParamsFor(n) defines the difficulty curve.</summary>
    public struct LevelParams
    {
        public int width, height, colors, berths, buffer;
        public float fill;          // share of yard cells holding containers
        public float longChance;    // chance a container piece is a 2-slot long container
        public int minCapacity, maxCapacity;
        public int locks;           // locked containers (each with its own key)
        public float hiddenChance;  // share of non-front containers with hidden colour

        public override string ToString() =>
            $"{width}x{height} colours={colors} fill={fill:0.00} long={longChance:0.00} cap={minCapacity}-{maxCapacity} locks={locks} hidden={hiddenChance:0.00}";
    }

    public static class LevelGenerator
    {
        public const int AuthoredLevelCount = 100;
        /// <summary>After the last authored level, the campaign keeps looping the final stretch (81-100).</summary>
        public const int LoopStart = 81;

        /// <summary>The authored level a level number plays (levels past the campaign loop the final stretch).</summary>
        public static int Canonical(int level)
        {
            if (level < 1) return 1;
            return level <= AuthoredLevelCount ? level : LoopStart + (level - LoopStart) % (AuthoredLevelCount - LoopStart + 1);
        }

        /// <summary>Difficulty curve for the prototype's 30 levels, with a small sawtooth (every 5th level is a spike).</summary>
        public static LevelParams ParamsFor(int level)
        {
            int n = Canonical(level);
            var p = new LevelParams { berths = 3, buffer = 5, minCapacity = 3, maxCapacity = 3, fill = 0.8f };

            if (n <= 2)        { p.width = 4; p.height = 3 + (n - 1); p.colors = 2; p.fill = 0.75f; }
            else if (n <= 5)   { p.width = 5; p.height = 4 + (n >= 5 ? 1 : 0); p.colors = 3; p.fill = 0.8f; }
            else if (n <= 8)   { p.width = 5; p.height = 5; p.colors = 3; p.longChance = 0.18f; p.maxCapacity = 4; p.fill = 0.84f; }
            else if (n <= 11)  { p.width = 6; p.height = 5; p.colors = 4; p.longChance = 0.2f; p.maxCapacity = 4; p.fill = 0.85f; }
            else if (n <= 14)  { p.width = 6; p.height = 6; p.colors = 4; p.longChance = 0.2f; p.maxCapacity = 4; p.locks = 1; p.fill = 0.85f; }
            else if (n <= 17)  { p.width = 6; p.height = 6; p.colors = 4; p.longChance = 0.25f; p.maxCapacity = 4; p.locks = 1; p.fill = 0.88f; }
            else if (n <= 21)  { p.width = 6; p.height = 6; p.colors = 4; p.longChance = 0.25f; p.maxCapacity = 4; p.locks = 1; p.hiddenChance = 0.12f; p.fill = 0.88f; }
            else if (n <= 25)  { p.width = 6; p.height = 7; p.colors = 5; p.longChance = 0.25f; p.maxCapacity = 4; p.locks = 1; p.hiddenChance = 0.15f; p.fill = 0.86f; }
            else if (n <= 30)  { p.width = 6; p.height = 7; p.colors = 5; p.longChance = 0.3f; p.maxCapacity = 4; p.locks = 2; p.hiddenChance = 0.2f; p.fill = 0.88f; }
            else
            {
                // Levels 31-100: the late game, rising gently in blocks of ten, with themed variety inside each block
                // so consecutive levels feel different (more locks, more mystery containers, more long containers).
                int block = (n - 31) / 10;                                   // 0..6
                p.width = 6; p.height = 7; p.colors = 5; p.maxCapacity = 4;
                p.longChance = Math.Min(0.36f, 0.26f + 0.015f * block);
                p.locks = Math.Min(3, 1 + (block + 1) / 3);
                p.hiddenChance = Math.Min(0.28f, 0.16f + 0.02f * block);
                p.fill = Math.Min(0.9f, 0.86f + 0.006f * block);
                switch (n % 10)
                {
                    case 2: case 7: p.locks = Math.Min(4, p.locks + 1); break;              // lock-heavy
                    case 3: case 8: p.hiddenChance = Math.Min(0.36f, p.hiddenChance + 0.1f); break; // foggy: many "?" containers
                    case 4: p.longChance = Math.Min(0.45f, p.longChance + 0.1f); break;     // long-container heavy
                }
            }

            // Sawtooth: spike every 5th level, breather right after.
            if (n % 5 == 0) p.fill = Math.Min(0.95f, p.fill + 0.06f);
            if (n % 5 == 1 && n > 1) p.fill = Math.Max(0.7f, p.fill - 0.05f);
            return p;
        }

        /// <summary>Target casual-player win rate for a level: easy tutorial, then a rising curve with spikes every 5th level.</summary>
        public static (float min, float max) TargetWinRate(int level)
        {
            int n = Canonical(level);
            (float min, float max) band = n <= 3 ? (0.6f, 1f) : n <= 10 ? (0.35f, 0.85f) : n <= 20 ? (0.2f, 0.65f) : n <= 30 ? (0.08f, 0.5f)
                : (0.06f, 0.45f);
            if (n > 30 && n % 5 == 1) band = (0.2f, 0.6f);   // breather after each spike
            if (n % 5 == 0) band = (Math.Max(0.03f, band.min - 0.08f), band.max - 0.2f);
            return band;
        }

        /// <summary>
        /// Generates a solvable level whose casual-player win rate falls in the target band
        /// (or the closest candidate found). Deterministic for a given level number.
        /// </summary>
        public static LevelData Generate(int levelNumber, int maxAttempts = 250)
        {
            var p = ParamsFor(levelNumber);
            var (min, max) = TargetWinRate(levelNumber);
            LevelData best = null;
            float bestDistance = float.MaxValue;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int seed = unchecked(levelNumber * 7919 + attempt * 104729);
                var level = TryBuild(levelNumber, seed, p);
                if (level == null || level.Validate() != null) continue;
                if (!Solver.Solve(level, 8000).solvable) continue;
                float rate = Solver.CasualWinRate(level, 40, seed);
                level.casualWinRate = rate;
                if (rate >= min && rate <= max) return level;
                float distance = rate < min ? min - rate : rate - max;
                if (distance < bestDistance) { best = level; bestDistance = distance; }
            }
            if (best != null) return best;
            throw new InvalidOperationException($"Could not generate a solvable layout for level {levelNumber} ({p}).");
        }

        public static LevelData TryBuild(int levelNumber, int seed, LevelParams p)
        {
            var rng = new Random(seed);
            int cells = p.width * p.height;
            int target = (int)Math.Round(cells * p.fill);

            // 1) Ship queue: every colour at least once, no colour twice in a row where avoidable.
            var ships = new List<ShipSpec>();
            int slots = 0;
            var order = new List<int>();
            for (int c = 0; c < p.colors; c++) order.Add(c);
            Shuffle(order, rng);
            int lastColor = -1;
            int k = 0;
            while (true)
            {
                int color;
                if (k < order.Count) color = order[k];
                else
                {
                    color = rng.Next(p.colors);
                    if (color == lastColor && p.colors > 1) color = (color + 1 + rng.Next(p.colors - 1)) % p.colors;
                }
                int cap = rng.Next(p.minCapacity, p.maxCapacity + 1);
                if (slots + cap > cells) break;
                if (slots >= target && k >= order.Count) break;
                ships.Add(new ShipSpec { color = color, capacity = cap });
                slots += cap;
                lastColor = color;
                k++;
            }
            if (ships.Count < p.berths) return null;

            // 2) Split each ship's load into container pieces (1- or 2-slot).
            var pieces = new List<(int color, int length)>();
            foreach (var s in ships)
            {
                int left = s.capacity;
                while (left > 0)
                {
                    int len = left >= 2 && rng.NextDouble() < p.longChance ? 2 : 1;
                    pieces.Add((s.color, len));
                    left -= len;
                }
            }
            Shuffle(pieces, rng);

            // 3) Place pieces at random free spots; long pieces that don't fit become two short ones.
            var grid = new bool[p.width, p.height];
            var level = new LevelData
            {
                levelNumber = levelNumber, seed = seed, width = p.width, height = p.height,
                berths = p.berths, bufferSize = p.buffer, colorCount = p.colors, ships = ships,
            };
            int id = 0;
            var queue = new Queue<(int color, int length)>(pieces);
            while (queue.Count > 0)
            {
                var (color, length) = queue.Dequeue();
                var spots = new List<(int x, int y)>();
                for (int y = 0; y < p.height; y++)
                    for (int x = 0; x + length <= p.width; x++)
                        if (!grid[x, y] && (length == 1 || !grid[x + 1, y])) spots.Add((x, y));
                if (spots.Count == 0)
                {
                    if (length == 2) { queue.Enqueue((color, 1)); queue.Enqueue((color, 1)); continue; }
                    return null;
                }
                var (px, py) = spots[rng.Next(spots.Count)];
                for (int i = 0; i < length; i++) grid[px + i, py] = true;
                level.containers.Add(new ContainerSpec { id = id++, x = px, y = py, length = length, color = color });
            }

            // 4) Locks and keys: lock a container away from the front, key on a different container.
            var candidates = new List<ContainerSpec>(level.containers);
            Shuffle(candidates, rng);
            int lockId = 0;
            foreach (var c in candidates)
            {
                if (lockId >= p.locks) break;
                if (c.y == 0 || c.kind != ContainerKind.Normal || c.keyId >= 0) continue;
                ContainerSpec key = null;
                foreach (var other in candidates)
                {
                    if (other == c || other.kind != ContainerKind.Normal || other.keyId >= 0) continue;
                    if (other.y >= c.y) continue; // keys sit closer to the quay than their lock
                    key = other;
                    break;
                }
                if (key == null) continue;
                c.kind = ContainerKind.Locked;
                c.lockId = lockId;
                key.keyId = lockId;
                lockId++;
            }

            // 5) Hidden colours on containers that start blocked.
            if (p.hiddenChance > 0)
            {
                foreach (var c in level.containers)
                    if (c.y > 0 && c.kind == ContainerKind.Normal && c.keyId < 0 && rng.NextDouble() < p.hiddenChance)
                        c.kind = ContainerKind.Hidden;
            }
            return level;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>Caches generated levels so each is built once per run.</summary>
    public static class LevelCatalog
    {
        private static readonly Dictionary<int, LevelData> Cache = new Dictionary<int, LevelData>();

        public static LevelData Get(int levelNumber)
        {
            if (!Cache.TryGetValue(levelNumber, out var level))
            {
                level = LevelGenerator.Generate(levelNumber);
                Cache[levelNumber] = level;
            }
            return level.Clone();
        }
    }
}
