using System;
using System.Collections.Generic;

namespace ShapeEscape.Core
{
    public sealed class GenerationRequest
    {
        public string objectId;
        public DifficultyTier tier;
        /// <summary>Desired piece count. Clamped into the tier range; 0 = middle of the range.</summary>
        public int targetPieces;
        public int seed;
        public MechanicPlan mechanics = new MechanicPlan();
        public int maxAttempts = 10;
        /// <summary>Optional cap used by low-end devices (never below the tier minimum).</summary>
        public int devicePieceCap;
    }

    public sealed class GenerationReport
    {
        public int attempts;
        public ValidationReport validation;
        public bool usedFallback;
        public override string ToString()
        {
            return "attempts=" + attempts + (usedFallback ? " (fallback)" : "") + " " + validation;
        }
    }

    /// <summary>
    /// Procedural arrow-puzzle generator.
    ///
    /// Algorithm ("peeling"): starting from the full voxel silhouette, repeatedly pick a piece that has a clear
    /// path out of the *remaining* shape and give it that direction. The peel order is a valid solution by
    /// construction: every piece only depends on pieces peeled before it, so dependencies are acyclic.
    /// Difficulty comes from pointing arrows through already-peeled pieces (creating long dependency chains),
    /// which is measured by the validator's layer count and retried with derived seeds until it fits the tier.
    /// Special arrows are layered on top using the known solution order so they can never break solvability,
    /// and the final layout is always re-validated before it is returned.
    /// </summary>
    public static class PuzzleGenerator
    {
        public static readonly string[] GateColorHex = { "#FF8C42", "#2EC4B6", "#E056FD" };

        public static PuzzleDefinition Generate(GenerationRequest req, out GenerationReport report)
        {
            var template = ObjectCatalog.Get(req.objectId);
            if (template == null) throw new ArgumentException("Unknown object id: " + req.objectId);

            var tier = TierInfo.Get(req.tier);
            int min = tier.minPieces, max = tier.maxPieces;
            if (req.devicePieceCap > 0) max = Math.Max(min, Math.Min(max, req.devicePieceCap));
            int target = req.targetPieces > 0 ? Math.Max(min, Math.Min(max, req.targetPieces)) : (min + max) / 2;

            var voxels = Voxelizer.FitToCount(template, min, max, target);

            report = new GenerationReport();
            PuzzleDefinition best = null;
            ValidationReport bestReport = null;
            int bestScore = int.MaxValue;

            int attempts = Math.Max(1, req.maxAttempts);
            for (int a = 0; a < attempts; a++)
            {
                report.attempts = a + 1;
                var rng = new DeterministicRandom(DeterministicRandom.Combine(req.seed, a));
                var def = BuildAttempt(req, voxels, tier, rng);
                var v = PuzzleValidator.Validate(def);
                if (!v.IsValid) continue;

                int score = 0;
                if (v.Layers < tier.minLayers) score += (tier.minLayers - v.Layers) * 10;
                if (v.Layers > tier.maxLayers) score += (v.Layers - tier.maxLayers) * 10;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = def;
                    bestReport = v;
                }
                if (score == 0) break;
            }

            if (best == null)
            {
                // Should never happen (peeling is solvable by construction); fall back to plain arrows.
                var rng = new DeterministicRandom(req.seed);
                best = BuildAttempt(new GenerationRequest { objectId = req.objectId, tier = req.tier, seed = req.seed, mechanics = new MechanicPlan() }, voxels, tier, rng);
                bestReport = PuzzleValidator.Validate(best);
                report.usedFallback = true;
            }
            report.validation = bestReport;
            return best;
        }

        public static PuzzleDefinition Generate(GenerationRequest req)
        {
            GenerationReport r;
            return Generate(req, out r);
        }

