using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Runtime record of the level being played (stats feed rewards and analytics).</summary>
    public sealed class LevelSession
    {
        public LevelSpec Spec;
        public PuzzleDefinition Definition;
        public ObjectTemplate Template;
        public ValidationReport Validation;
        public float StartTime;
        public float EndTime;
        public int Taps;
        public int BlockedTaps;
        public int HintsUsed;
        public bool Started;
        public bool Completed;

        public bool IsDaily { get { return Spec != null && Spec.isDaily; } }
        public float Duration { get { return (Completed ? EndTime : Time.unscaledTime) - StartTime; } }
        public int TotalPieces { get { return Definition != null ? Definition.pieces.Count : 0; } }

        /// <summary>1–3 stars: fewer blocked moves and no hints earn more.</summary>
        public int Stars
        {
            get
            {
                int stars = 3;
                if (BlockedTaps > Mathf.Max(2, TotalPieces / 6)) stars--;
                if (HintsUsed > 0) stars--;
                return Mathf.Max(1, stars);
            }
        }
    }
}
