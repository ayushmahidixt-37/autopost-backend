using System;
using System.Collections;
using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// The "Everyday Object Discovery" moment: after the last piece leaves, all pieces fly back in their true
    /// colours (reconstruction), then melt into the smooth, fully coloured object model with confetti, a chime
    /// and a gentle spin.
    /// </summary>
    public sealed class RevealSequence : MonoBehaviour
    {
        public IEnumerator Play(PuzzleBoardView board, ObjectTemplate template, GameManager game, Action<GameObject> modelCreated)
        {
            var cam = game.CameraRig;
            cam.ResetView();
            board.SetDecorVisible(false);
            game.Audio.Play(Sfx.Whoosh);

            // 1) Reconstruction: pieces return home in full colour, arrows hidden.
            var pieces = new List<ArrowPiece>(board.Pieces);
            pieces.Sort((a, b) => a.HomeLocalPosition.y.CompareTo(b.HomeLocalPosition.y)); // build bottom-up
            float spread = Mathf.Max(1f, board.Radius);
            float buildTime = Mathf.Clamp(0.5f + pieces.Count * 0.006f, 0.6f, 1.1f);
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                p.SetArrowsVisible(false);
                p.SetBodyColor(PuzzleBoardView.PaletteColor(template, p.Data.palette));
                Vector3 home = p.HomeLocalPosition;
                Vector3 dir = home.sqrMagnitude > 0.01f ? home.normalized : Vector3.up;
                float delay = buildTime * i / Mathf.Max(1, pieces.Count);
                StartCoroutine(p.FlyIn(home + dir * spread * 1.6f + Vector3.up * 2f, 0.4f, delay));
            }
            yield return new WaitForSeconds(buildTime + 0.45f);

            // 2) Morph into the smooth model.
            var model = ObjectModelBuilder.Build(template, board.Root);
            ObjectModelBuilder.AlignToBoard(model.transform, board);
            Vector3 finalScale = model.transform.localScale;
            Vector3 finalPos = model.transform.localPosition;
            model.transform.localScale = finalScale * 0.6f;
            if (modelCreated != null) modelCreated(model);

            game.Audio.Play(Sfx.Complete);
            game.Haptics.Success();
            game.Vfx.Confetti(board.Root.position, board.Radius);
            game.Vfx.SparkleRing(board.Root.position, board.Radius);
            cam.SetAutoSpin(22f);

            const float morph = 0.55f;
            float t = 0;
            while (t < morph)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / morph);
                model.transform.localScale = finalScale * Mathf.LerpUnclamped(0.6f, 1f, Tween.EaseOutBack(k));
                model.transform.localPosition = finalPos;
                float shrink = 1f - Tween.EaseInCubic(Mathf.Clamp01(k * 1.6f));
                foreach (var p in pieces)
                    if (p != null) p.transform.localScale = Vector3.one * shrink;
                yield return null;
            }
            foreach (var p in pieces)
                if (p != null) p.gameObject.SetActive(false);
            model.transform.localScale = finalScale;

            yield return new WaitForSeconds(0.35f);
        }
    }
}
