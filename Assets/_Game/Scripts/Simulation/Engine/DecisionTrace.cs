using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Why a policy chose a plan, kept with the order record so the UI can explain any decision afterwards.
    /// Rule-based policies fill only <see cref="Rule"/>; scoring policies also fill the criterion scores.
    /// </summary>
    public sealed class DecisionTrace
    {
        public string Policy;

        /// <summary>Plain-language decision rule of the policy.</summary>
        public string Rule;

        /// <summary>Criterion keys, e.g. cost, time, stock, split, fill. Empty for rule-based policies.</summary>
        public IReadOnlyList<string> Criteria = new string[0];

        /// <summary>Weight of each criterion (sums to 1).</summary>
        public double[] Weights = new double[0];

        /// <summary>Raw input of each criterion per candidate: [candidate][criterion].</summary>
        public double[][] Raw = new double[0][];

        /// <summary>Score in [0, 1] of each criterion per candidate: [candidate][criterion].</summary>
        public double[][] Scores = new double[0][];

        /// <summary>Weighted total per candidate.</summary>
        public double[] Totals = new double[0];

        public int Chosen;

        /// <summary>Extra line for adaptive policies (e.g. the weight profile chosen for this order).</summary>
        public string Note;

        public bool HasScores => Criteria.Count > 0 && Totals.Length > 0;

        /// <summary>Weighted contribution of one criterion to a candidate's total.</summary>
        public double Contribution(int candidate, int criterion) => Weights[criterion] * Scores[candidate][criterion];
    }

    /// <summary>A policy that can say why it chose its last plan.</summary>
    public interface IExplainablePolicy
    {
        DecisionTrace LastTrace { get; }
    }
}
