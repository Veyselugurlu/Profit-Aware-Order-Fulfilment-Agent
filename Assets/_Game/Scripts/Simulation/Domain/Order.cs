using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    public enum OrderPriority
    {
        Standard = 0,
        Express = 1
    }

    public readonly struct OrderLine
    {
        public readonly int SkuId;
        public readonly int Quantity;

        public OrderLine(int skuId, int quantity)
        {
            SkuId = skuId;
            Quantity = quantity;
        }

        public override string ToString() => $"sku{SkuId}x{Quantity}";
    }

    /// <summary>
    /// A customer order as produced by the order generator. Immutable: what happens to the order
    /// (assignment, shipping, delivery) is tracked by the simulation engine, not stored here.
    /// </summary>
    public sealed class Order
    {
        public int Id { get; }
        public int CreatedTick { get; }
        public int RegionId { get; }
        public OrderPriority Priority { get; }

        /// <summary>Tick by which the order was promised to arrive.</summary>
        public int PromisedTick { get; }

        public IReadOnlyList<OrderLine> Lines { get; }

        public Order(int id, int createdTick, int regionId, OrderPriority priority, int promisedTick, IReadOnlyList<OrderLine> lines)
        {
            Id = id;
            CreatedTick = createdTick;
            RegionId = regionId;
            Priority = priority;
            PromisedTick = promisedTick;
            Lines = lines;
        }

        public int TotalUnits
        {
            get
            {
                int units = 0;
                for (int i = 0; i < Lines.Count; i++)
                    units += Lines[i].Quantity;
                return units;
            }
        }

        public override string ToString() => $"#{Id} t={CreatedTick} r{RegionId} {Priority} lines={Lines.Count}";
    }
}
