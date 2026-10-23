using System;
using System.Collections.Generic;

namespace ShapeEscape.Core
{
    public enum TapOutcome
    {
        /// <summary>The piece slid out of the object.</summary>
        Removed,
        /// <summary>Something is in the way; the piece bumps and returns.</summary>
        Blocked,
        /// <summary>The piece is locked until its key piece is removed.</summary>
        Locked,
        /// <summary>A rotating arrow was blocked and turned to its next direction.</summary>
        Rotated,
        /// <summary>Tap on an already removed / unknown piece.</summary>
        Ignored
    }

    public enum BlockReason
    {
        None,
        Piece,
        Gate,
        Lock
    }

    /// <summary>Result of sweeping a piece along a direction through the grid.</summary>
    public struct PathCheck
    {
        public bool Clear;
        public BlockReason Reason;
        /// <summary>Id of the blocking piece (Reason == Piece) or of the key piece (Reason == Lock), otherwise -1.</summary>
        public int BlockerId;
        /// <summary>Grid cell where the obstruction sits.</summary>
        public Int3 BlockCell;
        /// <summary>How many whole cells the piece can travel before touching the obstruction.</summary>
        public int FreeSteps;

        public static PathCheck Free() { return new PathCheck { Clear = true, Reason = BlockReason.None, BlockerId = -1 }; }
    }

    public sealed class TapResult
    {
        public TapOutcome Outcome;
        public int PieceId;
        public Dir MoveDir;
        public PathCheck Check;
        /// <summary>For rotating arrows: the new direction after rotation.</summary>
        public Dir NewDir;
        /// <summary>Every piece removed by this tap, in order (tapped piece first, then chain reactions).</summary>
        public readonly List<int> Removed = new List<int>();
        /// <summary>Pieces whose lock opened because their key was removed by this tap.</summary>
        public readonly List<int> Unlocked = new List<int>();
    }

    /// <summary>
    /// Live puzzle board and authoritative rules engine. All collision checks are exact grid sweeps:
    /// a piece can leave when every cell on its path (until it is outside the board) is empty of other
    /// remaining pieces and of gates that reject its colour.
    /// Removing pieces never makes another piece harder to remove (the rules are monotone), which is
    /// what makes greedy validation complete and guarantees players can never reach a dead end.
    /// </summary>
    public sealed class PuzzleState
    {
        readonly PuzzleDefinition _def;
        readonly PuzzlePiece[] _pieces;
        readonly Dictionary<int, int> _indexById = new Dictionary<int, int>();
        readonly Dictionary<Int3, int> _occupancy = new Dictionary<Int3, int>();
        readonly Dictionary<Int3, ColorGate> _gates = new Dictionary<Int3, ColorGate>();
        readonly bool[] _removed;
        readonly int[] _rotationIndex;
        Int3 _min;
        Int3 _max;
        int _remaining;

        public PuzzleDefinition Definition { get { return _def; } }
        public int PieceCount { get { return _pieces.Length; } }
        public int RemainingCount { get { return _remaining; } }
        public bool IsSolved { get { return _remaining == 0; } }
        public Int3 BoundsMin { get { return _min; } }
        public Int3 BoundsMax { get { return _max; } }

        public PuzzleState(PuzzleDefinition def)
        {
            if (def == null) throw new ArgumentNullException("def");
            _def = def;
            _pieces = new PuzzlePiece[def.pieces.Count];
            _removed = new bool[_pieces.Length];
            _rotationIndex = new int[_pieces.Length];
            _min = new Int3(int.MaxValue, int.MaxValue, int.MaxValue);
            _max = new Int3(int.MinValue, int.MinValue, int.MinValue);

            for (int i = 0; i < _pieces.Length; i++)
            {
                var p = def.pieces[i];
                _pieces[i] = p;
                if (_indexById.ContainsKey(p.id)) throw new InvalidOperationException("Duplicate piece id " + p.id);
                _indexById[p.id] = i;
                foreach (var c in p.cells)
                {
                    if (_occupancy.ContainsKey(c)) throw new InvalidOperationException("Overlapping pieces at " + c);
                    _occupancy[c] = p.id;
                    _min = Int3.Min(_min, c);
                    _max = Int3.Max(_max, c);
                }
                if (p.IsRotating)
                {
                    int idx = Array.IndexOf(p.rotationCycle, p.dir);
                    _rotationIndex[i] = idx < 0 ? 0 : idx;
                }
            }
            foreach (var g in def.gates)
            {
                _gates[g.cell] = g;
                _min = Int3.Min(_min, g.cell);
                _max = Int3.Max(_max, g.cell);
            }
            if (_pieces.Length == 0)
            {
                _min = Int3.Zero;
                _max = Int3.Zero;
            }
            _remaining = _pieces.Length;
        }

