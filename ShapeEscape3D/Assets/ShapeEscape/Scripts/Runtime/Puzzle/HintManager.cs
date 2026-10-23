using System;
using System.Collections;
using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Hints point at a piece that can move right now (preferring the one unblocking the most others), turn the
    /// object so its arrow faces the player, and draw the exit path. They never make the move for the player.
    /// Hints are a consumable: earned through play, bought with coins, or granted by an optional rewarded ad.
    /// </summary>
    public sealed class HintManager : MonoBehaviour
    {
        public const int HintCoinCost = 60;

        public event Action<int> HintsChanged;

        GameManager _game;
        ArrowPiece _highlighted;
        LineRenderer _path;
        Coroutine _pathRoutine;

        public int Available { get { return _game != null ? _game.Save.Data.hints : 0; } }

        public void Initialize(GameManager game)
        {
            _game = game;
            var go = new GameObject("HintPath");
            go.transform.SetParent(transform, false);
            _path = go.AddComponent<LineRenderer>();
            _path.useWorldSpace = true;
            _path.positionCount = 2;
            _path.widthMultiplier = 0.16f;
            _path.numCapVertices = 6;
            _path.textureMode = LineTextureMode.Tile;
            _path.sharedMaterial = MaterialLibrary.Fx(TextureFactory.SoftCircle);
            _path.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _path.enabled = false;
        }

        /// <summary>Uses one hint if available. Returns false when the player has none (UI then offers coins/ad).</summary>
        public bool TryUseHint()
        {
            var levels = _game.Levels;
            if (!levels.Interactive || levels.Current == null) return false;
            if (_game.Save.Data.hints <= 0) return false;
            if (!ShowHint()) return false;

            _game.Save.Data.hints--;
            _game.Save.MarkDirty();
            levels.Current.HintsUsed++;
            _game.Analytics.Log(AnalyticsEvents.HintUsed, "level", levels.LevelKey(), "hints_left", _game.Save.Data.hints, "source", "inventory");
            if (HintsChanged != null) HintsChanged(_game.Save.Data.hints);
            return true;
        }

        public void AddHints(int amount, string source)
        {
            _game.Save.Data.hints += amount;
            _game.Save.MarkDirty();
            _game.Analytics.Log(AnalyticsEvents.HintEarned, "amount", amount, "source", source);
            if (HintsChanged != null) HintsChanged(_game.Save.Data.hints);
        }

        public bool BuyHintWithCoins()
        {
            if (!_game.Progression.SpendCoins(HintCoinCost, "hint")) return false;
            AddHints(1, "coins");
            return true;
        }

        bool ShowHint()
        {
            var state = _game.Levels.Collision.State;
            if (state == null) return false;
            bool needsRotation;
            int id = PuzzleValidator.FindHint(state, out needsRotation);
            if (id < 0) return false;
            var piece = _game.Levels.Board.Get(id);
            if (piece == null) return false;

            ClearHint();
            _highlighted = piece;
            piece.SetHintHighlight(true);

            // Turn the object so one of the arrow faces looks at the player (prefer the most "outward" face).
            var faces = new List<Vector3>();
            piece.GetArrowFaceNormals(faces);
            Vector3 best = faces.Count > 0 ? faces[0] : Vector3.back;
            float bestScore = float.MinValue;
            Vector3 outward = piece.HomeLocalPosition.normalized;
            foreach (var f in faces)
            {
                float s = Vector3.Dot(f, outward);
                if (s > bestScore) { bestScore = s; best = f; }
            }
            _game.CameraRig.FocusLocalDirection(best);

            if (_pathRoutine != null) StopCoroutine(_pathRoutine);
            _pathRoutine = StartCoroutine(PathRoutine(piece, needsRotation));
            _game.Audio.Play(Sfx.Hint);
            return true;
        }

        IEnumerator PathRoutine(ArrowPiece piece, bool needsRotation)
        {
            _path.enabled = !needsRotation;
            var color = ColorUtil.Hex("#FFD166");
            float t = 0;
            while (_highlighted == piece && piece != null && !piece.IsRemoved)
            {
                t += Time.deltaTime;
                Vector3 start = piece.transform.position;
                Vector3 dir = piece.transform.parent.TransformDirection(ArrowPiece.DirVector(piece.Direction));
                float len = _game.Levels.Board.Radius * 1.4f + 1.5f;
                _path.SetPosition(0, start + dir * 0.55f);
                _path.SetPosition(1, start + dir * (0.55f + len * Mathf.Clamp01(t * 2.5f)));
                float a = 0.45f + 0.35f * Mathf.Sin(t * 6f);
                _path.startColor = color.WithAlpha(a);
                _path.endColor = color.WithAlpha(0f);
                yield return null;
            }
            _path.enabled = false;
            _pathRoutine = null;
        }

        public void ClearHint()
        {
            if (_highlighted != null && !_highlighted.IsRemoved) _highlighted.SetHintHighlight(false);
            _highlighted = null;
            if (_path != null) _path.enabled = false;
        }
    }
}
