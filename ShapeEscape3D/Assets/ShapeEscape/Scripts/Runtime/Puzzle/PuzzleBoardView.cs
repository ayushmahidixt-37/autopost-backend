using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Spawns and owns the 3D representation of a puzzle: pooled pieces, colour gates and chain links, all
    /// parented to <see cref="Root"/> (the transform the player rotates). Grid cell (x,y,z) maps to local
    /// position (x,y,z) - GridCenter, so one cell = one world unit.
    /// </summary>
    public sealed class PuzzleBoardView : MonoBehaviour
    {
        readonly Dictionary<int, ArrowPiece> _pieces = new Dictionary<int, ArrowPiece>();
        readonly Stack<ArrowPiece> _pool = new Stack<ArrowPiece>();
        readonly List<GameObject> _decor = new List<GameObject>();
        readonly List<ChainLink> _chains = new List<ChainLink>();

        struct ChainLink
        {
            public int a;
            public int b;
            public LineRenderer line;
        }

        public Transform Root { get; private set; }
        public Vector3 GridCenter { get; private set; }
        public float Radius { get; private set; }
        public PuzzleDefinition Definition { get; private set; }
        public ObjectTemplate Template { get; private set; }
        public IEnumerable<ArrowPiece> Pieces { get { return _pieces.Values; } }

        void Awake()
        {
            Root = new GameObject("BoardRoot").transform;
            Root.SetParent(transform, false);
        }

        public static Color PaletteColor(ObjectTemplate t, int index)
        {
            if (t == null || t.palette == null || t.palette.Length == 0) return Color.white;
            return ColorUtil.Hex(t.palette[Mathf.Clamp(index, 0, t.palette.Length - 1)]);
        }

        public static Color GateColor(int colorId)
        {
            var hex = PuzzleGenerator.GateColorHex;
            return ColorUtil.Hex(hex[((colorId % hex.Length) + hex.Length) % hex.Length]);
        }

        /// <summary>Builds the board. <paramref name="mysteryAmount"/> (0..1) desaturates colours before the reveal.</summary>
        public void Build(PuzzleDefinition def, ObjectTemplate template, float mysteryAmount)
        {
            Clear();
            Definition = def;
            Template = template;

            Int3 min, max;
            def.GetBounds(out min, out max);
            GridCenter = new Vector3(min.x + max.x, min.y + max.y, min.z + max.z) * 0.5f;
            Radius = 0.5f * new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1).magnitude;

            foreach (var p in def.pieces)
            {
                var piece = _pool.Count > 0 ? _pool.Pop() : ArrowPiece.Create(Root);
                piece.transform.SetParent(Root, false);
                var body = ColorUtil.Mystery(PaletteColor(template, p.palette), mysteryAmount);
                piece.Setup(p, GridToLocal(p.cells[0]), body, ArrowColorFor(p));
                _pieces[p.id] = piece;
            }

            foreach (var g in def.gates) _decor.Add(CreateGate(g));

            foreach (var p in def.pieces)
                if (p.IsChain)
                    foreach (var target in p.chainTargets)
                        _chains.Add(new ChainLink { a = p.id, b = target, line = CreateChainLine(p.id, target) });
        }

        public static Color ArrowColorFor(PuzzlePiece p)
        {
            switch (p.Kind)
            {
                case ArrowKind.Locked: return ArrowPiece.ArrowLocked;
                case ArrowKind.Chain: return ArrowPiece.ArrowChain;
                case ArrowKind.Rotating: return ArrowPiece.ArrowRotating;
                case ArrowKind.Color: return GateColor(p.colorId);
                default: return ArrowPiece.ArrowNormal;
            }
        }

        public Vector3 GridToLocal(Int3 c)
        {
            return new Vector3(c.x, c.y, c.z) - GridCenter;
        }

        public ArrowPiece Get(int id)
        {
            ArrowPiece p;
            return _pieces.TryGetValue(id, out p) ? p : null;
        }

        /// <summary>Called by pieces when their exit animation finishes.</summary>
        public void Recycle(ArrowPiece piece)
        {
            if (piece == null) return;
            piece.gameObject.SetActive(false);
        }

        public void OnPieceRemoved(int id)
        {
            for (int i = _chains.Count - 1; i >= 0; i--)
            {
                if (_chains[i].a != id && _chains[i].b != id) continue;
                if (_chains[i].line != null) Destroy(_chains[i].line.gameObject);
                _chains.RemoveAt(i);
            }
        }

        void LateUpdate()
        {
            // Chain lines follow pieces during bump animations.
            foreach (var c in _chains)
            {
                var a = Get(c.a);
                var b = Get(c.b);
                if (a == null || b == null || c.line == null) continue;
                c.line.SetPosition(0, a.transform.position);
                c.line.SetPosition(1, b.transform.position);
            }
        }

        /// <summary>Removes everything; pieces go back to the pool.</summary>
        public void Clear()
        {
            foreach (var kv in _pieces)
            {
                if (kv.Value == null) continue;
                kv.Value.StopAllCoroutines();
                kv.Value.gameObject.SetActive(false);
                _pool.Push(kv.Value);
            }
            _pieces.Clear();
            foreach (var go in _decor) if (go != null) Destroy(go);
            _decor.Clear();
            foreach (var c in _chains) if (c.line != null) Destroy(c.line.gameObject);
            _chains.Clear();
            Root.localRotation = Quaternion.identity;
            Root.localScale = Vector3.one;
            Definition = null;
        }

        public void SetDecorVisible(bool visible)
        {
            foreach (var go in _decor) if (go != null) go.SetActive(visible);
            foreach (var c in _chains) if (c.line != null) c.line.enabled = visible;
        }

        GameObject CreateGate(ColorGate g)
        {
            var go = new GameObject("Gate " + g.colorId);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = GridToLocal(g.cell) - ArrowPiece.DirVector(g.facing) * 0.35f;
            go.transform.localRotation = Quaternion.LookRotation(ArrowPiece.DirVector(g.facing), Mathf.Abs(ArrowPiece.DirVector(g.facing).y) > 0.5f ? Vector3.forward : Vector3.up);
            var color = GateColor(g.colorId);

            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.GateFrameMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialLibrary.Lit(color, 0.6f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            fill.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad;
            var fr = fill.AddComponent<MeshRenderer>();
            fr.sharedMaterial = MaterialLibrary.Fx(TextureFactory.SoftSquare);
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color.WithAlpha(0.35f));
            block.SetColor("_BaseColor", color.WithAlpha(0.35f));
            fr.SetPropertyBlock(block);
            return go;
        }

        LineRenderer CreateChainLine(int a, int b)
        {
            var go = new GameObject("Chain " + a + "-" + b);
            go.transform.SetParent(Root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.09f;
            line.numCapVertices = 4;
            line.sharedMaterial = MaterialLibrary.Fx(TextureFactory.SoftSquare);
            line.startColor = line.endColor = ArrowPiece.ArrowChain;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
    }
}
