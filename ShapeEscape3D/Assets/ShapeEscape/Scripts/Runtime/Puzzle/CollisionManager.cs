using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Owns the authoritative <see cref="PuzzleState"/> for the active level and answers movement / collision
    /// questions. Collision is resolved on the puzzle grid (exact swept-cell tests), not with physics: a piece
    /// may leave only if every cell along its arrow is free of other pieces and of gates of another colour.
    /// Unity physics is used for nothing but tap ray-casts against piece colliders.
    /// </summary>
    public sealed class CollisionManager : MonoBehaviour
    {
        public PuzzleState State { get; private set; }

        public void Begin(PuzzleDefinition def)
        {
            State = new PuzzleState(def);
        }

        public void End()
        {
            State = null;
        }

        public TapResult Tap(int pieceId)
        {
            return State != null ? State.Tap(pieceId) : new TapResult { Outcome = TapOutcome.Ignored, PieceId = pieceId };
        }

        public bool CanMove(int pieceId)
        {
            return State != null && State.CanRemoveNow(pieceId);
        }

        public PathCheck Probe(int pieceId)
        {
            return State != null ? State.Check(pieceId) : PathCheck.Free();
        }

        /// <summary>Ray-casts a screen point against piece colliders.</summary>
        public static ArrowPiece PickPiece(Camera cam, Vector2 screenPos)
        {
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(screenPos);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 500f, ~0, QueryTriggerInteraction.Ignore)) return null;
            var piece = hit.collider.GetComponentInParent<ArrowPiece>();
            return piece != null && !piece.IsRemoved ? piece : null;
        }
    }
}
