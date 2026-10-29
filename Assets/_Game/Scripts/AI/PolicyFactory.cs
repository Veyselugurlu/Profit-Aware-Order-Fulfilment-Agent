using System;
using System.Collections.Generic;
using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>Creates policies by name for the UI policy selector and the experiment runner.</summary>
    public static class PolicyFactory
    {
        public const string Nearest = "Nearest";
        public const string LowestCost = "LowestCost";
        public const string Utility = "Utility";

        public static readonly IReadOnlyList<string> Names = new[] { Nearest, LowestCost, Utility };

        /// <summary>Baselines need no config; the Utility AI needs its weights and curves.</summary>
        public static IFulfilmentPolicy Create(string name, UtilityConfig utility = null)
        {
            switch (name)
            {
                case Nearest: return new NearestPolicy();
                case LowestCost: return new LowestCostPolicy();
                case Utility:
                    if (utility == null) throw new ArgumentException("The Utility policy needs a utility config.");
                    return new UtilityPolicy(utility);
                default: throw new ArgumentException($"Unknown policy '{name}'. Known: {string.Join(", ", Names)}");
            }
        }
    }
}
