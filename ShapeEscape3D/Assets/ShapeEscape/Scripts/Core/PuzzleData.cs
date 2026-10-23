using System;
using System.Collections.Generic;

namespace ShapeEscape.Core
{
    public enum DifficultyTier
    {
        Beginner = 0,
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Expert = 4
    }

    /// <summary>The visible "kind" of a piece, used by the view layer to pick arrow styling.</summary>
    public enum ArrowKind
    {
        Normal,
        Locked,
        Chain,
        Rotating,
        Hidden,
        Color
    }

    /// <summary>
    /// One puzzle piece. Most pieces occupy a single grid cell, but multi-cell pieces are fully supported
    /// by the rules engine (useful for handcrafted levels).
    /// </summary>
    [Serializable]
    public class PuzzlePiece
    {
        public int id;
        public Int3[] cells = new Int3[0];
        public Dir dir;
        /// <summary>Index into the object template palette (body colour).</summary>
        public int palette;

        /// <summary>Locked arrow: piece cannot move until the piece with this id has been removed. -1 = not locked.</summary>
        public int lockKeyId = -1;

        /// <summary>Chain arrow: when this piece leaves, these pieces try to slide out automatically.</summary>
        public int[] chainTargets = new int[0];

        /// <summary>Rotating arrow: tapping while blocked advances the arrow through this cycle. Empty = not rotating.</summary>
        public Dir[] rotationCycle = new Dir[0];

        /// <summary>Hidden arrow: arrow is concealed until the player rotates the object to look at it.</summary>
        public bool hidden;

        /// <summary>Colour arrow: piece may pass through colour gates of the same id. -1 = uncoloured.</summary>
        public int colorId = -1;

        public bool IsLocked { get { return lockKeyId >= 0; } }
        public bool IsChain { get { return chainTargets != null && chainTargets.Length > 0; } }
        public bool IsRotating { get { return rotationCycle != null && rotationCycle.Length > 1; } }
        public bool IsColored { get { return colorId >= 0; } }

        public ArrowKind Kind
        {
            get
            {
                if (IsLocked) return ArrowKind.Locked;
                if (IsRotating) return ArrowKind.Rotating;
                if (IsChain) return ArrowKind.Chain;
                if (IsColored) return ArrowKind.Color;
                if (hidden) return ArrowKind.Hidden;
                return ArrowKind.Normal;
            }
        }

        public PuzzlePiece Clone()
        {
            var p = (PuzzlePiece)MemberwiseClone();
            p.cells = (Int3[])cells.Clone();
            p.chainTargets = chainTargets == null ? new int[0] : (int[])chainTargets.Clone();
            p.rotationCycle = rotationCycle == null ? new Dir[0] : (Dir[])rotationCycle.Clone();
            return p;
        }
    }

    /// <summary>A colour gate sitting just outside the object. Only pieces with the matching colour id can pass through it.</summary>
    [Serializable]
    public class ColorGate
    {
        public Int3 cell;
        public int colorId;
        /// <summary>The direction pieces travel when passing through (used to orient the gate frame).</summary>
        public Dir facing;
    }

    /// <summary>Complete, serialisable description of one puzzle layout.</summary>
    [Serializable]
    public class PuzzleDefinition
    {
        public string objectId;
        public DifficultyTier tier;
        public int seed;
        /// <summary>Size of one grid cell in template units (used to align the smooth reveal model with the pieces).</summary>
        public float cellSize = 1f;
        /// <summary>Template-space position of grid cell (0,0,0).</summary>
        public Float3 origin;
        public List<PuzzlePiece> pieces = new List<PuzzlePiece>();
        public List<ColorGate> gates = new List<ColorGate>();

        public PuzzleDefinition Clone()
        {
            var d = (PuzzleDefinition)MemberwiseClone();
            d.pieces = new List<PuzzlePiece>(pieces.Count);
            for (int i = 0; i < pieces.Count; i++) d.pieces.Add(pieces[i].Clone());
            d.gates = new List<ColorGate>(gates.Count);
            for (int i = 0; i < gates.Count; i++)
                d.gates.Add(new ColorGate { cell = gates[i].cell, colorId = gates[i].colorId, facing = gates[i].facing });
            return d;
        }

        public void GetBounds(out Int3 min, out Int3 max)
        {
            min = new Int3(int.MaxValue, int.MaxValue, int.MaxValue);
            max = new Int3(int.MinValue, int.MinValue, int.MinValue);
            bool any = false;
            foreach (var p in pieces)
                foreach (var c in p.cells)
                {
                    min = Int3.Min(min, c);
                    max = Int3.Max(max, c);
                    any = true;
                }
            if (!any)
            {
                min = Int3.Zero;
                max = Int3.Zero;
            }
        }

        public int CountKind(ArrowKind kind)
        {
            int n = 0;
            foreach (var p in pieces) if (p.Kind == kind) n++;
            return n;
        }
    }

    /// <summary>Piece-count and difficulty targets per tier.</summary>
    public struct TierInfo
    {
        public int minPieces;
        public int maxPieces;
        /// <summary>Target number of dependency "layers" (rounds of simultaneous removals) in the validator.</summary>
        public int minLayers;
        public int maxLayers;
        /// <summary>0..1 — how strongly the generator points arrows through already-peeled pieces (creates dependencies).</summary>
        public float inwardBias;
        /// <summary>Upper bound on the fraction of pieces that may be removable at the start (hardening pass target).</summary>
        public float maxInitialFraction;
        public int coinReward;

        public static TierInfo Get(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Beginner:
                    return new TierInfo { minPieces = 8, maxPieces = 15, minLayers = 2, maxLayers = 4, inwardBias = 0.25f, maxInitialFraction = 0.8f, coinReward = 10 };
                case DifficultyTier.Easy:
                    return new TierInfo { minPieces = 15, maxPieces = 25, minLayers = 3, maxLayers = 6, inwardBias = 0.4f, maxInitialFraction = 0.6f, coinReward = 15 };
                case DifficultyTier.Medium:
                    return new TierInfo { minPieces = 25, maxPieces = 45, minLayers = 4, maxLayers = 9, inwardBias = 0.55f, maxInitialFraction = 0.45f, coinReward = 20 };
                case DifficultyTier.Hard:
                    return new TierInfo { minPieces = 45, maxPieces = 70, minLayers = 5, maxLayers = 13, inwardBias = 0.7f, maxInitialFraction = 0.38f, coinReward = 30 };
                default:
                    return new TierInfo { minPieces = 70, maxPieces = 120, minLayers = 6, maxLayers = 18, inwardBias = 0.8f, maxInitialFraction = 0.32f, coinReward = 40 };
            }
        }
    }

    /// <summary>Which special arrows a level may use, and how many of each.</summary>
    [Serializable]
    public class MechanicPlan
    {
        public int locked;
        public int chain;
        public int rotating;
        public int hidden;
        public int colorGates;

        public bool Any { get { return locked + chain + rotating + hidden + colorGates > 0; } }

        public MechanicPlan Clone() { return (MechanicPlan)MemberwiseClone(); }

        public override string ToString()
        {
            return "L" + locked + " C" + chain + " R" + rotating + " H" + hidden + " G" + colorGates;
        }
    }
}
