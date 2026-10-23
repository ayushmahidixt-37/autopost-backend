using System;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Synthesises every sound effect and the background music at startup, so the game ships with original,
    /// royalty-free audio and zero imported audio files. Replace any clip by dropping an AudioClip at
    /// Resources/ShapeEscape/Audio/{name} (e.g. "Remove", "Music") — AudioManager prefers those when present.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 22050;

        static float Sine(float freq, float t) { return Mathf.Sin(2f * Mathf.PI * freq * t); }

        static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] Buffer(float seconds) { return new float[Mathf.CeilToInt(seconds * SampleRate)]; }

        /// <summary>Soft attack + exponential decay envelope.</summary>
        static float Env(float t, float attack, float decay)
        {
            if (t < 0) return 0;
            float a = attack > 0 ? Mathf.Clamp01(t / attack) : 1f;
            return a * Mathf.Exp(-t / Mathf.Max(0.0001f, decay));
        }

        static void Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            foreach (var v in d) max = Mathf.Max(max, Mathf.Abs(v));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
            // Short fade-out to avoid clicks.
            int fade = Mathf.Min(d.Length, SampleRate / 200);
            for (int i = 0; i < fade; i++) d[d.Length - 1 - i] *= i / (float)fade;
        }

        static void Bell(float[] d, float start, float freq, float amp, float decay)
        {
            int s0 = Mathf.Max(0, (int)(start * SampleRate));
            for (int i = s0; i < d.Length; i++)
            {
                float t = (i - s0) / (float)SampleRate;
                float e = Env(t, 0.004f, decay);
                if (e < 0.0005f && t > 0.05f) break;
                d[i] += amp * e * (Sine(freq, t) + 0.35f * Sine(freq * 2.01f, t) + 0.12f * Sine(freq * 3.98f, t));
            }
        }

        public static AudioClip Tap()
        {
            var d = Buffer(0.05f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                d[i] = Env(t, 0.001f, 0.012f) * Sine(1750f, t);
            }
            Normalize(d, 0.5f);
            return ToClip("Tap", d);
        }

        /// <summary>Bubbly "pop" with a rising pitch sweep; pitch is varied per combo by the AudioManager.</summary>
        public static AudioClip Remove()
        {
            var d = Buffer(0.22f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = Mathf.Lerp(480f, 920f, Mathf.Clamp01(t / 0.07f));
                phase += 2f * Mathf.PI * f / SampleRate;
                d[i] = Env(t, 0.003f, 0.06f) * (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f));
            }
            Normalize(d, 0.6f);
            return ToClip("Remove", d);
        }

        public static AudioClip Blocked()
        {
            var d = Buffer(0.22f);
            var rng = new System.Random(7);
            float phase = 0f;
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = Mathf.Lerp(170f, 85f, Mathf.Clamp01(t / 0.12f));
                phase += 2f * Mathf.PI * f / SampleRate;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.08f;
                d[i] = Env(t, 0.002f, 0.07f) * (Mathf.Sin(phase) * 0.9f + lp * 0.6f);
            }
            Normalize(d, 0.55f);
            return ToClip("Blocked", d);
        }

        public static AudioClip Locked()
        {
            var d = Buffer(0.25f);
            for (int k = 0; k < 2; k++)
            {
                float start = k * 0.09f;
                for (int i = (int)(start * SampleRate); i < d.Length; i++)
                {
                    float t = i / (float)SampleRate - start;
                    float sq = Mathf.Sign(Sine(310f, t)) * 0.3f + Sine(620f, t) * 0.4f;
                    d[i] += Env(t, 0.001f, 0.025f) * sq;
                }
            }
            Normalize(d, 0.45f);
            return ToClip("Locked", d);
        }

        public static AudioClip Unlock()
        {
            var d = Buffer(0.7f);
            Bell(d, 0f, 880f, 0.6f, 0.18f);
            Bell(d, 0.09f, 1318.5f, 0.6f, 0.25f);
            Normalize(d, 0.55f);
            return ToClip("Unlock", d);
        }

        public static AudioClip Rotate()
        {
            var d = Buffer(0.12f);
            float phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = Mathf.Lerp(600f, 1000f, t / 0.12f);
                phase += 2f * Mathf.PI * f / SampleRate;
                d[i] = Env(t, 0.003f, 0.035f) * Mathf.Sin(phase);
            }
            Normalize(d, 0.45f);
            return ToClip("Rotate", d);
        }

        public static AudioClip Chain()
        {
            var d = Buffer(0.5f);
            Bell(d, 0f, 1046.5f, 0.5f, 0.12f);
            Bell(d, 0.06f, 1568f, 0.4f, 0.12f);
            Bell(d, 0.12f, 2093f, 0.3f, 0.15f);
            Normalize(d, 0.45f);
            return ToClip("Chain", d);
        }

        /// <summary>Major arpeggio with a soft shimmer tail.</summary>
        public static AudioClip Complete()
        {
            var d = Buffer(1.8f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
            for (int i = 0; i < notes.Length; i++) Bell(d, i * 0.1f, notes[i], i == notes.Length - 1 ? 0.8f : 0.55f, 0.35f + i * 0.08f);
            Bell(d, 0.55f, 523.25f, 0.35f, 0.6f);
            Bell(d, 0.55f, 659.25f, 0.3f, 0.6f);
            Bell(d, 0.55f, 783.99f, 0.3f, 0.6f);
            Normalize(d, 0.7f);
            return ToClip("Complete", d);
        }

        public static AudioClip Coin()
        {
            var d = Buffer(0.45f);
            Bell(d, 0f, 1318.5f, 0.6f, 0.08f);
            Bell(d, 0.07f, 1975.5f, 0.7f, 0.18f);
            Normalize(d, 0.5f);
            return ToClip("Coin", d);
        }

        public static AudioClip UiClick()
        {
            var d = Buffer(0.06f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                d[i] = Env(t, 0.001f, 0.015f) * (Sine(1100f, t) * 0.7f + Sine(2200f, t) * 0.2f);
            }
            Normalize(d, 0.35f);
            return ToClip("UiClick", d);
        }

        public static AudioClip Hint()
        {
            var d = Buffer(0.8f);
            float[] notes = { 783.99f, 987.77f, 1174.66f };
            for (int i = 0; i < notes.Length; i++) Bell(d, i * 0.08f, notes[i], 0.5f, 0.25f);
            Normalize(d, 0.45f);
            return ToClip("Hint", d);
        }

        public static AudioClip Reveal()
        {
            var d = Buffer(0.35f);
            Bell(d, 0f, 1567.98f, 0.5f, 0.1f);
            Bell(d, 0.05f, 2093f, 0.4f, 0.12f);
            Normalize(d, 0.35f);
            return ToClip("Reveal", d);
        }

        public static AudioClip Whoosh()
        {
            var d = Buffer(0.6f);
            var rng = new System.Random(3);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float cutoff = Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI));
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                d[i] = lp2 * Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI);
            }
            Normalize(d, 0.35f);
            return ToClip("Whoosh", d);
        }

        // ---------------------------------------------------------------- music

        /// <summary>
        /// Computes a calm ~32 s loop (pads + soft pentatonic plucks) on any thread. Every note is fully contained
        /// in the loop so it repeats seamlessly. Call <see cref="MusicClip"/> on the main thread with the result.
        /// </summary>
        public static float[] ComputeMusic(int seed)
        {
            const float bpm = 72f;
            float beat = 60f / bpm;
            int bars = 8;
            float barLen = beat * 4f;
            float length = bars * barLen;
            var d = new float[Mathf.CeilToInt(length * SampleRate)];
            var rng = new System.Random(seed);

            // Cmaj7 - Am7 - Fmaj7 - G6 (x2), voiced low and wide.
            float[][] chords =
            {
                new[] { 130.81f, 196.00f, 246.94f, 329.63f },
                new[] { 110.00f, 164.81f, 196.00f, 261.63f },
                new[] { 87.31f, 130.81f, 164.81f, 220.00f },
                new[] { 98.00f, 146.83f, 196.00f, 246.94f }
            };
            float[] penta = { 523.25f, 587.33f, 659.25f, 783.99f, 880.00f, 1046.5f };

            for (int bar = 0; bar < bars; bar++)
            {
                var chord = chords[bar % chords.Length];
                float start = bar * barLen;
                int s0 = (int)(start * SampleRate);
                int s1 = Mathf.Min(d.Length, (int)((start + barLen) * SampleRate));
                for (int i = s0; i < s1; i++)
                {
                    float t = (i - s0) / (float)SampleRate;
                    float env = Mathf.Clamp01(t / 0.9f) * Mathf.Clamp01((barLen - t) / 1.1f);
                    float v = 0f;
                    for (int n = 0; n < chord.Length; n++)
                    {
                        float f = chord[n];
                        float ph = 2f * Mathf.PI * f * t;
                        // Slightly detuned sine pair + soft 2nd harmonic = warm pad.
                        v += Mathf.Sin(ph) * 0.5f + Mathf.Sin(ph * 1.003f + n) * 0.35f + Mathf.Sin(ph * 2f) * 0.08f;
                    }
                    d[i] += v * env * 0.06f;
                }

                // Melody: 0–3 plucks per beat drawn from the pentatonic scale.
                for (int b = 0; b < 4; b++)
                {
                    if (rng.NextDouble() < 0.35) continue;
                    float noteStart = start + b * beat + (rng.NextDouble() < 0.3 ? beat * 0.5f : 0f);
                    float f = penta[rng.Next(penta.Length)];
                    int n0 = (int)(noteStart * SampleRate);
                    int maxLen = (int)(1.6f * SampleRate);
                    for (int k = 0; k < maxLen && n0 + k < d.Length; k++)
                    {
                        float t = k / (float)SampleRate;
                        float e = Env(t, 0.005f, 0.45f);
                        // Fade out near the loop end so nothing is cut off.
                        float tail = Mathf.Clamp01((length - (noteStart + t)) / 0.3f);
                        d[n0 + k] += e * tail * 0.11f * (Sine(f, t) + 0.3f * Sine(f * 2f, t) + 0.1f * Sine(f * 3f, t));
                    }
                }
            }

            float max = 0.0001f;
            foreach (var v in d) max = Mathf.Max(max, Mathf.Abs(v));
            float k2 = 0.55f / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k2;
            return d;
        }

        public static AudioClip MusicClip(float[] data)
        {
            return ToClip("Music", data);
        }
    }
}
