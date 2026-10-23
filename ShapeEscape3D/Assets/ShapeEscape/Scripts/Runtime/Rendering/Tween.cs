using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Easing curves shared by gameplay and UI animations.</summary>
    public static class Tween
    {
        public static float EaseInQuad(float t) { return t * t; }
        public static float EaseOutQuad(float t) { return 1f - (1f - t) * (1f - t); }
        public static float EaseInCubic(float t) { return t * t * t; }
        public static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        public static float EaseInOutCubic(float t) { return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        /// <summary>Frame-rate independent exponential smoothing factor.</summary>
        public static float Damp(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-sharpness * dt);
        }
    }
}
