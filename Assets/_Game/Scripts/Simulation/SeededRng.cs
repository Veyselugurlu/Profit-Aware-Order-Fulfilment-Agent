using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Independent random streams of one episode. Orders, replenishment and the policy each draw
    /// from their own stream, so changing the policy never changes the order stream of a seed.
    /// </summary>
    public enum RngStream
    {
        Orders = 1,
        Replenishment = 2,
        Policy = 3,
        Layout = 4
    }

    /// <summary>
    /// Deterministic xoshiro256** generator seeded through SplitMix64.
    /// The simulation uses only this class; UnityEngine.Random and System.Random are not used,
    /// so a seed gives the same sequence in the Editor, in batchmode and in a player build.
    /// </summary>
    public sealed class SeededRng
    {
        private ulong s0, s1, s2, s3;
        private double? spareNormal;

        public int Seed { get; }
        public RngStream Stream { get; }

        public SeededRng(int seed, RngStream stream)
        {
            Seed = seed;
            Stream = stream;
            ulong x = ((ulong)(uint)seed << 32) ^ (ulong)(int)stream ^ 0x9E3779B97F4A7C15UL;
            s0 = SplitMix64(ref x);
            s1 = SplitMix64(ref x);
            s2 = SplitMix64(ref x);
            s3 = SplitMix64(ref x);
        }

        private static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong RotateLeft(ulong v, int k) => (v << k) | (v >> (64 - k));

        public ulong NextULong()
        {
            ulong result = RotateLeft(s1 * 5, 7) * 9;
            ulong t = s1 << 17;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = RotateLeft(s3, 45);
            return result;
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextULong() % range));
        }

        public double Range(double minInclusive, double maxExclusive) =>
            minInclusive + (maxExclusive - minInclusive) * NextDouble();

        public bool Chance(double probability) => NextDouble() < probability;

        /// <summary>Standard normal sample (Box–Muller, the second value is kept for the next call).</summary>
        public double NextNormal()
        {
            if (spareNormal.HasValue)
            {
                double spare = spareNormal.Value;
                spareNormal = null;
                return spare;
            }

            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            double radius = Math.Sqrt(-2.0 * Math.Log(u1));
            double angle = 2.0 * Math.PI * u2;
            spareNormal = radius * Math.Sin(angle);
            return radius * Math.Cos(angle);
        }

        public double NextNormal(double mean, double stdDev) => mean + stdDev * NextNormal();

        /// <summary>
        /// Poisson sample. Knuth's product method for small means (per-tick order arrivals),
        /// normal approximation for large means.
        /// </summary>
        public int NextPoisson(double mean)
        {
            if (mean < 0.0)
                throw new ArgumentOutOfRangeException(nameof(mean));
            if (mean == 0.0)
                return 0;

            if (mean > 30.0)
                return Math.Max(0, (int)Math.Round(NextNormal(mean, Math.Sqrt(mean))));

            double limit = Math.Exp(-mean);
            double product = NextDouble();
            int count = 0;
            while (product > limit)
            {
                product *= NextDouble();
                count++;
            }
            return count;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
