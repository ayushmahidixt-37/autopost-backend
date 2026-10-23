using System;
using System.Collections.Generic;

namespace ShapeEscape.Core
{
    public enum PrimitiveShape
    {
        Box,
        Ellipsoid,
        /// <summary>Cylinder / cone section along an axis (radiusA at the negative end, radiusB at the positive end).</summary>
        Frustum,
        /// <summary>Ring around an axis (radiusA = major radius, radiusB = tube radius), optionally limited to an arc.</summary>
        Torus,
        /// <summary>Rounded segment from center to pointB with radius radiusA.</summary>
        Capsule
    }

    public enum PrimitiveRole
    {
        /// <summary>Part of the puzzle silhouette and of the reveal model.</summary>
        Solid,
        /// <summary>Carved out of the silhouette (e.g. the inside of a mug). Not drawn in the reveal model.</summary>
        Carve,
        /// <summary>Only drawn in the smooth reveal model (small details such as coffee, lens glass, clock hands).</summary>
        RevealOnly
    }

    public enum ObjectCategory
    {
        Kitchen,
        LivingRoom,
        Bedroom,
        Office,
        Garage,
        Electronics,
        Toys,
        Outdoor
    }

    /// <summary>
    /// Analytic shape primitive. The same data drives both the voxel silhouette (puzzle pieces) and the smooth
    /// reveal model, so artists can later swap the reveal model for a polished mesh without touching the puzzle engine.
    /// </summary>
    [Serializable]
    public class ShapePrimitive
    {
        public PrimitiveShape shape;
        public PrimitiveRole role;
        public int palette;
        public Float3 center;
        public Float3 halfSize;
        public int axis = 1;
        public float radiusA;
        public float radiusB;
        public float halfHeight;
        public float arcStart;
        public float arcEnd = 360f;
        public Float3 pointB;
        /// <summary>Reveal-model only: frustums with hollowInner &gt; 0 render as open tubes (mugs, lamp shades).</summary>
        public float hollowInner;
        /// <summary>Fraction of the height that is hollow (1 = open at both ends).</summary>
        public float hollowDepth = 1f;
        /// <summary>Hollow opens on the negative axis side instead of the positive one.</summary>
        public bool hollowFromNegative;

        /// <summary>Orthonormal frame for an axis: point = A*a + N*h + B*b. Shared with the mesh builder so both agree exactly.</summary>
        public static void Basis(int axis, out Float3 a, out Float3 n, out Float3 b)
        {
            switch (axis)
            {
                case 0:
                    a = new Float3(0, 1, 0); n = new Float3(1, 0, 0); b = new Float3(0, 0, 1);
                    break;
                case 2:
                    a = new Float3(1, 0, 0); n = new Float3(0, 0, 1); b = new Float3(0, 1, 0);
                    break;
                default:
                    a = new Float3(1, 0, 0); n = new Float3(0, 1, 0); b = new Float3(0, 0, 1);
                    break;
            }
        }

        public bool Contains(Float3 p)
        {
            Float3 d = p - center;
            switch (shape)
            {
                case PrimitiveShape.Box:
                    return Math.Abs(d.x) <= halfSize.x && Math.Abs(d.y) <= halfSize.y && Math.Abs(d.z) <= halfSize.z;
                case PrimitiveShape.Ellipsoid:
                {
                    float ex = d.x / halfSize.x, ey = d.y / halfSize.y, ez = d.z / halfSize.z;
                    return ex * ex + ey * ey + ez * ez <= 1f;
                }
                case PrimitiveShape.Frustum:
                {
                    Float3 A, N, B;
                    Basis(axis, out A, out N, out B);
                    float h = Float3.Dot(d, N);
                    if (h < -halfHeight || h > halfHeight) return false;
                    float t = halfHeight > 0 ? (h + halfHeight) / (2 * halfHeight) : 0.5f;
                    float r = radiusA + (radiusB - radiusA) * t;
                    float a = Float3.Dot(d, A), b = Float3.Dot(d, B);
                    return a * a + b * b <= r * r;
                }
                case PrimitiveShape.Torus:
                {
                    Float3 A, N, B;
                    Basis(axis, out A, out N, out B);
                    float a = Float3.Dot(d, A), b = Float3.Dot(d, B), h = Float3.Dot(d, N);
                    float q = (float)Math.Sqrt(a * a + b * b);
                    float dr = q - radiusA;
                    if (dr * dr + h * h > radiusB * radiusB) return false;
                    if (arcEnd - arcStart >= 360f) return true;
                    float ang = (float)(Math.Atan2(b, a) * 180.0 / Math.PI);
                    if (ang < 0) ang += 360f;
                    float s = arcStart % 360f; if (s < 0) s += 360f;
                    float e = s + (arcEnd - arcStart);
                    return (ang >= s && ang <= e) || (ang + 360f >= s && ang + 360f <= e);
                }
                case PrimitiveShape.Capsule:
                {
                    Float3 ab = pointB - center;
                    float len2 = ab.SqrLength;
                    float t = len2 > 0 ? Float3.Dot(d, ab) / len2 : 0;
                    if (t < 0) t = 0;
                    else if (t > 1) t = 1;
                    Float3 closest = center + ab * t;
                    return (p - closest).SqrLength <= radiusA * radiusA;
                }
            }
            return false;
        }

