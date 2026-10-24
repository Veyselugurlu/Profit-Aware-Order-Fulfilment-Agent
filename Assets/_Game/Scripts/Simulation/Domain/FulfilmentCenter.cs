namespace Fulfilment.Simulation
{
    /// <summary>A fulfilment center (warehouse) placed on a cell of the city grid.</summary>
    public sealed class FulfilmentCenter
    {
        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public GridCell Cell { get; }

        /// <summary>Orders the center can pick and pack per simulated hour.</summary>
        public int CapacityPerHour { get; }

        /// <summary>Share of the network's stock held here (all centers sum to 1).</summary>
        public double StockShare { get; }

        public FulfilmentCenter(int id, string code, string name, GridCell cell, int capacityPerHour, double stockShare)
        {
            Id = id;
            Code = code;
            Name = name;
            Cell = cell;
            CapacityPerHour = capacityPerHour;
            StockShare = stockShare;
        }

        public override string ToString() => $"{Code} {Name}";
    }
}
