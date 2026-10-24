using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>A supplier order on its way to a center.</summary>
    public readonly struct ReplenishmentOrder
    {
        public readonly int CenterId;
        public readonly int SkuId;
        public readonly int Quantity;
        public readonly int OrderedTick;
        public readonly int ArrivalTick;

        public ReplenishmentOrder(int centerId, int skuId, int quantity, int orderedTick, int arrivalTick)
        {
            CenterId = centerId;
            SkuId = skuId;
            Quantity = quantity;
            OrderedTick = orderedTick;
            ArrivalTick = arrivalTick;
        }
    }

    /// <summary>
    /// Periodic review (R, S) policy: every R hours each center orders up to S for every SKU whose
    /// inventory position (on hand + on order) is below S; the delivery arrives after a random lead time.
    /// One lead time is drawn per center per review whether or not anything is ordered, so every
    /// fulfilment policy sees the same lead times for a seed (common random numbers).
    /// </summary>
    public sealed class ReplenishmentSystem
    {
        private readonly StockPlan plan;
        private readonly SeededRng rng;
        private readonly int reviewTicks;
        private readonly double leadMinHours;
        private readonly double leadMaxHours;
        private readonly List<ReplenishmentOrder> pending = new List<ReplenishmentOrder>();
        private readonly int[,] onOrder;

        public IReadOnlyList<ReplenishmentOrder> Pending => pending;

        public ReplenishmentSystem(StockPlan plan, ScenarioConfig scenario, int seed)
        {
            this.plan = plan;
            rng = new SeededRng(seed, RngStream.Replenishment);
            reviewTicks = System.Math.Max(1, SimTime.FromHours(scenario.replenishReviewHours));
            leadMinHours = scenario.leadTimeHoursMin;
            leadMaxHours = scenario.leadTimeHoursMax;
            onOrder = new int[plan.CenterCount, plan.SkuCount];
        }

        public int OnOrder(int centerId, int skuId) => onOrder[centerId, skuId];

        public bool IsReviewTick(int tick) => tick > 0 && tick % reviewTicks == 0;

        /// <summary>Receives deliveries due at <paramref name="tick"/>, then places orders on review ticks.</summary>
        public void Step(int tick, Inventory inventory, List<SimEvent> events)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                ReplenishmentOrder order = pending[i];
                if (order.ArrivalTick > tick) continue;
                inventory.Add(order.CenterId, order.SkuId, order.Quantity);
                onOrder[order.CenterId, order.SkuId] -= order.Quantity;
                pending.RemoveAt(i);
                events?.Add(SimEvent.Replenished(tick, order.CenterId, order.SkuId, order.Quantity));
            }

            if (!IsReviewTick(tick))
                return;

            for (int c = 0; c < plan.CenterCount; c++)
            {
                int arrival = tick + SimTime.FromHours(rng.Range(leadMinHours, leadMaxHours));
                for (int s = 0; s < plan.SkuCount; s++)
                {
                    int position = inventory.OnHand(c, s) + onOrder[c, s];
                    int quantity = plan.OrderUpTo(c, s) - position;
                    if (quantity <= 0) continue;
                    pending.Add(new ReplenishmentOrder(c, s, quantity, tick, arrival));
                    onOrder[c, s] += quantity;
                    events?.Add(SimEvent.ReplenishmentOrdered(tick, c, s, quantity));
                }
            }
        }
    }
}
