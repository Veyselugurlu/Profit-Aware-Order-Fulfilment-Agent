using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Builds the candidate plans a policy chooses from:
    /// one single-center plan per center that holds the whole order, a min-cost split that takes each line
    /// from the center with the lowest marginal shipping cost, and a max-coverage split that greedily uses
    /// the centers covering the most units (fewest lost units and parcels). Duplicate plans are dropped,
    /// so a policy sees at most centers + 2 plans and always at least one.
    /// </summary>
    public sealed class CandidateGenerator
    {
        private readonly World world;
        private readonly IDistanceModel distances;
        private readonly CostConfig cost;

        public CandidateGenerator(World world, IDistanceModel distances, CostConfig cost)
        {
            this.world = world;
            this.distances = distances;
            this.cost = cost;
        }

        public List<FulfilmentPlan> Generate(Order order, Inventory inventory)
        {
            var plans = new List<FulfilmentPlan>();
            var signatures = new HashSet<string>();

            for (int c = 0; c < world.Centers.Count; c++)
            {
                if (!inventory.CanFulfilAll(c, order.Lines)) continue;
                var allocations = order.Lines.Select(l => new Allocation(c, l.SkuId, l.Quantity)).ToArray();
                Add(plans, signatures, new FulfilmentPlan(PlanKind.SingleCenter, allocations, Array.Empty<OrderLine>()));
            }

            Add(plans, signatures, MinCostSplit(order, inventory));
            Add(plans, signatures, MaxCoverageSplit(order, inventory));
            return plans;
        }

        private static void Add(List<FulfilmentPlan> plans, HashSet<string> signatures, FulfilmentPlan plan)
        {
            if (signatures.Add(Signature(plan)))
                plans.Add(plan);
        }

        public static string Signature(FulfilmentPlan plan)
        {
            var sb = new StringBuilder();
            foreach (Allocation a in plan.Allocations.OrderBy(a => a.CenterId).ThenBy(a => a.SkuId))
                sb.Append(a.CenterId).Append(':').Append(a.SkuId).Append('x').Append(a.Quantity).Append(';');
            sb.Append('|');
            foreach (OrderLine l in plan.LostLines.OrderBy(l => l.SkuId))
                sb.Append(l.SkuId).Append('x').Append(l.Quantity).Append(';');
            return sb.ToString();
        }

        private FulfilmentPlan MinCostSplit(Order order, Inventory inventory)
        {
            var allocations = new List<Allocation>();
            var lost = new List<OrderLine>();
            var used = new HashSet<int>();

            IEnumerable<OrderLine> lines = order.Lines
                .OrderByDescending(l => world.Skus[l.SkuId].WeightKg * l.Quantity)
                .ThenBy(l => l.SkuId);

            foreach (OrderLine line in lines)
            {
                double weight = world.Skus[line.SkuId].WeightKg;
                int remaining = line.Quantity;

                // Prefer one center that can ship the whole line at the lowest marginal cost.
                int best = -1;
                double bestCost = double.MaxValue;
                for (int c = 0; c < world.Centers.Count; c++)
                {
                    if (inventory.OnHand(c, line.SkuId) < remaining) continue;
                    double marginal = MarginalCost(c, order.RegionId, weight * remaining, used);
                    if (marginal < bestCost) { bestCost = marginal; best = c; }
                }

                if (best >= 0)
                {
                    allocations.Add(new Allocation(best, line.SkuId, remaining));
                    used.Add(best);
                    continue;
                }

                // Otherwise take what each center has, cheapest first.
                foreach (int c in Enumerable.Range(0, world.Centers.Count)
                             .Where(c => inventory.OnHand(c, line.SkuId) > 0)
                             .OrderBy(c => MarginalCost(c, order.RegionId, weight, used)))
                {
                    int take = Math.Min(remaining, inventory.OnHand(c, line.SkuId));
                    allocations.Add(new Allocation(c, line.SkuId, take));
                    used.Add(c);
                    remaining -= take;
                    if (remaining == 0) break;
                }

                if (remaining > 0)
                    lost.Add(new OrderLine(line.SkuId, remaining));
            }

            return new FulfilmentPlan(PlanKind.MinCostSplit, allocations, lost);
        }

        private FulfilmentPlan MaxCoverageSplit(Order order, Inventory inventory)
        {
            var remaining = order.Lines.Select(l => l.Quantity).ToArray();
            var allocations = new List<Allocation>();
            var used = new HashSet<int>();

            while (true)
            {
                int best = -1, bestUnits = 0;
                double bestKm = double.MaxValue;
                for (int c = 0; c < world.Centers.Count; c++)
                {
                    if (used.Contains(c)) continue;
                    int units = 0;
                    for (int i = 0; i < order.Lines.Count; i++)
                        units += Math.Min(remaining[i], inventory.OnHand(c, order.Lines[i].SkuId));
                    double km = distances.DistanceKm(c, order.RegionId);
                    if (units > bestUnits || (units == bestUnits && units > 0 && km < bestKm))
                    {
                        best = c;
                        bestUnits = units;
                        bestKm = km;
                    }
                }

                if (best < 0) break;

                used.Add(best);
                for (int i = 0; i < order.Lines.Count; i++)
                {
                    int take = Math.Min(remaining[i], inventory.OnHand(best, order.Lines[i].SkuId));
                    if (take <= 0) continue;
                    allocations.Add(new Allocation(best, order.Lines[i].SkuId, take));
                    remaining[i] -= take;
                }
            }

            var lost = new List<OrderLine>();
            for (int i = 0; i < order.Lines.Count; i++)
                if (remaining[i] > 0)
                    lost.Add(new OrderLine(order.Lines[i].SkuId, remaining[i]));

            return new FulfilmentPlan(PlanKind.MaxCoverageSplit, allocations, lost);
        }

        /// <summary>Extra shipping cost of putting <paramref name="weightKg"/> on center c's parcel.</summary>
        private double MarginalCost(int centerId, int regionId, double weightKg, HashSet<int> usedCenters)
        {
            double km = distances.DistanceKm(centerId, regionId);
            double cost = this.cost.shippingPerKgKm * weightKg * km;
            if (usedCenters.Contains(centerId))
                return cost;
            cost += this.cost.shippingBasePerPackage + this.cost.shippingPerKm * km;
            if (usedCenters.Count > 0)
                cost += this.cost.splitPenaltyPerExtraPackage;
            return cost;
        }
    }
}
