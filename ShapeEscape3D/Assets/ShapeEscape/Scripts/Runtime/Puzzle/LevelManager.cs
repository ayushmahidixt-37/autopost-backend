using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Loads levels (procedural generation runs on a worker thread), builds the board, routes taps through the
    /// rules engine, plays feedback, reveals hidden arrows, and runs the completion/reveal sequence.
    /// </summary>
    public sealed class LevelManager : MonoBehaviour
    {
        public event Action<LevelSession> LevelReady;
        public event Action<LevelSession> LevelStarted;
        public event Action<LevelSession> ProgressChanged;
        public event Action<LevelSession> LevelCompleted;
        /// <summary>Raised when a special arrow type is seen for the first time in a level (tutorial cards).</summary>
        public event Action<ArrowKind> MechanicIntroduced;

        public LevelSession Current { get; private set; }
        public bool IsLoading { get; private set; }
        public bool Interactive { get; private set; }
        public PuzzleBoardView Board { get; private set; }
        public CollisionManager Collision { get; private set; }
        public GameObject RevealModel { get; private set; }

        readonly Dictionary<string, KeyValuePair<PuzzleDefinition, ValidationReport>> _cache = new Dictionary<string, KeyValuePair<PuzzleDefinition, ValidationReport>>();
        readonly List<Vector3> _faceNormals = new List<Vector3>();
        GameManager _game;
        Coroutine _loadRoutine;
        int _combo;
        float _lastRemoveTime;
        float _hiddenCheckTimer;
        int _loadToken;

        public void Initialize(GameManager game)
        {
            _game = game;
            Board = new GameObject("PuzzleBoard").AddComponent<PuzzleBoardView>();
            Board.transform.SetParent(transform, false);
            Collision = gameObject.AddComponent<CollisionManager>();
        }

        // ---------------------------------------------------------------- loading

        /// <summary>
        /// Loads a level. When <paramref name="interactive"/> is false the board is shown as a slowly spinning
        /// preview (home screen) and <see cref="StartPlaying"/> enables input later without rebuilding.
        /// </summary>
        public void Load(LevelSpec spec, bool interactive, Action<LevelSession> onReady = null)
        {
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);
            _loadRoutine = StartCoroutine(LoadRoutine(spec, interactive, onReady));
        }

        IEnumerator LoadRoutine(LevelSpec spec, bool interactive, Action<LevelSession> onReady)
        {
            int token = ++_loadToken;
            IsLoading = true;
            Interactive = false;
            ClearRevealModel();

            var template = ObjectCatalog.Get(spec.objectId) ?? ObjectCatalog.Get(ObjectCatalog.CampaignOrder[0]);
            PuzzleDefinition def = null;
            ValidationReport validation = null;

            KeyValuePair<PuzzleDefinition, ValidationReport> cached;
            if (_cache.TryGetValue(spec.Key, out cached))
            {
                def = cached.Key;
                validation = cached.Value;
            }
            else
            {
                var handcrafted = LevelProvider.GetHandcrafted(spec);
                if (handcrafted != null)
                {
                    def = handcrafted;
                    validation = PuzzleValidator.Validate(def);
                    if (!validation.IsValid)
                    {
                        Debug.LogError("[LevelManager] Handcrafted level " + spec.number + " is invalid (" + validation + "). Falling back to procedural generation.");
                        def = null;
                    }
                }
                if (def == null)
                {
                    var request = spec.ToRequest(_game.Quality.PieceCap);
                    GenerationReport report = null;
                    var task = Task.Run(() =>
                    {
                        GenerationReport r;
                        var d = PuzzleGenerator.Generate(request, out r);
                        report = r;
                        return d;
                    });
                    while (!task.IsCompleted) yield return null;
                    if (task.IsFaulted)
                    {
                        Debug.LogException(task.Exception);
                        // Synchronous retry so the player is never stuck.
                        def = PuzzleGenerator.Generate(request, out report);
                    }
                    else def = task.Result;
                    validation = report.validation;
                }
                _cache[spec.Key] = new KeyValuePair<PuzzleDefinition, ValidationReport>(def, validation);
            }
            if (token != _loadToken) yield break;

            Collision.Begin(def);
            Board.Build(def, template, 0.42f);
            Current = new LevelSession { Spec = spec, Definition = def, Template = template, Validation = validation };

            var cam = _game.CameraRig;
            cam.SetTarget(Board.Root, Board.Radius, true, true);
            cam.SetAutoSpin(interactive ? 0f : 12f);
            cam.UserControlEnabled = interactive;
            _game.Environment.SetShadow(Board.Radius * 0.8f, true);

            yield return StartCoroutine(IntroAnimation());
            if (token != _loadToken) yield break;

            IsLoading = false;
            _loadRoutine = null;
            if (LevelReady != null) LevelReady(Current);
            if (onReady != null) onReady(Current);
            if (interactive) StartPlaying();
        }

        IEnumerator IntroAnimation()
        {
            var pieces = new List<ArrowPiece>(Board.Pieces);
            float maxDelay = 0.35f;
            float r = Mathf.Max(1f, Board.Radius);
            foreach (var p in pieces)
            {
                Vector3 home = p.HomeLocalPosition;
                Vector3 outward = home.sqrMagnitude > 0.01f ? home.normalized : Vector3.up;
                float delay = maxDelay * Mathf.Clamp01(home.magnitude / r);
                StartCoroutine(p.FlyIn(home + outward * 1.5f, 0.35f, delay));
            }
            yield return new WaitForSeconds(maxDelay + 0.4f);
        }

        /// <summary>Makes a loaded (preview) level playable.</summary>
        public void StartPlaying()
        {
            if (Current == null || Current.Completed) return;
            Interactive = true;
            var cam = _game.CameraRig;
            cam.SetAutoSpin(0f);
            cam.UserControlEnabled = true;
            cam.ResetView();
            if (!Current.Started)
            {
                Current.Started = true;
                Current.StartTime = Time.unscaledTime;
                if (LevelStarted != null) LevelStarted(Current);
                if (Current.Spec.introduces != ArrowKind.Normal && MechanicIntroduced != null)
                    MechanicIntroduced(Current.Spec.introduces);
            }
        }

        public void SetInteractive(bool interactive)
        {
            if (Current == null || Current.Completed) interactive = false;
            Interactive = interactive;
            _game.CameraRig.UserControlEnabled = interactive;
        }

        /// <summary>Rebuilds the current level from scratch.</summary>
        public void Restart()
        {
            if (Current == null) return;
            var spec = Current.Spec;
            Load(spec, true);
        }

        public void Unload()
        {
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);
            _loadToken++;
            IsLoading = false;
            Interactive = false;
            ClearRevealModel();
            Board.Clear();
            Collision.End();
            Current = null;
            _game.Environment.SetShadow(1f, false);
        }

        public void ClearRevealModel()
        {
            if (RevealModel != null) Destroy(RevealModel);
            RevealModel = null;
        }

        // ---------------------------------------------------------------- input

        public void HandleTap(Vector2 screenPos)
        {
            if (!Interactive || IsLoading || Current == null || Current.Completed) return;
            var piece = CollisionManager.PickPiece(_game.Environment.MainCamera, screenPos);
            if (piece == null) return;
            ProcessTap(piece);
        }

        void ProcessTap(ArrowPiece piece)
        {
            _game.Hints.ClearHint();
            Current.Taps++;
            var state = Collision.State;
            var result = Collision.Tap(piece.PieceId);
            var audio = _game.Audio;

            switch (result.Outcome)
            {
                case TapOutcome.Removed:
                    if (Time.unscaledTime - _lastRemoveTime > 2.5f) _combo = 0;
                    _lastRemoveTime = Time.unscaledTime;
                    for (int i = 0; i < result.Removed.Count; i++)
                    {
                        int id = result.Removed[i];
                        var dir = i == 0 ? result.MoveDir : state.CurrentDir(id);
                        StartCoroutine(RemoveAfter(id, dir, i * 0.09f, _combo++));
                    }
                    foreach (var id in result.Unlocked)
                    {
                        var p = Board.Get(id);
                        if (p != null) p.PlayUnlock();
                    }
                    if (result.Unlocked.Count > 0) audio.Play(Sfx.Unlock, 0.12f);
                    if (result.Removed.Count > 1) audio.Play(Sfx.Chain, 0.05f);
                    _game.Haptics.Light();
                    if (ProgressChanged != null) ProgressChanged(Current);
                    if (state.IsSolved) StartCoroutine(CompleteRoutine());
                    break;

                case TapOutcome.Blocked:
                    Current.BlockedTaps++;
                    _combo = 0;
                    piece.PlayBump(result.MoveDir, result.Check.FreeSteps);
                    StartCoroutine(FlashBlockerAfter(result, 0.05f + 0.045f * (result.Check.FreeSteps + 0.16f)));
                    audio.Play(Sfx.Blocked);
                    _game.Haptics.Medium();
                    _game.Analytics.Log(AnalyticsEvents.MoveBlocked, "level", LevelKey(), "pieces_left", state.RemainingCount);
                    break;

                case TapOutcome.Locked:
                    Current.BlockedTaps++;
                    piece.PlayLockedShake();
                    var key = Board.Get(result.Check.BlockerId);
                    if (key != null) StartCoroutine(BriefHighlight(key, 0.9f));
                    audio.Play(Sfx.Locked);
                    _game.Haptics.Medium();
                    break;

                case TapOutcome.Rotated:
                    piece.SetDirection(result.NewDir, true);
                    audio.Play(Sfx.Rotate);
                    _game.Haptics.Light();
                    break;
            }
        }

        IEnumerator RemoveAfter(int id, Dir dir, float delay, int combo)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var p = Board.Get(id);
            if (p == null) yield break;
            _game.Audio.PlayRemove(combo);
            _game.Vfx.Puff(p.transform.position, p.BodyColor);
            Board.OnPieceRemoved(id);
            p.PlayRemove(dir, Board.Radius * 2f + 6f, Board.Recycle);
        }

        IEnumerator FlashBlockerAfter(TapResult result, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (result.Check.Reason == BlockReason.Piece)
            {
                var blocker = Board.Get(result.Check.BlockerId);
                if (blocker != null) blocker.FlashBlocked();
            }
        }

        IEnumerator BriefHighlight(ArrowPiece piece, float seconds)
        {
            piece.SetHintHighlight(true);
            yield return new WaitForSeconds(seconds);
            if (piece != null && !piece.IsRemoved) piece.SetHintHighlight(false);
        }

        // ---------------------------------------------------------------- hidden arrows

        void Update()
        {
            if (!Interactive || Current == null) return;
            _hiddenCheckTimer -= Time.unscaledDeltaTime;
            if (_hiddenCheckTimer > 0) return;
            _hiddenCheckTimer = 0.12f;
            RevealVisibleHiddenArrows();
        }

        /// <summary>A hidden arrow is revealed once one of its arrow faces is turned toward the camera and unobstructed.</summary>
        void RevealVisibleHiddenArrows()
        {
            var cam = _game.Environment.MainCamera;
            if (cam == null) return;
            foreach (var p in Board.Pieces)
            {
                if (p.IsRemoved || !p.IsConcealed) continue;
                p.GetArrowFaceNormals(_faceNormals);
                foreach (var localN in _faceNormals)
                {
                    Vector3 n = p.transform.TransformDirection(localN);
                    Vector3 facePoint = p.transform.position + n * 0.5f;
                    Vector3 toCam = (cam.transform.position - facePoint).normalized;
                    if (Vector3.Dot(n, toCam) < 0.7f) continue;
                    // Line of sight: nothing else between the face and the camera.
                    RaycastHit hit;
                    if (Physics.Raycast(facePoint + n * 0.02f, toCam, out hit, 200f) && hit.collider.GetComponentInParent<ArrowPiece>() != p)
                        continue;
                    p.RevealHidden();
                    _game.Audio.Play(Sfx.Reveal);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- completion

        IEnumerator CompleteRoutine()
        {
            Interactive = false;
            Current.Completed = true;
            Current.EndTime = Time.unscaledTime;
            _game.CameraRig.UserControlEnabled = false;
            yield return new WaitForSeconds(0.45f);

            GameObject model = null;
            yield return StartCoroutine(_game.Reveal.Play(Board, Current.Template, _game, m => model = m));
            RevealModel = model;
            if (LevelCompleted != null) LevelCompleted(Current);
        }

        public string LevelKey()
        {
            return Current != null ? Current.Spec.Key : "none";
        }
    }
}
