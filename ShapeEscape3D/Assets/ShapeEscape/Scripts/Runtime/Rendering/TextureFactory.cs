using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Small procedural textures (no imported art needed).</summary>
    public static class TextureFactory
    {
        static Texture2D _softCircle;
        static Texture2D _softSquare;
        static Texture2D _sparkle;

        /// <summary>Radial falloff, used for blob shadows and glows.</summary>
        public static Texture2D SoftCircle
        {
            get
            {
                if (_softCircle == null)
                    _softCircle = Generate("SoftCircle", 64, (x, y) =>
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        float a = Mathf.Clamp01(1f - d);
                        return a * a * (3f - 2f * a);
                    });
                return _softCircle;
            }
        }

        /// <summary>Rounded square with soft edges (confetti, gate fill, chain lines).</summary>
        public static Texture2D SoftSquare
        {
            get
            {
                if (_softSquare == null)
                    _softSquare = Generate("SoftSquare", 32, (x, y) =>
                    {
                        float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                        return Mathf.Clamp01((1f - d) * 6f);
                    });
                return _softSquare;
            }
        }

        /// <summary>Four-point star used for sparkles.</summary>
        public static Texture2D Sparkle
        {
            get
            {
                if (_sparkle == null)
                    _sparkle = Generate("Sparkle", 64, (x, y) =>
                    {
                        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                        float star = Mathf.Clamp01(1f - (Mathf.Sqrt(ax) + Mathf.Sqrt(ay)) * 0.9f);
                        float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 2.2f);
                        return Mathf.Clamp01(star * 2f + core);
                    });
                return _sparkle;
            }
        }

        /// <summary>White texture with alpha from <paramref name="alpha"/>(x,y) where x,y are in [-1,1].</summary>
        public static Texture2D Generate(string name, int size, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) / size * 2f - 1f;
                    float fy = (y + 0.5f) / size * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(fx, fy)) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Vertical gradient (bottom → top) for backgrounds.</summary>
        public static Texture2D VerticalGradient(Color bottom, Color top, int height = 128)
        {
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Gradient" };
            for (int y = 0; y < height; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(height - 1)));
            tex.Apply(false, false);
            return tex;
        }
    }
}
