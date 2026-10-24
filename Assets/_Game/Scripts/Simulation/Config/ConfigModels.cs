using System;

namespace Fulfilment.Simulation
{
    // Plain serialisable data read from Assets/StreamingAssets/configs/*.json.
    // Public fields (not properties) so that UnityEngine.JsonUtility can fill them in the
    // presentation layer; the simulation itself never parses JSON.

    [Serializable]
    public sealed class WorldConfig
    {
        public string name;
        public int gridWidth;
        public int gridHeight;
        public CenterConfig[] centers;
        public RegionConfig[] regions;
        public SkuConfig[] skus;
    }

    [Serializable]
    public sealed class CenterConfig
    {
        public string code;
        public string name;
        public int x;
        public int y;
        public int capacityPerHour;
        public double stockShare;
    }

    [Serializable]
    public sealed class RegionConfig
    {
        public string code;
        public string name;
        public int x;
        public int y;
        public double demandWeight;
    }

    [Serializable]
    public sealed class SkuConfig
    {
        public string code;
        public string name;
        public string category;
        public double price;
        public double unitCost;
        public double weightKg;
    }

    /// <summary>Shape of demand that does not depend on the load level (taken from the retail dataset).</summary>
    [Serializable]
    public sealed class DemandProfileConfig
    {
        public string source;

        /// <summary>24 relative weights, one per hour of day.</summary>
        public double[] hourWeights;

        /// <summary>Relative popularity per SKU in world order. Empty → Zipf over the SKU order.</summary>
        public double[] skuWeights;

        public double zipfExponent;

        /// <summary>Index 0 is a basket of 1 distinct SKU, index 1 is 2 SKUs, ...</summary>
        public double[] basketSizeWeights;

        /// <summary>Index 0 is quantity 1 per line, index 1 is quantity 2, ...</summary>
        public double[] quantityWeights;
    }

    /// <summary>Load level (Low / Medium / High) selectable from the scenario panel.</summary>
    [Serializable]
    public sealed class ScenarioConfig
    {
        public string code;
        public string displayName;
        public int episodeDays;
        public double ordersPerDay;
        public double expressShare;
        public double promisedHoursStandard;
        public double promisedHoursExpress;

        /// <summary>Initial and order-up-to stock, in days of expected network demand.</summary>
        public double stockDays;

        public double replenishReviewHours;
        public double leadTimeHoursMin;
        public double leadTimeHoursMax;

        public int EpisodeTicks => episodeDays * SimTime.TicksPerDay;
        public double ExpectedOrders => ordersPerDay * episodeDays;
    }
}
