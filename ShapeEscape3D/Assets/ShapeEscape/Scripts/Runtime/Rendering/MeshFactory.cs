using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Accumulates vertices/triangles. Triangles are auto-oriented against an expected normal, so callers never fight winding order.</summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<int> Triangles = new List<int>();

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            Vertices.Add(p);
            Normals.Add(n);
            Uvs.Add(uv);
            return Vertices.Count - 1;
        }

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv, Color c)
        {
            Colors.Add(c);
            return AddVertex(p, n, uv);
        }

        /// <summary>Unity treats a triangle as front-facing when cross(p1-p0, p2-p0) points toward the viewer.</summary>
        public void AddTriangle(int a, int b, int c, Vector3 expectedNormal)
        {
            var cross = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            if (Vector3.Dot(cross, expectedNormal) >= 0)
            {
                Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
            }
            else
            {
                Triangles.Add(a); Triangles.Add(c); Triangles.Add(b);
            }
        }

        public void AddTriangle(int a, int b, int c)
        {
            AddTriangle(a, b, c, (Normals[a] + Normals[b] + Normals[c]));
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        public void Append(MeshBuilder other, Matrix4x4 m)
        {
            int offset = Vertices.Count;
            for (int i = 0; i < other.Vertices.Count; i++)
            {
                Vertices.Add(m.MultiplyPoint3x4(other.Vertices[i]));
                Normals.Add(m.MultiplyVector(other.Normals[i]).normalized);
                Uvs.Add(other.Uvs[i]);
            }
            for (int i = 0; i < other.Triangles.Count; i++) Triangles.Add(other.Triangles[i] + offset);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            if (Colors.Count == Vertices.Count) mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }

    /// <summary>
    /// Procedural meshes: rounded puzzle cubes, arrow decals, and the analytic primitives used by the smooth
    /// reveal models. Everything is generated at runtime, so the project needs no imported model files.
    /// </summary>
    public static class MeshFactory
    {
        public const float PieceHalf = 0.45f;
        const float PieceRadius = 0.12f;
        const float DecalLift = 0.006f;

        static Mesh _pieceMesh;
        static readonly Mesh[] ArrowMeshes = new Mesh[6];
        static Mesh _hiddenMesh;
        static Mesh _cageMesh;
        static Mesh _gateFrameMesh;
        static Mesh _quadMesh;

        static Vector3 V(Int3 v) { return new Vector3(v.x, v.y, v.z); }
        static Vector3 V(Float3 v) { return new Vector3(v.x, v.y, v.z); }

        public static Mesh PieceMesh
        {
            get
            {
                if (_pieceMesh == null) _pieceMesh = RoundedBox(new Vector3(PieceHalf, PieceHalf, PieceHalf), PieceRadius, 3).ToMesh("PieceRoundedCube");
                return _pieceMesh;
            }
        }

        public static Mesh ArrowMesh(Dir d)
        {
            int i = (int)d;
            if (ArrowMeshes[i] == null) ArrowMeshes[i] = BuildArrowDecal(d).ToMesh("Arrow" + d);
            return ArrowMeshes[i];
        }

        public static Mesh HiddenMarkerMesh
        {
            get
            {
                if (_hiddenMesh == null) _hiddenMesh = BuildHiddenMarker().ToMesh("HiddenMarker");
                return _hiddenMesh;
            }
        }

        public static Mesh CageMesh
        {
            get
            {
                if (_cageMesh == null) _cageMesh = BuildCage(PieceHalf + 0.035f, 0.045f).ToMesh("LockCage");
                return _cageMesh;
            }
        }

        public static Mesh GateFrameMesh
        {
            get
            {
                if (_gateFrameMesh == null) _gateFrameMesh = BuildGateFrame().ToMesh("GateFrame");
                return _gateFrameMesh;
            }
        }

        /// <summary>Unit quad in the XY plane facing -Z (towards a default camera).</summary>
        public static Mesh Quad
        {
            get
            {
                if (_quadMesh == null)
                {
                    var b = new MeshBuilder();
                    var n = Vector3.back;
                    int a = b.AddVertex(new Vector3(-0.5f, -0.5f, 0), n, new Vector2(0, 0));
                    int c = b.AddVertex(new Vector3(0.5f, -0.5f, 0), n, new Vector2(1, 0));
                    int d = b.AddVertex(new Vector3(0.5f, 0.5f, 0), n, new Vector2(1, 1));
                    int e = b.AddVertex(new Vector3(-0.5f, 0.5f, 0), n, new Vector2(0, 1));
                    b.AddTriangle(a, c, d, n);
                    b.AddTriangle(a, d, e, n);
                    _quadMesh = b.ToMesh("Quad");
                }
                return _quadMesh;
            }
        }

        // ------------------------------------------------------------------ rounded box

        static float[] RoundedAxis(float half, float radius, int seg)
        {
            float inner = half - radius;
            var list = new List<float>();
            for (int k = 0; k <= seg; k++) list.Add(-inner - radius * Mathf.Cos(k * Mathf.PI * 0.5f / seg));
            for (int k = 0; k <= seg; k++)
            {
                float x = inner + radius * Mathf.Sin(k * Mathf.PI * 0.5f / seg);
                if (k == 0 && inner <= 0.0001f) continue;
                list.Add(x);
            }
            return list.ToArray();
        }

        public static MeshBuilder RoundedBox(Vector3 half, float radius, int seg)
        {
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
            var b = new MeshBuilder();
            var inner = half - Vector3.one * radius;
            var axes = new[]
            {
                new[] { Vector3.right, Vector3.up, Vector3.forward },
                new[] { Vector3.left, Vector3.up, Vector3.forward },
                new[] { Vector3.up, Vector3.right, Vector3.forward },
                new[] { Vector3.down, Vector3.right, Vector3.forward },
                new[] { Vector3.forward, Vector3.right, Vector3.up },
                new[] { Vector3.back, Vector3.right, Vector3.up }
            };
            foreach (var f in axes)
            {
                Vector3 n = f[0], u = f[1], v = f[2];
                float hn = Mathf.Abs(Vector3.Dot(half, n));
                float hu = Mathf.Abs(Vector3.Dot(half, u));
                float hv = Mathf.Abs(Vector3.Dot(half, v));
                var cu = RoundedAxis(hu, radius, seg);
                var cv = RoundedAxis(hv, radius, seg);
                int start = b.Vertices.Count;
                for (int i = 0; i < cu.Length; i++)
                    for (int j = 0; j < cv.Length; j++)
                    {
                        Vector3 p = n * hn + u * cu[i] + v * cv[j];
                        var clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 nn = p - clamped;
                        nn = nn.sqrMagnitude > 1e-8f ? nn.normalized : n;
                        b.AddVertex(clamped + nn * radius, nn, new Vector2(i / (float)(cu.Length - 1), j / (float)(cv.Length - 1)));
                    }
                for (int i = 0; i < cu.Length - 1; i++)
                    for (int j = 0; j < cv.Length - 1; j++)
                    {
                        int a = start + i * cv.Length + j;
                        int c = a + cv.Length;
                        b.AddTriangle(a, c, c + 1, n);
                        b.AddTriangle(a, c + 1, a + 1, n);
                    }
            }
            return b;
        }

        // ------------------------------------------------------------------ decals

        static void AddPolygon(MeshBuilder b, Vector3 origin, Vector3 n, Vector3 u, Vector3 v, Vector2[] pts)
        {
            int start = b.Vertices.Count;
            foreach (var p in pts) b.AddVertex(origin + u * p.x + v * p.y, n, new Vector2(p.x + 0.5f, p.y + 0.5f));
            for (int i = 1; i < pts.Length - 1; i++) b.AddTriangle(start, start + i, start + i + 1, n);
        }

        static Vector2[] Circle(float r, int segs)
        {
            var pts = new Vector2[segs];
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return pts;
        }

        /// <summary>Arrow on each of the four faces parallel to the travel direction plus a target dot on the exit face.</summary>
        static MeshBuilder BuildArrowDecal(Dir d)
        {
            var b = new MeshBuilder();
            Vector3 t = V(d.ToVector());
            float off = PieceHalf + DecalLift;
            var shaft = new[] { new Vector2(-0.27f, -0.075f), new Vector2(0.03f, -0.075f), new Vector2(0.03f, 0.075f), new Vector2(-0.27f, 0.075f) };
            var head = new[] { new Vector2(0.0f, -0.21f), new Vector2(0.29f, 0f), new Vector2(0.0f, 0.21f) };
            foreach (var faceDir in DirUtil.All)
            {
                Vector3 n = V(faceDir.ToVector());
                if (faceDir.Axis() == d.Axis())
                {
                    if (faceDir == d) AddPolygon(b, n * off, n, Vector3.Cross(n, Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up).normalized, Vector3.Cross(n, Vector3.Cross(n, Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up)).normalized, Circle(0.13f, 20));
                    continue;
                }
                Vector3 side = Vector3.Cross(n, t);
                AddPolygon(b, n * off, n, t, side, shaft);
                AddPolygon(b, n * off, n, t, side, head);
            }
            return b;
        }

        static MeshBuilder BuildHiddenMarker()
        {
            var b = new MeshBuilder();
            float off = PieceHalf + DecalLift;
            var diamond = new[] { new Vector2(0, -0.11f), new Vector2(0.11f, 0), new Vector2(0, 0.11f), new Vector2(-0.11f, 0) };
            foreach (var faceDir in DirUtil.All)
            {
                Vector3 n = V(faceDir.ToVector());
                Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up).normalized;
                Vector3 v = Vector3.Cross(n, u);
                AddPolygon(b, n * off, n, u, v, diamond);
            }
            return b;
        }

        static void AddBar(MeshBuilder b, Vector3 center, Vector3 half)
        {
            var box = RoundedBox(half, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.9f, 1);
            b.Append(box, Matrix4x4.Translate(center));
        }

        /// <summary>Twelve thin bars on the cube edges: a "cage" marking locked pieces without hiding their arrows.</summary>
        static MeshBuilder BuildCage(float h, float t)
        {
            var b = new MeshBuilder();
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    AddBar(b, new Vector3(sx * h, sy * h, 0), new Vector3(t, t, h + t));
                    AddBar(b, new Vector3(sx * h, 0, sy * h), new Vector3(t, h + t, t));
                    AddBar(b, new Vector3(0, sx * h, sy * h), new Vector3(h + t, t, t));
                }
            return b;
        }

        /// <summary>Square frame in the XY plane (rotate so +Z matches the gate's facing).</summary>
        static MeshBuilder BuildGateFrame()
        {
            var b = new MeshBuilder();
            const float h = 0.5f, t = 0.07f, depth = 0.06f;
            AddBar(b, new Vector3(0, h, 0), new Vector3(h + t, t, depth));
            AddBar(b, new Vector3(0, -h, 0), new Vector3(h + t, t, depth));
            AddBar(b, new Vector3(h, 0, 0), new Vector3(t, h + t, depth));
            AddBar(b, new Vector3(-h, 0, 0), new Vector3(t, h + t, depth));
            return b;
        }

        // ------------------------------------------------------------------ analytic primitives (reveal models)

        static void BasisVectors(int axis, out Vector3 a, out Vector3 n, out Vector3 b)
        {
            Float3 fa, fn, fb;
            ShapePrimitive.Basis(axis, out fa, out fn, out fb);
            a = V(fa);
            n = V(fn);
            b = V(fb);
        }

        public static MeshBuilder Frustum(Vector3 center, int axis, float rNeg, float rPos, float halfHeight, int segs = 28)
        {
            Vector3 A, N, B;
            BasisVectors(axis, out A, out N, out B);
            var mb = new MeshBuilder();
            float slope = (rPos - rNeg) / Mathf.Max(0.0001f, 2f * halfHeight);
            int ringStart = 0;
            for (int i = 0; i <= segs; i++)
            {
                float ang = i * Mathf.PI * 2f / segs;
                Vector3 radial = A * Mathf.Cos(ang) + B * Mathf.Sin(ang);
                Vector3 nrm = (radial - N * slope).normalized;
                mb.AddVertex(center + radial * rNeg - N * halfHeight, nrm, new Vector2(i / (float)segs, 0));
                mb.AddVertex(center + radial * rPos + N * halfHeight, nrm, new Vector2(i / (float)segs, 1));
            }
            for (int i = 0; i < segs; i++)
            {
                int a = ringStart + i * 2;
                mb.AddQuad(a, a + 2, a + 3, a + 1);
            }
            Cap(mb, center - N * halfHeight, -N, A, B, rNeg, segs);
            Cap(mb, center + N * halfHeight, N, A, B, rPos, segs);
            return mb;
        }

        /// <summary>
        /// Open tube (cup / lamp shade). The opening is on the +axis side unless <paramref name="openNegative"/>;
        /// <paramref name="depth"/> &lt; 1 adds an inner floor, otherwise the tube is open at both ends.
        /// </summary>
        public static MeshBuilder Tube(Vector3 center, int axis, float rNeg, float rPos, float halfHeight, float innerScale, float depth, bool openNegative, int segs = 32)
        {
            Vector3 A, N, B;
            BasisVectors(axis, out A, out N, out B);
            if (openNegative)
            {
                N = -N;
                float tmp = rNeg; rNeg = rPos; rPos = tmp;
            }
            var mb = new MeshBuilder();
            depth = Mathf.Clamp01(depth);
            float floorH = halfHeight - depth * 2f * halfHeight;
            float slope = (rPos - rNeg) / Mathf.Max(0.0001f, 2f * halfHeight);
            System.Func<float, float> outerR = h => rNeg + (rPos - rNeg) * (h + halfHeight) / (2f * halfHeight);

            for (int pass = 0; pass < 2; pass++)
            {
                bool inner = pass == 1;
                float h0 = inner ? floorH : -halfHeight;
                float r0 = outerR(h0) * (inner ? innerScale : 1f);
                float r1 = rPos * (inner ? innerScale : 1f);
                int start = mb.Vertices.Count;
                for (int i = 0; i <= segs; i++)
                {
                    float ang = i * Mathf.PI * 2f / segs;
                    Vector3 radial = A * Mathf.Cos(ang) + B * Mathf.Sin(ang);
                    Vector3 nrm = (radial - N * slope).normalized * (inner ? -1f : 1f);
                    mb.AddVertex(center + radial * r0 + N * h0, nrm, new Vector2(i / (float)segs, 0));
                    mb.AddVertex(center + radial * r1 + N * halfHeight, nrm, new Vector2(i / (float)segs, 1));
                }
                for (int i = 0; i < segs; i++)
                {
                    int a = start + i * 2;
                    mb.AddQuad(a, a + 2, a + 3, a + 1);
                }
            }
            // Rim between inner and outer walls.
            Annulus(mb, center + N * halfHeight, N, A, B, rPos * innerScale, rPos, segs);
            if (depth < 0.999f)
            {
                Cap(mb, center - N * halfHeight, -N, A, B, rNeg, segs);
                Cap(mb, center + N * floorH, N, A, B, outerR(floorH) * innerScale, segs);
            }
            else
            {
                Annulus(mb, center - N * halfHeight, -N, A, B, rNeg * innerScale, rNeg, segs);
            }
            return mb;
        }

        static void Annulus(MeshBuilder mb, Vector3 c, Vector3 n, Vector3 A, Vector3 B, float rIn, float rOut, int segs)
        {
            int start = mb.Vertices.Count;
            for (int i = 0; i <= segs; i++)
            {
                float ang = i * Mathf.PI * 2f / segs;
                Vector3 radial = A * Mathf.Cos(ang) + B * Mathf.Sin(ang);
                mb.AddVertex(c + radial * rIn, n, new Vector2(i / (float)segs, 0));
                mb.AddVertex(c + radial * rOut, n, new Vector2(i / (float)segs, 1));
            }
            for (int i = 0; i < segs; i++)
            {
                int a = start + i * 2;
                mb.AddTriangle(a, a + 2, a + 3, n);
                mb.AddTriangle(a, a + 3, a + 1, n);
            }
        }

        static void Cap(MeshBuilder mb, Vector3 c, Vector3 n, Vector3 A, Vector3 B, float r, int segs)
        {
            if (r <= 0.0001f) return;
            int center = mb.AddVertex(c, n, new Vector2(0.5f, 0.5f));
            int first = mb.Vertices.Count;
            for (int i = 0; i <= segs; i++)
            {
                float ang = i * Mathf.PI * 2f / segs;
                mb.AddVertex(c + (A * Mathf.Cos(ang) + B * Mathf.Sin(ang)) * r, n, new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            for (int i = 0; i < segs; i++) mb.AddTriangle(center, first + i, first + i + 1, n);
        }

        public static MeshBuilder Torus(Vector3 center, int axis, float major, float minor, float arcStartDeg, float arcEndDeg, int majorSegs = 36, int minorSegs = 12)
        {
            Vector3 A, N, B;
            BasisVectors(axis, out A, out N, out B);
            var mb = new MeshBuilder();
            bool full = arcEndDeg - arcStartDeg >= 360f;
            float a0 = arcStartDeg * Mathf.Deg2Rad;
            float span = (full ? 360f : arcEndDeg - arcStartDeg) * Mathf.Deg2Rad;
            int segs = Mathf.Max(6, Mathf.CeilToInt(majorSegs * span / (Mathf.PI * 2f)));
            for (int i = 0; i <= segs; i++)
            {
                float th = a0 + span * i / segs;
                Vector3 e = A * Mathf.Cos(th) + B * Mathf.Sin(th);
                Vector3 c = center + e * major;
                for (int j = 0; j <= minorSegs; j++)
                {
                    float ph = j * Mathf.PI * 2f / minorSegs;
                    Vector3 nrm = e * Mathf.Cos(ph) + N * Mathf.Sin(ph);
                    mb.AddVertex(c + nrm * minor, nrm, new Vector2(i / (float)segs, j / (float)minorSegs));
                }
            }
            int row = minorSegs + 1;
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < minorSegs; j++)
                {
                    int a = i * row + j;
                    mb.AddQuad(a, a + row, a + row + 1, a + 1);
                }
            if (!full)
            {
                // Rounded end caps.
                for (int k = 0; k < 2; k++)
                {
                    float th = a0 + span * k;
                    Vector3 e = A * Mathf.Cos(th) + B * Mathf.Sin(th);
                    var cap = Ellipsoid(center + e * major, Vector3.one * minor, 8, 12);
                    mb.Append(cap, Matrix4x4.identity);
                }
            }
            return mb;
        }

        public static MeshBuilder Ellipsoid(Vector3 center, Vector3 radii, int lat = 14, int lon = 24)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i <= lat; i++)
            {
                float th = i * Mathf.PI / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float ph = j * Mathf.PI * 2f / lon;
                    var unit = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    var p = Vector3.Scale(unit, radii);
                    var n = new Vector3(unit.x / Mathf.Max(radii.x, 1e-4f), unit.y / Mathf.Max(radii.y, 1e-4f), unit.z / Mathf.Max(radii.z, 1e-4f)).normalized;
                    mb.AddVertex(center + p, n, new Vector2(j / (float)lon, i / (float)lat));
                }
            }
            int row = lon + 1;
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = i * row + j;
                    if (i != 0) mb.AddTriangle(a, a + row, a + 1);
                    if (i != lat - 1) mb.AddTriangle(a + 1, a + row, a + row + 1);
                }
            return mb;
        }

        public static MeshBuilder Capsule(Vector3 a, Vector3 b, float r, int segs = 20)
        {
            var mb = new MeshBuilder();
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len > 1e-4f)
            {
                Vector3 n = axis / len;
                Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 v = Vector3.Cross(n, u);
                for (int i = 0; i <= segs; i++)
                {
                    float ang = i * Mathf.PI * 2f / segs;
                    Vector3 radial = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
                    mb.AddVertex(a + radial * r, radial, new Vector2(i / (float)segs, 0));
                    mb.AddVertex(b + radial * r, radial, new Vector2(i / (float)segs, 1));
                }
                for (int i = 0; i < segs; i++)
                {
                    int k = i * 2;
                    mb.AddQuad(k, k + 2, k + 3, k + 1);
                }
            }
            mb.Append(Ellipsoid(a, Vector3.one * r, 10, segs), Matrix4x4.identity);
            mb.Append(Ellipsoid(b, Vector3.one * r, 10, segs), Matrix4x4.identity);
            return mb;
        }

        /// <summary>Smooth mesh for a template primitive (template space).</summary>
        public static MeshBuilder ForPrimitive(ShapePrimitive p)
        {
            Vector3 c = V(p.center);
            switch (p.shape)
            {
                case PrimitiveShape.Box:
                {
                    var half = V(p.halfSize);
                    var box = RoundedBox(half, Mathf.Min(0.08f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.6f), 3);
                    var mb = new MeshBuilder();
                    mb.Append(box, Matrix4x4.Translate(c));
                    return mb;
                }
                case PrimitiveShape.Ellipsoid:
                    return Ellipsoid(c, V(p.halfSize));
                case PrimitiveShape.Frustum:
                    if (p.hollowInner > 0.01f)
                        return Tube(c, p.axis, p.radiusA, p.radiusB, p.halfHeight, p.hollowInner, p.hollowDepth, p.hollowFromNegative);
                    return Frustum(c, p.axis, p.radiusA, p.radiusB, p.halfHeight);
                case PrimitiveShape.Torus:
                    return Torus(c, p.axis, p.radiusA, p.radiusB, p.arcStart, p.arcEnd);
                default:
                    return Capsule(c, V(p.pointB), p.radiusA);
            }
        }
    }
}
