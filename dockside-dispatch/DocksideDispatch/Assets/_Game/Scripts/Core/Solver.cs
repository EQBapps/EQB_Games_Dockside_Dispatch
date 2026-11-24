using System.Collections.Generic;

namespace Dockside.Core
{
    public struct SolveResult
    {
        public bool solvable;
        public int nodesExplored;
        public bool hitNodeLimit;
        public List<int> solution;   // container indices in tap order, when solvable
        public int peakBufferUse;    // highest holding-bay use along the solution (difficulty signal)
    }

    /// <summary>
    /// Depth-first search with a visited set and good move ordering. Levels are small
    /// (at most ~45 containers) and most moves are forced, so this finishes quickly.
    /// </summary>
    public static class Solver
    {
        public static SolveResult Solve(LevelData level, int nodeLimit = 60000)
        {
            var root = new GameSession(level) { RecordEvents = false };
            var visited = new HashSet<string>();
            var path = new List<int>();
            var result = new SolveResult();
            int peak = 0;
            bool found = Dfs(root, visited, path, ref result, nodeLimit, 0, ref peak);
            result.solvable = found;
            if (found)
            {
                result.solution = new List<int>(path);
                result.peakBufferUse = peak;
            }
            return result;
        }

        private static bool Dfs(GameSession s, HashSet<string> visited, List<int> path, ref SolveResult r,
                                int limit, int depthPeak, ref int bestPeak)
        {
            if (s.IsWon) { bestPeak = depthPeak; return true; }
            if (s.IsLost) return false;
            if (r.nodesExplored >= limit) { r.hitNodeLimit = true; return false; }
            if (!visited.Add(s.StateKey())) return false;
            r.nodesExplored++;

            var moves = s.LegalMoves();
            moves.Sort((a, b) => Score(s, b).CompareTo(Score(s, a)));
            foreach (var m in moves)
            {
                var next = s.Clone();
                next.Tap(m);
                path.Add(m);
                int peak = depthPeak > next.BufferUsed ? depthPeak : next.BufferUsed;
                if (Dfs(next, visited, path, ref r, limit, peak, ref bestPeak)) return true;
                path.RemoveAt(path.Count - 1);
                if (r.hitNodeLimit) return false;
            }
            return false;
        }

        /// <summary>
        /// Difficulty estimate: share of games a casual player wins. The simulated player loads a
        /// waiting ship when they can (most of the time) and otherwise taps a random legal container.
        /// </summary>
        public static float CasualWinRate(LevelData level, int trials, int seed)
        {
            var rng = new System.Random(seed);
            int wins = 0;
            var root = new GameSession(level) { RecordEvents = false };
            for (int t = 0; t < trials; t++)
            {
                var s = root.Clone();
                while (!s.IsOver)
                {
                    var moves = s.LegalMoves();
                    if (moves.Count == 0) break;
                    int pick = -1;
                    if (rng.NextDouble() < 0.8)
                    {
                        var direct = moves.FindAll(m => s.BerthFor(m) >= 0);
                        if (direct.Count > 0) pick = direct[rng.Next(direct.Count)];
                    }
                    if (pick < 0) pick = moves[rng.Next(moves.Count)];
                    s.Tap(pick);
                }
                if (s.IsWon) wins++;
            }
            return wins / (float)trials;
        }

        /// <summary>
        /// Skill check: share of games won by a player who never plans. They always tap some container that can go
        /// straight onto a waiting ship (any of them, at random), and only park (at random) when none can.
        /// A normal level should mostly let this player through; a hard level should mostly stop them, so that
        /// thinking about what to park, and when, is what wins it.
        /// </summary>
        public static float GreedyWinRate(LevelData level, int trials, int seed)
        {
            var rng = new System.Random(seed ^ 0x5bd1e995);
            int wins = 0;
            var root = new GameSession(level) { RecordEvents = false };
            for (int t = 0; t < trials; t++)
            {
                var s = root.Clone();
                while (!s.IsOver)
                {
                    var moves = s.LegalMoves();
                    if (moves.Count == 0) break;
                    var direct = moves.FindAll(m => s.BerthFor(m) >= 0);
                    s.Tap(direct.Count > 0 ? direct[rng.Next(direct.Count)] : moves[rng.Next(moves.Count)]);
                }
                if (s.IsWon) wins++;
            }
            return wins / (float)trials;
        }

        /// <summary>Higher is tried first: loading a ship beats parking; park colours that are needed soon.</summary>
        private static int Score(GameSession s, int containerIndex)
        {
            var c = s.Spec(containerIndex);
            int berth = s.BerthFor(containerIndex);
            if (berth >= 0)
            {
                int free = s.FreeSlotsOnBerth(berth);
                int score = 1000 - free * 10;          // completes ships sooner
                if (c.keyId >= 0) score += 50;         // unlocking opens the yard
                return score;
            }
            // Parking: prefer colours whose ship arrives soonest, then containers that open paths.
            int distance = 99;
            var ships = s.Level.ships;
            for (int i = s.NextShipIndex; i < ships.Count && i < s.NextShipIndex + 8; i++)
            {
                if (ships[i].color == c.color) { distance = i - s.NextShipIndex; break; }
            }
            int sc = 500 - distance * 20 - c.length * 5;
            if (c.keyId >= 0) sc += 40;
            return sc;
        }
    }
}