        PuzzleState(PuzzleState src)
        {
            _def = src._def;
            _pieces = src._pieces;
            _indexById = src._indexById;
            _gates = src._gates;
            _occupancy = new Dictionary<Int3, int>(src._occupancy);
            _removed = (bool[])src._removed.Clone();
            _rotationIndex = (int[])src._rotationIndex.Clone();
            _min = src._min;
            _max = src._max;
            _remaining = src._remaining;
        }

        /// <summary>Copies the mutable state (removed flags, rotations). Definition data is shared.</summary>
        public PuzzleState Clone() { return new PuzzleState(this); }

        public IEnumerable<PuzzlePiece> Pieces { get { return _pieces; } }

        /// <summary>True if a (not yet removed) piece occupies the cell.</summary>
        public bool IsOccupiedInitially(Int3 cell) { return _occupancy.ContainsKey(cell); }

        public PuzzlePiece GetPiece(int id)
        {
            int idx;
            return _indexById.TryGetValue(id, out idx) ? _pieces[idx] : null;
        }

        public bool IsRemoved(int id)
        {
            int idx;
            return !_indexById.TryGetValue(id, out idx) || _removed[idx];
        }

        /// <summary>The direction the piece's arrow currently points (rotating arrows change over time).</summary>
        public Dir CurrentDir(int id)
        {
            int idx = _indexById[id];
            var p = _pieces[idx];
            return p.IsRotating ? p.rotationCycle[_rotationIndex[idx]] : p.dir;
        }

        public bool IsLockedNow(int id)
        {
            var p = GetPiece(id);
            return p != null && p.IsLocked && !IsRemoved(p.lockKeyId);
        }

        /// <summary>Sweeps the piece along <paramref name="dir"/> and reports the first obstruction, ignoring locks.</summary>
        public PathCheck SweepPath(int id, Dir dir)
        {
            var p = GetPiece(id);
            if (p == null) return PathCheck.Free();
            Int3 step = dir.ToVector();
            int axis = dir.Axis();
            int sign = dir.Sign();
            var best = PathCheck.Free();
            best.FreeSteps = int.MaxValue;

            foreach (var start in p.cells)
            {
                Int3 pos = start;
                int t = 0;
                while (true)
                {
                    pos = pos + step;
                    if (sign > 0 ? pos[axis] > _max[axis] : pos[axis] < _min[axis]) break; // left the board

                    int occ;
                    if (_occupancy.TryGetValue(pos, out occ) && occ != p.id)
                    {
                        if (!best.Clear && best.FreeSteps <= t) break;
                        best = new PathCheck { Clear = false, Reason = BlockReason.Piece, BlockerId = occ, BlockCell = pos, FreeSteps = t };
                        break;
                    }
                    ColorGate gate;
                    if (_gates.TryGetValue(pos, out gate) && gate.colorId != p.colorId)
                    {
                        if (!best.Clear && best.FreeSteps <= t) break;
                        best = new PathCheck { Clear = false, Reason = BlockReason.Gate, BlockerId = -1, BlockCell = pos, FreeSteps = t };
                        break;
                    }
                    t++;
                }
            }
            if (best.Clear) best.FreeSteps = 0;
            return best;
        }

