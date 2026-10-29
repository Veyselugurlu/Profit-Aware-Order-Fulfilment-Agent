using System;
using System.Linq;
using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>
    /// Chromosome of the evolved decision table, following the Week 3 "Stay on Platform" DNA class.
    /// There one gene per sensed situation (ground ahead / edge ahead) holds an action code (forward, left, right).
    /// Here there is one gene per sensed order situation (3 binary sensors → 8 states, see <see cref="Brain"/>) and
    /// each gene holds an action code: which Utility weight profile to decide with (balanced, cost, speed, stock).
    /// As in the lecture, a gene value is an action code, not a target value.
    /// </summary>
    [Serializable]
    public sealed class DNA
    {
        public const int StateCount = 8;
        public const int ActionCount = 4;

        private readonly int[] genes;

        public DNA(params int[] genes)
        {
            if (genes == null || genes.Length != StateCount)
                throw new ArgumentException($"A genome has exactly {StateCount} genes.", nameof(genes));
            this.genes = genes.Select(CheckAction).ToArray();
        }

        public int Length => genes.Length;

        public int GetGene(int index)
        {
            if (index < 0 || index >= genes.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return genes[index];
        }

        public int[] ToArray() => (int[])genes.Clone();

        public DNA Copy() => new DNA(ToArray());

        public static DNA RandomGenome(SeededRng rng)
        {
            var g = new int[StateCount];
            for (int i = 0; i < g.Length; i++) g[i] = rng.NextInt(0, ActionCount);
            return new DNA(g);
        }

        /// <summary>
        /// One-point crossover (lecture TODO A): genes before the cut come from A, the rest from B. With two genes
        /// the lecture's cut can only fall in the middle; with eight genes it is drawn from 1..7, so a child always
        /// gets at least one gene from each parent. Crossover recombines existing values; only mutation adds new ones.
        /// </summary>
        public static DNA Cross(DNA parentA, DNA parentB, SeededRng rng)
        {
            if (parentA == null || parentB == null) throw new ArgumentNullException(parentA == null ? nameof(parentA) : nameof(parentB));
            int cut = rng.NextInt(1, StateCount);
            var child = new int[StateCount];
            for (int i = 0; i < StateCount; i++)
                child[i] = i < cut ? parentA.genes[i] : parentB.genes[i];
            return new DNA(child);
        }

        /// <summary>
        /// Per-gene mutation (lecture TODO B): with probability p a gene moves to a DIFFERENT action,
        /// (gene + shift) mod ActionCount with shift in 1..ActionCount-1, never "mutating" to itself.
        /// </summary>
        public void Mutate(double probabilityPerGene, SeededRng rng)
        {
            double p = Math.Max(0.0, Math.Min(1.0, probabilityPerGene));
            for (int i = 0; i < genes.Length; i++)
            {
                if (rng.NextDouble() >= p) continue;
                int shift = rng.NextInt(1, ActionCount);
                genes[i] = (genes[i] + shift) % ActionCount;
            }
        }

        private static int CheckAction(int action)
        {
            if (action < 0 || action >= ActionCount) throw new ArgumentOutOfRangeException(nameof(action));
            return action;
        }

        public override string ToString() => "[" + string.Join(", ", genes) + "]";

        public override bool Equals(object obj) => obj is DNA other && genes.SequenceEqual(other.genes);
        public override int GetHashCode() => genes.Aggregate(17, (h, g) => h * 31 + g);
    }
}
