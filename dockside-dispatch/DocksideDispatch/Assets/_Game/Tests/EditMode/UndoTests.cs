using System.Collections.Generic;
using System.Linq;
using Dockside.Core;
using NUnit.Framework;

namespace Dockside.Tests
{
    /// <summary>The Undo booster's rules: one tap, and everything it set off, is taken back.</summary>
    public class UndoTests
    {
        private static LevelData Row(int[] colors, List<ShipSpec> ships, int berths = 1, int buffer = 5)
        {
            var level = new LevelData { width = colors.Length, height = 1, berths = berths, bufferSize = buffer, ships = ships };
            for (int x = 0; x < colors.Length; x++) level.containers.Add(new ContainerSpec { id = x, x = x, y = 0, color = colors[x] });
            level.colorCount = colors.Distinct().Count();
            return level;
        }

        private static GameSession Session(LevelData level) => new GameSession(level) { KeepHistory = true };

        [Test]
        public void UndoPutsTheContainerBackInItsCell()
        {
            var s = Session(Row(new[] { 0, 0, 0 }, new List<ShipSpec> { new ShipSpec { color = 0, capacity = 3 } }));
            string start = s.StateKey();
            Assert.IsFalse(s.CanUndo);
            s.Tap(1);
            Assert.IsTrue(s.CanUndo);
            Assert.AreEqual(1, s.Undo());
            Assert.AreEqual(start, s.StateKey());
            Assert.IsTrue(s.InYard(1));
            Assert.AreEqual(1, s.CellAt(1, 0));
            Assert.AreEqual(0, s.LoadAtBerth(0).Count);
            Assert.AreEqual(0, s.MovesMade);
            Assert.IsFalse(s.CanUndo);
            Assert.AreEqual(-1, s.Undo(), "Nothing left to undo");
        }

        [Test]
        public void UndoBringsBackAShipThatLeft()
        {
            var s = Session(Row(new[] { 0, 0, 1 },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 2 }, new ShipSpec { color = 1, capacity = 1 } }));
            s.Tap(0);
            s.Tap(1);   // the first ship fills, leaves, and the second docks
            Assert.AreEqual(1, s.ShipAtBerth(0));
            Assert.AreEqual(1, s.ShipsDeparted);

