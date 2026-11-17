using System.Collections.Generic;
using System.Linq;
using Dockside.Core;
using NUnit.Framework;

namespace Dockside.Tests
{
    public class CoreRulesTests
    {
        // Helper: grid of single containers; colours given row by row (row 0 first). -1 = empty cell.
        private static LevelData Grid(int[][] rows, List<ShipSpec> ships, int berths = 1, int buffer = 5)
        {
            var level = new LevelData { width = rows[0].Length, height = rows.Length, berths = berths, bufferSize = buffer, ships = ships };
            int id = 0;
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] >= 0)
                        level.containers.Add(new ContainerSpec { id = id++, x = x, y = y, color = rows[y][x] });
            level.colorCount = level.containers.Select(c => c.color).Distinct().Count();
            return level;
        }

        private static int At(GameSession s, int x, int y) => s.CellAt(x, y);

        [Test]
        public void FrontRowIsReachable_BackRowBlockedUntilPathOpens()
        {
            var level = Grid(new[] { new[] { 0, 0 }, new[] { 0, 0 } },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 4 } });
            var s = new GameSession(level);
            Assert.IsTrue(s.IsReachable(At(s, 0, 0)));
            Assert.IsTrue(s.IsReachable(At(s, 1, 0)));
            Assert.IsFalse(s.IsReachable(At(s, 0, 1)));

            int back = At(s, 0, 1);
            Assert.AreEqual(TapResult.Blocked, s.Tap(back));
            Assert.AreEqual(TapResult.Accepted, s.Tap(At(s, 0, 0)));
            Assert.IsTrue(s.IsReachable(back), "Back container should be reachable once the cell in front is empty.");
        }

        [Test]
        public void SideOpeningGivesAPath()
        {
            // Row 0: [A][ ]   Row 1: [B][C]  -> C is reachable through the empty cell above it.
            var level = Grid(new[] { new[] { 0, -1 }, new[] { 0, 0 } },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 3 } });
            var s = new GameSession(level);
            Assert.IsTrue(s.IsReachable(At(s, 1, 1)));
            Assert.IsFalse(s.IsReachable(At(s, 0, 1)));
        }

        [Test]
        public void MatchingContainerGoesStraightToShip_FullShipDepartsAndNextDocks()
        {
            var level = Grid(new[] { new[] { 0, 0, 1 } },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 2 }, new ShipSpec { color = 1, capacity = 1 } });
            var s = new GameSession(level);
            var ev = new List<GameEvent>();
            s.Tap(At(s, 0, 0), ev);
            Assert.AreEqual(GameEventType.ContainerToShip, ev[0].type);
            ev.Clear();
            s.Tap(At(s, 1, 0), ev);
            var types = ev.Select(e => e.type).ToList();
            CollectionAssert.IsSubsetOf(new[] { GameEventType.ContainerToShip, GameEventType.ShipDeparted, GameEventType.ShipArrived }, types);
            Assert.AreEqual(1, s.ShipAtBerth(0));
            s.Tap(At(s, 2, 0));
            Assert.IsTrue(s.IsWon);
        }

        [Test]
        public void NonMatchingContainerWaitsInBuffer_AndIsPulledWhenItsShipArrives()
        {
            // Blue first while a red ship is docked: blue must wait in the holding bay.
            var level = Grid(new[] { new[] { 1, 0 } },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 1 }, new ShipSpec { color = 1, capacity = 1 } });
            var s = new GameSession(level);
            var ev = new List<GameEvent>();
            s.Tap(0, ev); // blue
            Assert.AreEqual(GameEventType.ContainerToBuffer, ev[0].type);
            Assert.AreEqual(1, s.BufferUsed);
            ev.Clear();
            s.Tap(1, ev); // red fills red ship -> blue ship docks -> blue pulled from bay -> departs -> win
            var types = ev.Select(e => e.type).ToList();
            Assert.Contains(GameEventType.BufferToShip, types);
            Assert.AreEqual(0, s.BufferUsed);
            Assert.IsTrue(s.IsWon);
        }

        [Test]
        public void FullHoldingBayLosesTheLevel()
        {
            var level = Grid(new[] { new[] { 1, 1, 0 } },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 1 }, new ShipSpec { color = 1, capacity = 2 } }, berths: 1, buffer: 2);
            var s = new GameSession(level);
            s.Tap(0);
            Assert.IsFalse(s.IsLost);
            s.Tap(1);
            Assert.IsTrue(s.IsLost);
            Assert.AreEqual(LoseReason.BufferFull, s.LoseReason);
        }

        [Test]
        public void SecondChanceSlotsClearTheLoss()
        {
            var level = Grid(new[] { new[] { 1, 1, 0 } },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 1 }, new ShipSpec { color = 1, capacity = 2 } }, berths: 1, buffer: 2);
            var s = new GameSession(level);
            s.Tap(0); s.Tap(1);
            Assert.IsTrue(s.IsLost);
            s.AddBufferSlots(2);
            Assert.IsFalse(s.IsLost);
            s.Tap(2); // red ship leaves, blue ship docks and takes both waiting blues
            Assert.IsTrue(s.IsWon);
        }

        [Test]
        public void LongContainerNeedsTwoSlots()
        {
            var level = new LevelData
            {
                width = 2, height = 1, berths = 1, bufferSize = 5, colorCount = 1,
                containers = { new ContainerSpec { id = 0, x = 0, y = 0, length = 2, color = 0 } },
                ships = { new ShipSpec { color = 0, capacity = 2 } },
            };
            var s = new GameSession(level);
            Assert.AreEqual(0, s.CellAt(0, 0));
            Assert.AreEqual(0, s.CellAt(1, 0));
            s.Tap(0);
            Assert.IsTrue(s.IsWon);
        }

        [Test]
        public void LockedContainerOpensWhenKeyLeaves()
        {
            var level = new LevelData
            {
                width = 2, height = 1, berths = 1, bufferSize = 5, colorCount = 1,
                containers =
                {
                    new ContainerSpec { id = 0, x = 0, y = 0, color = 0, kind = ContainerKind.Locked, lockId = 0 },
                    new ContainerSpec { id = 1, x = 1, y = 0, color = 0, keyId = 0 },
                },
                ships = { new ShipSpec { color = 0, capacity = 2 } },
            };
            var s = new GameSession(level);
            Assert.AreEqual(TapResult.Locked, s.Tap(0));
            var ev = new List<GameEvent>();
            s.Tap(1, ev);
            Assert.IsTrue(ev.Any(e => e.type == GameEventType.ContainerUnlocked && e.containerId == 0));
            Assert.AreEqual(TapResult.Accepted, s.Tap(0));
            Assert.IsTrue(s.IsWon);
        }

        [Test]
        public void HiddenContainerRevealsWhenReachable()
        {
            var level = new LevelData
            {
                width = 1, height = 2, berths = 1, bufferSize = 5, colorCount = 1,
                containers =
                {
                    new ContainerSpec { id = 0, x = 0, y = 0, color = 0 },
                    new ContainerSpec { id = 1, x = 0, y = 1, color = 0, kind = ContainerKind.Hidden },
                },
                ships = { new ShipSpec { color = 0, capacity = 2 } },
            };
            var s = new GameSession(level);
            Assert.IsFalse(s.IsRevealed(1));
            var ev = new List<GameEvent>();
            s.Tap(0, ev);
            Assert.IsTrue(s.IsRevealed(1));
            Assert.IsTrue(ev.Any(e => e.type == GameEventType.ContainerRevealed && e.containerId == 1));
        }

        [Test]
        public void CraneLiftIgnoresPath()
        {
            var level = Grid(new[] { new[] { 0 }, new[] { 0 } }, new List<ShipSpec> { new ShipSpec { color = 0, capacity = 2 } });
            var s = new GameSession(level);
            Assert.AreEqual(TapResult.Blocked, s.Tap(1));
            Assert.AreEqual(TapResult.Accepted, s.Tap(1, null, ignorePath: true));
        }

        [Test]
        public void CraneLiftTakesLockedContainers()
        {
            var level = new LevelData
            {
                width = 2, height = 1, berths = 1, bufferSize = 5, colorCount = 1,
                containers = new List<ContainerSpec>
                {
                    new ContainerSpec { id = 0, x = 0, y = 0, color = 0, kind = ContainerKind.Locked, lockId = 0 },
                    new ContainerSpec { id = 1, x = 1, y = 0, color = 0, keyId = 0 },
                },
                ships = new List<ShipSpec> { new ShipSpec { color = 0, capacity = 2 } },
            };
            var s = new GameSession(level);
            Assert.AreEqual(TapResult.Locked, s.Tap(0));
            var ev = new List<GameEvent>();
            Assert.AreEqual(TapResult.Accepted, s.Tap(0, ev, ignorePath: true));
            Assert.IsFalse(s.IsLocked(0));
            Assert.IsTrue(ev.Any(e => e.type == GameEventType.ContainerUnlocked && e.containerId == 0));
            // The key container still works normally afterwards and the level can be won.
            Assert.AreEqual(TapResult.Accepted, s.Tap(1));
            Assert.IsTrue(s.IsWon);
        }

        [Test]
        public void ValidateCatchesUnbalancedColours()
        {
            var level = Grid(new[] { new[] { 0, 0 } }, new List<ShipSpec> { new ShipSpec { color = 0, capacity = 3 } });
            Assert.IsNotNull(level.Validate());
        }
    }

    public class LevelGenerationTests
    {
        [Test]
        public void AllPrototypeLevelsAreValidSolvableAndDeterministic()
        {
            for (int n = 1; n <= LevelGenerator.AuthoredLevelCount; n++)
            {
                var a = LevelGenerator.Generate(n);
                Assert.IsNull(a.Validate(), $"Level {n} invalid");
                var solve = Solver.Solve(a);
                Assert.IsTrue(solve.solvable, $"Level {n} not solvable");

                // Replaying the solver's moves on a fresh session must win.
                var s = new GameSession(a);
                foreach (var m in solve.solution) Assert.AreEqual(TapResult.Accepted, s.Tap(m), $"Level {n}: replay move rejected");
                Assert.IsTrue(s.IsWon, $"Level {n}: replay did not win");

                var b = LevelGenerator.Generate(n);
                Assert.AreEqual(a.seed, b.seed, $"Level {n} is not deterministic");
            }
        }

        [Test]
        public void DifficultyRampsUp()
        {
            var early = LevelGenerator.Generate(2);
            var late = LevelGenerator.Generate(28);
            Assert.Less(early.containers.Count, late.containers.Count);
            Assert.Less(early.colorCount, late.colorCount);
        }
    }
}