        static PuzzleDefinition BuildAttempt(GenerationRequest req, VoxelModel voxels, TierInfo tier, DeterministicRandom rng)
        {
            int n = voxels.cells.Count;
            var cells = new Int3[n];
            for (int i = 0; i < n; i++) cells[i] = voxels.cells[i].cell;

            Dir[] dirs;
            int[] order;
            Peel(cells, tier.inwardBias, rng, out dirs, out order);

            var def = new PuzzleDefinition
            {
                objectId = req.objectId,
                tier = req.tier,
                seed = req.seed,
                cellSize = voxels.cellSize,
                origin = voxels.origin
            };
            for (int i = 0; i < n; i++)
            {
                def.pieces.Add(new PuzzlePiece
                {
                    id = i,
                    cells = new[] { cells[i] },
                    dir = dirs[i],
                    palette = voxels.cells[i].palette
                });
            }

            Harden(def, tier.maxInitialFraction, rng);

            // Rank = position in a valid solution (recomputed because hardening changed directions).
            var rank = new int[n];
            var solution = PuzzleValidator.Validate(def).Solution;
            if (solution.Count == n)
                for (int i = 0; i < n; i++) rank[solution[i]] = i;
            else
                for (int i = 0; i < n; i++) rank[order[i]] = i;

            if (req.mechanics != null && req.mechanics.Any)
                ApplyMechanics(def, req.mechanics, rank, rng);
            return def;
        }

        /// <summary>
        /// Difficulty pass: re-points initially-free pieces into other pieces (so they must wait) while a fast
        /// solvability check stays green, until at most <paramref name="maxInitialFraction"/> of pieces start free.
        /// </summary>
        static void Harden(PuzzleDefinition def, float maxInitialFraction, DeterministicRandom rng)
        {
            int n = def.pieces.Count;
            int goal = (int)Math.Ceiling(n * maxInitialFraction);
            var state = new PuzzleState(def); // shares the piece objects, so direction edits are seen immediately

            var free = new List<int>();
            foreach (var p in def.pieces)
                if (state.SweepPath(p.id, p.dir).Clear) free.Add(p.id);
            if (free.Count <= goal) return;

            rng.Shuffle(free);
            int freeCount = free.Count;
            var dirs = new List<Dir>(DirUtil.All);
            foreach (int id in free)
            {
                if (freeCount <= goal) break;
                var piece = state.GetPiece(id);
                Dir old = piece.dir;
                rng.Shuffle(dirs);
                foreach (var d in dirs)
                {
                    if (d == old) continue;
                    var c = state.SweepPath(id, d);
                    if (c.Clear) continue;
                    piece.dir = d;
                    if (IsSolvableFast(state))
                    {
                        freeCount--;
                        break;
                    }
                    piece.dir = old;
                }
            }
        }

        static bool IsSolvableFast(PuzzleState original)
        {
            var s = original.Clone();
            var batch = new List<int>();
            while (!s.IsSolved)
            {
                batch.Clear();
                foreach (var p in s.Pieces)
                    if (!s.IsRemoved(p.id) && s.SweepPath(p.id, p.dir).Clear) batch.Add(p.id);
                if (batch.Count == 0) return false;
                foreach (var id in batch) s.ForceRemove(id);
            }
            return true;
        }

