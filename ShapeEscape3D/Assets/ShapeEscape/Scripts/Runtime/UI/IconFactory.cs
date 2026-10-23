using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShapeEscape
{
    public enum Icon
    {
        Coin, Star, Gear, Pause, Play, Home, Lock, Bulb, Recenter, Restart, Close, Calendar, Grid,
        Check, Video, Music, Speaker, Vibrate, Shield, Flame, Back, Globe, Gift, Gauge, Document, Heart, Map, Friends
    }

    /// <summary>
    /// Draws crisp, anti-aliased white UI icons from signed-distance functions at runtime (tint them with
    /// Image.color). No imported sprites are required; swap any icon by placing a sprite at
    /// Resources/ShapeEscape/Icons/{Icon}.
    /// </summary>
    public static class IconFactory
    {
        const int Size = 128;
        static readonly Dictionary<Icon, Sprite> Cache = new Dictionary<Icon, Sprite>();

        public static Sprite Get(Icon icon)
        {
            Sprite s;
            if (Cache.TryGetValue(icon, out s) && s != null) return s;
            s = Resources.Load<Sprite>("ShapeEscape/Icons/" + icon);
            if (s == null) s = Render(icon.ToString(), SdfFor(icon));
            Cache[icon] = s;
            return s;
        }

        static Sprite Render(string name, Func<float, float, float> sdf)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Icon" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[Size * Size];
            float pixel = 2f / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // Icon space: [-1,1], y up.
                    float fx = (x + 0.5f) / Size * 2f - 1f;
                    float fy = (y + 0.5f) / Size * 2f - 1f;
                    float d = sdf(fx, fy);
                    float a = Mathf.Clamp01(0.5f - d / pixel);
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        // ---------------------------------------------------------------- SDF primitives

        static float Circle(float x, float y, float cx, float cy, float r) { return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r; }

        static float Box(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x - cx) - hw + r, qy = Mathf.Abs(y - cy) - hh + r;
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float ex = px - dx * h, ey = py - dy * h;
            return Mathf.Sqrt(ex * ex + ey * ey) - r;
        }

        /// <summary>Exact signed distance to a simple polygon (Inigo Quilez).</summary>
        static float Polygon(float x, float y, Vector2[] v)
        {
            float d = (x - v[0].x) * (x - v[0].x) + (y - v[0].y) * (y - v[0].y);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                float wx = x - v[i].x, wy = y - v[i].y;
                float h = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * h, by = wy - ey * h;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = y >= v[i].y, c2 = y < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        static float Ring(float x, float y, float r, float w) { return Mathf.Abs(Mathf.Sqrt(x * x + y * y) - r) - w; }

        static float Union(float a, float b) { return Mathf.Min(a, b); }
        static float Sub(float a, float b) { return Mathf.Max(a, -b); }

        static Vector2[] StarPoints(float outer, float inner, float cy)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r + cy);
            }
            return pts;
        }

        static readonly Vector2[] StarPts = StarPoints(0.92f, 0.4f, -0.05f);
        static readonly Vector2[] PlayPts = { new Vector2(-0.45f, -0.62f), new Vector2(0.65f, 0f), new Vector2(-0.45f, 0.62f) };
        static readonly Vector2[] RoofPts = { new Vector2(-0.9f, 0.05f), new Vector2(0f, 0.85f), new Vector2(0.9f, 0.05f) };
        static readonly Vector2[] ShieldPts = { new Vector2(-0.7f, 0.62f), new Vector2(0f, 0.88f), new Vector2(0.7f, 0.62f), new Vector2(0.62f, -0.1f), new Vector2(0f, -0.88f), new Vector2(-0.62f, -0.1f) };
        static readonly Vector2[] HeartPts = { new Vector2(-0.86f, 0.12f), new Vector2(0f, -0.86f), new Vector2(0.86f, 0.12f), new Vector2(0f, 0.3f) };
        static readonly Vector2[] MapPts = { new Vector2(-0.85f, -0.65f), new Vector2(-0.85f, 0.72f), new Vector2(-0.3f, 0.9f), new Vector2(0.3f, 0.72f), new Vector2(0.85f, 0.9f), new Vector2(0.85f, -0.47f), new Vector2(0.3f, -0.65f), new Vector2(-0.3f, -0.47f) };
        static readonly Vector2[] FlamePts = { new Vector2(-0.5f, -0.25f), new Vector2(-0.05f, 0.92f), new Vector2(0.15f, 0.45f), new Vector2(0.5f, -0.25f) };

        static Func<float, float, float> SdfFor(Icon icon)
        {
            switch (icon)
            {
                case Icon.Coin:
                    return (x, y) => Sub(Circle(x, y, 0, 0, 0.92f), Ring(x, y, 0.62f, 0.07f));
                case Icon.Star:
                    return (x, y) => Polygon(x, y, StarPts) - 0.04f;
                case Icon.Gear:
                    return (x, y) =>
                    {
                        float d = Circle(x, y, 0, 0, 0.6f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            float rx = x * Mathf.Cos(a) + y * Mathf.Sin(a), ry = -x * Mathf.Sin(a) + y * Mathf.Cos(a);
                            d = Union(d, Box(rx, ry, 0.72f, 0, 0.2f, 0.15f, 0.05f));
                        }
                        return Sub(d, Circle(x, y, 0, 0, 0.27f));
                    };
                case Icon.Pause:
                    return (x, y) => Union(Box(x, y, -0.3f, 0, 0.16f, 0.62f, 0.08f), Box(x, y, 0.3f, 0, 0.16f, 0.62f, 0.08f));
                case Icon.Play:
                    return (x, y) => Polygon(x, y, PlayPts) - 0.06f;
                case Icon.Home:
                    return (x, y) => Sub(Union(Polygon(x, y, RoofPts) - 0.04f, Box(x, y, 0, -0.38f, 0.6f, 0.45f, 0.06f)), Box(x, y, 0, -0.55f, 0.16f, 0.3f, 0.05f));
                case Icon.Lock:
                    return (x, y) => Union(Sub(Box(x, y, 0, -0.3f, 0.62f, 0.5f, 0.12f), Circle(x, y, 0, -0.28f, 0.12f)),
                        Mathf.Max(Ring(x, y - 0.22f, 0.4f, 0.1f), -(y - 0.2f)));
                case Icon.Bulb:
                    return (x, y) => Union(Union(Circle(x, y, 0, 0.22f, 0.58f), Box(x, y, 0, -0.38f, 0.28f, 0.22f, 0.06f)), Box(x, y, 0, -0.72f, 0.2f, 0.08f, 0.06f));
                case Icon.Recenter:
                    return (x, y) =>
                    {
                        float d = Union(Ring(x, y, 0.58f, 0.1f), Circle(x, y, 0, 0, 0.2f));
                        d = Union(d, Box(x, y, 0, 0.8f, 0.08f, 0.15f, 0.04f));
                        d = Union(d, Box(x, y, 0, -0.8f, 0.08f, 0.15f, 0.04f));
                        d = Union(d, Box(x, y, 0.8f, 0, 0.15f, 0.08f, 0.04f));
                        return Union(d, Box(x, y, -0.8f, 0, 0.15f, 0.08f, 0.04f));
                    };
                case Icon.Restart:
                    return (x, y) =>
                    {
                        float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                        float arc = ang > 20f && ang < 80f ? 10f : Ring(x, y, 0.6f, 0.12f);
                        var head = new[] { new Vector2(0.25f, 0.48f), new Vector2(0.62f, 0.95f), new Vector2(0.78f, 0.35f) };
                        return Union(arc, Polygon(x, y, head) - 0.02f);
                    };
                case Icon.Close:
                    return (x, y) => Union(Segment(x, y, -0.55f, -0.55f, 0.55f, 0.55f, 0.13f), Segment(x, y, -0.55f, 0.55f, 0.55f, -0.55f, 0.13f));
                case Icon.Calendar:
                    return (x, y) =>
                    {
                        float body = Sub(Box(x, y, 0, -0.1f, 0.78f, 0.7f, 0.14f), Box(x, y, 0, -0.25f, 0.62f, 0.45f, 0.06f));
                        body = Union(body, Box(x, y, -0.3f, -0.15f, 0.12f, 0.1f, 0.03f));
                        body = Union(body, Box(x, y, 0.05f, -0.15f, 0.12f, 0.1f, 0.03f));
                        body = Union(body, Box(x, y, -0.3f, -0.45f, 0.12f, 0.1f, 0.03f));
                        return Union(Union(body, Box(x, y, -0.4f, 0.68f, 0.09f, 0.2f, 0.06f)), Box(x, y, 0.4f, 0.68f, 0.09f, 0.2f, 0.06f));
                    };
                case Icon.Grid:
                    return (x, y) => Union(Union(Box(x, y, -0.4f, 0.4f, 0.33f, 0.33f, 0.1f), Box(x, y, 0.4f, 0.4f, 0.33f, 0.33f, 0.1f)),
                        Union(Box(x, y, -0.4f, -0.4f, 0.33f, 0.33f, 0.1f), Box(x, y, 0.4f, -0.4f, 0.33f, 0.33f, 0.1f)));
                case Icon.Check:
                    return (x, y) => Union(Segment(x, y, -0.6f, 0.0f, -0.15f, -0.45f, 0.14f), Segment(x, y, -0.15f, -0.45f, 0.65f, 0.5f, 0.14f));
                case Icon.Video:
                    return (x, y) => Sub(Box(x, y, 0, 0, 0.85f, 0.6f, 0.18f), Polygon(x * 1.4f, y * 1.4f, PlayPts) / 1.4f);
                case Icon.Music:
                    return (x, y) => Union(Union(Circle(x, y, -0.3f, -0.5f, 0.27f), Box(x, y, -0.07f, 0.12f, 0.07f, 0.62f, 0.03f)),
                        Segment(x, y, -0.07f, 0.7f, 0.5f, 0.35f, 0.1f));
                case Icon.Speaker:
                    return (x, y) =>
                    {
                        var cone = new[] { new Vector2(-0.75f, -0.25f), new Vector2(-0.75f, 0.25f), new Vector2(-0.4f, 0.25f), new Vector2(0.05f, 0.65f), new Vector2(0.05f, -0.65f), new Vector2(-0.4f, -0.25f) };
                        float d = Polygon(x, y, cone) - 0.03f;
                        float wave = Mathf.Max(Ring(x - 0.05f, y, 0.42f, 0.07f), -(x - 0.25f));
                        float wave2 = Mathf.Max(Ring(x - 0.05f, y, 0.72f, 0.07f), -(x - 0.45f));
                        return Union(d, Union(wave, wave2));
                    };
                case Icon.Vibrate:
                    return (x, y) =>
                    {
                        float phone = Sub(Box(x, y, 0, 0, 0.36f, 0.72f, 0.12f), Box(x, y, 0, 0.02f, 0.24f, 0.52f, 0.05f));
                        float l = Union(Segment(x, y, -0.62f, -0.35f, -0.62f, 0.35f, 0.07f), Segment(x, y, -0.84f, -0.2f, -0.84f, 0.2f, 0.07f));
                        float r = Union(Segment(x, y, 0.62f, -0.35f, 0.62f, 0.35f, 0.07f), Segment(x, y, 0.84f, -0.2f, 0.84f, 0.2f, 0.07f));
                        return Union(phone, Union(l, r));
                    };
                case Icon.Shield:
                    return (x, y) => Sub(Polygon(x, y, ShieldPts) - 0.04f, Union(Segment(x, y, -0.28f, 0.0f, -0.05f, -0.25f, 0.08f), Segment(x, y, -0.05f, -0.25f, 0.32f, 0.25f, 0.08f)));
                case Icon.Flame:
                    return (x, y) => Union(Circle(x, y, 0, -0.35f, 0.52f), Polygon(x, y, FlamePts) - 0.02f);
                case Icon.Back:
                    return (x, y) => Union(Segment(x, y, -0.55f, 0, 0.6f, 0, 0.12f), Union(Segment(x, y, -0.55f, 0, -0.1f, 0.45f, 0.12f), Segment(x, y, -0.55f, 0, -0.1f, -0.45f, 0.12f)));
                case Icon.Globe:
                    return (x, y) =>
                    {
                        float d = Ring(x, y, 0.78f, 0.08f);
                        d = Union(d, Mathf.Abs(Mathf.Sqrt(x * x * 3.2f + y * y) - 0.78f) - 0.07f);
                        d = Union(d, Box(x, y, 0, 0, 0.78f, 0.06f, 0.02f));
                        return Union(d, Box(x, y, 0, 0, 0.06f, 0.78f, 0.02f));
                    };
                case Icon.Gift:
                    return (x, y) =>
                    {
                        float box = Box(x, y, 0, -0.25f, 0.7f, 0.5f, 0.08f);
                        float lid = Box(x, y, 0, 0.32f, 0.8f, 0.14f, 0.06f);
                        float d = Sub(Union(box, lid), Box(x, y, 0, 0, 0.1f, 1f, 0f));
                        float bow = Union(Circle(x, y, -0.25f, 0.62f, 0.18f), Circle(x, y, 0.25f, 0.62f, 0.18f));
                        return Union(d, bow);
                    };
                case Icon.Gauge:
                    return (x, y) =>
                    {
                        float arc = Mathf.Max(Ring(x, y + 0.2f, 0.72f, 0.1f), -(y + 0.2f));
                        return Union(arc, Segment(x, y, 0, -0.2f, 0.35f, 0.3f, 0.09f));
                    };
                case Icon.Heart:
                    return (x, y) => Union(Union(Circle(x, y, -0.4f, 0.28f, 0.44f), Circle(x, y, 0.4f, 0.28f, 0.44f)), Polygon(x, y, HeartPts));
                case Icon.Map:
                    return (x, y) =>
                    {
                        float d = Polygon(x, y, MapPts) - 0.03f;
                        d = Sub(d, Segment(x, y, -0.3f, 0.72f, -0.3f, -0.47f, 0.05f));
                        return Sub(d, Segment(x, y, 0.3f, 0.6f, 0.3f, -0.6f, 0.05f));
                    };
                case Icon.Friends:
                    return (x, y) =>
                    {
                        float a = Union(Circle(x, y, -0.3f, 0.35f, 0.26f), Box(x, y, -0.3f, -0.4f, 0.42f, 0.32f, 0.3f));
                        float b = Union(Circle(x, y, 0.38f, 0.42f, 0.22f), Box(x, y, 0.42f, -0.25f, 0.34f, 0.28f, 0.26f));
                        return Union(a, b);
                    };
                default: // Document
                    return (x, y) =>
                    {
                        float d = Sub(Box(x, y, 0, 0, 0.6f, 0.82f, 0.12f), Box(x, y, 0, 0, 0.46f, 0.68f, 0.05f));
                        for (int i = 0; i < 3; i++) d = Union(d, Box(x, y, 0, 0.35f - i * 0.3f, 0.3f, 0.05f, 0.03f));
                        return d;
                    };
            }
        }
    }
}
