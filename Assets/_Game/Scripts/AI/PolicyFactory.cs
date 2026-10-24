using System;
using System.Collections.Generic;
using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>Creates policies by name for the UI policy selector and the experiment runner.</summary>
    public static class PolicyFactory
    {
        public static readonly IReadOnlyList<string> Names = new[] { "Nearest", "LowestCost" };

        public static IFulfilmentPolicy Create(string name)
        {
            switch (name)
            {
                case "Nearest": return new NearestPolicy();
                case "LowestCost": return new LowestCostPolicy();
                default: throw new ArgumentException($"Unknown policy '{name}'. Known: {string.Join(", ", Names)}");
            }
        }
    }
}