        /// <summary>Computes a removal order and a direction for every cell. order[k] = index of the k-th removed cell.</summary>
        static void Peel(Int3[] cells, float inwardBias, DeterministicRandom rng, out Dir[] dirs, out int[] order)
        {
            int n = cells.Length;
            dirs = new Dir[n];
            order = new int[n];
            if (n == 0) return;

            Int3 min = cells[0], max = cells[0];
            foreach (var c in cells)
            {
                min = Int3.Min(min, c);
                max = Int3.Max(max, c);
            }
            int sx = max.x - min.x + 1, sy = max.y - min.y + 1, sz = max.z - min.z + 1;
            var grid = new int[sx, sy, sz];
            for (int x = 0; x < sx; x++) for (int y = 0; y < sy; y++) for (int z = 0; z < sz; z++) grid[x, y, z] = -1;
            for (int i = 0; i < n; i++) grid[cells[i].x - min.x, cells[i].y - min.y, cells[i].z - min.z] = i;

            var remaining = new bool[n];
            for (int i = 0; i < n; i++) remaining[i] = true;

            var candPieces = new List<int>();
            var candFreeCount = new List<int>();
            var freeDirs = new List<Dir>();
            var blockCounts = new List<int>();

            for (int step = 0; step < n; step++)
            {
                candPieces.Clear();
                candFreeCount.Clear();
                for (int i = 0; i < n; i++)
                {
                    if (!remaining[i]) continue;
                    int free = 0;
                    foreach (var d in DirUtil.All)
                    {
                        int blocks;
                        if (RayClear(grid, min, sx, sy, sz, cells[i], d, remaining, out blocks)) free++;
                    }
                    if (free > 0)
                    {
                        candPieces.Add(i);
                        candFreeCount.Add(free);
                    }
                }

                // Constrained pieces (fewer exits) are preferred on harder tiers: they read as "deeper" moves.
                int pick;
                if (rng.Chance(inwardBias * 0.5f))
                {
                    int minFree = int.MaxValue;
                    foreach (var f in candFreeCount) minFree = Math.Min(minFree, f);
                    var narrowed = new List<int>();
                    for (int k = 0; k < candPieces.Count; k++) if (candFreeCount[k] == minFree) narrowed.Add(candPieces[k]);
                    pick = narrowed[rng.Next(narrowed.Count)];
                }
                else
                {
                    pick = candPieces[rng.Next(candPieces.Count)];
                }

                freeDirs.Clear();
                blockCounts.Clear();
                foreach (var d in DirUtil.All)
                {
                    int blocks;
                    if (RayClear(grid, min, sx, sy, sz, cells[pick], d, remaining, out blocks))
                    {
                        freeDirs.Add(d);
                        blockCounts.Add(blocks);
                    }
                }

                int choice;
                if (rng.Chance(inwardBias))
                {
                    int bestBlocks = -1;
                    var bestIdx = new List<int>();
                    for (int k = 0; k < freeDirs.Count; k++)
                    {
                        if (blockCounts[k] > bestBlocks)
                        {
                            bestBlocks = blockCounts[k];
                            bestIdx.Clear();
                        }
                        if (blockCounts[k] == bestBlocks) bestIdx.Add(k);
                    }
                    choice = bestIdx[rng.Next(bestIdx.Count)];
                }
                else if (rng.Chance(0.5f))
                {
                    // Easy-leaning: prefer directions that are free in the original shape.
                    int bestBlocks = int.MaxValue;
                    var bestIdx = new List<int>();
                    for (int k = 0; k < freeDirs.Count; k++)
                    {
                        if (blockCounts[k] < bestBlocks)
                        {
                            bestBlocks = blockCounts[k];
                            bestIdx.Clear();
                        }
                        if (blockCounts[k] == bestBlocks) bestIdx.Add(k);
                    }
                    choice = bestIdx[rng.Next(bestIdx.Count)];
                }
                else
                {
                    choice = rng.Next(freeDirs.Count);
                }

                dirs[pick] = freeDirs[choice];
                order[step] = pick;
                remaining[pick] = false;
            }
        }

        /// <summary>
        /// True if no *remaining* cell lies on the ray from <paramref name="from"/> in <paramref name="d"/>.
        /// <paramref name="peeledBlocks"/> counts already-peeled cells on the ray (they will block this piece in the real puzzle).
        /// </summary>
        static bool RayClear(int[,,] grid, Int3 min, int sx, int sy, int sz, Int3 from, Dir d, bool[] remaining, out int peeledBlocks)
        {
            peeledBlocks = 0;
            Int3 step = d.ToVector();
            Int3 p = from - min + step;
            while (p.x >= 0 && p.y >= 0 && p.z >= 0 && p.x < sx && p.y < sy && p.z < sz)
            {
                int idx = grid[p.x, p.y, p.z];
                if (idx >= 0)
                {
                    if (remaining[idx]) return false;
                    peeledBlocks++;
                }
                p = p + step;
            }
            return true;
        }

        // ------------------------------------------------------------------ special arrows

        static void ApplyMechanics(PuzzleDefinition def, MechanicPlan plan, int[] rank, DeterministicRandom rng)
        {
            int n = def.pieces.Count;
            var special = new bool[n];
            var state = new PuzzleState(def);

            // Locked arrows: key must come earlier in the known solution.
            var lockCandidates = Shuffled(n, rng);
            int locks = 0;
            foreach (int i in lockCandidates)
            {
                if (locks >= plan.locked) break;
                if (special[i] || rank[i] < 3) continue;
                int key = -1, bestDist = int.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (j == i || rank[j] >= rank[i] || def.pieces[j].IsLocked) continue;
                    int dist = def.pieces[i].cells[0].ManhattanDistance(def.pieces[j].cells[0]);
                    if (dist < bestDist || (dist == bestDist && rng.Chance(0.5f)))
                    {
                        bestDist = dist;
                        key = j;
                    }
                }
                if (key < 0 || bestDist > 4) continue;
                def.pieces[i].lockKeyId = def.pieces[key].id;
                special[i] = true;
                special[key] = true;
                locks++;
            }

