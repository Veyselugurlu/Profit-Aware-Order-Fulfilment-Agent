using System;
using System.Collections.Generic;
using System.Linq;

namespace Fulfilment.Experiments
{
    /// <summary>
    /// The only place where seeds are defined. Training seeds are for learning and calibration, validation seeds
    /// for choosing hyperparameters, test seeds only for the reported results; nothing is tuned on test seeds.
    /// </summary>
    public static class SeedSets
    {
        public static readonly IReadOnlyList<int> Train = Enumerable.Range(1, 40).ToArray();
        public static readonly IReadOnlyList<int> Validation = Enumerable.Range(501, 10).ToArray();
        public static readonly IReadOnlyList<int> Test = Enumerable.Range(1001, 10).ToArray();

        /// <summary>The brief asks for at least 30 test episodes for reinforcement-learning results.</summary>
        public static readonly IReadOnlyList<int> RlTest = Enumerable.Range(1001, 30).ToArray();

        public static IReadOnlyList<int> ByName(string name)
        {
            switch (name)
            {
                case "train": return Train;
                case "validation": return Validation;
                case "test": return Test;
                case "rl_test": return RlTest;
                default: throw new ArgumentException($"Unknown seed set '{name}' (train, validation, test, rl_test).");
            }
        }
    }
}
