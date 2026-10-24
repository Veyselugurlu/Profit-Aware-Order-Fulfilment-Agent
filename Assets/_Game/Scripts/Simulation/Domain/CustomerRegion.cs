namespace Fulfilment.Simulation
{
    /// <summary>A customer region (district) whose orders are delivered to its center cell.</summary>
    public sealed class CustomerRegion
    {
        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public GridCell Cell { get; }

        /// <summary>Relative order volume of the region (normalised by the order generator).</summary>
        public double DemandWeight { get; }

        public CustomerRegion(int id, string code, string name, GridCell cell, double demandWeight)
        {
            Id = id;
            Code = code;
            Name = name;
            Cell = cell;
            DemandWeight = demandWeight;
        }

        public override string ToString() => $"{Code} {Name}";
    }
}
