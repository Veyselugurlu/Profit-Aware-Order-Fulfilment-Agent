using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>One parcel of a plan: where it leaves from, how heavy, how far, when it would arrive.</summary>
    public readonly struct PackageEstimate
    {
        public readonly int CenterId;
        public readonly double WeightKg;
        public readonly double DistanceKm;
        public readonly double ShippingCost;
        public readonly double DispatchTick;
        public readonly double ArrivalTick;

        public PackageEstimate(int centerId, double weightKg, double distanceKm, double shippingCost, double dispatchTick, double arrivalTick)
        {
            CenterId = centerId;
            WeightKg = weightKg;
            DistanceKm = distanceKm;
            ShippingCost = shippingCost;
            DispatchTick = dispatchTick;
            ArrivalTick = arrivalTick;
        }
    }

    /// <summary>
    /// Every number a policy may look at for one candidate plan, computed by the simulation's cost model
    /// at decision time. All policies (baselines, Utility AI, learning agent) read the same evaluation, so
    /// differences between them come only from how they weigh these numbers.
    /// </summary>
    public sealed class PlanEvaluation
    {
        public double Revenue;
        public double Cogs;
        public double ShippingCost;
        public double SplitCost;
        public double StockoutPenalty;
        public double ExpectedLatenessCost;

        /// <summary>Estimated future cost of pushing centers below safety stock; not a cash cost.</summary>
        public double OpportunityCost;

        public int ShippedUnits;
        public int LostUnits;
        public int PackageCount;
        public double TotalKm;
        public double AverageKm;
        public double LatestArrivalTick;

        /// <summary>Promised time minus latest arrival, in minutes; negative means late.</summary>
        public double SlackMinutes;

        public List<PackageEstimate> Packages = new List<PackageEstimate>();

        public double CashCost => ShippingCost + SplitCost + StockoutPenalty + ExpectedLatenessCost;
        public double ExpectedProfit => Revenue - Cogs - CashCost;
    }
}
