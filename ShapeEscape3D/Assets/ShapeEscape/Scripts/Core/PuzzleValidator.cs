using System;
using System.Collections.Generic;

namespace ShapeEscape.Core
{
    public sealed class ValidationReport
    {
        public bool Solvable;
        public readonly List<string> Errors = new List<string>();
        /// <summary>One valid removal order (validated against the real rules).</summary>
        public readonly List<int> Solution = new List<int>();
        /// <summary>Rounds needed when removing every currently-free piece at once. A good difficulty proxy.</summary>
        public int Layers;
        /// <summary>How many pieces are removable at the very start.</summary>
        public int InitialRemovable;
        /// <summary>Average number of removable pieces per step along the solution (lower = more constrained).</summary>
        public float AverageChoices;
        public int PieceCount;

        public bool IsValid { get { return Solvable && Errors.Count == 0; } }

        public override string ToString()
        {
            return (IsValid ? "VALID" : "INVALID") + " pieces=" + PieceCount + " layers=" + Layers +
                   " initial=" + InitialRemovable + " avgChoices=" + AverageChoices.ToString("0.0") +
                   (Errors.Count > 0 ? " errors=" + string.Join("; ", Errors.ToArray()) : "");
        }
    }

    /// <summary>
    /// Structural checks plus a complete solvability proof. Because the rules are monotone (a removal never
    /// blocks anything), repeatedly removing any removable piece reaches the solved state if and only if the
    /// puzzle is solvable, so the greedy simulation below is an exact decision procedure.
    /// </summary>
    public static class PuzzleValidator
    {
        public static ValidationReport Validate(PuzzleDefinition def)
        {
            var report = new ValidationReport();
            if (def == null)
            {
                report.Errors.Add("Definition is null");
                return report;
            }
            report.PieceCount = def.pieces.Count;
            CheckStructure(def, report);
            if (report.Errors.Count > 0) return report;

            PuzzleState state;
            try
            {
                state = new PuzzleState(def);
            }
            catch (Exception e)
            {
                report.Errors.Add(e.Message);
                return report;
            }

            // Layered simulation: gives the difficulty metric and a proof of solvability.
            var layerState = state.Clone();
            var free = new List<int>();
            int steps = 0;
            float choiceSum = 0;
            while (!layerState.IsSolved)
            {
                free.Clear();
                foreach (var p in def.pieces)
                    if (!layerState.IsRemoved(p.id) && layerState.CanEventuallyLeaveNow(p.id))
                        free.Add(p.id);
                if (free.Count == 0) break;
                if (report.Layers == 0) report.InitialRemovable = free.Count;
                report.Layers++;
                foreach (var id in free)
                {
                    choiceSum += free.Count;
                    steps++;
                    layerState.ForceRemove(id);
                }
            }
            report.AverageChoices = steps > 0 ? choiceSum / steps : 0;

            // Sequential replay through the real Tap() rules, recording an exact tap-valid solution.
            var play = state.Clone();
            int guard = def.pieces.Count * 8 + 16;
            while (!play.IsSolved && guard-- > 0)
            {
                int pick = -1;
                foreach (var p in def.pieces)
                {
                    if (play.IsRemoved(p.id)) continue;
                    if (play.CanRemoveNow(p.id)) { pick = p.id; break; }
                }
                if (pick < 0)
                {
                    // Rotate a rotating arrow towards a free direction (each blocked tap rotates once).
                    foreach (var p in def.pieces)
                    {
                        if (play.IsRemoved(p.id) || !p.IsRotating) continue;
                        if (play.CanEventuallyLeaveNow(p.id)) { pick = p.id; break; }
                    }
                    if (pick < 0) break;
                }
                var r = play.Tap(pick);
                if (r.Outcome == TapOutcome.Removed) report.Solution.AddRange(r.Removed);
            }

            report.Solvable = play.IsSolved;
            if (!report.Solvable)
                report.Errors.Add("Unsolvable: " + play.RemainingCount + " piece(s) can never leave");
            return report;
        }

        static void CheckStructure(PuzzleDefinition def, ValidationReport report)
        {
            var ids = new HashSet<int>();
            var cells = new HashSet<Int3>();
            foreach (var p in def.pieces)
            {
                if (!ids.Add(p.id)) report.Errors.Add("Duplicate piece id " + p.id);
                if (p.cells == null || p.cells.Length == 0) report.Errors.Add("Piece " + p.id + " has no cells");
                else
                    foreach (var c in p.cells)
                        if (!cells.Add(c)) report.Errors.Add("Overlapping cell " + c);
            }
            foreach (var p in def.pieces)
            {
                if (p.IsLocked)
                {
                    if (p.lockKeyId == p.id) report.Errors.Add("Piece " + p.id + " is its own key");
                    else if (!ids.Contains(p.lockKeyId)) report.Errors.Add("Piece " + p.id + " has missing key " + p.lockKeyId);
                }
                if (p.chainTargets != null)
                    foreach (var t in p.chainTargets)
                        if (!ids.Contains(t) || t == p.id) report.Errors.Add("Piece " + p.id + " has invalid chain target " + t);
                if (p.IsRotating && Array.IndexOf(p.rotationCycle, p.dir) < 0)
                    report.Errors.Add("Rotating piece " + p.id + " direction not in its cycle");
            }
            var gateCells = new HashSet<Int3>();
            foreach (var g in def.gates)
            {
                if (cells.Contains(g.cell)) report.Errors.Add("Gate overlaps a piece at " + g.cell);
                if (!gateCells.Add(g.cell)) report.Errors.Add("Duplicate gate at " + g.cell);
            }
        }

        /// <summary>
        /// Picks the most useful next move for the hint system. Prefers a piece that can leave right now and
        /// that currently blocks the most other pieces. Falls back to a rotating arrow that needs turning.
        /// Returns -1 if nothing can move (should never happen for validated puzzles).
        /// </summary>
        public static int FindHint(PuzzleState state, out bool needsRotation)
        {
            needsRotation = false;
            int best = -1;
            int bestScore = int.MinValue;
            foreach (var p in state.Pieces)
            {
                if (state.IsRemoved(p.id) || !state.CanRemoveNow(p.id)) continue;
                int score = state.CountPiecesBlockedBy(p.id) * 10 - (p.hidden ? 3 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p.id;
                }
            }
            if (best >= 0) return best;

            foreach (var p in state.Pieces)
            {
                if (state.IsRemoved(p.id) || !p.IsRotating) continue;
                if (state.CanEventuallyLeaveNow(p.id))
                {
                    needsRotation = true;
                    return p.id;
                }
            }
            return -1;
        }
    }
}
