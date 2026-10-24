namespace Fulfilment.Simulation
{
    /// <summary>Closed-form expectations of the demand profile, used to size stock and safety levels.</summary>
    public static class DemandMath
    {
        public static double[] SkuProbabilities(DemandProfileConfig demand, int skuCount)
        {
            bool hasWeights = demand.skuWeights != null && demand.skuWeights.Length > 0;
            var sampler = new DiscreteSampler(hasWeights ? demand.skuWeights : DiscreteSampler.ZipfWeights(skuCount, demand.zipfExponent));
            var p = new double[skuCount];
            for (int i = 0; i < skuCount; i++) p[i] = sampler.Probability(i);
            return p;
        }

        /// <summary>Mean of a 1-based distribution given by weights (index 0 → value 1).</summary>
        public static double Mean(double[] weights)
        {
            double total = 0.0, weighted = 0.0;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
                weighted += (i + 1) * weights[i];
            }
            return weighted / total;
        }

        /// <summary>Expected units of each SKU ordered per day across the whole network.</summary>
        public static double[] ExpectedDailyUnits(DemandProfileConfig demand, ScenarioConfig scenario, int skuCount)
        {
            double[] p = SkuProbabilities(demand, skuCount);
            double linesPerDay = scenario.ordersPerDay * Mean(demand.basketSizeWeights);
            double unitsPerLine = Mean(demand.quantityWeights);
            var daily = new double[skuCount];
            for (int i = 0; i < skuCount; i++)
                daily[i] = linesPerDay * p[i] * unitsPerLine;
            return daily;
        }
    }
}
