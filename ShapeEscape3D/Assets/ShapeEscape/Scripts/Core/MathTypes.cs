using System;

namespace ShapeEscape.Core
{
    /// <summary>Integer grid coordinate. Engine-independent so the puzzle core can be unit tested outside Unity.</summary>
    [Serializable]
    public struct Int3 : IEquatable<Int3>
    {
        public int x;
        public int y;
        public int z;

        public Int3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly Int3 Zero = new Int3(0, 0, 0);

        public static Int3 operator +(Int3 a, Int3 b) { return new Int3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Int3 operator -(Int3 a, Int3 b) { return new Int3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Int3 operator *(Int3 a, int s) { return new Int3(a.x * s, a.y * s, a.z * s); }
        public static bool operator ==(Int3 a, Int3 b) { return a.x == b.x && a.y == b.y && a.z == b.z; }
        public static bool operator !=(Int3 a, Int3 b) { return !(a == b); }

        public static Int3 Min(Int3 a, Int3 b) { return new Int3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z)); }
        public static Int3 Max(Int3 a, Int3 b) { return new Int3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z)); }

        public int ManhattanDistance(Int3 o) { return Math.Abs(x - o.x) + Math.Abs(y - o.y) + Math.Abs(z - o.z); }

        public int this[int axis]
        {
            get { return axis == 0 ? x : axis == 1 ? y : z; }
        }

        public bool Equals(Int3 other) { return this == other; }
        public override bool Equals(object obj) { return obj is Int3 && this == (Int3)obj; }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = x * 73856093;
                h ^= y * 19349663;
                h ^= z * 83492791;
                return h;
            }
        }

        public override string ToString() { return "(" + x + "," + y + "," + z + ")"; }
    }

    /// <summary>Float vector used by object templates (Unity-free).</summary>
    [Serializable]
    public struct Float3
    {
        public float x;
        public float y;
        public float z;

        public Float3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly Float3 Zero = new Float3(0, 0, 0);

        public static Float3 operator +(Float3 a, Float3 b) { return new Float3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Float3 operator -(Float3 a, Float3 b) { return new Float3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Float3 operator *(Float3 a, float s) { return new Float3(a.x * s, a.y * s, a.z * s); }
        public static Float3 operator /(Float3 a, float s) { return new Float3(a.x / s, a.y / s, a.z / s); }

        public static float Dot(Float3 a, Float3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Float3 Min(Float3 a, Float3 b) { return new Float3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z)); }
        public static Float3 Max(Float3 a, Float3 b) { return new Float3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z)); }

        public float Length { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public float SqrLength { get { return x * x + y * y + z * z; } }

        public float this[int axis]
        {
            get { return axis == 0 ? x : axis == 1 ? y : z; }
        }

        public override string ToString() { return "(" + x.ToString("0.###") + "," + y.ToString("0.###") + "," + z.ToString("0.###") + ")"; }
    }
}
