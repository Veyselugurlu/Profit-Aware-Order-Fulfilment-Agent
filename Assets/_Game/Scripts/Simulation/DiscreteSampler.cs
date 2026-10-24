using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Samples an index with probability proportional to its weight (cumulative table + binary search).
    /// Used for SKU popularity, basket size and region demand weights.
    /// </summary>
    public sealed class DiscreteSampler
    {
        private readonly double[] cumulative;

        public int Count => cumulative.Length;

        public DiscreteSampler(IReadOnlyList<double> weights)
        {
            if (weights == null || weights.Count == 0)
                throw new ArgumentException("At least one weight is required.", nameof(weights));

            cumulative = new double[weights.Count];
            double total = 0.0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0.0 || double.IsNaN(weights[i]))
                    throw new ArgumentException($"Weight {i} must be non-negative.", nameof(weights));
                total += weights[i];
                cumulative[i] = total;
            }

            if (total <= 0.0)
                throw new ArgumentException("Weights must not all be zero.", nameof(weights));

            for (int i = 0; i < cumulative.Length; i++)
                cumulative[i] /= total;
            cumulative[cumulative.Length - 1] = 1.0;
        }

        public double Probability(int index) =>
            index == 0 ? cumulative[0] : cumulative[index] - cumulative[index - 1];

        public int Sample(SeededRng rng)
        {
            double u = rng.NextDouble();
            int lo = 0, hi = cumulative.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (u < cumulative[mid]) hi = mid;
                else lo = mid + 1;
            }
            return lo;
        }

        /// <summary>Zipf weights 1/rank^exponent for ranks 1..count.</summary>
        public static double[] ZipfWeights(int count, double exponent)
        {
            var weights = new double[count];
            for (int rank = 1; rank <= count; rank++)
                weights[rank - 1] = 1.0 / Math.Pow(rank, exponent);
            return weights;
        }
    }
}
