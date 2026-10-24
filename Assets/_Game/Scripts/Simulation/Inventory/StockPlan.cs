using System;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Stock levels of every (center, SKU): the order-up-to level S used for the initial stock and for
    /// replenishment, and the safety level below which a decision is charged an opportunity cost.
    /// Both follow the expected demand of the SKU times the center's stock share.
    /// </summary>
    public sealed class StockPlan
    {
        private readonly int[,] orderUpTo;
        private readonly double[,] safety;

        public int CenterCount { get; }
        public int SkuCount { get; }

        public StockPlan(World world, ScenarioConfig scenario, DemandProfileConfig demand, CostConfig cost, double stockMultiplier = 1.0)
        {
            CenterCount = world.Centers.Count;
            SkuCount = world.Skus.Count;
            orderUpTo = new int[CenterCount, SkuCount];
            safety = new double[CenterCount, SkuCount];

            double[] daily = DemandMath.ExpectedDailyUnits(demand, scenario, SkuCount);
            for (int c = 0; c < CenterCount; c++)
            {
                double share = world.Centers[c].StockShare;
                for (int s = 0; s < SkuCount; s++)
                {
                    double centerDaily = daily[s] * share;
                    orderUpTo[c, s] = Math.Max(1, (int)Math.Ceiling(scenario.stockDays * stockMultiplier * centerDaily));
                    safety[c, s] = centerDaily * cost.safetyStockHours / 24.0;
                }
            }
        }

        public int OrderUpTo(int centerId, int skuId) => orderUpTo[centerId, skuId];
        public double SafetyStock(int centerId, int skuId) => safety[centerId, skuId];

        public Inventory CreateInitialInventory()
        {
            var inventory = new Inventory(CenterCount, SkuCount);
            for (int c = 0; c < CenterCount; c++)
                for (int s = 0; s < SkuCount; s++)
                    inventory.Set(c, s, orderUpTo[c, s]);
            return inventory;
        }
    }
}