        public void GetBounds(out Float3 min, out Float3 max)
        {
            switch (shape)
            {
                case PrimitiveShape.Box:
                case PrimitiveShape.Ellipsoid:
                    min = center - halfSize;
                    max = center + halfSize;
                    return;
                case PrimitiveShape.Frustum:
                {
                    float r = Math.Max(radiusA, radiusB);
                    Float3 A, N, B;
                    Basis(axis, out A, out N, out B);
                    Float3 ext = Abs(A) * r + Abs(N) * halfHeight + Abs(B) * r;
                    min = center - ext;
                    max = center + ext;
                    return;
                }
                case PrimitiveShape.Torus:
                {
                    float r = radiusA + radiusB;
                    Float3 A, N, B;
                    Basis(axis, out A, out N, out B);
                    Float3 ext = Abs(A) * r + Abs(N) * radiusB + Abs(B) * r;
                    min = center - ext;
                    max = center + ext;
                    return;
                }
                default:
                {
                    var r = new Float3(radiusA, radiusA, radiusA);
                    min = Float3.Min(center, pointB) - r;
                    max = Float3.Max(center, pointB) + r;
                    return;
                }
            }
        }

        [NonSerialized] bool _boundsCached;
        [NonSerialized] Float3 _bmin;
        [NonSerialized] Float3 _bmax;

        /// <summary>Cheap AABB rejection used by the voxelizer before the exact test.</summary>
        public bool BoundsContain(Float3 p)
        {
            if (!_boundsCached)
            {
                Float3 a, b;
                GetBounds(out a, out b);
                _bmin = a;
                _bmax = b;
                _boundsCached = true;
            }
            return p.x >= _bmin.x && p.y >= _bmin.y && p.z >= _bmin.z && p.x <= _bmax.x && p.y <= _bmax.y && p.z <= _bmax.z;
        }

        static Float3 Abs(Float3 v) { return new Float3(Math.Abs(v.x), Math.Abs(v.y), Math.Abs(v.z)); }

        // ---------- Fluent factory helpers ----------
        public static ShapePrimitive Box(Float3 c, Float3 half, int pal)
        {
            return new ShapePrimitive { shape = PrimitiveShape.Box, center = c, halfSize = half, palette = pal };
        }

        public static ShapePrimitive Ellipsoid(Float3 c, Float3 radii, int pal)
        {
            return new ShapePrimitive { shape = PrimitiveShape.Ellipsoid, center = c, halfSize = radii, palette = pal };
        }

        public static ShapePrimitive Sphere(Float3 c, float r, int pal) { return Ellipsoid(c, new Float3(r, r, r), pal); }

        public static ShapePrimitive Cylinder(Float3 c, int axis, float radius, float halfHeight, int pal)
        {
            return Frustum(c, axis, radius, radius, halfHeight, pal);
        }

        public static ShapePrimitive Frustum(Float3 c, int axis, float rNeg, float rPos, float halfHeight, int pal)
        {
            return new ShapePrimitive { shape = PrimitiveShape.Frustum, center = c, axis = axis, radiusA = rNeg, radiusB = rPos, halfHeight = halfHeight, palette = pal };
        }

        public static ShapePrimitive Torus(Float3 c, int axis, float major, float minor, int pal, float arcStart = 0, float arcEnd = 360)
        {
            return new ShapePrimitive { shape = PrimitiveShape.Torus, center = c, axis = axis, radiusA = major, radiusB = minor, palette = pal, arcStart = arcStart, arcEnd = arcEnd };
        }

        public static ShapePrimitive Capsule(Float3 a, Float3 b, float r, int pal)
        {
            return new ShapePrimitive { shape = PrimitiveShape.Capsule, center = a, pointB = b, radiusA = r, palette = pal };
        }

        public ShapePrimitive Carve() { role = PrimitiveRole.Carve; return this; }

        /// <summary>Marks a frustum as an open tube in the reveal model (pair it with a Carve primitive for the voxels).</summary>
        public ShapePrimitive Hollow(float innerScale, float depth = 1f, bool fromNegative = false)
        {
            hollowInner = innerScale;
            hollowDepth = depth;
            hollowFromNegative = fromNegative;
            return this;
        }
        public ShapePrimitive RevealOnly() { role = PrimitiveRole.RevealOnly; return this; }
    }

    /// <summary>An everyday object: name, collection category, colour palette and analytic shape.</summary>
    [Serializable]
    public class ObjectTemplate
    {
        public string id;
        public string displayName;
        public ObjectCategory category;
        /// <summary>Daily-challenge exclusive collectibles never appear in the main level list.</summary>
        public bool dailyExclusive;
        /// <summary>Hex colours (#RRGGBB) indexed by ShapePrimitive.palette.</summary>
        public string[] palette = new string[0];
        public List<ShapePrimitive> primitives = new List<ShapePrimitive>();

        public void GetSolidBounds(out Float3 min, out Float3 max)
        {
            min = new Float3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Float3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in primitives)
            {
                if (p.role != PrimitiveRole.Solid) continue;
                Float3 a, b;
                p.GetBounds(out a, out b);
                min = Float3.Min(min, a);
                max = Float3.Max(max, b);
            }
        }

        /// <summary>Returns the palette index at a point, or -1 if the point is outside the silhouette.</summary>
        public int Sample(Float3 p)
        {
            int pal = -1;
            for (int i = 0; i < primitives.Count; i++)
            {
                var prim = primitives[i];
                if (prim.role == PrimitiveRole.RevealOnly) continue;
                if (!prim.BoundsContain(p) || !prim.Contains(p)) continue;
                if (prim.role == PrimitiveRole.Carve) return -1;
                if (pal < 0) pal = prim.palette;
            }
            return pal;
        }
    }
}
