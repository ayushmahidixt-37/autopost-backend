using System;

namespace ShapeEscape.Core
{
    /// <summary>
    /// Small, platform-independent PRNG (xorshift128+ seeded through SplitMix64).
    /// System.Random and UnityEngine.Random are not guaranteed to produce identical streams across
    /// runtimes, so every level layout uses this generator to stay reproducible from its seed.
    /// </summary>
    public sealed class DeterministicRandom
    {
        ulong _s0;
        ulong _s1;

        public DeterministicRandom(int seed) : this((ulong)(uint)seed) { }

        public DeterministicRandom(ulong seed)
        {
            ulong x = seed + 0x9E3779B97F4A7C15UL;
            _s0 = SplitMix(ref x);
            _s1 = SplitMix(ref x);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
        }

        static ulong SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            ulong result = s0 + s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return result;
        }

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextULong() >> 40) / (float)(1UL << 24);
        }

        public bool Chance(float probability) { return NextFloat() < probability; }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>Stable 32-bit hash for combining seeds (FNV-1a).</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < text.Length; i++)
                {
                    h ^= text[i];
                    h *= 16777619;
                }
                return (int)h;
            }
        }

        public static int Combine(int a, int b)
        {
            unchecked
            {
                return (int)((uint)a * 2654435761u ^ (uint)b * 2246822519u ^ 0x27d4eb2fu);
            }
        }
    }
}
