// Standalone test runner for the engine-independent puzzle core.
// Lets CI (or a developer without Unity open) verify that all 30 campaign levels and a year of daily
// challenges generate valid, solvable puzzles. See Tools/CoreTests/run.sh.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ShapeEscape.Core;

public static class CoreTestRunner
{
    static int _failures;

    static void Check(bool condition, string message)
    {
        if (condition) return;
        _failures++;
        Console.WriteLine("  FAIL: " + message);
    }

    public static int Main(string[] args)
    {
        bool verbose = Array.IndexOf(args, "-v") >= 0;
        var sw = Stopwatch.StartNew();

        Console.WriteLine("== Rules engine ==");
        TestBasicBlocking();
        TestDeadlockDetected();
        TestLockedChainRotatingGate();

        Console.WriteLine("== Templates ==");
        foreach (var t in ObjectCatalog.All)
        {
            var m = Voxelizer.FitToCount(t, 8, 15, 12);
            Check(m.cells.Count >= 8, t.id + " cannot produce a beginner silhouette (" + m.cells.Count + ")");
            var big = Voxelizer.FitToCount(t, 70, 120, 100);
            Check(big.cells.Count >= 70 && big.cells.Count <= 120, t.id + " expert count out of range: " + big.cells.Count);
            if (verbose) Console.WriteLine("  " + t.id.PadRight(16) + " beginner=" + m.cells.Count + " expert=" + big.cells.Count);
        }

        Console.WriteLine("== Campaign levels ==");
        foreach (var spec in LevelCatalog.Campaign)
        {
            var t0 = sw.ElapsedMilliseconds;
            GenerationReport report;
            var def = PuzzleGenerator.Generate(spec.ToRequest(), out report);
            var info = TierInfo.Get(spec.tier);
            var v = report.validation;
            Check(v.IsValid, "Level " + spec.number + " invalid: " + v);
            Check(def.pieces.Count >= info.minPieces && def.pieces.Count <= info.maxPieces,
                "Level " + spec.number + " piece count " + def.pieces.Count + " outside " + info.minPieces + "-" + info.maxPieces);
            Check(!report.usedFallback, "Level " + spec.number + " used fallback generation");

            // Determinism: regenerate and compare.
            var again = PuzzleGenerator.Generate(spec.ToRequest());
            Check(Fingerprint(def) == Fingerprint(again), "Level " + spec.number + " is not deterministic");

            // Every special arrow requested should be present (best effort for gates/chains).
            if (spec.introduces != ArrowKind.Normal)
                Check(def.CountKind(spec.introduces) > 0, "Level " + spec.number + " does not contain introduced mechanic " + spec.introduces);

            // Replay the validator's solution through the real tap rules.
            var state = new PuzzleState(def);
            foreach (var id in v.Solution)
                if (!state.IsRemoved(id))
                {
                    var r = state.Tap(id);
                    int guard = 4;
                    while (r.Outcome == TapOutcome.Rotated && guard-- > 0) r = state.Tap(id);
                }
            Check(state.IsSolved, "Level " + spec.number + " solution replay failed (" + state.RemainingCount + " left)");

            Console.WriteLine(string.Format("  L{0,2} {1,-16} {2,-8} pieces={3,3} layers={4,2} initial={5,2} {6} gates={7} [{8}] {9}ms",
                spec.number, spec.objectId, spec.tier, def.pieces.Count, v.Layers, v.InitialRemovable,
                KindSummary(def), def.gates.Count, report.attempts, sw.ElapsedMilliseconds - t0));
        }

        Console.WriteLine("== Daily challenges (365 days) ==");
        var start = new DateTime(2026, 1, 1);
        int dailyMax = 0;
        for (int d = 0; d < 365; d++)
        {
            var spec = DailyChallengeRules.Build(start.AddDays(d), d % 20, 30);
            var t0 = sw.ElapsedMilliseconds;
            GenerationReport report;
            PuzzleGenerator.Generate(spec.ToRequest(), out report);
            dailyMax = Math.Max(dailyMax, (int)(sw.ElapsedMilliseconds - t0));
            Check(report.validation.IsValid, "Daily " + spec.dailyDate + " invalid: " + report.validation);
        }
        Console.WriteLine("  slowest daily generation: " + dailyMax + "ms");

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : _failures + " FAILURE(S)");
        Console.WriteLine("Total time: " + sw.ElapsedMilliseconds + "ms");
        return _failures == 0 ? 0 : 1;
    }

    static string KindSummary(PuzzleDefinition def)
    {
        return "lock=" + def.CountKind(ArrowKind.Locked) + " chain=" + def.CountKind(ArrowKind.Chain) +
               " rot=" + def.CountKind(ArrowKind.Rotating) + " hid=" + def.CountKind(ArrowKind.Hidden) +
               " col=" + def.CountKind(ArrowKind.Color);
    }

    static string Fingerprint(PuzzleDefinition def)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in def.pieces)
            sb.Append(p.cells[0]).Append(p.dir).Append(p.lockKeyId).Append(p.colorId).Append(p.hidden ? 'h' : '-').Append(';');
        return sb.ToString();
    }

    static PuzzleDefinition Line(params Dir[] dirs)
    {
        var def = new PuzzleDefinition();
        for (int i = 0; i < dirs.Length; i++)
            def.pieces.Add(new PuzzlePiece { id = i, cells = new[] { new Int3(i, 0, 0) }, dir = dirs[i] });
        return def;
    }

    static void TestBasicBlocking()
    {
        // [0 ->][1 ->]: piece 0 is blocked by piece 1 until 1 leaves.
        var state = new PuzzleState(Line(Dir.PosX, Dir.PosX));
        var r = state.Tap(0);
        Check(r.Outcome == TapOutcome.Blocked && r.Check.BlockerId == 1 && r.Check.FreeSteps == 0, "piece 0 should be blocked by 1");
        Check(state.Tap(1).Outcome == TapOutcome.Removed, "piece 1 should leave");
        Check(state.Tap(0).Outcome == TapOutcome.Removed && state.IsSolved, "piece 0 should leave after 1");
    }

    static void TestDeadlockDetected()
    {
        // [0 ->][<- 1] point at each other: unsolvable.
        var v = PuzzleValidator.Validate(Line(Dir.PosX, Dir.NegX));
        Check(!v.Solvable, "facing pieces must be reported unsolvable");
        var ok = PuzzleValidator.Validate(Line(Dir.NegX, Dir.PosX));
        Check(ok.Solvable && ok.Layers == 1, "diverging pieces should be solvable in one layer");
    }

    static void TestLockedChainRotatingGate()
    {
        // Lock: piece 1 locked by piece 0.
        var def = Line(Dir.NegX, Dir.PosX);
        def.pieces[1].lockKeyId = 0;
        var s = new PuzzleState(def);
        Check(s.Tap(1).Outcome == TapOutcome.Locked, "locked piece should not move");
        var r0 = s.Tap(0);
        Check(r0.Unlocked.Contains(1), "removing key should report unlock");
        Check(s.Tap(1).Outcome == TapOutcome.Removed, "unlocked piece should move");

        // Chain: removing 0 pushes 1 out automatically.
        def = Line(Dir.NegX, Dir.PosY);
        def.pieces[0].chainTargets = new[] { 1 };
        s = new PuzzleState(def);
        var rc = s.Tap(0);
        Check(rc.Removed.Count == 2 && s.IsSolved, "chain should remove both pieces");

        // Rotating: blocked +X arrow rotates to +Y and can then leave.
        def = Line(Dir.PosX, Dir.PosX);
        def.pieces[0].rotationCycle = new[] { Dir.PosX, Dir.PosY, Dir.NegX, Dir.NegY };
        s = new PuzzleState(def);
        var rr = s.Tap(0);
        Check(rr.Outcome == TapOutcome.Rotated && rr.NewDir == Dir.PosY, "rotating arrow should turn when blocked");
        Check(s.Tap(0).Outcome == TapOutcome.Removed, "rotated arrow should leave");

        // Gate: uncoloured piece blocked, matching colour passes.
        def = Line(Dir.PosX);
        def.gates.Add(new ColorGate { cell = new Int3(2, 0, 0), colorId = 1, facing = Dir.PosX });
        s = new PuzzleState(def);
        Check(s.Tap(0).Check.Reason == BlockReason.Gate, "uncoloured piece must be stopped by gate");
        def.pieces[0].colorId = 1;
        s = new PuzzleState(def);
        Check(s.Tap(0).Outcome == TapOutcome.Removed, "matching colour should pass gate");
    }
}