            // Chain arrows: trigger = the last piece (in solution order) blocking the target, so the chain fires visibly.
            int chains = 0;
            foreach (int b in Shuffled(n, rng))
            {
                if (chains >= plan.chain) break;
                if (special[b]) continue;
                var check = state.SweepPath(def.pieces[b].id, def.pieces[b].dir);
                if (check.Clear) continue;
                int a = -1, aRank = -1;
                var tmp = state.Clone();
                // Collect every blocker along b's path by removing blockers one by one.
                for (int guard = 0; guard < 32; guard++)
                {
                    var c = tmp.SweepPath(def.pieces[b].id, def.pieces[b].dir);
                    if (c.Clear) break;
                    if (c.Reason != BlockReason.Piece) { a = -1; break; }
                    int blockerIdx = c.BlockerId; // ids equal indices for generated puzzles
                    if (rank[blockerIdx] > aRank)
                    {
                        aRank = rank[blockerIdx];
                        a = blockerIdx;
                    }
                    tmp.ForceRemove(c.BlockerId);
                }
                if (a < 0 || special[a] || rank[a] > rank[b]) continue;
                def.pieces[a].chainTargets = new[] { def.pieces[b].id };
                special[a] = true;
                special[b] = true;
                chains++;
            }

            // Rotating arrows: the solution direction stays in the cycle; the arrow just starts elsewhere.
            int rots = 0;
            foreach (int i in Shuffled(n, rng))
            {
                if (rots >= plan.rotating) break;
                if (special[i]) continue;
                var p = def.pieces[i];
                int dirAxis = p.dir.Axis();
                int rotAxis = (dirAxis + 1 + rng.Next(2)) % 3;
                var cycle = DirUtil.CycleAround(rotAxis);
                int idx = Array.IndexOf(cycle, p.dir);
                if (idx < 0) continue;
                int startIdx = (idx + 1 + rng.Next(3)) % 4;
                // Store the cycle rotated so the start direction is first and remember it in "dir".
                var rotated = new Dir[4];
                for (int k = 0; k < 4; k++) rotated[k] = cycle[(startIdx + k) % 4];
                p.rotationCycle = rotated;
                p.dir = rotated[0];
                special[i] = true;
                rots++;
            }

            // Hidden arrows: purely visual, pick mid-solution pieces so they matter.
            int hidden = 0;
            foreach (int i in Shuffled(n, rng))
            {
                if (hidden >= plan.hidden) break;
                if (special[i] || rank[i] < n / 4) continue;
                def.pieces[i].hidden = true;
                special[i] = true;
                hidden++;
            }

            // Colour gates: placed just outside the object on a piece's exit line; every piece using that line gets the colour.
            Int3 bmin, bmax;
            def.GetBounds(out bmin, out bmax);
            int gates = 0;
            var gateCells = new HashSet<Int3>();
            foreach (int i in Shuffled(n, rng))
            {
                if (gates >= plan.colorGates) break;
                if (special[i]) continue;
                var p = def.pieces[i];
                if (p.IsRotating) continue;
                Int3 step = p.dir.ToVector();
                Int3 g = p.cells[0];
                int axis = p.dir.Axis();
                while (g[axis] >= bmin[axis] && g[axis] <= bmax[axis]) g = g + step;
                if (gateCells.Contains(g)) continue;

                var line = new List<int>();
                bool ok = true;
                for (int j = 0; j < n; j++)
                {
                    var q = def.pieces[j];
                    if (q.dir != p.dir || q.IsRotating) continue;
                    var c = q.cells[0];
                    bool sameLine = true;
                    for (int ax = 0; ax < 3; ax++) if (ax != axis && c[ax] != g[ax]) sameLine = false;
                    if (!sameLine) continue;
                    if (q.IsColored) { ok = false; break; }
                    line.Add(j);
                }
                if (!ok || line.Count == 0) continue;
                int color = gates % GateColorHex.Length;
                foreach (int j in line)
                {
                    def.pieces[j].colorId = color;
                    special[j] = true;
                }
                def.gates.Add(new ColorGate { cell = g, colorId = color, facing = p.dir });
                gateCells.Add(g);
                gates++;
            }
        }

        static List<int> Shuffled(int n, DeterministicRandom rng)
        {
            var l = new List<int>(n);
            for (int i = 0; i < n; i++) l.Add(i);
            rng.Shuffle(l);
            return l;
        }
    }
}
