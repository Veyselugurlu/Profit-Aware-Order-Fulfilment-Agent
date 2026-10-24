using System;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Picking capacity of the centers. Each center packs one parcel at a time (60 / capacityPerHour minutes
    /// per parcel) in first-come order; a center that receives many orders builds a queue and its parcels
    /// leave later. Times are in ticks (minutes) and kept fractional so capacity is not rounded away.
    /// </summary>
    public sealed class CenterQueues
    {
        private readonly double[] nextFree;
        private readonly double[] pickMinutes;
        private readonly double handlingMinutes;

        public CenterQueues(World world, CostConfig cost)
        {
            nextFree = new double[world.Centers.Count];
            pickMinutes = new double[world.Centers.Count];
            for (int c = 0; c < pickMinutes.Length; c++)
                pickMinutes[c] = 60.0 / world.Centers[c].CapacityPerHour;
            handlingMinutes = cost.handlingMinutes;
        }

        /// <summary>Minutes a new parcel would wait before picking starts.</summary>
        public double Backlog(int centerId, int tick) => Math.Max(0.0, nextFree[centerId] - tick);

        /// <summary>When a parcel accepted now would leave the center, without reserving the slot.</summary>
        public double PeekDispatch(int centerId, int tick) =>
            Math.Max(tick, nextFree[centerId]) + pickMinutes[centerId] + handlingMinutes;

        /// <summary>Reserves the next picking slot and returns the dispatch time.</summary>
        public double Reserve(int centerId, int tick)
        {
            double start = Math.Max(tick, nextFree[centerId]);
            nextFree[centerId] = start + pickMinutes[centerId];
            return start + pickMinutes[centerId] + handlingMinutes;
        }
    }
}
