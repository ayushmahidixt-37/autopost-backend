using System;
using System.Collections.Generic;

namespace ShapeEscape.Core
{
    public struct VoxelCell
    {
        public Int3 cell;
        public int palette;
    }

    public sealed class VoxelModel
    {
        public readonly List<VoxelCell> cells = new List<VoxelCell>();
        public float cellSize;
        /// <summary>Template-space position of cell (0,0,0).</summary>
        public Float3 origin;
    }

    /// <summary>Turns an object template into a grid of puzzle cells at a chosen resolution.</summary>
    public static class Voxelizer
    {
        /// <summary>
        /// Samples the template on a grid centred on the object. Each cell is super-sampled (k^3 points) and kept
        /// when at least <paramref name="fillThreshold"/> of the samples are inside, which preserves thin parts
        /// (handles, straps) far better than a single centre sample.
        /// </summary>
        public static VoxelModel Voxelize(ObjectTemplate t, float cellSize, int subsamples = 3, float fillThreshold = 0.3f)
        {
            return Voxelize(t, cellSize, Float3.Zero, subsamples, fillThreshold);
        }

        /// <param name="gridShift">Offset of the sampling grid in cells (e.g. 0.5 on an axis samples between the default centres).</param>
        public static VoxelModel Voxelize(ObjectTemplate t, float cellSize, Float3 gridShift, int subsamples = 3, float fillThreshold = 0.3f)
        {
            Float3 bmin, bmax;
            t.GetSolidBounds(out bmin, out bmax);
            Float3 center = (bmin + bmax) * 0.5f + gridShift * cellSize;

            int ix0 = (int)Math.Floor((bmin.x - center.x) / cellSize) - 1, ix1 = (int)Math.Ceiling((bmax.x - center.x) / cellSize) + 1;
            int iy0 = (int)Math.Floor((bmin.y - center.y) / cellSize) - 1, iy1 = (int)Math.Ceiling((bmax.y - center.y) / cellSize) + 1;
            int iz0 = (int)Math.Floor((bmin.z - center.z) / cellSize) - 1, iz1 = (int)Math.Ceiling((bmax.z - center.z) / cellSize) + 1;

            var model = new VoxelModel { cellSize = cellSize };
            int k = Math.Max(1, subsamples);
            int total = k * k * k;
            int needed = Math.Max(1, (int)Math.Ceiling(total * fillThreshold));
            var votes = new int[Math.Max(1, t.palette.Length) + 8];
            var offsets = new float[k];
            for (int i = 0; i < k; i++) offsets[i] = ((i + 0.5f) / k - 0.5f) * cellSize;

            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            for (int ix = ix0; ix <= ix1; ix++)
                for (int iy = iy0; iy <= iy1; iy++)
                    for (int iz = iz0; iz <= iz1; iz++)
                    {
                        var c = new Float3(center.x + ix * cellSize, center.y + iy * cellSize, center.z + iz * cellSize);
                        Array.Clear(votes, 0, votes.Length);
                        int inside = 0;
                        for (int a = 0; a < k; a++)
                            for (int b = 0; b < k; b++)
                                for (int d = 0; d < k; d++)
                                {
                                    int pal = t.Sample(new Float3(c.x + offsets[a], c.y + offsets[b], c.z + offsets[d]));
                                    if (pal < 0) continue;
                                    inside++;
                                    if (pal < votes.Length) votes[pal]++;
                                }
                        if (inside < needed) continue;
                        int best = 0;
                        for (int v = 1; v < votes.Length; v++) if (votes[v] > votes[best]) best = v;
                        model.cells.Add(new VoxelCell { cell = new Int3(ix, iy, iz), palette = best });
                        minX = Math.Min(minX, ix);
                        minY = Math.Min(minY, iy);
                        minZ = Math.Min(minZ, iz);
                    }

            if (model.cells.Count == 0)
            {
                model.origin = center;
                return model;
            }

            // Normalise indices so the grid starts at (0,0,0).
            var shift = new Int3(minX, minY, minZ);
            for (int i = 0; i < model.cells.Count; i++)
            {
                var vc = model.cells[i];
                vc.cell = vc.cell - shift;
                model.cells[i] = vc;
            }
            model.origin = new Float3(center.x + minX * cellSize, center.y + minY * cellSize, center.z + minZ * cellSize);
            return model;
        }

