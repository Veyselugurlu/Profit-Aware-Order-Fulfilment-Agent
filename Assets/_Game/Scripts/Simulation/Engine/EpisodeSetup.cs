using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>Everything that defines one episode except the policy: configs, regime and seed.</summary>
    public sealed class EpisodeSetup
    {
        public World World { get; }
        public ScenarioConfig Scenario { get; }
        public DemandProfileConfig Demand { get; }
        public CostConfig Cost { get; }
        public RegimeConfig Regime { get; }
        public int Seed { get; }

        /// <summary>Scales the order-up-to level (stock scarcity scenarios); 1 = scenario default.</summary>
        public double StockMultiplier { get; }

        public EpisodeSetup(World world, ScenarioConfig scenario, DemandProfileConfig demand, CostConfig cost,
            RegimeConfig regime, int seed, double stockMultiplier = 1.0)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Demand = demand ?? throw new ArgumentNullException(nameof(demand));
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
            Regime = regime;
            Seed = seed;
            StockMultiplier = stockMultiplier;

            List<string> errors = ConfigValidator.Validate(cost);
            if (errors.Count > 0)
                throw new ArgumentException("Invalid cost config:\n- " + string.Join("\n- ", errors));
        }

        public string RegimeCode => Regime?.code ?? "stable";
    }
}
