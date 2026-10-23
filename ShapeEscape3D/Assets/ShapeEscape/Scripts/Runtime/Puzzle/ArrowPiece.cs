using System;
using System.Collections;
using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Visual + interactive representation of one puzzle piece (a rounded cube with arrow decals).
    /// Gameplay rules live in <see cref="PuzzleState"/>; this component only animates what the rules decide.
    /// Instances are pooled by <see cref="PuzzleBoardView"/>.
    /// </summary>
    public sealed class ArrowPiece : MonoBehaviour
    {
        public static readonly Color ArrowNormal = ColorUtil.Hex("#2B2D42");
        public static readonly Color ArrowLocked = ColorUtil.Hex("#6C757D");
        public static readonly Color ArrowChain = ColorUtil.Hex("#C9A227");
        public static readonly Color ArrowRotating = ColorUtil.Hex("#7B2CBF");
        public static readonly Color CageColor = ColorUtil.Hex("#8D99AE");
        public static readonly Color BlockFlash = ColorUtil.Hex("#FF5A5F");

        sealed class CellVisual
        {
            public Transform root;
            public MeshRenderer body;
            public MeshFilter arrowFilter;
            public MeshRenderer arrowRenderer;
            public BoxCollider collider;
        }

        readonly List<CellVisual> _cells = new List<CellVisual>();
        GameObject _cage;
        Coroutine _motion;
        Coroutine _flash;
        Coroutine _pulse;
        Material _bodyMat;
        Material _arrowMat;
        Dir _dir;
        bool _hiddenConcealed;

        public int PieceId { get; private set; }
        public PuzzlePiece Data { get; private set; }
        public Vector3 HomeLocalPosition { get; private set; }
        public bool IsRemoved { get; private set; }
        public bool IsConcealed { get { return _hiddenConcealed; } }
        public Color BodyColor { get; private set; }
        public Dir Direction { get { return _dir; } }

        public static ArrowPiece Create(Transform parent)
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(parent, false);
            var piece = go.AddComponent<ArrowPiece>();
            piece.AddCellVisual(go.transform);
            return piece;
        }

        CellVisual AddCellVisual(Transform root)
        {
            var cv = new CellVisual { root = root };
            // Note: Unity's fake-null objects make "??" unsafe for components, so add explicitly (roots are always fresh objects).
            var mf = root.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = MeshFactory.PieceMesh;
            cv.body = root.gameObject.AddComponent<MeshRenderer>();
            cv.body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            cv.body.receiveShadows = true;
            cv.collider = root.gameObject.AddComponent<BoxCollider>();
            cv.collider.size = Vector3.one * 0.98f;

            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(root, false);
            cv.arrowFilter = arrow.AddComponent<MeshFilter>();
            cv.arrowRenderer = arrow.AddComponent<MeshRenderer>();
            cv.arrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cv.arrowRenderer.receiveShadows = false;
            _cells.Add(cv);
            return cv;
        }

        /// <summary>Prepares a pooled piece for a new level.</summary>
        public void Setup(PuzzlePiece data, Vector3 localPos, Color bodyColor, Color arrowColor)
        {
            StopAllCoroutines();
            _motion = _flash = _pulse = null;
            Data = data;
            PieceId = data.id;
            IsRemoved = false;
            HomeLocalPosition = localPos;
            BodyColor = bodyColor;
            transform.localPosition = localPos;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.name = "Piece " + data.id;

            // Extra cells for multi-cell (handcrafted) pieces.
            while (_cells.Count > 1)
            {
                var extra = _cells[_cells.Count - 1];
                _cells.RemoveAt(_cells.Count - 1);
                Destroy(extra.root.gameObject);
            }
            for (int i = 1; i < data.cells.Length; i++)
            {
                var child = new GameObject("Cell" + i);
                child.transform.SetParent(transform, false);
                var offset = data.cells[i] - data.cells[0];
                child.transform.localPosition = new Vector3(offset.x, offset.y, offset.z);
                AddCellVisual(child.transform);
            }

            _bodyMat = MaterialLibrary.Lit(bodyColor);
            _arrowMat = MaterialLibrary.Unlit(arrowColor);
            foreach (var cv in _cells)
            {
                cv.body.sharedMaterial = _bodyMat;
                cv.arrowRenderer.sharedMaterial = _arrowMat;
                cv.collider.enabled = true;
                cv.arrowFilter.transform.localScale = Vector3.one;
            }

            _hiddenConcealed = data.hidden;
            SetDirection(data.dir, false);

            if (data.IsLocked) ShowCage(true);
            else ShowCage(false);
            gameObject.SetActive(true);
        }

        void ShowCage(bool show)
        {
            if (show && _cage == null)
            {
                _cage = new GameObject("Cage");
                _cage.transform.SetParent(transform, false);
                _cage.AddComponent<MeshFilter>().sharedMesh = MeshFactory.CageMesh;
                var r = _cage.AddComponent<MeshRenderer>();
                r.sharedMaterial = MaterialLibrary.Lit(CageColor, 0.7f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (_cage != null)
            {
                _cage.transform.localScale = Vector3.one;
                _cage.SetActive(show);
            }
        }

        public void SetDirection(Dir d, bool animate)
        {
            _dir = d;
            var mesh = _hiddenConcealed ? MeshFactory.HiddenMarkerMesh : MeshFactory.ArrowMesh(d);
            foreach (var cv in _cells) cv.arrowFilter.sharedMesh = mesh;
            if (animate && isActiveAndEnabled) StartCoroutine(PopArrow());
        }

        /// <summary>Hidden arrow became visible because the player looked at it.</summary>
        public void RevealHidden()
        {
            if (!_hiddenConcealed) return;
            _hiddenConcealed = false;
            SetDirection(_dir, true);
        }

        IEnumerator PopArrow()
        {
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float k = Tween.EaseOutBack(Mathf.Clamp01(t / 0.3f));
                foreach (var cv in _cells) cv.arrowFilter.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, k);
                yield return null;
            }
            foreach (var cv in _cells) cv.arrowFilter.transform.localScale = Vector3.one;
        }

        /// <summary>Slides the piece out of the object and hides it. <paramref name="done"/> fires when finished.</summary>
        public void PlayRemove(Dir d, float travel, Action<ArrowPiece> done)
        {
            IsRemoved = true;
            SetHintHighlight(false);
            foreach (var cv in _cells) cv.collider.enabled = false;
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(RemoveRoutine(d, travel, done));
        }

        IEnumerator RemoveRoutine(Dir d, float travel, Action<ArrowPiece> done)
        {
            var dirVec = DirVector(d);
            Vector3 start = transform.localPosition;
            const float duration = 0.42f;
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Quick anticipation, then accelerate away.
                float s = k < 0.12f ? -0.06f * Mathf.Sin(k / 0.12f * Mathf.PI) : Tween.EaseInCubic((k - 0.12f) / 0.88f) * travel;
                transform.localPosition = start + dirVec * s;
                float scale = k > 0.7f ? Mathf.Lerp(1f, 0.2f, (k - 0.7f) / 0.3f) : 1f;
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
            _motion = null;
            gameObject.SetActive(false);
            if (done != null) done(this);
        }

        /// <summary>Slides toward the obstacle, bumps it, and springs back home.</summary>
        public void PlayBump(Dir d, int freeSteps)
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(BumpRoutine(d, freeSteps));
        }

        IEnumerator BumpRoutine(Dir d, int freeSteps)
        {
            var dirVec = DirVector(d);
            Vector3 home = HomeLocalPosition;
            float reach = freeSteps + 0.16f;
            float outTime = 0.05f + 0.045f * reach;
            float t = 0;
            while (t < outTime)
            {
                t += Time.deltaTime;
                transform.localPosition = home + dirVec * (reach * Tween.EaseInQuad(Mathf.Clamp01(t / outTime)));
                yield return null;
            }
            float back = 0.18f + 0.03f * reach;
            t = 0;
            while (t < back)
            {
                t += Time.deltaTime;
                float k = Tween.EaseOutBack(Mathf.Clamp01(t / back));
                transform.localPosition = home + dirVec * (reach * (1f - k));
                yield return null;
            }
            transform.localPosition = home;
            _motion = null;
        }

        /// <summary>Little "no" wiggle for locked pieces.</summary>
        public void PlayLockedShake()
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(ShakeRoutine(0.07f, 0.3f));
        }

        IEnumerator ShakeRoutine(float amp, float duration)
        {
            float t = 0;
            Vector3 home = HomeLocalPosition;
            var side = Mathf.Abs(DirVector(_dir).x) > 0.5f ? Vector3.forward : Vector3.right;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                transform.localPosition = home + side * Mathf.Sin(k * Mathf.PI * 6f) * amp * (1f - k);
                yield return null;
            }
            transform.localPosition = home;
            _motion = null;
        }

        /// <summary>Red flash + squash on the piece that blocked a move.</summary>
        public void FlashBlocked()
        {
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRoutine(BlockFlash, 0.28f));
        }

        IEnumerator FlashRoutine(Color color, float duration)
        {
            var flashMat = MaterialLibrary.Lit(Color.Lerp(BodyColor, color, 0.75f));
            foreach (var cv in _cells) cv.body.sharedMaterial = flashMat;
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                transform.localScale = Vector3.one * (1f + 0.07f * k);
                yield return null;
            }
            transform.localScale = Vector3.one;
            foreach (var cv in _cells) cv.body.sharedMaterial = _bodyMat;
            _flash = null;
        }

        /// <summary>Lock opened: cage bursts away.</summary>
        public void PlayUnlock()
        {
            if (_cage == null || !_cage.activeSelf) return;
            StartCoroutine(UnlockRoutine());
        }

        IEnumerator UnlockRoutine()
        {
            float t = 0;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.35f);
                _cage.transform.localScale = Vector3.one * (1f + 0.6f * Tween.EaseOutCubic(k)) * (1f - k * k);
                yield return null;
            }
            _cage.SetActive(false);
            _arrowMat = MaterialLibrary.Unlit(ArrowNormal);
            foreach (var cv in _cells) cv.arrowRenderer.sharedMaterial = _arrowMat;
        }

        /// <summary>Gentle pulsing used by the hint system.</summary>
        public void SetHintHighlight(bool on)
        {
            if (_pulse != null)
            {
                StopCoroutine(_pulse);
                _pulse = null;
                if (!IsRemoved) transform.localScale = Vector3.one;
                foreach (var cv in _cells) cv.body.sharedMaterial = _bodyMat;
            }
            if (on && isActiveAndEnabled) _pulse = StartCoroutine(PulseRoutine());
        }

        IEnumerator PulseRoutine()
        {
            var glow = MaterialLibrary.Lit(ColorUtil.Brighten(BodyColor, 0.45f), 0.6f);
            foreach (var cv in _cells) cv.body.sharedMaterial = glow;
            float t = 0;
            while (true)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.one * (1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(t * 7f)));
                yield return null;
            }
        }

        /// <summary>Rebuild animation: fly in from <paramref name="from"/> to the home position.</summary>
        public IEnumerator FlyIn(Vector3 from, float duration, float delay)
        {
            gameObject.SetActive(true);
            transform.localScale = Vector3.zero;
            transform.localPosition = from;
            if (delay > 0) yield return new WaitForSeconds(delay);
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Tween.EaseOutCubic(Mathf.Clamp01(t / duration));
                transform.localPosition = Vector3.LerpUnclamped(from, HomeLocalPosition, k);
                transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, k);
                yield return null;
            }
            transform.localPosition = HomeLocalPosition;
            transform.localScale = Vector3.one;
        }

        /// <summary>Applies the fully saturated colour (used during the reconstruction animation).</summary>
        public void SetBodyColor(Color c)
        {
            BodyColor = c;
            _bodyMat = MaterialLibrary.Lit(c);
            foreach (var cv in _cells) cv.body.sharedMaterial = _bodyMat;
        }

        public void SetArrowsVisible(bool visible)
        {
            foreach (var cv in _cells) cv.arrowRenderer.enabled = visible;
            if (_cage != null && !visible) _cage.SetActive(false);
        }

        /// <summary>Local-space normals of the faces showing an arrow (perpendicular to the travel direction).</summary>
        public void GetArrowFaceNormals(List<Vector3> result)
        {
            result.Clear();
            foreach (var d in DirUtil.All)
                if (d.Axis() != _dir.Axis()) result.Add(DirVector(d));
        }

        public static Vector3 DirVector(Dir d)
        {
            var v = d.ToVector();
            return new Vector3(v.x, v.y, v.z);
        }
    }
}