        static readonly Dictionary<string, VoxelModel> Cache = new Dictionary<string, VoxelModel>();
        static readonly object CacheLock = new object();

        /// <summary>
        /// Finds a resolution whose cell count lands inside [min, max], as close as possible to <paramref name="target"/>.
        /// Deterministic (the same inputs always produce the same model) and cached per template/budget.
        /// </summary>
        public static VoxelModel FitToCount(ObjectTemplate t, int min, int max, int target)
        {
            string key = t.id + "|" + min + "|" + max + "|" + target;
            lock (CacheLock)
            {
                VoxelModel cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
            }

            Float3 bmin, bmax;
            t.GetSolidBounds(out bmin, out bmax);
            Float3 size = bmax - bmin;
            float extent = Math.Max(size.x, Math.Max(size.y, size.z));

            VoxelModel best = null;
            int bestScore = int.MaxValue;
            Action<VoxelModel> consider = m =>
            {
                int n = m.cells.Count;
                int outside = n < min ? (min - n) * 1000 : n > max ? (n - max) * 1000 : 0;
                int score = outside + Math.Abs(n - target);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = m;
                }
            };

            // Binary search on cell size (count decreases roughly monotonically as cells get bigger).
            float lo = extent / 40f, hi = extent / 1.2f;
            for (int i = 0; i < 14; i++)
            {
                float mid = (lo + hi) * 0.5f;
                var m = Voxelize(t, mid);
                consider(m);
                if (m.cells.Count > target) lo = mid;
                else hi = mid;
            }

            // Local scan around the best size to escape non-monotonic plateaus.
            float s0 = best.cellSize;
            for (int i = -6; i <= 6; i++)
            {
                if (i == 0 || bestScore <= 2) continue;
                consider(Voxelize(t, s0 * (1f + i * 0.02f)));
            }

            // Still outside the budget: try half-cell grid shifts, which change how thin parts are captured.
            if (bestScore >= 1000)
            {
                var shifts = new[] { new Float3(0.5f, 0, 0), new Float3(0, 0.5f, 0), new Float3(0, 0, 0.5f), new Float3(0.5f, 0.5f, 0.5f) };
                foreach (var shift in shifts)
                    for (int i = -4; i <= 4 && bestScore >= 1000; i++)
                        consider(Voxelize(t, s0 * (1f + i * 0.03f), shift));
            }

            if (best.cells.Count > max) TrimInterior(best, max);
            if (best.cells.Count > max) TrimSurface(best, max);

            lock (CacheLock) Cache[key] = best;
            return best;
        }

        /// <summary>Last resort: drops the cells with the fewest neighbours (silhouette noise) until within budget.</summary>
        static void TrimSurface(VoxelModel model, int max)
        {
            while (model.cells.Count > max)
            {
                var set = new HashSet<Int3>();
                foreach (var c in model.cells) set.Add(c.cell);
                int worst = -1, worstN = int.MaxValue;
                for (int i = 0; i < model.cells.Count; i++)
                {
                    int nb = 0;
                    foreach (var d in DirUtil.All) if (set.Contains(model.cells[i].cell + d.ToVector())) nb++;
                    if (nb < worstN) { worstN = nb; worst = i; }
                }
                model.cells.RemoveAt(worst);
            }
        }

        /// <summary>Removes fully enclosed cells (invisible to the player) until the model fits the budget.</summary>
        static void TrimInterior(VoxelModel model, int max)
        {
            var set = new HashSet<Int3>();
            foreach (var c in model.cells) set.Add(c.cell);
            for (int i = model.cells.Count - 1; i >= 0 && model.cells.Count > max; i--)
            {
                var c = model.cells[i].cell;
                bool enclosed = true;
                foreach (var d in DirUtil.All)
                    if (!set.Contains(c + d.ToVector())) { enclosed = false; break; }
                if (!enclosed) continue;
                set.Remove(c);
                model.cells.RemoveAt(i);
            }
        }
    }
}
