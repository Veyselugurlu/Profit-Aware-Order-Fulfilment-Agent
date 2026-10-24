using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    public enum PlanKind
    {
        /// <summary>One center ships the whole order.</summary>
        SingleCenter = 0,
        /// <summary>Lines are spread over centers greedily by shipping cost.</summary>
        MinCostSplit = 1,
        /// <summary>Lines are spread over centers to lose as few units as possible.</summary>
        MaxCoverageSplit = 2
    }

    /// <summary>Quantity of one SKU taken from one center.</summary>
    public readonly struct Allocation
    {
        public readonly int CenterId;
        public readonly int SkuId;
        public readonly int Quantity;

        public Allocation(int centerId, int skuId, int quantity)
        {
            CenterId = centerId;
            SkuId = skuId;
            Quantity = quantity;
        }

        public override string ToString() => $"fc{CenterId}:sku{SkuId}x{Quantity}";
    }

    /// <summary>
    /// A candidate way to fulfil an order: which center ships which units, and which units are lost
    /// because no center has them. Policies choose among candidate plans.
    /// </summary>
    public sealed class FulfilmentPlan
    {
        public PlanKind Kind { get; }
        public IReadOnlyList<Allocation> Allocations { get; }
        public IReadOnlyList<OrderLine> LostLines { get; }

        /// <summary>Distinct centers used, i.e. the number of parcels sent.</summary>
        public int PackageCount { get; }

        public bool IsSplit => PackageCount > 1;
        public bool HasLostUnits => LostLines.Count > 0;

        public FulfilmentPlan(PlanKind kind, IReadOnlyList<Allocation> allocations, IReadOnlyList<OrderLine> lostLines)
        {
            Kind = kind;
            Allocations = allocations;
            LostLines = lostLines;

            var centers = new HashSet<int>();
            for (int i = 0; i < allocations.Count; i++)
                centers.Add(allocations[i].CenterId);
            PackageCount = centers.Count;
        }

        public IEnumerable<int> Centers()
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < Allocations.Count; i++)
                if (seen.Add(Allocations[i].CenterId))
                    yield return Allocations[i].CenterId;
        }

        public override string ToString() => $"{Kind} packages={PackageCount} lost={LostLines.Count}";
    }
}
