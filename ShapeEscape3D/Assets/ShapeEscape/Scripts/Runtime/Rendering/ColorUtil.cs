using UnityEngine;

namespace ShapeEscape
{
    public static class ColorUtil
    {
        public static Color Hex(string hex)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return Color.magenta;
        }

        public static Color Hex(string hex, float alpha)
        {
            var c = Hex(hex);
            c.a = alpha;
            return c;
        }

        /// <summary>Pulls a colour toward a soft pastel grey: used for the "mystery" look before the reveal.</summary>
        public static Color Mystery(Color c, float amount)
        {
            var grey = new Color(0.86f, 0.85f, 0.9f);
            var mixed = Color.Lerp(c, grey, amount);
            mixed.a = 1f;
            return mixed;
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static Color Brighten(Color c, float amount)
        {
            return Color.Lerp(c, Color.white, amount);
        }

        public static Color Darken(Color c, float amount)
        {
            return Color.Lerp(c, Color.black, amount);
        }

        public static string ToHex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }
    }
}