        /// <summary>Full movement check for the piece's current arrow, including locks.</summary>
        public PathCheck Check(int id)
        {
            var p = GetPiece(id);
            if (p == null || IsRemoved(id)) return PathCheck.Free();
            if (p.IsLocked && !IsRemoved(p.lockKeyId))
            {
                return new PathCheck { Clear = false, Reason = BlockReason.Lock, BlockerId = p.lockKeyId, FreeSteps = 0 };
            }
            return SweepPath(id, CurrentDir(id));
        }

        public bool CanRemoveNow(int id)
        {
            return !IsRemoved(id) && Check(id).Clear;
        }

        /// <summary>True if the piece can leave now in its current direction or, for rotating arrows, after some rotations.</summary>
        public bool CanEventuallyLeaveNow(int id)
        {
            var p = GetPiece(id);
            if (p == null || IsRemoved(id)) return false;
            if (p.IsLocked && !IsRemoved(p.lockKeyId)) return false;
            if (!p.IsRotating) return SweepPath(id, p.dir).Clear;
            foreach (var d in p.rotationCycle)
                if (SweepPath(id, d).Clear) return true;
            return false;
        }

        /// <summary>Applies a player tap: removes, bumps, or rotates the piece and resolves chain reactions.</summary>
        public TapResult Tap(int id)
        {
            var result = new TapResult { PieceId = id, Outcome = TapOutcome.Ignored };
            if (IsRemoved(id)) return result;

            var p = GetPiece(id);
            result.MoveDir = CurrentDir(id);
            var check = Check(id);
            result.Check = check;

            if (check.Clear)
            {
                result.Outcome = TapOutcome.Removed;
                RemoveWithChain(id, result);
                return result;
            }

            if (check.Reason == BlockReason.Lock)
            {
                result.Outcome = TapOutcome.Locked;
                return result;
            }

            if (p.IsRotating)
            {
                int idx = _indexById[id];
                _rotationIndex[idx] = (_rotationIndex[idx] + 1) % p.rotationCycle.Length;
                result.Outcome = TapOutcome.Rotated;
                result.NewDir = CurrentDir(id);
                return result;
            }

            result.Outcome = TapOutcome.Blocked;
            return result;
        }

        /// <summary>Removes a piece unconditionally (used by validators and editor tools).</summary>
        public void ForceRemove(int id)
        {
            int idx;
            if (!_indexById.TryGetValue(id, out idx) || _removed[idx]) return;
            _removed[idx] = true;
            _remaining--;
            foreach (var c in _pieces[idx].cells) _occupancy.Remove(c);
        }

        /// <summary>Rotates a rotating arrow to the given direction (validator helper).</summary>
        public void SetRotation(int id, Dir dir)
        {
            int idx = _indexById[id];
            var p = _pieces[idx];
            if (!p.IsRotating) return;
            int r = Array.IndexOf(p.rotationCycle, dir);
            if (r >= 0) _rotationIndex[idx] = r;
        }

        void RemoveWithChain(int id, TapResult result)
        {
            var queue = new Queue<int>();
            queue.Enqueue(id);
            bool first = true;
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                if (IsRemoved(cur)) continue;
                if (!first && !Check(cur).Clear) continue; // chain partner still blocked: it stays
                first = false;

                ForceRemove(cur);
                result.Removed.Add(cur);

                // Locks opened by this removal.
                foreach (var other in _pieces)
                    if (other.lockKeyId == cur && !IsRemoved(other.id))
                        result.Unlocked.Add(other.id);

                var piece = GetPiece(cur);
                if (piece.IsChain)
                    foreach (var target in piece.chainTargets)
                        if (!IsRemoved(target)) queue.Enqueue(target);
            }
        }

        /// <summary>Number of remaining pieces whose exit path currently crosses this piece (used by hints).</summary>
        public int CountPiecesBlockedBy(int id)
        {
            int n = 0;
            foreach (var other in _pieces)
            {
                if (other.id == id || IsRemoved(other.id)) continue;
                var c = SweepPath(other.id, CurrentDir(other.id));
                if (!c.Clear && c.Reason == BlockReason.Piece && c.BlockerId == id) n++;
                if (other.lockKeyId == id) n++;
            }
            return n;
        }
    }
}
