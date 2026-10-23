using System;
using NUnit.Framework;
using ShapeEscape.Core;

namespace ShapeEscape.Tests
{
    /// <summary>Window > General > Test Runner > EditMode > Run All.</summary>
    public sealed class PuzzleCoreTests
    {
        static PuzzleDefinition Line(params Dir[] dirs)
        {
            var def = new PuzzleDefinition();
            for (int i = 0; i < dirs.Length; i++)
                def.pieces.Add(new PuzzlePiece { id = i, cells = new[] { new Int3(i, 0, 0) }, dir = dirs[i] });
            return def;
        }

        [Test]
        public void BlockedPieceLeavesAfterBlockerIsRemoved()
        {
            var s = new PuzzleState(Line(Dir.PosX, Dir.PosX));
            var r = s.Tap(0);
            Assert.AreEqual(TapOutcome.Blocked, r.Outcome);
            Assert.AreEqual(1, r.Check.BlockerId);
            Assert.AreEqual(TapOutcome.Removed, s.Tap(1).Outcome);
            Assert.AreEqual(TapOutcome.Removed, s.Tap(0).Outcome);
            Assert.IsTrue(s.IsSolved);
        }

        [Test]
        public void FacingPiecesAreUnsolvable()
        {
            Assert.IsFalse(PuzzleValidator.Validate(Line(Dir.PosX, Dir.NegX)).Solvable);
            Assert.IsTrue(PuzzleValidator.Validate(Line(Dir.NegX, Dir.PosX)).Solvable);
        }

        [Test]
        public void LockedChainRotatingAndGateRules()
        {
            var locked = Line(Dir.NegX, Dir.PosX);
            locked.pieces[1].lockKeyId = 0;
            var s = new PuzzleState(locked);
            Assert.AreEqual(TapOutcome.Locked, s.Tap(1).Outcome);
            Assert.Contains(1, s.Tap(0).Unlocked);
            Assert.AreEqual(TapOutcome.Removed, s.Tap(1).Outcome);

            var chain = Line(Dir.NegX, Dir.PosY);
            chain.pieces[0].chainTargets = new[] { 1 };
            s = new PuzzleState(chain);
            Assert.AreEqual(2, s.Tap(0).Removed.Count);

            var rot = Line(Dir.PosX, Dir.PosX);
            rot.pieces[0].rotationCycle = new[] { Dir.PosX, Dir.PosY, Dir.NegX, Dir.NegY };
            s = new PuzzleState(rot);
            Assert.AreEqual(TapOutcome.Rotated, s.Tap(0).Outcome);
            Assert.AreEqual(TapOutcome.Removed, s.Tap(0).Outcome);

            var gate = Line(Dir.PosX);
            gate.gates.Add(new ColorGate { cell = new Int3(2, 0, 0), colorId = 1, facing = Dir.PosX });
            Assert.AreEqual(BlockReason.Gate, new PuzzleState(gate).Tap(0).Check.Reason);
        }

        [Test]
        public void EveryCampaignLevelIsValidDeterministicAndInRange()
        {
            foreach (var spec in LevelCatalog.Campaign)
            {
                GenerationReport report;
                var def = PuzzleGenerator.Generate(spec.ToRequest(), out report);
                var tier = TierInfo.Get(spec.tier);
                Assert.IsTrue(report.validation.IsValid, "Level " + spec.number + ": " + report.validation);
                Assert.That(def.pieces.Count, Is.InRange(tier.minPieces, tier.maxPieces), "Level " + spec.number + " piece count");
                var again = PuzzleGenerator.Generate(spec.ToRequest());
                for (int i = 0; i < def.pieces.Count; i++)
                    Assert.AreEqual(def.pieces[i].dir, again.pieces[i].dir, "Level " + spec.number + " not deterministic");
            }
        }

        [Test]
        public void DifficultyRisesWithSawToothBreathers()
        {
            Func<int, float> startFree = n =>
            {
                var spec = LevelCatalog.Get(n);
                GenerationReport report;
                var def = PuzzleGenerator.Generate(spec.ToRequest(), out report);
                return report.validation.InitialRemovable / (float)def.pieces.Count;
            };
            Assert.GreaterOrEqual(startFree(1), 0.6f, "level 1 stays gentle");
            Assert.LessOrEqual(startFree(30), 0.15f, "level 30 is tough");
            Assert.Less(startFree(15), startFree(16), "super hard 15 is tougher than breather 16");
            Assert.IsTrue(LevelCatalog.Get(10).hard && LevelCatalog.Get(25).superHard && !LevelCatalog.Get(11).hard);
        }

        [Test]
        public void DailyChallengeIsSameForEveryoneOnADate()
        {
            var date = new DateTime(2026, 10, 23);
            var a = DailyChallengeRules.Build(date, 0, 30);
            var b = DailyChallengeRules.Build(date, 0, 30);
            Assert.AreEqual(a.seed, b.seed);
            Assert.AreEqual(a.objectId, b.objectId);
            Assert.IsTrue(PuzzleValidator.Validate(PuzzleGenerator.Generate(a.ToRequest())).IsValid);
        }
    }
}
