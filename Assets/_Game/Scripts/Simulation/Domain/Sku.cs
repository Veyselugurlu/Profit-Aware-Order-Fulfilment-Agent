namespace Fulfilment.Simulation
{
    /// <summary>A product (stock keeping unit). Ids are dense indices 0..n-1.</summary>
    public sealed class Sku
    {
        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string Category { get; }
        public double Price { get; }
        public double UnitCost { get; }
        public double WeightKg { get; }

        public double Margin => Price - UnitCost;

        public Sku(int id, string code, string name, string category, double price, double unitCost, double weightKg)
        {
            Id = id;
            Code = code;
            Name = name;
            Category = category;
            Price = price;
            UnitCost = unitCost;
            WeightKg = weightKg;
        }

        public override string ToString() => $"{Code} {Name}";
    }
}