            Assert.AreEqual(1, s.Undo());
            Assert.AreEqual(0, s.ShipAtBerth(0), "The ship that left is back at its berth");
            CollectionAssert.AreEqual(new[] { 0 }, s.LoadAtBerth(0).ToArray(), "with what it had on board");
            Assert.AreEqual(0, s.ShipsDeparted);
            Assert.AreEqual(1, s.NextShipIndex);
            Assert.IsTrue(s.InYard(1));
        }

        [Test]
        public void UndoTakesBackHoldingBayPulls()
        {
            // Blue waits in the bay; sending red off docks the blue ship, which pulls blue from the bay. Undo puts both back.
            var s = Session(Row(new[] { 1, 0 },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 1 }, new ShipSpec { color = 1, capacity = 1 } }));
            s.Tap(0);
            Assert.AreEqual(1, s.BufferUsed);
            s.Tap(1);
            Assert.IsTrue(s.IsWon);
            Assert.IsFalse(s.CanUndo, "A won level stays won");

            var t = Session(Row(new[] { 1, 0, 0 },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 1 }, new ShipSpec { color = 1, capacity = 1 },
                                     new ShipSpec { color = 0, capacity = 1 } }));
            t.Tap(0);                    // blue to the bay
            t.Tap(1);                    // red ship leaves, blue ship docks and takes blue from the bay, then leaves too
            Assert.AreEqual(0, t.BufferUsed);
            Assert.AreEqual(2, t.ShipsDeparted);
            t.Undo();
            Assert.AreEqual(1, t.BufferUsed, "Blue is back in the bay");
            CollectionAssert.AreEqual(new[] { 0 }, t.Buffer.ToArray());
            Assert.AreEqual(0, t.ShipAtBerth(0));
            Assert.AreEqual(0, t.ShipsDeparted);
        }

        [Test]
        public void UndoAfterALossPutsTheLevelBackInPlay()
        {
            var s = Session(Row(new[] { 1, 1, 0 },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 1 }, new ShipSpec { color = 1, capacity = 2 } }, buffer: 2));
            s.Tap(0);
            s.Tap(1);
            Assert.IsTrue(s.IsLost);
            Assert.IsTrue(s.CanUndo, "A lost level can be undone");
            Assert.AreEqual(1, s.Undo());
            Assert.IsFalse(s.IsLost);
            Assert.AreEqual(LoseReason.None, s.LoseReason);
            Assert.AreEqual(1, s.BufferUsed);
            // The right move now wins: red leaves, the blue ship docks and takes the waiting blue.
            Assert.AreEqual(TapResult.Accepted, s.Tap(2));
            Assert.AreEqual(TapResult.Accepted, s.Tap(1));
            Assert.IsTrue(s.IsWon);
        }

        [Test]
        public void UndoKeepsExtraBaySlots()
        {
            var s = Session(Row(new[] { 1, 0 },
                new List<ShipSpec> { new ShipSpec { color = 0, capacity = 1 }, new ShipSpec { color = 1, capacity = 1 } }, buffer: 3));
            s.Tap(0);
            s.AddBufferSlots(1);
            s.Undo();
            Assert.AreEqual(4, s.BufferCapacity);
        }

        [Test]
        public void UndoRelocksButKeepsRevealedColours()
        {
            var level = new LevelData
            {
                width = 2, height = 2, berths = 1, bufferSize = 5, colorCount = 1,
                containers =
                {
                    new ContainerSpec { id = 0, x = 0, y = 0, color = 0, kind = ContainerKind.Locked, lockId = 0 },
                    new ContainerSpec { id = 1, x = 1, y = 0, color = 0, keyId = 0 },
                    new ContainerSpec { id = 2, x = 1, y = 1, color = 0, kind = ContainerKind.Hidden },
                },
                ships = { new ShipSpec { color = 0, capacity = 3 } },
            };
            var s = Session(level);
            Assert.IsFalse(s.IsRevealed(2));
            s.Tap(1);   // the key leaves: 0 unlocks, and 2 is uncovered
            Assert.IsFalse(s.IsLocked(0));
            Assert.IsTrue(s.IsRevealed(2));
            s.Undo();
            Assert.IsTrue(s.IsLocked(0), "The key is back in the yard, so the lock is back");
            Assert.IsTrue(s.IsRevealed(2), "A colour the player has seen stays shown");
            Assert.IsFalse(s.IsReachable(2));
        }

        [Test]
        public void CraneLiftMovesCanBeUndone()
        {
            var level = new LevelData
            {
                width = 1, height = 2, berths = 1, bufferSize = 5, colorCount = 1,
                containers = { new ContainerSpec { id = 0, x = 0, y = 0, color = 0 }, new ContainerSpec { id = 1, x = 0, y = 1, color = 0 } },
                ships = { new ShipSpec { color = 0, capacity = 2 } },
            };
            var s = Session(level);
            s.Tap(1, null, ignorePath: true);
            Assert.AreEqual(1, s.Undo());
            Assert.IsTrue(s.InYard(1));
            Assert.IsFalse(s.IsReachable(1));
        }

        [Test]
        public void NoHistoryUnlessAskedFor()
        {
            var s = new GameSession(Row(new[] { 0, 0 }, new List<ShipSpec> { new ShipSpec { color = 0, capacity = 2 } }));
            s.Tap(0);
            Assert.IsFalse(s.CanUndo, "The solver's sessions keep no history");
            Assert.AreEqual(-1, s.Undo());
            Assert.IsFalse(s.Clone().CanUndo);
        }

        /// <summary>
        /// On real shipped levels (both modes): after every move of a winning line, undoing it gives back exactly the state
        /// before it, and replaying it gives exactly the state after it, so the level is still won.
        /// </summary>
        [Test]
        public void UndoAndReplayOnShippedLevelsMatchesPlainPlay([Values(3, 12, 25, 47, 88)] int n, [Values("", "Unload")] string mode)
        {
            var dir = System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Game/Resources/Levels", mode);
            var level = UnityEngine.JsonUtility.FromJson<LevelData>(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, $"level_{n:00}.json")));
            var solve = Solver.Solve(level);
            Assert.IsTrue(solve.solvable);

            var plain = new GameSession(level);
            var s = Session(level);
            foreach (int m in solve.solution)
            {
                string before = s.StateKey();
                Assert.AreEqual(TapResult.Accepted, s.Tap(m));
                plain.Tap(m);
                string after = s.StateKey();
                Assert.AreEqual(plain.StateKey(), after);
                if (s.IsWon) break;
                Assert.AreEqual(m, s.Undo());
                Assert.AreEqual(before, s.StateKey(), $"Undo of {m} did not restore the state");
                Assert.AreEqual(TapResult.Accepted, s.Tap(m));
                Assert.AreEqual(after, s.StateKey(), $"Replaying {m} after an undo went differently");
            }
            Assert.IsTrue(s.IsWon);
            Assert.AreEqual(solve.solution.Count, s.UndoDepth);
        }
    }
}
