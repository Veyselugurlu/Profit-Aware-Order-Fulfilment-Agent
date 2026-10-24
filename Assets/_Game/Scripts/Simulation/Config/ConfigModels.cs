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

    /// <summary>Logistics and cost coefficients (cost.json). Money is in the configured currency.</summary>
    [Serializable]
    public sealed class CostConfig
    {
        public string currency;

        // Logistics
        public double kmPerCell;
        public double vehicleSpeedKmh;
        /// <summary>Fixed processing delay after picking (labelling, loading); does not use center capacity.</summary>
        public double handlingMinutes;

        // Shipping: per parcel base + per km + per kg·km
        public double shippingBasePerPackage;
        public double shippingPerKm;
        public double shippingPerKgKm;

        /// <summary>Extra cost per additional parcel of a split order (handling, customer inconvenience).</summary>
        public double splitPenaltyPerExtraPackage;

        public double latenessPerHour;
        public double expressLatenessMultiplier;

        /// <summary>Goodwill penalty per lost unit as a fraction of its price (the lost margin is lost anyway).</summary>
        public double lostSalePenaltyFactor;

        /// <summary>Fraction of a unit's margin charged when a decision pushes a center below safety stock.</summary>
        public double opportunityCostFactor;

        /// <summary>Safety stock of a center: its expected demand over this many hours.</summary>
        public double safetyStockHours;
    }

    /// <summary>Demand regime (regimes/*.json): stable, seasonal or volatile arrival rate.</summary>
    [Serializable]
    public sealed class RegimeConfig
    {
        public string code;
        public string displayName;
        public string type;

        // seasonal: 1 + amplitude · sin(2π (t/day + phase) / period)
        public double amplitude;
        public double periodDays;
        public double phaseDays;

        // volatile: Poisson number of demand shocks with random length and height
        public double shocksPerDay;
        public double shockMinHours;
        public double shockMaxHours;
        public double shockMinMultiplier;
        public double shockMaxMultiplier;
    }
}
