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

        /// <summary>Containers a truck carries in Unload: two short ones or one long one, like a real truck.</summary>
        public const int TruckCapacity = 2;
        public const int MaxColors = 5;

        /// <summary>
        /// Difficulty curve (every 5th level is a spike). Unload uses the same curve; only the vehicles change:
        /// trucks carry 2 slots instead of a ship's 3-4, so a level has about twice as many of them.
        /// </summary>
        public static LevelParams ParamsFor(int level, GameMode mode)
        {
            var p = ParamsFor(level);
            if (mode == GameMode.Unload)
            {
                p.minCapacity = TruckCapacity; p.maxCapacity = TruckCapacity;
                // Small trucks turn over quickly, which makes the middle levels easier than Load's: one more colour
                // there brings the casual win rate back into the same target band (the late game already lands in it).
                if (Canonical(level) >= 5) p.colors = Math.Min(MaxColors, p.colors + 1);
            }
            return p;
        }

        /// <summary>Every 5th level is a "Hard level": badge and banner, a fuller yard, fewer berths early on, more coins.</summary>
        public static bool IsHard(int level) => Canonical(level) % 5 == 0;

        /// <summary>
        /// Difficulty curve for Load, with a sawtooth: every 5th level is a hard level, the one after it a breather.
        /// The holding bay matters from level 2 (more colours than ships at the quay), and the twists arrive early:
        /// long containers at 4, locked at 8, mystery at 12, five colours at 15.
        /// </summary>
        public static LevelParams ParamsFor(int level)
        {
            int n = Canonical(level);
            var p = new LevelParams { berths = 3, buffer = 5, minCapacity = 3, maxCapacity = 3, fill = 0.8f };

            if (n == 1)        { p.width = 4; p.height = 3; p.colors = 2; p.fill = 0.75f; }                 // how to play
            else if (n == 2)   { p.width = 4; p.height = 4; p.colors = 3; p.berths = 2; p.fill = 0.85f; }  // the bay matters
            else if (n == 3)   { p.width = 5; p.height = 4; p.colors = 4; p.berths = 2; p.fill = 0.82f; }  // two colours wait
            else if (n == 4)   { p.width = 5; p.height = 4; p.colors = 3; p.berths = 2; p.longChance = 0.2f; p.maxCapacity = 4; p.fill = 0.82f; }
            else if (n <= 7)   { p.width = 5; p.height = 5; p.colors = 4; p.longChance = 0.2f; p.maxCapacity = 4; p.fill = 0.84f; }
            else if (n <= 9)   { p.width = 5; p.height = 5; p.colors = 4; p.longChance = 0.2f; p.maxCapacity = 4; p.locks = 1; p.fill = 0.84f; }
            else if (n <= 11)  { p.width = 6; p.height = 5; p.colors = 4; p.longChance = 0.22f; p.maxCapacity = 4; p.locks = 1; p.fill = 0.85f; }
            else if (n <= 14)  { p.width = 6; p.height = 6; p.colors = 4; p.longChance = 0.22f; p.maxCapacity = 4; p.locks = 1; p.hiddenChance = 0.12f; p.fill = 0.85f; }
            else if (n <= 19)  { p.width = 6; p.height = 6; p.colors = 5; p.longChance = 0.25f; p.maxCapacity = 4; p.locks = 1; p.hiddenChance = 0.14f; p.fill = 0.86f; }
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

            // Sawtooth: a hard level every 5th, a breather right after. Up to level 20 a hard level also has one berth
            // fewer, so it can't be won by tapping matches: what to park, and when, decides it.
            if (n % 5 == 0)
            {
                p.fill = Math.Min(0.95f, p.fill + 0.06f);
                if (n <= 20) p.berths = 2;
            }
            if (n % 5 == 1 && n > 1) p.fill = Math.Max(0.7f, p.fill - 0.05f);
            return p;
        }

        /// <summary>
        /// Target casual-player win rate (Solver.CasualWinRate) for a level: a gentle first level, then a rising curve,
        /// a dip on every hard level and a breather after it. Real players also have boosters, Undo and a second chance.
        /// </summary>
        public static (float min, float max) TargetWinRate(int level)
        {
            int n = Canonical(level);
            (float min, float max) band = n == 1 ? (0.9f, 1f) : n == 2 ? (0.8f, 1f) : n <= 4 ? (0.55f, 0.95f) : n <= 10 ? (0.35f, 0.8f)
                : n <= 20 ? (0.2f, 0.65f) : n <= 30 ? (0.08f, 0.5f) : (0.06f, 0.45f);
            if (n % 5 == 1 && n > 1) band = n > 30 ? (0.2f, 0.6f) : (Math.Min(0.9f, band.min + 0.15f), Math.Min(1f, band.max + 0.1f));
            if (n % 5 == 0) band = n == 5 ? (0.25f, 0.5f) : n == 10 ? (0.2f, 0.45f) : (Math.Max(0.03f, band.min - 0.08f), band.max - 0.2f);
            return band;
        }

        /// <summary>
        /// Target win rate for a player who never plans (Solver.GreedyWinRate): such a player should get through the
        /// early normal levels, but a hard level should mostly stop them, so a win there is earned by thinking.
        /// </summary>
        public static (float min, float max) TargetGreedyWinRate(int level)
        {
            int n = Canonical(level);
            if (n == 1) return (0.95f, 1f);
            if (n % 5 == 0) return n <= 30 ? (0f, 0.45f) : (0f, 0.6f);
            return n <= 4 ? (0.6f, 1f) : n <= 15 ? (0.35f, 1f) : (0f, 1f);
        }

        /// <summary>
        /// Generates a solvable level whose casual-player win rate falls in the target band
        /// (or the closest candidate found). Deterministic for a given level number.
        /// </summary>
        public static LevelData Generate(int levelNumber, int maxAttempts = 250) => Generate(levelNumber, GameMode.Load, maxAttempts);

        /// <summary>As Generate, for either mode. Unload levels use their own seeds, so their layouts differ from Load's.</summary>
        public static LevelData Generate(int levelNumber, GameMode mode, int maxAttempts = 250)
        {
            var p = ParamsFor(levelNumber, mode);
            var (min, max) = TargetWinRate(levelNumber);
            var (gMin, gMax) = TargetGreedyWinRate(levelNumber);
            static float Outside(float v, float lo, float hi) => v < lo ? lo - v : v > hi ? v - hi : 0f;
            LevelData best = null;
            float bestDistance = float.MaxValue;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int seed = unchecked(levelNumber * 7919 + attempt * 104729 + (mode == GameMode.Unload ? 15485863 : 0));
                var level = TryBuild(levelNumber, seed, p);
                if (level != null) level.mode = mode;
                if (level == null || level.Validate() != null) continue;
                if (!Solver.Solve(level, 8000).solvable) continue;
                float rate = Solver.CasualWinRate(level, 40, seed);
                level.casualWinRate = rate;
                float casualMiss = Outside(rate, min, max);
                // Far off the casual band: keep it only as a fallback, without spending the greedy simulation on it.
                if (casualMiss <= 0.25f) level.greedyWinRate = Solver.GreedyWinRate(level, 40, seed);
                float distance = casualMiss + (level.greedyWinRate < 0 ? 1f : Outside(level.greedyWinRate, gMin, gMax));
                if (distance <= 0f) return level;
                if (distance < bestDistance) { best = level; bestDistance = distance; }
            }
            if (best != null) return best;
            throw new InvalidOperationException($"Could not generate a solvable {mode} layout for level {levelNumber} ({p}).");
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
        private static readonly Dictionary<(GameMode, int), LevelData> Cache = new Dictionary<(GameMode, int), LevelData>();

        public static LevelData Get(int levelNumber, GameMode mode = GameMode.Load)
        {
            if (!Cache.TryGetValue((mode, levelNumber), out var level))
            {
                level = LevelGenerator.Generate(levelNumber, mode);
                Cache[(mode, levelNumber)] = level;
            }
            return level.Clone();
        }
    }
}
