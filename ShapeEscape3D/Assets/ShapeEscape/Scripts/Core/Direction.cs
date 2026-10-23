using System;

namespace ShapeEscape.Core
{
    /// <summary>The six axis-aligned directions a piece can slide in.</summary>
    public enum Dir : byte
    {
        PosX = 0,
        NegX = 1,
        PosY = 2,
        NegY = 3,
        PosZ = 4,
        NegZ = 5
    }

    public static class DirUtil
    {
        public static readonly Dir[] All = { Dir.PosX, Dir.NegX, Dir.PosY, Dir.NegY, Dir.PosZ, Dir.NegZ };

        static readonly Int3[] Vectors =
        {
            new Int3(1, 0, 0), new Int3(-1, 0, 0),
            new Int3(0, 1, 0), new Int3(0, -1, 0),
            new Int3(0, 0, 1), new Int3(0, 0, -1)
        };

        // Rotation cycles (clockwise when looking down the positive axis) used by rotating arrows.
        static readonly Dir[][] CyclesAroundAxis =
        {
            new[] { Dir.PosY, Dir.PosZ, Dir.NegY, Dir.NegZ }, // around X
            new[] { Dir.PosX, Dir.PosZ, Dir.NegX, Dir.NegZ }, // around Y
            new[] { Dir.PosX, Dir.PosY, Dir.NegX, Dir.NegY }  // around Z
        };

        public static Int3 ToVector(this Dir d) { return Vectors[(int)d]; }

        public static int Axis(this Dir d) { return (int)d / 2; }

        public static int Sign(this Dir d) { return ((int)d & 1) == 0 ? 1 : -1; }

        public static Dir Opposite(this Dir d) { return (Dir)((int)d ^ 1); }

        public static Dir FromAxis(int axis, int sign) { return (Dir)(axis * 2 + (sign >= 0 ? 0 : 1)); }

        /// <summary>Returns the 4-direction rotation cycle around <paramref name="axis"/>. The axis must differ from the direction's own axis.</summary>
        public static Dir[] CycleAround(int axis)
        {
            if (axis < 0 || axis > 2) throw new ArgumentOutOfRangeException("axis");
            return (Dir[])CyclesAroundAxis[axis].Clone();
        }

        public static string ShortName(this Dir d)
        {
            switch (d)
            {
                case Dir.PosX: return "+X";
                case Dir.NegX: return "-X";
                case Dir.PosY: return "+Y";
                case Dir.NegY: return "-Y";
                case Dir.PosZ: return "+Z";
                default: return "-Z";
            }
        }
    }
}
