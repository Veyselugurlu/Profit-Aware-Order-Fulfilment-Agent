using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Cost and timing of fulfilment. Cash components: shipping, split penalty, lateness and the
    /// lost-sale (stock-out) penalty. The stock-depletion opportunity cost is an estimate used for
    /// decisions and reported separately; it never enters realised profit.
    /// </summary>
    public sealed class CostModel
    {
        private readonly World world;
        private readonly IDistanceModel distances;
        private readonly StockPlan stockPlan;

        public CostConfig Config { get; }

        public CostModel(World world, IDistanceModel distances, CostConfig config, StockPlan stockPlan)
        {
            this.world = world;
            this.distances = distances;
            this.stockPlan = stockPlan;
            Config = config;
        }

        public double DistanceKm(int centerId, int regionId) => distances.DistanceKm(centerId, regionId);

        public double ShippingCost(double km, double weightKg) =>
            Config.shippingBasePerPackage + Config.shippingPerKm * km + Config.shippingPerKgKm * weightKg * km;

        public double TravelMinutes(double km) => km / Config.vehicleSpeedKmh * 60.0;

        public double SplitCost(int packageCount) => Math.Max(0, packageCount - 1) * Config.splitPenaltyPerExtraPackage;

        public double LatenessCost(double lateMinutes, OrderPriority priority)
        {
            if (lateMinutes <= 0.0) return 0.0;
            double multiplier = priority == OrderPriority.Express ? Config.expressLatenessMultiplier : 1.0;
            return lateMinutes / 60.0 * Config.latenessPerHour * multiplier;
        }

        public double StockoutPenalty(OrderLine lostLine) =>
            lostLine.Quantity * world.Skus[lostLine.SkuId].Price * Config.lostSalePenaltyFactor;

        /// <summary>Opportunity cost of taking <paramref name="quantity"/> units: only the units that end below safety stock count.</summary>
        public double OpportunityCost(int centerId, int skuId, int quantity, int onHandBefore)
        {
            double safety = stockPlan.SafetyStock(centerId, skuId);
            int after = onHandBefore - quantity;
            double belowBefore = Math.Max(0.0, safety - onHandBefore);
            double belowAfter = Math.Max(0.0, safety - after);
            double units = Math.Min(quantity, belowAfter - belowBefore);
            return units * world.Skus[skuId].Margin * Config.opportunityCostFactor;
        }

        public bool IsBelowSafety(int centerId, int skuId, int onHand) => onHand < stockPlan.SafetyStock(centerId, skuId);

        /// <summary>Prices a candidate plan for the current inventory and center queues, without changing either.</summary>
        public PlanEvaluation Evaluate(Order order, FulfilmentPlan plan, Inventory inventory, CenterQueues queues, int tick)
        {
            var eval = new PlanEvaluation();
            var weightByCenter = new Dictionary<int, double>();

            foreach (Allocation a in plan.Allocations)
            {
                Sku sku = world.Skus[a.SkuId];
                eval.Revenue += sku.Price * a.Quantity;
                eval.Cogs += sku.UnitCost * a.Quantity;
                eval.ShippedUnits += a.Quantity;
                eval.OpportunityCost += OpportunityCost(a.CenterId, a.SkuId, a.Quantity, inventory.OnHand(a.CenterId, a.SkuId));
                weightByCenter.TryGetValue(a.CenterId, out double w);
                weightByCenter[a.CenterId] = w + sku.WeightKg * a.Quantity;
            }

            foreach (OrderLine lost in plan.LostLines)
            {
                eval.LostUnits += lost.Quantity;
                eval.StockoutPenalty += StockoutPenalty(lost);
            }

            eval.LatestArrivalTick = tick;
            foreach (KeyValuePair<int, double> entry in weightByCenter)
            {
                int c = entry.Key;
                double km = DistanceKm(c, order.RegionId);
                double shipping = ShippingCost(km, entry.Value);
                double dispatch = queues.PeekDispatch(c, tick);
                double arrival = dispatch + TravelMinutes(km);
                eval.Packages.Add(new PackageEstimate(c, entry.Value, km, shipping, dispatch, arrival));
                eval.ShippingCost += shipping;
                eval.TotalKm += km;
                eval.LatestArrivalTick = Math.Max(eval.LatestArrivalTick, arrival);
            }

            eval.PackageCount = eval.Packages.Count;
            eval.AverageKm = eval.PackageCount > 0 ? eval.TotalKm / eval.PackageCount : 0.0;
            eval.SplitCost = SplitCost(eval.PackageCount);

            if (eval.PackageCount > 0)
            {
                eval.SlackMinutes = order.PromisedTick - eval.LatestArrivalTick;
                eval.ExpectedLatenessCost = LatenessCost(-eval.SlackMinutes, order.Priority);
            }
            else
            {
                eval.SlackMinutes = order.PromisedTick - tick;
            }

            return eval;
        }
    }
}
